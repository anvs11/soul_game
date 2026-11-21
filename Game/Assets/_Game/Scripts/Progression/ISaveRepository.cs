namespace Game.Progression
{
    public enum SaveLoadStatus { Missing, Ready, Recovered, IncompatibleSchema, Corrupt, Unavailable }
    public enum SaveWriteStatus { Saved, RevisionConflict, IncompatibleSchema, InvalidProfile, Unavailable }
    public enum SaveWriteMode { PreferAtomicReplacement, DualSlot }

    public readonly struct SaveLoadResult
    {
        public SaveLoadStatus Status { get; }
        public ProfileData Profile { get; }

        public SaveLoadResult(SaveLoadStatus status, ProfileData profile = null)
        {
            Status = status;
            Profile = profile;
        }
    }

    public interface ISaveRepository
    {
        SaveLoadResult Load();

        // The candidate must be exactly one revision ahead of expectedRevision.
        SaveWriteStatus TryCommit(long expectedRevision, ProfileData candidate);
    }
}
