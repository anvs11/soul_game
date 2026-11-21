using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Game.Progression
{
    public sealed class JsonSaveRepository : ISaveRepository
    {
        // Serializes callers in this process. The lock file also excludes other cooperating processes.
        private static readonly object ProcessGate = new();
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly string directory, slotA, slotB, pending, lockPath;
        private readonly SaveWriteMode mode;

        public JsonSaveRepository(string directory, SaveWriteMode mode = SaveWriteMode.PreferAtomicReplacement)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory required.", nameof(directory));
            this.directory = Path.GetFullPath(directory);
            this.mode = mode;
            slotA = Path.Combine(this.directory, "profile-a.json");
            slotB = Path.Combine(this.directory, "profile-b.json");
            pending = Path.Combine(this.directory, "profile.pending.json");
            lockPath = Path.Combine(this.directory, ".profile.lock");
        }

        public SaveLoadResult Load()
        {
            lock (ProcessGate)
            {
                try
                {
                    using var lease = LockFiles();
                    return ReadSlots(out _);
                }
                catch (Exception error) when (IsIoError(error))
                {
                    return new SaveLoadResult(SaveLoadStatus.Unavailable);
                }
            }
        }

        public SaveWriteStatus TryCommit(long expectedRevision, ProfileData candidate)
        {
            if (candidate == null || expectedRevision < 0 || expectedRevision == long.MaxValue ||
                candidate.revision != expectedRevision + 1 || !candidate.IsValid())
                return SaveWriteStatus.InvalidProfile;

            lock (ProcessGate)
            {
                try
                {
                    using var lease = LockFiles();
                    return CommitLocked(expectedRevision, candidate);
                }
                catch (Exception error) when (IsIoError(error))
                {
                    return SaveWriteStatus.Unavailable;
                }
            }
        }

        private SaveWriteStatus CommitLocked(long expectedRevision, ProfileData candidate)
        {
            var old = ReadSlots(out string current);
            if (old.Status == SaveLoadStatus.IncompatibleSchema) return SaveWriteStatus.IncompatibleSchema;
            if (old.Status == SaveLoadStatus.Corrupt) return SaveWriteStatus.Unavailable;
            if ((old.Profile?.revision ?? 0) != expectedRevision) return SaveWriteStatus.RevisionConflict;

            var document = candidate.Copy();
            document.integrity = Checksum(document);
            WriteAndFlush(pending, JsonUtility.ToJson(document, true));
            if (!Matches(ReadSlot(pending), document)) return SaveWriteStatus.Unavailable;

            if (current == null)
            {
                File.Move(pending, slotA);
                return SaveWriteStatus.Saved;
            }

            string spare = current == slotA ? slotB : slotA;
            if (mode == SaveWriteMode.PreferAtomicReplacement)
            {
                try
                {
                    File.Replace(pending, current, spare);
                    return SaveWriteStatus.Saved;
                }
                catch (Exception error) when (error is IOException || error is NotSupportedException)
                {
                    // An ambiguous failure may have published the candidate. Only fall back with the old
                    // valid revision still present, so writing the other slot cannot lose it.
                    var after = ReadSlots(out current);
                    if (Matches(after, document)) return SaveWriteStatus.Saved;
                    if (!Matches(after, old.Profile)) return SaveWriteStatus.Unavailable;
                    spare = current == slotA ? slotB : slotA;
                }
            }

            WriteAndFlush(spare, JsonUtility.ToJson(document, true));
            return VerifyPublished(spare, document);
        }

        private FileStream LockFiles()
        {
            Directory.CreateDirectory(directory);
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private SaveLoadResult ReadSlots(out string newestPath)
        {
            newestPath = null;
            var a = ReadSlot(slotA);
            var b = ReadSlot(slotB);
            if (a.Status == SaveLoadStatus.IncompatibleSchema || b.Status == SaveLoadStatus.IncompatibleSchema)
                return new SaveLoadResult(SaveLoadStatus.IncompatibleSchema);
            if (a.Profile == null && b.Profile == null)
                return a.Status == SaveLoadStatus.Missing && b.Status == SaveLoadStatus.Missing
                    ? new SaveLoadResult(SaveLoadStatus.Missing) : new SaveLoadResult(SaveLoadStatus.Corrupt);
            if (a.Profile != null && b.Profile != null && a.Profile.revision == b.Profile.revision &&
                !Matches(a, b.Profile))
                return new SaveLoadResult(SaveLoadStatus.Corrupt);

            bool chooseA = a.Profile != null && (b.Profile == null || a.Profile.revision >= b.Profile.revision);
            newestPath = chooseA ? slotA : slotB;
            var newest = chooseA ? a.Profile : b.Profile;
            bool recovered = a.Status == SaveLoadStatus.Corrupt || b.Status == SaveLoadStatus.Corrupt ||
                a.Status == SaveLoadStatus.Missing || (b.Status == SaveLoadStatus.Missing && newest.revision > 1);
            return new SaveLoadResult(recovered ? SaveLoadStatus.Recovered : SaveLoadStatus.Ready, newest);
        }

        private static SaveLoadResult ReadSlot(string path)
        {
            string json;
            try { json = File.ReadAllText(path, Utf8); }
            catch (FileNotFoundException) { return new SaveLoadResult(SaveLoadStatus.Missing); }
            catch (DecoderFallbackException) { return new SaveLoadResult(SaveLoadStatus.Corrupt); }
            try
            {
                var header = JsonUtility.FromJson<SchemaHeader>(json);
                if (header != null && header.schemaVersion > ProfileData.CurrentSchemaVersion)
                    return new SaveLoadResult(SaveLoadStatus.IncompatibleSchema);
                var profile = JsonUtility.FromJson<ProfileData>(json);
                if (header != null && header.schemaVersion == ProfileData.CurrentSchemaVersion &&
                    profile != null && profile.IsValid() && profile.integrity == Checksum(profile))
                    return new SaveLoadResult(SaveLoadStatus.Ready, profile);
            }
            catch (ArgumentException) { } // An incomplete or invalid JSON document.
            return new SaveLoadResult(SaveLoadStatus.Corrupt);
        }

        private static bool Matches(SaveLoadResult read, ProfileData expected) =>
            read.Profile != null && read.Profile.revision == expected.revision &&
            read.Profile.integrity == expected.integrity;

        private static SaveWriteStatus VerifyPublished(string path, ProfileData expected) =>
            Matches(ReadSlot(path), expected) ? SaveWriteStatus.Saved : SaveWriteStatus.Unavailable;

        private static void WriteAndFlush(string path, string json)
        {
            byte[] bytes = Utf8.GetBytes(json);
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        private static string Checksum(ProfileData profile)
        {
            var unsigned = profile.Copy();
            unsigned.integrity = "";
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Utf8.GetBytes(JsonUtility.ToJson(unsigned))));
        }

        private static bool IsIoError(Exception error) => error is IOException ||
            error is UnauthorizedAccessException || error is NotSupportedException;

        [Serializable]
        private sealed class SchemaHeader { public int schemaVersion = 0; }
    }
}
