using System;
using System.Collections.Generic;
using Game.Generation;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GenerationContractTests
    {
        private static ModuleDescriptor Module(string id, ModuleKind kind = ModuleKind.Combat)
            => new ModuleDescriptor(id, kind, 2, 2, new[] { true, true, true, true },
                new Cell(0, 0), new Cell(1, 1), new[] { new Cell(1, 0) });

        [Test] public void GridAndSpawnInputsAreSnapshotsAndCannotBeEditedThroughViews()
        {
            var grid = new[] { true, true, true, true }; var spawns = new[] { new Cell(1, 0) };
            var module = new ModuleDescriptor("a", ModuleKind.Combat, 2, 2, grid, new Cell(0, 0), new Cell(1, 1), spawns);
            grid[0] = false; spawns[0] = new Cell(0, 1);
            Assert.IsTrue(module.Walkable[0]); Assert.AreEqual(new Cell(1, 0), module.SpawnCells[0]);
            Assert.Throws<NotSupportedException>(() => ((IList<bool>)module.Walkable)[0] = false);
            var input = new[] { module }; var catalog = new StageCatalog(input); input[0] = Module("b");
            Assert.AreEqual("a", catalog.Modules[0].Id);
        }

        [Test] public void InvalidShapeBoundsAndDuplicateIdsFailAtConstruction()
        {
            Assert.Throws<ArgumentException>(() => new StageCatalog(new[] { Module("a"), Module("a") }));
            Assert.Throws<ArgumentException>(() => new ModuleDescriptor("bad", ModuleKind.Combat, 2, 2,
                new[] { true }, new Cell(0, 0), new Cell(1, 1), Array.Empty<Cell>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModuleDescriptor("bad", ModuleKind.Boss, 1, 1,
                new[] { true }, new Cell(0, 0), new Cell(2, 0), Array.Empty<Cell>()));
        }

        [Test] public void ResultFailureAndLayoutSnapshotsHaveUnambiguousState()
        {
            var segments = new[] { new SegmentLayout(1, "a") };
            var layout = new RunLayout(-1, 1, 1, segments); segments[0] = new SegmentLayout(2, "b");
            Assert.AreEqual(1, layout.Segments[0].Index);
            var failure = GenerationResult.Failure("invalid catalog");
            Assert.IsFalse(failure.IsSuccess); Assert.IsNull(failure.Layout); Assert.IsFalse(failure.UsedFallback);
            Assert.Throws<ArgumentException>(() => GenerationResult.Failure(""));
            var success = GenerationResult.Success(layout, false, "candidate; must validate");
            Assert.IsTrue(success.IsSuccess); Assert.AreSame(layout, success.Layout);
        }

        [Test] public void SixIdExampleGeneratesCandidateButValidatorStillBlocksCombat()
        {
            var catalog = new StageCatalog(new[] { Module("street_entry", ModuleKind.Entry), Module("street_a"),
                Module("street_b"), Module("street_c"), Module("street_rest", ModuleKind.Rest), Module("street_boss", ModuleKind.Boss) });
            Assert.AreEqual(6, catalog.Modules.Count);
            var generated = new RunGenerator().Generate(int.MinValue, catalog, 1, 1);
            Assert.IsTrue(generated.IsSuccess); Assert.IsNotNull(generated.Layout); Assert.IsNotEmpty(generated.Diagnostic);
            Assert.IsFalse(new LayoutValidator().Validate(generated.Layout, catalog).IsValid);
            var candidate = new RunLayout(1, 1, 1, new[] { new SegmentLayout(1, "street_a") });
            var validation = new LayoutValidator().Validate(candidate, catalog);
            Assert.IsFalse(validation.IsValid); Assert.IsNotEmpty(validation.Reasons);
        }
    }
}
