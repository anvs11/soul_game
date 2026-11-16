using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Generation
{
    public readonly struct Cell : IEquatable<Cell>
    {
        public int X { get; }
        public int Y { get; }
        public Cell(int x, int y) { X = x; Y = y; }
        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Cell other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
    }

    public enum ModuleKind { Entry, Combat, Rest, Boss }

    internal static class Snapshot
    {
        internal static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> items, string name)
        {
            if (items == null) throw new ArgumentNullException(name);
            return new List<T>(items).AsReadOnly();
        }
        internal static string Id(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id must be nonempty.", nameof(id));
            return id;
        }
    }

    public sealed class ModuleDescriptor
    {
        public string Id { get; }
        public ModuleKind Kind { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<bool> Walkable { get; }
        public Cell Entry { get; }
        public Cell Exit { get; }
        public IReadOnlyList<Cell> SpawnCells { get; }
        public ModuleDescriptor(string id, ModuleKind kind, int width, int height,
            IEnumerable<bool> walkable, Cell entry, Cell exit, IEnumerable<Cell> spawnCells)
        {
            Id = Snapshot.Id(id);
            if (!Enum.IsDefined(typeof(ModuleKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(width), "Positive dimensions with an int-sized grid required.");
            Kind = kind; Width = width; Height = height;
            Walkable = Snapshot.Copy(walkable, nameof(walkable));
            if (Walkable.Count != (long)width * height) throw new ArgumentException("Grid size mismatch.", nameof(walkable));
            CheckCell(entry); CheckCell(exit);
            Entry = entry; Exit = exit; SpawnCells = Snapshot.Copy(spawnCells, nameof(spawnCells));
            foreach (var cell in SpawnCells) CheckCell(cell);
        }
        private void CheckCell(Cell cell)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= Width || cell.Y >= Height)
                throw new ArgumentOutOfRangeException(nameof(cell), "Cell is outside the module.");
        }
    }

    public sealed class StageCatalog
    {
        public IReadOnlyList<ModuleDescriptor> Modules { get; }
        public StageCatalog(IEnumerable<ModuleDescriptor> modules)
        {
            Modules = Snapshot.Copy(modules, nameof(modules));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var module in Modules)
                if (module == null || !ids.Add(module.Id))
                    throw new ArgumentException("Null module or duplicate ordinal Id.", nameof(modules));
        }
    }

    public sealed class SegmentLayout
    {
        public int Index { get; }
        public string ModuleId { get; }
        public SegmentLayout(int index, string moduleId)
        {
            if (index < 1 || index > 5) throw new ArgumentOutOfRangeException(nameof(index));
            Index = index; ModuleId = Snapshot.Id(moduleId);
        }
    }

    public sealed class RunLayout
    {
        public int Seed { get; }
        public int GeneratorVersion { get; }
        public int ContentVersion { get; }
        public IReadOnlyList<SegmentLayout> Segments { get; }
        public RunLayout(int seed, int generatorVersion, int contentVersion, IEnumerable<SegmentLayout> segments)
        {
            if (generatorVersion <= 0) throw new ArgumentOutOfRangeException(nameof(generatorVersion));
            if (contentVersion <= 0) throw new ArgumentOutOfRangeException(nameof(contentVersion));
            Seed = seed; GeneratorVersion = generatorVersion; ContentVersion = contentVersion;
            Segments = Snapshot.Copy(segments, nameof(segments));
            foreach (var segment in Segments)
                if (segment == null) throw new ArgumentException("Null segment.", nameof(segments));
        }
    }

    public sealed class GenerationResult
    {
        public bool IsSuccess => Layout != null;
        public RunLayout Layout { get; }
        public bool UsedFallback { get; }
        public string Diagnostic { get; }
        private GenerationResult(RunLayout layout, bool usedFallback, string diagnostic)
        { Layout = layout; UsedFallback = usedFallback; Diagnostic = diagnostic; }
        public static GenerationResult Success(RunLayout layout, bool usedFallback, string diagnostic)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (diagnostic == null) throw new ArgumentNullException(nameof(diagnostic));
            return new GenerationResult(layout, usedFallback, diagnostic);
        }
        public static GenerationResult Failure(string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(diagnostic)) throw new ArgumentException("Failure requires a reason.", nameof(diagnostic));
            return new GenerationResult(null, false, diagnostic);
        }
    }

    public sealed class ValidationResult
    {
        public bool IsValid => Reasons.Count == 0;
        public IReadOnlyList<string> Reasons { get; }
        public ValidationResult(IEnumerable<string> reasons)
        {
            Reasons = Snapshot.Copy(reasons, nameof(reasons));
            foreach (var reason in Reasons)
                if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Empty reason.", nameof(reasons));
        }
    }
}
