using System;
using System.Collections.Generic;
using Game.Generation;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class RunGeneratorTests
    {
        private static ModuleDescriptor Module(string id, ModuleKind kind)
            => new ModuleDescriptor(id, kind, 2, 2, new[] { true, true, true, true },
                new Cell(0, 0), new Cell(1, 1), new[] { new Cell(1, 0) });
        private static StageCatalog Catalog(bool reverse = false)
        {
            var modules = new[] { Module("street_c", ModuleKind.Combat), Module("street_entry", ModuleKind.Entry),
                Module("street_boss_z", ModuleKind.Boss), Module("street_a", ModuleKind.Combat),
                Module("street_rest", ModuleKind.Rest), Module("street_b", ModuleKind.Combat), Module("street_boss", ModuleKind.Boss) };
            if (reverse) Array.Reverse(modules);
            return new StageCatalog(modules);
        }
        private static string Signature(GenerationResult result)
        {
            Assert.IsTrue(result.IsSuccess, result.Diagnostic); Assert.IsFalse(result.UsedFallback);
            var ids = new List<string>();
            foreach (var segment in result.Layout.Segments) ids.Add(segment.ModuleId);
            return string.Join(",", ids);
        }

        [TestCase(0, "street_b,street_a,street_b,street_b,street_boss_z")]
        [TestCase(-1, "street_b,street_a,street_c,street_c,street_boss_z")]
        [TestCase(int.MinValue, "street_b,street_b,street_a,street_c,street_boss")]
        [TestCase(int.MaxValue, "street_a,street_b,street_b,street_b,street_boss")]
        [TestCase(123, "street_a,street_b,street_c,street_a,street_boss")]
        public void VersionOneHasStableKnownVectors(int seed, string expected)
        {
            var result = new RunGenerator().Generate(seed, Catalog(), 1, 1);
            Assert.AreEqual(expected, Signature(result));
            Assert.AreEqual(seed, result.Layout.Seed);
            Assert.AreEqual(1, result.Layout.GeneratorVersion); Assert.AreEqual(1, result.Layout.ContentVersion);
        }

        [Test] public void PermutedCatalogAndInterleavedCallsDoNotChangeSameSeed()
        {
            var generator = new RunGenerator(); var catalog = Catalog();
            string firstId = catalog.Modules[0].Id;
            string expected = Signature(generator.Generate(-123, catalog, 1, 1));
            generator.Generate(99, catalog, 1, 1);
            Assert.AreEqual(expected, Signature(generator.Generate(-123, Catalog(true), 1, 1)));
            Assert.AreEqual(expected, Signature(new RunGenerator().Generate(-123, catalog, 1, 1)));
            Assert.AreEqual(firstId, catalog.Modules[0].Id);
        }

        [Test] public void GlobalUnityRandomIsNeitherReadNorAdvanced()
        {
            var saved = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(17); var before = UnityEngine.Random.state;
                var result = new RunGenerator().Generate(123, Catalog(), 1, 1);
                Assert.AreEqual(before, UnityEngine.Random.state);
                UnityEngine.Random.InitState(999); UnityEngine.Random.Range(0, 1000);
                Assert.AreEqual(Signature(result), Signature(new RunGenerator().Generate(123, Catalog(), 1, 1)));
            }
            finally { UnityEngine.Random.state = saved; }
        }

        [Test] public void EverySeedHasFourCombatThenBossAndSampleHasVariety()
        {
            var signatures = new HashSet<string>(); var generator = new RunGenerator(); var catalog = Catalog();
            for (int seed = 0; seed < 64; seed++)
            {
                var result = generator.Generate(seed, catalog, 1, 1); signatures.Add(Signature(result));
                Assert.AreEqual(5, result.Layout.Segments.Count);
                for (int i = 0; i < 5; i++)
                {
                    var segment = result.Layout.Segments[i]; Assert.AreEqual(i + 1, segment.Index);
                    var kind = ModuleKind.Entry;
                    foreach (var module in catalog.Modules) if (module.Id == segment.ModuleId) kind = module.Kind;
                    Assert.AreEqual(i == 4 ? ModuleKind.Boss : ModuleKind.Combat, kind);
                }
            }
            Assert.Greater(signatures.Count, 1);
        }

        [Test] public void SingleCombatCanRepeatWithoutIncludingAuxiliaryModules()
        {
            var catalog = new StageCatalog(new[] { Module("only", ModuleKind.Combat), Module("boss", ModuleKind.Boss) });
            Assert.AreEqual("only,only,only,only,boss", Signature(new RunGenerator().Generate(0, catalog, 1, 1)));
        }

        [Test] public void MissingKindsAndUnsupportedVersionsReturnExplicitFailure()
        {
            var generator = new RunGenerator();
            foreach (var catalog in new[] { new StageCatalog(Array.Empty<ModuleDescriptor>()),
                new StageCatalog(new[] { Module("a", ModuleKind.Combat) }),
                new StageCatalog(new[] { Module("b", ModuleKind.Boss) }) })
            {
                var result = generator.Generate(0, catalog, 1, 1);
                Assert.IsFalse(result.IsSuccess); Assert.IsNull(result.Layout); Assert.IsNotEmpty(result.Diagnostic);
            }
            Assert.IsFalse(generator.Generate(0, Catalog(), 2, 1).IsSuccess);
            Assert.IsFalse(generator.Generate(0, Catalog(), 1, 2).IsSuccess);
            Assert.Throws<ArgumentNullException>(() => generator.Generate(0, null, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(0, Catalog(), 0, 1));
        }
    }
}
