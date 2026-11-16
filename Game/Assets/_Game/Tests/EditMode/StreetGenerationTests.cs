using System;
using System.Collections.Generic;
using Game.Core;
using Game.Generation;
using Game.Street;
using NUnit.Framework;
using UnityEngine;
namespace Game.Tests
{
    public sealed class StreetGenerationTests
    {
        private static StageCatalog Catalog() => StreetGenerationAdapter.BuildCatalog(Resources.Load<StreetCatalogAsset>("StreetCatalog").Modules);
        [Test] public void ActualExportMatchesUnityAssetsAndProductionThousandSeedReviewPasses()
        {
            var live=Catalog();var exported=GenerationSweepReview.LoadExport("Game/Docs/Exports/S3_STREET_CATALOG.json");
            Assert.That(exported.Modules.Count,Is.EqualTo(6));
            for(int i=0;i<6;i++)
            {
                var a=live.Modules[i];var b=exported.Modules[i];
                Assert.That(a.Id,Is.EqualTo(b.Id));Assert.That(a.Kind,Is.EqualTo(b.Kind));
                Assert.That(a.Width,Is.EqualTo(b.Width));Assert.That(a.Height,Is.EqualTo(b.Height));
                Assert.That(a.Walkable,Is.EqualTo(b.Walkable));Assert.That(a.Entry,Is.EqualTo(b.Entry));Assert.That(a.Exit,Is.EqualTo(b.Exit));Assert.That(a.SpawnCells,Is.EqualTo(b.SpawnCells));
            }
            var summary=GenerationSweepReview.Review(exported);Assert.That(summary.Successes,Is.EqualTo(1000));Assert.That(summary.Failures,Is.Zero);
        }
        [Test] public void SeedParsingRepeatAndCorruptDataFailBeforeRunStart()
        {
            var catalog=Catalog();
            foreach(string text in new[]{"abc","2147483648","1.5"})
            {Assert.That(StreetGenerationAdapter.TryGenerate(text,catalog,out var result,out var error),Is.False);Assert.That(result,Is.Null);Assert.That(error,Does.Contain("Seed"));}
            foreach(string text in new[]{"-2147483648","2147483647","-42",""})
            {Assert.That(StreetGenerationAdapter.TryGenerate(text,catalog,out var result,out var error),Is.True,error);Assert.That(result.Layout.Segments.Count,Is.EqualTo(5));}
            StreetGenerationAdapter.TryGenerate("123",catalog,out var first,out _);StreetGenerationAdapter.TryGenerate("123",catalog,out var second,out _);
            Assert.That(StreetGenerationAdapter.ToRoute(first.Layout).ModuleIds,Is.EqualTo(StreetGenerationAdapter.ToRoute(second.Layout).ModuleIds));
            var broken=new List<ModuleDescriptor>();
            foreach(var m in catalog.Modules)
            {
                var grid=new List<bool>(m.Walkable);if(m.Kind==ModuleKind.Boss)grid[m.Entry.Y*m.Width+m.Entry.X]=false;
                broken.Add(new ModuleDescriptor(m.Id,m.Kind,m.Width,m.Height,grid,m.Entry,m.Exit,m.SpawnCells));
            }
            Assert.That(StreetGenerationAdapter.TryGenerate("123",new StageCatalog(broken),out var failed,out var reason),Is.False);
            Assert.That(failed.IsSuccess,Is.False);Assert.That(reason,Does.Contain("street_boss"));
        }
        [Test] public void NaturalFallbackRemainsValidatedAndPublishesItsCause()
        {
            var modules=new List<ModuleDescriptor>(Catalog().Modules);
            for(int i=0;i<64;i++)modules.Add(new ModuleDescriptor("bad_"+i.ToString("D2"),ModuleKind.Combat,1,1,new[]{false},new Cell(0,0),new Cell(0,0),new[]{new Cell(0,0)}));
            Assert.That(StreetGenerationAdapter.TryGenerate("0",new StageCatalog(modules),out var result,out var error),Is.True,error);
            Assert.That(result.UsedFallback,Is.True);Assert.That(result.Diagnostic,Does.Contain("last rejection=").And.Contain("validated fallback"));
            Assert.That(StreetGenerationAdapter.ToRoute(result.Layout).ModuleIds,Is.EqualTo(StreetRoute.Fixed().ModuleIds));
        }
        private sealed class Flow : ISceneFlow
        {
            public int Loads;
            public bool TryLoad(string scene,Action<bool,string> done){Loads++;done(true,null);return true;}
        }
        [Test] public void CoordinatorRejectsForgedSuccessBeforeLoadingAndRetainsGenerationMetadata()
        {
            var catalog=Catalog();var flow=new Flow();using(var run=new RunCoordinator(flow))
            {
                run.Begin();
                var forged=new RunLayout(1,1,1,new[]{new SegmentLayout(1,"missing")});
                Assert.Throws<ArgumentException>(()=>run.StartGeneratedStreetRun(GenerationResult.Success(forged,false,"forged"),catalog));
                Assert.That(run.Phase,Is.EqualTo(RunPhase.Home));Assert.That(flow.Loads,Is.EqualTo(1));
                StreetGenerationAdapter.TryGenerate("91",catalog,out var result,out _);run.StartGeneratedStreetRun(result,catalog);
                Assert.That(run.StreetGeneration,Is.SameAs(result));Assert.That(run.CurrentRequest.Seed,Is.EqualTo(91));
            }
        }
    }
}
