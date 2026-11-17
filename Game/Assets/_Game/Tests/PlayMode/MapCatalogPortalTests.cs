using System;
using System.Collections;
using System.Linq;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.StreetMap;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Game.Tests
{
    public sealed class MapCatalogPortalTests
    {
        private GameBootstrap boot;private StreetMapScreen map;
        [UnitySetUp]public IEnumerator Setup()
        {
            foreach(var old in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))Object.Destroy(old.gameObject);
            Time.timeScale=1;yield return null;yield return SceneManager.LoadSceneAsync(ScenePaths.Bootstrap);
            yield return Until(()=>{boot=Object.FindFirstObjectByType<GameBootstrap>();return boot!=null && boot.Coordinator!=null && boot.Coordinator.Phase==RunPhase.Home;});
            Object.FindFirstObjectByType<HomeScreen>().FixedStreetButton.onClick.Invoke();yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena);
            map=Object.FindFirstObjectByType<StreetMapScreen>();
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            Time.timeScale=1;if(boot!=null)Object.Destroy(boot.gameObject);yield return null;
            var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("MapCatalogCleanup"));if(old.isLoaded)yield return SceneManager.UnloadSceneAsync(old);
        }
        [UnityTest]public IEnumerator MiniMap_ProjectsHeroFullMapSafeAreaWithoutEnemyMarkers()
        {
            map.Player.GetComponent<Rigidbody2D>().position=new Vector2(10,0);yield return null;yield return null;
            Assert.That(Vector2.Distance(map.MiniMap.HeroMapPosition,StreetMiniMap.Project(map.Player.transform.position)),Is.LessThan(.01f));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(map.MiniMap.SafeRoot.GetComponentsInChildren<UnityEngine.UI.Image>().Length,Is.EqualTo(11),"Background, 5 rooms, 4 roads, hero; no enemies.");
            var safe=map.MiniMap.SafeRoot;var area=Screen.safeArea;
            Assert.That(safe.anchorMin.x,Is.EqualTo(area.xMin/Screen.width).Within(.001));
            Assert.That(safe.anchorMax.y,Is.EqualTo(area.yMax/Screen.height).Within(.001));
        }
        [UnityTest]public IEnumerator Catalog_PhysicalClearanceAndReachabilityCoverEveryPointAndFinalExit()
        {
            var catalog=StreetMapCatalog.Create();var reachable=StreetMapCatalog.Reachable(.45f);
            foreach(var room in catalog.Rooms)foreach(var point in room.Points)
            {
                Vector2 world=new Vector2(room.CenterX+point.X,room.CenterY+point.Y);
                Assert.That(Physics2D.OverlapCircle(world,point.ClearanceRadius,LayerMask.GetMask("World")),Is.Null,room.RoomId+"/"+point.PointId);
                Assert.That(reachable.Contains(Vector2Int.FloorToInt(world)),Is.True);
            }
            Assert.That(catalog.Rooms[1].Budget.MinEnemies,Is.EqualTo(6));Assert.That(catalog.Rooms[1].Budget.MaxEnemies,Is.EqualTo(8));
            Assert.That(catalog.Rooms[1].Points.Count,Is.EqualTo(9));Assert.That(reachable.Contains(new Vector2Int(85,24)),Is.True);
            yield return null;
        }
        [UnityTest]public IEnumerator FourClears_OnePortal_RetriesTemporaryObstructionAndReturnsHome()
        {
            map.TryExit();Assert.That(map.Portal.IsOpen,Is.False);
            for(int index=0;index<4;index++)
            {
                var room=map.Rooms[index];map.Player.GetComponent<Rigidbody2D>().position=StreetMapLayout.Rooms[index+1].Center;Physics2D.SyncTransforms();
                float deadline=Time.realtimeSinceStartup+8;
                while(room.State.Phase!=RoomPhase.Cleared)
                {
                    Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));
                    foreach(var enemy in Object.FindObjectsByType<SandboxEnemy>(FindObjectsSortMode.None))
                    {
                        if(enemy.IsDead)continue;
                        GameObject blocker=null;
                        if(index==3 && room.State.AliveCount==1)
                        {blocker=new GameObject("TemporaryPortalObstruction",typeof(BoxCollider2D));blocker.layer=LayerMask.NameToLayer("World");blocker.transform.position=new Vector2(83,24);blocker.GetComponent<BoxCollider2D>().size=new Vector2(28,24);Physics2D.SyncTransforms();}
                        var hit=new DamagePacket(-1,999);enemy.GetComponent<DamageReceiver>().ApplyDamage(in hit);
                        if(blocker!=null){Assert.That(map.Portal.IsOpen,Is.False);Object.Destroy(blocker);}
                    }
                    yield return null;
                }
            }
            Assert.That(map.ClearedCount,Is.EqualTo(4));map.TryExit();
            Assert.That(map.Portal.IsOpen,Is.True);Assert.That(Object.FindObjectsByType<FixedMapPortal>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(boot.Coordinator.LastOutcome,Is.Null,"Far interaction cannot leave.");
            map.Pause();map.Player.GetComponent<Rigidbody2D>().position=map.Portal.transform.position;map.TryExit();Assert.That(boot.Coordinator.LastOutcome,Is.Null);map.Resume();
            yield return new WaitForFixedUpdate();yield return null;
            map.TryExit();map.TryExit();yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
            Assert.That(boot.Coordinator.LastOutcome,Is.EqualTo(RunOutcome.Victory));Assert.That(Object.FindFirstObjectByType<FixedMapPortal>(),Is.Null);
        }
        private static IEnumerator Until(Func<bool> condition){float until=Time.realtimeSinceStartup+20;while(!condition()){Assert.That(Time.realtimeSinceStartup,Is.LessThan(until));yield return null;}}
    }
}
