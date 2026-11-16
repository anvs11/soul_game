using System;
using System.Collections.Generic;
using Game.Generation;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class LayoutValidatorTests
    {
        private static ModuleDescriptor Module(string id, ModuleKind kind, bool[] grid = null,
            Cell? entry = null, Cell? exit = null, Cell[] spawns = null)
            => new ModuleDescriptor(id, kind, 3, 3,
                grid ?? new[] { true, true, true, true, true, true, true, true, true },
                entry ?? new Cell(0, 0), exit ?? new Cell(2, 2), spawns ?? new[] { new Cell(1, 0) });
        private static StageCatalog Catalog(ModuleDescriptor first)
            => new StageCatalog(new[] { first, Module("street_b", ModuleKind.Combat),
                Module("street_c", ModuleKind.Combat), Module("street_boss", ModuleKind.Boss) });
        private static RunLayout Layout(string first = "street_a") => new RunLayout(0, 1, 1, new[] {
            new SegmentLayout(1, first), new SegmentLayout(2, "street_b"), new SegmentLayout(3, "street_c"),
            new SegmentLayout(4, "street_a"), new SegmentLayout(5, "street_boss") });
        private static ValidationResult Check(bool[] grid, Cell? entry = null, Cell? exit = null, Cell[] spawns = null)
            => new LayoutValidator().Validate(Layout(), Catalog(Module("street_a", ModuleKind.Combat, grid, entry, exit, spawns)));

        [Test] public void FourNeighborDetourReachesExitAndAllSpawns()
        {
            // y0 ... ; y1 ##. ; y2 ... : unique path goes around the wall on the right.
            Assert.IsTrue(Check(new[] { true,true,true, false,false,true, true,true,true },
                exit: new Cell(0, 2), spawns: new[] { new Cell(2, 1), new Cell(1, 2) }).IsValid);
        }
        [Test] public void SolidRowBlocksExit()
        {
            var result = Check(new[] { true,true,true, false,false,false, true,true,true });
            Assert.IsFalse(result.IsValid); StringAssert.Contains("Exit", string.Join("|", result.Reasons));
        }
        [Test] public void IsolatedSpawnFailsEvenWhenExitIsReachable()
        {
            var result = Check(new[] { true,true,true, true,false,false, true,false,true },
                exit: new Cell(0, 2), spawns: new[] { new Cell(2, 2) });
            Assert.IsFalse(result.IsValid); StringAssert.Contains("Spawn (2,2)", string.Join("|", result.Reasons));
        }
        [Test] public void DiagonalContactDoesNotConnectCells()
        {
            Assert.IsFalse(Check(new[] { true,false,false, false,true,false, false,false,true }).IsValid);
        }
        [Test] public void BlockedEntryExitAndMissingCombatSpawnFail()
        {
            Assert.IsFalse(Check(new[] { false,true,true, true,true,true, true,true,true }).IsValid);
            Assert.IsFalse(Check(new[] { true,true,true, true,true,true, true,true,false }).IsValid);
            Assert.IsFalse(Check(null, spawns: Array.Empty<Cell>()).IsValid);
        }
        [Test] public void UnknownModuleOrderCountAndVersionsFail()
        {
            var catalog = Catalog(Module("street_a", ModuleKind.Combat)); var validator = new LayoutValidator();
            Assert.IsFalse(validator.Validate(Layout("missing"), catalog).IsValid);
            Assert.IsFalse(validator.Validate(Layout("street_boss"), catalog).IsValid);
            Assert.IsFalse(validator.Validate(new RunLayout(0, 1, 1, new[] { new SegmentLayout(2,"street_a") }), catalog).IsValid);
            Assert.IsFalse(validator.Validate(new RunLayout(0, 2, 1, Layout().Segments), catalog).IsValid);
            var segments = new List<SegmentLayout>(Layout().Segments); segments[4] = new SegmentLayout(5, "street_a");
            Assert.IsFalse(validator.Validate(new RunLayout(0, 1, 1, segments), catalog).IsValid);
        }
        [Test] public void MalformedDataAndDuplicateIdsAreRejectedByConstructors()
        {
            Assert.Throws<ArgumentException>(() => new StageCatalog(new[] { Module("x",ModuleKind.Combat), Module("x",ModuleKind.Boss) }));
            Assert.Throws<ArgumentException>(() => Module("x", ModuleKind.Combat, new[] { true }));
            Assert.Throws<ArgumentOutOfRangeException>(() => Module("x", ModuleKind.Combat, entry: new Cell(-1,0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Module("x", ModuleKind.Combat, exit: new Cell(3,0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Module("x", ModuleKind.Combat, spawns: new[] { new Cell(0,3) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModuleDescriptor("x", ModuleKind.Combat, 0, 1,
                Array.Empty<bool>(), new Cell(0,0),new Cell(0,0),Array.Empty<Cell>()));
        }
        [Test] public void TwentyRejectedCandidatesUseValidatedFallbackWithContext()
        {
            var modules = new List<ModuleDescriptor>(Catalog(Module("street_a", ModuleKind.Combat)).Modules);
            for (int i = 0; i < 64; i++) modules.Add(Module("bad_" + i.ToString("D2"), ModuleKind.Combat,
                new[] { true,true,true, false,false,false, true,true,true }));
            var catalog = new StageCatalog(modules);
            var result = new RunGenerator().Generate(0, catalog, 1, 1);
            Assert.IsTrue(result.IsSuccess, result.Diagnostic); Assert.IsTrue(result.UsedFallback);
            Assert.IsTrue(new LayoutValidator().Validate(result.Layout, catalog).IsValid);
            StringAssert.Contains("attempts=20", result.Diagnostic); StringAssert.Contains("seed=0", result.Diagnostic);
            StringAssert.Contains("generatorVersion=1; contentVersion=1", result.Diagnostic);
            StringAssert.Contains("last rejection=", result.Diagnostic);
            Assert.AreEqual("street_a", result.Layout.Segments[0].ModuleId);
            Assert.AreEqual("street_b", result.Layout.Segments[1].ModuleId);
            Assert.AreEqual("street_c", result.Layout.Segments[2].ModuleId);
            Assert.AreEqual("street_a", result.Layout.Segments[3].ModuleId);
            Assert.AreEqual("street_boss", result.Layout.Segments[4].ModuleId);
        }
        [Test] public void RejectedFirstCandidateCanSucceedOnSecondWithoutFallback()
        {
            var modules = new List<ModuleDescriptor>(Catalog(Module("street_a", ModuleKind.Combat)).Modules);
            modules.Add(Module("bad", ModuleKind.Combat, new[] { true,true,true,false,false,false,true,true,true }));
            var catalog = new StageCatalog(modules);
            var result = new RunGenerator().Generate(2, catalog, 1, 1);
            Assert.IsTrue(result.IsSuccess, result.Diagnostic); Assert.IsFalse(result.UsedFallback);
            StringAssert.Contains("attempts=2", result.Diagnostic);
            Assert.IsTrue(new LayoutValidator().Validate(result.Layout, catalog).IsValid);
            Assert.AreEqual("street_b", result.Layout.Segments[0].ModuleId);
            Assert.AreEqual("street_b", result.Layout.Segments[1].ModuleId);
            Assert.AreEqual("street_a", result.Layout.Segments[2].ModuleId);
            Assert.AreEqual("street_a", result.Layout.Segments[3].ModuleId);
        }
        [Test] public void InvalidFallbackAndRepeatedErrorsDoNotMutateOrLeakState()
        {
            var grid = new[] { true,true,true, false,false,false, true,true,true };
            var bad = Module("street_a", ModuleKind.Combat, grid);
            var catalog = Catalog(bad); grid[3] = grid[4] = grid[5] = true;
            var generator = new RunGenerator();
            // All possible random candidates must fail through a blocked Boss as well.
            var invalid = new StageCatalog(new[] { bad, Module("street_b",ModuleKind.Combat), Module("street_c",ModuleKind.Combat),
                Module("street_boss",ModuleKind.Boss,new[] { true,true,true,false,false,false,true,true,true }) });
            string before = generator.Generate(-1, invalid, 1, 1).Diagnostic;
            var success = generator.Generate(0, Catalog(Module("street_a",ModuleKind.Combat)), 1, 1);
            Assert.IsTrue(success.IsSuccess); Assert.IsFalse(success.UsedFallback);
            var repeat = generator.Generate(-1, invalid, 1, 1);
            Assert.IsFalse(repeat.IsSuccess); Assert.IsNull(repeat.Layout); Assert.IsFalse(repeat.UsedFallback);
            Assert.AreEqual(before, repeat.Diagnostic); StringAssert.Contains("fallback invalid", before);
            Assert.IsFalse(bad.Walkable[3]); Assert.AreEqual("street_a", catalog.Modules[0].Id);
        }
        [Test] public void MissingFallbackIdsNeverReturnUnvalidatedSuccess()
        {
            var catalog = new StageCatalog(new[] { Module("only", ModuleKind.Combat,
                new[] { true,true,true,false,false,false,true,true,true }), Module("boss",ModuleKind.Boss) });
            var failure = new RunGenerator().Generate(123, catalog, 1, 1);
            Assert.IsFalse(failure.IsSuccess); Assert.IsNull(failure.Layout);
            StringAssert.Contains("attempts=20", failure.Diagnostic); StringAssert.Contains("Unknown module", failure.Diagnostic);
        }
    }
}
