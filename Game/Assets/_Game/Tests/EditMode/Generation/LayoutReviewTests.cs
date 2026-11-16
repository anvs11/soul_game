using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Game.Generation;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // Test-only entry point for U2 exports; invokes the real production C# implementation.
    public static class GenerationSweepReview
    {
        [Serializable] private sealed class Export
        {
            public int schemaVersion, generatorVersion, contentVersion;
            public Module[] modules;
        }
        [Serializable] private sealed class Module
        {
            public string id, kind;
            public int width, height;
            public bool[] walkable;
            public int[] entry, exit;
            public Point[] spawnCells;
        }
        [Serializable] private sealed class Point { public int x, y; }
        // JsonUtility cannot deserialize nested int[][]: exported spawns use {x,y} objects.
        private static Cell CellFrom(int[] value)
        {
            if (value == null || value.Length != 2) throw new ArgumentException("Cell requires [x,y].");
            return new Cell(value[0], value[1]);
        }
        public static StageCatalog LoadExport(string projectRelativePath)
        {
            // dataPath is <repo>/Game/Assets; fixture paths are relative to <repo>.
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string path = Path.GetFullPath(Path.Combine(root, projectRelativePath));
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, comparison)) throw new ArgumentException("Export must be inside project.");
            var data = JsonUtility.FromJson<Export>(File.ReadAllText(path));
            if (data == null || data.schemaVersion != 1 || data.generatorVersion != 1 || data.contentVersion != 1 || data.modules == null)
                throw new ArgumentException("Expected schema/generator/content 1/1/1 and modules.");
            var modules = new List<ModuleDescriptor>();
            foreach (var m in data.modules)
            {
                if (m == null || !Enum.TryParse(m.kind, false, out ModuleKind kind) || kind.ToString() != m.kind)
                    throw new ArgumentException("Invalid module kind.");
                if (m.spawnCells == null) throw new ArgumentException("Missing spawnCells.");
                var spawns = new List<Cell>();
                foreach (var point in m.spawnCells)
                {
                    if (point == null) throw new ArgumentException("Null spawn.");
                    spawns.Add(new Cell(point.x, point.y));
                }
                modules.Add(new ModuleDescriptor(m.id, kind, m.width, m.height, m.walkable,
                    CellFrom(m.entry), CellFrom(m.exit), spawns));
            }
            return new StageCatalog(modules);
        }

        public sealed class Summary
        {
            public int Successes { get; internal set; }
            public int Failures { get; internal set; }
            public int Fallbacks { get; internal set; }
            public double ElapsedMilliseconds { get; internal set; }
            public IReadOnlyList<string> Records { get; internal set; }
        }
        public static Summary Review(StageCatalog catalog, int count = 1000)
        {
            if (count < 1000) throw new ArgumentOutOfRangeException(nameof(count));
            var modules = new Dictionary<string, ModuleDescriptor>(StringComparer.Ordinal);
            foreach (var m in catalog.Modules) modules.Add(m.Id, m);
            var generator = new RunGenerator(); var validator = new LayoutValidator();
            var result = new Summary(); var records = new List<string>(); var timer = Stopwatch.StartNew();
            for (int seed = 0; seed < count; seed++)
            {
                var generated = generator.Generate(seed, catalog, 1, 1);
                if (generated.IsSuccess)
                {
                    var layout = generated.Layout;
                    Assert.AreEqual(5, layout.Segments.Count, "seed=" + seed);
                    Assert.AreEqual(seed, layout.Seed); Assert.AreEqual(1, layout.GeneratorVersion); Assert.AreEqual(1, layout.ContentVersion);
                    for (int i = 0; i < 5; i++)
                    {
                        Assert.AreEqual(i + 1, layout.Segments[i].Index);
                        Assert.IsTrue(modules.ContainsKey(layout.Segments[i].ModuleId));
                        Assert.AreEqual(i == 4 ? ModuleKind.Boss : ModuleKind.Combat, modules[layout.Segments[i].ModuleId].Kind);
                    }
                    var check = validator.Validate(layout, catalog);
                    Assert.IsTrue(check.IsValid, "seed=" + seed + ": " + string.Join(" | ", check.Reasons));
                    result.Successes++; if (generated.UsedFallback) result.Fallbacks++;
                }
                else
                {
                    Assert.IsNull(generated.Layout); Assert.IsFalse(generated.UsedFallback);
                    Assert.IsNotEmpty(generated.Diagnostic); result.Failures++;
                }
                records.Add(seed + ": " + generated.Diagnostic);
            }
            timer.Stop(); result.ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds;
            result.Records = records.AsReadOnly();
            TestContext.WriteLine("C# sweep seeds0.." + (count-1) + "; success=" + result.Successes + "; failure=" + result.Failures +
                "; fallback=" + result.Fallbacks + "; elapsedMs=" + result.ElapsedMilliseconds + "; Unity=" + Application.unityVersion);
            return result;
        }
    }

    public sealed class LayoutReviewTests
    {
        private static StageCatalog Reference() => GenerationSweepReview.LoadExport("Setup/Stage3/reference-catalog-v1.json");
        private static StageCatalog BlockCell(StageCatalog catalog, string id, int index)
        {
            var modules = new List<ModuleDescriptor>();
            foreach (var m in catalog.Modules)
            {
                if (m.Id != id) { modules.Add(m); continue; }
                var grid = new List<bool>(m.Walkable); grid[index] = false;
                modules.Add(new ModuleDescriptor(m.Id, m.Kind, m.Width, m.Height, grid, m.Entry, m.Exit, m.SpawnCells));
            }
            return new StageCatalog(modules);
        }
        [Test] public void ReferenceThousandSeedsValidateIncludingSingleCellCorridor()
        {
            var result = GenerationSweepReview.Review(Reference());
            Assert.AreEqual(1000, result.Successes); Assert.AreEqual(0, result.Failures); Assert.AreEqual(0, result.Fallbacks);
        }
        [Test] public void BlockedCorridorEntryIsRejectedWithExactModuleReason()
        {
            var catalog = BlockCell(Reference(), "street_a", 3);
            var layout = new RunLayout(0, 1, 1, new[] { new SegmentLayout(1,"street_a"), new SegmentLayout(2,"street_b"),
                new SegmentLayout(3,"street_c"), new SegmentLayout(4,"street_a"), new SegmentLayout(5,"street_boss") });
            var result = new LayoutValidator().Validate(layout, catalog);
            Assert.IsFalse(result.IsValid); StringAssert.Contains("street_a: Entry", string.Join(" | ", result.Reasons));
        }
        [Test] public void DisabledBossSpawnProducesThousandExplicitFailures()
        {
            var original = Reference(); var broken = BlockCell(original, "street_boss", 12);
            var result = GenerationSweepReview.Review(broken);
            Assert.AreEqual(0, result.Successes); Assert.AreEqual(1000, result.Failures);
            foreach (var record in result.Records) StringAssert.Contains("Spawn (2,2)", record);
            Assert.AreEqual(1000, GenerationSweepReview.Review(original).Successes);
        }
    }
}
