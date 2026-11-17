using System;
using System.Collections.Generic;
using Game.Contracts;
using Game.Core;
using Game.Street;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Game.Tests
{
    public sealed class StreetModuleTests
    {
        [Test] public void RouteCopiesInputAndRejectsInvalidComposition()
        {
            var ids=new[]{"street_a","street_b","street_c","street_a","street_boss"};
            var route=new StreetRoute(ids);ids[0]="bad";Assert.That(route.ModuleIds[0],Is.EqualTo("street_a"));
            Assert.Throws<ArgumentException>(()=>new StreetRoute(ids));
            Assert.Throws<ArgumentException>(()=>new StreetRoute(new[]{"street_boss"}));
        }
        private sealed class Flow : ISceneFlow
        {
            public int Loads;
            public bool TryLoad(string scene,Action<bool,string> done){Loads++;done(true,null);return true;}
        }
        private static CombatSnapshot Snapshot() => new CombatSnapshot(80,100,0,30,100,100,3,8,7,false,false,false);
        [Test] public void StreetProgressesOnceAndRejectsStaleSegmentThenPrefersDeath()
        {
            var flow=new Flow();using(var run=new RunCoordinator(flow))
            {
                run.Begin();run.StartRun(RunRequest.StreetRun(73));string id=run.RunId;
                for(int index=1;index<5;index++)
                {
                    run.CompleteArena(ScenePaths.Stage3StreetSandbox,Snapshot,index);
                    run.CompleteArena(ScenePaths.Stage3StreetSandbox,Snapshot,index);
                    run.ResolvePendingFinish();Assert.That(run.SegmentIndex,Is.EqualTo(index+1));
                    run.CompleteArena(ScenePaths.Stage3StreetSandbox,Snapshot,index);run.ResolvePendingFinish();
                    Assert.That(run.SegmentIndex,Is.EqualTo(index+1));Assert.That(run.LastOutcome,Is.Null);Assert.That(run.RunId,Is.EqualTo(id));
                }
                Assert.That(flow.Loads,Is.EqualTo(6));run.FinishRun(RunOutcome.Victory);run.FinishRun(RunOutcome.Defeat);run.ResolvePendingFinish();
                Assert.That(run.LastOutcome,Is.EqualTo(RunOutcome.Defeat));
            }
        }
        private sealed class DeferredFlow : ISceneFlow
        {
            private Action<bool,string> callback;
            public bool TryLoad(string scene,Action<bool,string> done){callback=done;return true;}
            public void Complete(){var done=callback;callback=null;done(true,null);}
        }
        [Test] public void DeadSnapshotAtBoundaryCannotEnterNextSegmentAndLoadingRejectsLateRequests()
        {
            var flow=new DeferredFlow();using(var run=new RunCoordinator(flow))
            {
                run.Begin();flow.Complete();run.StartRun(RunRequest.StreetRun());flow.Complete();
                var dead=new CombatSnapshot(0,100,0,30,100,100,0,8,7,true,false,true);
                run.CompleteArena(ScenePaths.Stage3StreetSandbox,()=>dead,1);run.ResolvePendingFinish();flow.Complete();
                Assert.That(run.LastOutcome,Is.EqualTo(RunOutcome.Defeat));Assert.That(run.SegmentIndex,Is.EqualTo(1));
                run.StartRun(RunRequest.StreetRun());flow.Complete();
                run.CompleteArena(ScenePaths.Stage3StreetSandbox,Snapshot,1);run.ResolvePendingFinish();
                Assert.That(run.Phase,Is.EqualTo(RunPhase.LoadingArena));
                run.CompleteArena(ScenePaths.Stage3StreetSandbox,()=>dead,1);run.FinishRun(RunOutcome.Defeat);
                flow.Complete();Assert.That(run.SegmentIndex,Is.EqualTo(2));Assert.That(run.LastOutcome,Is.Null);
            }
        }
        [Test] public void AllModuleCellsMatchPhysicsAndMandatoryPointsAreConnected()
        {
            foreach(string id in new[]{"street_entry","street_a","street_b","street_c","street_rest","street_boss"})
            {
                var d=AssetDatabase.LoadAssetAtPath<StreetModuleDefinition>("Assets/_Game/Data/Stages/Street/"+id+".asset");
                Assert.That(d,Is.Not.Null);Assert.That(d.Walkable.Length,Is.EqualTo(d.Width*d.Height));
                var offset=new Vector2(4000,4000);var instance=UnityEngine.Object.Instantiate(d.Prefab,offset,Quaternion.identity);
                try
                {
                    Physics2D.SyncTransforms();
                    for(int y=0;y<d.Height;y++) for(int x=0;x<d.Width;x++)
                    {
                        var cell=new Vector2Int(x,y);
                        bool free=Physics2D.OverlapCircle(d.CellCenter(cell)+offset,d.ActorClearance,LayerMask.GetMask("World"))==null;
                        Assert.That(d.Walkable[y*d.Width+x],Is.EqualTo(free),id+" "+cell);
                    }
                    var seen=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();queue.Enqueue(d.Entry);seen.Add(d.Entry);
                    while(queue.Count>0)
                    {
                        var current=queue.Dequeue();
                        foreach(var delta in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
                        {
                            var next=current+delta;
                            if(next.x>=0&&next.y>=0&&next.x<d.Width&&next.y<d.Height&&d.Walkable[next.y*d.Width+next.x]&&seen.Add(next))queue.Enqueue(next);
                        }
                    }
                    Assert.That(d.Walkable[d.Entry.y*d.Width+d.Entry.x],Is.True);
                    Assert.That(seen.Contains(d.Exit),Is.True,id+" exit unreachable");
                    foreach(var spawn in d.SpawnCells)Assert.That(seen.Contains(spawn),Is.True,id+" spawn unreachable");
                    if(d.Kind==StreetModuleKind.Boss)
                    {
                        var center=d.CellCenter(d.SpawnCells[0])+offset;
                        Assert.That(Physics2D.OverlapCircle(center,2.02f,LayerMask.GetMask("World")),Is.Null);
                        for(int candidate=0;candidate<24;candidate++)
                        {
                            float angle=(45+(candidate%4)*90+((candidate/4)%3)*30)*Mathf.Deg2Rad;
                            float radius=candidate<12?3:4.5f;
                            var point=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                            Assert.That(Physics2D.OverlapCircle(point,.45f,LayerMask.GetMask("World")),Is.Null,"Boss initial summon candidate blocked");
                        }
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(instance);}
            }
        }
    }
}
