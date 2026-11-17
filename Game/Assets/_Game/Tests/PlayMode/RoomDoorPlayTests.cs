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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Game.Tests
{
    public sealed class RoomDoorPlayTests
    {
        private GameBootstrap boot;
        private StreetMapScreen screen;
        private Rigidbody2D body;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach(var old in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            Time.timeScale=1; yield return null;
            yield return SceneManager.LoadSceneAsync(ScenePaths.Bootstrap);
            yield return Until(()=> {boot=Object.FindFirstObjectByType<GameBootstrap>(); return boot!=null && boot.Coordinator!=null && boot.Coordinator.Phase==RunPhase.Home;});
            Object.FindFirstObjectByType<HomeScreen>().FixedStreetButton.onClick.Invoke();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena);
            screen=Object.FindFirstObjectByType<StreetMapScreen>(); body=screen.Player.GetComponent<Rigidbody2D>();
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale=1;
            if(boot!=null) Object.Destroy(boot.gameObject);
            yield return null;
            var old=SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("RoomDoorCleanup"));
            if(old.IsValid() && old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
        }
        private void PlaceHero(Vector2 point) { body.position=point; Physics2D.SyncTransforms(); }
        private static SandboxEnemy[] ActiveEnemies() => Object.FindObjectsByType<SandboxEnemy>(FindObjectsSortMode.None).Where(e=>!e.IsDead).ToArray();
        private static void Kill(SandboxEnemy enemy)
        { var hit=new DamagePacket(-1,999); enemy.GetComponent<DamageReceiver>().ApplyDamage(in hit); }
        private IEnumerator BeginGap()
        {
            PlaceHero(new Vector2(23,24));
            yield return Until(()=>screen.Rooms[1].State.AliveCount==1);
            Kill(ActiveEnemies().Single());
            Assert.That(screen.Rooms[1].State.Phase,Is.EqualTo(RoomPhase.Waiting));
            Assert.That(screen.Rooms[1].State.PendingCount,Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator DelayedSecondWave_PauseKeepsDoorsClosed_ThenOneClearAndNoRespawn()
        {
            int clears=0; string key=null;
            screen.RoomCleared+=(run,room)=> {clears++;key=run+"/"+room;};
            yield return BeginGap();
            var room=screen.Rooms[1];
            Assert.That(ActiveEnemies(),Is.Empty); Assert.That(room.Doors.All(d=>d.IsClosed),Is.True);
            screen.Pause(); float remaining=room.State.DelayRemaining;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(room.State.DelayRemaining,Is.EqualTo(remaining)); Assert.That(clears,Is.Zero);
            screen.Resume(); yield return new WaitForSeconds(1);
            Assert.That(room.State.AliveCount,Is.Zero); Assert.That(room.Doors.All(d=>d.IsClosed),Is.True);
            yield return Until(()=>room.State.AliveCount==1);
            var second=ActiveEnemies().Single();
            second.gameObject.SetActive(false); yield return new WaitForFixedUpdate();
            Assert.That(room.State.AliveCount,Is.EqualTo(1),"Disable is not death.");
            Assert.That(room.Doors.All(d=>d.IsClosed),Is.True);
            second.gameObject.SetActive(true); Kill(second); Kill(second);
            Assert.That(room.State.Phase,Is.EqualTo(RoomPhase.Cleared));
            Assert.That(room.Doors.All(d=>!d.IsClosed),Is.True);
            Assert.That(clears,Is.EqualTo(1)); Assert.That(key,Is.EqualTo(boot.Coordinator.RunId+"/combat_b"));
            PlaceHero(new Vector2(23,12)); yield return new WaitForFixedUpdate();
            PlaceHero(new Vector2(23,24)); yield return new WaitForFixedUpdate();
            Assert.That(clears,Is.EqualTo(1)); Assert.That(ActiveEnemies(),Is.Empty);
            Assert.That(screen.Rooms[0].State.Phase,Is.EqualTo(RoomPhase.Dormant));
            Assert.That(screen.Rooms[2].State.Phase,Is.EqualTo(RoomPhase.Dormant));
        }
        [UnityTest]
        public IEnumerator DiagonalThreshold_WholeHeroEntersBeforeLock_AndKnockbackCannotCrossDoor()
        {
            PlaceHero(new Vector2(12.65f,1.3f));
            yield return new WaitForFixedUpdate();
            var room=screen.Rooms[0]; Assert.That(room.State.Phase,Is.EqualTo(RoomPhase.Dormant));
            Assert.That(room.Doors.All(d=>!d.IsClosed),Is.True);
            var motor=screen.Player.GetComponent<ActorMotor2D>(); motor.enabled=false;
            var commands=screen.Player.gameObject.AddComponent<MapTestCommands>(); motor.Configure(commands); motor.enabled=true;
            commands.Move=new Vector2(1,1);
            yield return Until(()=>room.State.Phase==RoomPhase.Fighting);
            commands.Move=Vector2.zero;
            Assert.That(body.position.x,Is.GreaterThan(13.5f));
            Assert.That(room.Doors.All(d=>d.IsClosed),Is.True);
            Assert.That(ActiveEnemies().Length,Is.EqualTo(2));
            PlaceHero(new Vector2(14.5f,0));
            motor.BeginKnockback(Vector2.left,5,.15f);
            yield return new WaitForSeconds(.3f);
            Assert.That(body.position.x,Is.GreaterThan(13.5f),"Knockback must stop at the locked gate.");
            var enemy=ActiveEnemies()[0]; enemy.enabled=false;
            var enemyBody=enemy.GetComponent<Rigidbody2D>(); enemyBody.position=new Vector2(14.5f,1.3f); enemyBody.linearVelocity=Vector2.left*20;
            yield return new WaitForSeconds(.25f);
            Assert.That(enemyBody.position.x,Is.GreaterThan(13.5f),"Enemy physics must also be held by the gate.");
            Assert.That(room.State.AliveCount,Is.EqualTo(2));
        }
        [UnityTest]
        public IEnumerator OccupiedSpawn_StaysPendingAndRetriesWithoutFalseClear()
        {
            PlaceHero(new Vector2(20,2));
            var room=screen.Rooms[0];
            yield return Until(()=>room.State.AliveCount==1);
            Assert.That(room.State.PendingCount,Is.EqualTo(1));
            Kill(ActiveEnemies().Single());
            Assert.That(room.State.Phase,Is.Not.EqualTo(RoomPhase.Cleared));
            PlaceHero(new Vector2(16,0));
            yield return Until(()=>room.State.AliveCount==1);
            Kill(ActiveEnemies().Single());
            Assert.That(room.State.Phase,Is.EqualTo(RoomPhase.Cleared));
        }
        [UnityTest]
        public IEnumerator DisableRoomDuringGap_CancelsWithoutDeathClearOrLaterSpawn()
        {
            int clears=0; screen.RoomCleared+=(a,b)=>clears++;
            yield return BeginGap(); var room=screen.Rooms[1]; room.enabled=false;
            yield return new WaitForSeconds(2.2f);
            Assert.That(room.State.Phase,Is.EqualTo(RoomPhase.Cancelled));
            Assert.That(room.State.PendingCount,Is.EqualTo(1)); Assert.That(clears,Is.Zero);
            Assert.That(ActiveEnemies(),Is.Empty); Assert.That(room.Doors.All(d=>d.IsClosed),Is.True);
        }
        [UnityTest]
        public IEnumerator HeroDeathDuringGap_CancelsBeforeSecondWaveAndReturnsHome()
        {
            int clears=0; screen.RoomCleared+=(a,b)=>clears++;
            yield return BeginGap(); var state=screen.Rooms[1].State;
            var hit=new DamagePacket(-1,999); screen.Player.GetComponent<DamageReceiver>().ApplyDamage(in hit);
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
            yield return new WaitForSeconds(2.1f);
            Assert.That(state.Phase,Is.EqualTo(RoomPhase.Cancelled)); Assert.That(state.PendingCount,Is.EqualTo(1));
            Assert.That(clears,Is.Zero); Assert.That(ActiveEnemies(),Is.Empty);
            Assert.That(boot.Coordinator.LastOutcome,Is.EqualTo(RunOutcome.Defeat));
        }
        [UnityTest]
        public IEnumerator LeavingDuringGap_UnloadsAndCancelsWithoutRewardOrNewActors()
        {
            int clears=0; screen.RoomCleared+=(a,b)=>clears++;
            yield return BeginGap(); var state=screen.Rooms[1].State;
            screen.Leave();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
            yield return new WaitForSeconds(2.1f);
            Assert.That(state.Phase,Is.EqualTo(RoomPhase.Cancelled)); Assert.That(state.PendingCount,Is.EqualTo(1));
            Assert.That(clears,Is.Zero); Assert.That(ActiveEnemies(),Is.Empty);
            Assert.That(Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None),Is.Empty);
        }
        private static IEnumerator Until(Func<bool> condition)
        {
            float deadline=Time.realtimeSinceStartup+10;
            while(!condition()) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),"Room/scene timed out."); yield return null; }
        }
    }
}
