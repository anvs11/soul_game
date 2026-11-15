using System;
using System.Collections;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Input;
using Game.Sandbox;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Game.Tests
{
    public sealed class Stage2ArenaTests
    {
        private GameBootstrap boot;
        private ArenaController arena;
        [UnitySetUp] public IEnumerator Setup()
        {
            foreach(var existing in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))Object.Destroy(existing.gameObject);
            Time.timeScale=1;yield return null;
            yield return SceneManager.LoadSceneAsync(ScenePaths.Bootstrap);
            yield return Until(()=> (boot=Object.FindFirstObjectByType<GameBootstrap>())!=null && boot.Coordinator.Phase==RunPhase.Home);
            var home=Object.FindFirstObjectByType<HomeScreen>();
            Assert.That(home.SecondaryButton,Is.Not.Null);
            home.SecondaryButton.onClick.Invoke();home.SecondaryButton.onClick.Invoke();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena);
            arena=Object.FindFirstObjectByType<ArenaController>();
            // Idle commands through the existing motor; no second production input path.
            var motor=arena.Player.GetComponent<ActorMotor2D>();motor.enabled=false;
            var input=arena.Player.gameObject.AddComponent<MovementTestInput>();motor.Configure(input);motor.enabled=true;
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
        }
        private static MonoBehaviour Enemy(string name)
        {
            foreach (var component in GameObject.Find(name).GetComponents<MonoBehaviour>())
                if (component is IEnemyTarget) return component;
            throw new InvalidOperationException("Missing enemy "+name);
        }
        [UnityTest] public IEnumerator RealSceneSeparatesNearHiddenAndFarAndCoverStopsRealBullet()
        {
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo(ScenePaths.Stage2CombatSandbox));
            Assert.That(Object.FindObjectsByType<TargetRegistry>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(arena.RemainingEnemies,Is.EqualTo(3));
            Assert.That(Enemy("NearZombie").GetComponent<EnemyPerception>().HasTarget,Is.True);
            var hidden=Enemy("HiddenZombie");var far=Enemy("FarZombie");
            Assert.That(hidden.GetComponent<EnemyPerception>().HasTarget,Is.False);
            Assert.That(far.GetComponent<EnemyPerception>().HasTarget,Is.False);
            Assert.That(hidden.GetComponent<Rigidbody2D>().linearVelocity,Is.EqualTo(Vector2.zero));
            Assert.That(far.GetComponent<Rigidbody2D>().linearVelocity,Is.EqualTo(Vector2.zero));
            var selector=arena.Player.GetComponent<AutoAimSelector>();
            Assert.That(selector.Select(arena.Player.transform.position,50,-1),Is.EqualTo(Enemy("NearZombie")));
            foreach(var name in new[]{"CoverA","CoverB","North","South","East","West"})
            {
                var cover=GameObject.Find(name);Assert.That(cover.layer,Is.EqualTo(LayerMask.NameToLayer("World")));
                Assert.That(cover.GetComponent<BoxCollider2D>().isTrigger,Is.False);
            }
            Enemy("NearZombie").enabled=false;
            float hp=hidden.GetComponent<DamageReceiver>().Health;
            Vector2 origin=arena.Player.transform.position;
            var request=new ProjectileSpawnRequest(origin,((Vector2)hidden.transform.position-origin).normalized,100,1,.08f,~0,
                arena.Player.GetComponent<ActorIdentity>().ActorId,CombatFaction.Player,new DamagePacket(-1,100));
            var pool=arena.Player.GetComponent<ProjectilePool>();
            Assert.That(pool.TrySpawn(in request),Is.True);
            yield return new WaitForSeconds(.2f);
            Assert.That(hidden.GetComponent<DamageReceiver>().Health,Is.EqualTo(hp),"Cover must stop actual projectile damage.");
            Assert.That(pool.ActiveCount,Is.Zero,"Bullet must return on cover contact, before its 1s lifetime expires.");
            var view=Object.FindFirstObjectByType<HudPresenter>();
            Assert.That(view.IsBound,Is.True);Assert.That(view.LastSnapshot.Health,Is.EqualTo(arena.Player.ReadSnapshot().Health));
        }
        [UnityTest] public IEnumerator MovingIntoSightAcquiresHiddenAndPauseAndRetryRemainFresh()
        {
            arena.Player.GetComponent<Rigidbody2D>().position=new Vector2(-6,-6);Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            var hidden=Enemy("HiddenZombie").GetComponent<EnemyPerception>();
            Assert.That(hidden.HasTarget,Is.True);Assert.That(hidden.HasLineOfSight,Is.True);
            Assert.That(Enemy("FarZombie").GetComponent<EnemyPerception>().HasTarget,Is.False);
            arena.Player.GetComponent<Rigidbody2D>().position=new Vector2(-12,-5);Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Assert.That(hidden.HasLineOfSight,Is.False);
            arena.Pause();double memory=hidden.SecondsWithoutSight;var position=hidden.transform.position;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(hidden.SecondsWithoutSight,Is.EqualTo(memory));Assert.That(hidden.transform.position,Is.EqualTo(position));
            arena.Resume();Assert.That(Time.timeScale,Is.EqualTo(1));
            string id=boot.Coordinator.RunId;var oldHero=arena.Player;
            arena.Leave();arena.Leave();yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
            var home=Object.FindFirstObjectByType<HomeScreen>();
            Assert.That(home.IsShowingResult,Is.True);Assert.That(home.SecondaryButton.gameObject.activeSelf,Is.False);
            home.PrimaryButton.onClick.Invoke();home.SecondaryButton.onClick.Invoke();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena);
            Assert.That(oldHero==null,Is.True);Assert.That(boot.Coordinator.RunId,Is.Not.EqualTo(id));
            arena=Object.FindFirstObjectByType<ArenaController>();
            Assert.That(arena.Player.ReadSnapshot().Health,Is.EqualTo(100));Assert.That(arena.Player.ReadSnapshot().Magazine,Is.EqualTo(8));
            Assert.That(arena.RemainingEnemies,Is.EqualTo(3));
            Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length,Is.Zero);
        }
        [UnityTest] public IEnumerator DiagnosticExitCountsDeathsAndOldProfileStillHasNoPerception()
        {
            arena.TryExit();Assert.That(boot.Coordinator.LastOutcome,Is.Null);
            foreach(var enemy in arena.Combatants)
                ((MonoBehaviour)enemy).GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));
            Assert.That(arena.RemainingEnemies,Is.Zero);
            arena.TryExit();Assert.That(boot.Coordinator.LastOutcome,Is.Null);
            arena.Player.transform.position=arena.ExitPosition;Physics2D.SyncTransforms();
            Assert.That(Vector2.Distance(arena.Player.transform.position,arena.ExitPosition),Is.LessThan(1.5f));
            arena.TryExit();arena.TryExit();yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
            Assert.That(boot.Coordinator.LastOutcome,Is.EqualTo(RunOutcome.Victory));
            var home=Object.FindFirstObjectByType<HomeScreen>();
            Assert.That(home.PrimaryButton.transform.parent.GetComponentsInChildren<UnityEngine.UI.Text>()[0].text,Does.Contain("Stage 2"));
            home.PrimaryButton.onClick.Invoke();home.PrimaryButton.onClick.Invoke();
            yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena);
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo(ScenePaths.CombatSandbox));
            Assert.That(Object.FindObjectsByType<EnemyPerception>(FindObjectsSortMode.None).Length,Is.Zero);
        }
        [UnityTest] public IEnumerator NewArenaWallsStopMotorKnockbackAndEnemyPhysics()
        {
            foreach(var enemy in arena.Combatants)((MonoBehaviour)enemy).enabled=false;
            var hero=arena.Player.GetComponent<Rigidbody2D>();var input=arena.Player.GetComponent<MovementTestInput>();
            var motor=arena.Player.GetComponent<ActorMotor2D>();
            var directions=new[]{Vector2.right,Vector2.left,Vector2.up,Vector2.down};
            foreach(var direction in directions)
            {
                Vector2 edge=direction.x!=0 ? direction*17 : direction*9;
                hero.position=edge;Physics2D.SyncTransforms();input.Command=new PlayerCommand(direction);
                yield return new WaitForSeconds(.35f);
                Assert.That(Mathf.Abs(hero.position.x),Is.LessThan(17.6f));Assert.That(Mathf.Abs(hero.position.y),Is.LessThan(9.6f));
                input.Command=default;motor.BeginKnockback(direction,3,.15f);yield return new WaitForSeconds(.2f);
                Assert.That(Mathf.Abs(hero.position.x),Is.LessThan(17.6f));Assert.That(Mathf.Abs(hero.position.y),Is.LessThan(9.6f));
            }
            var body=Enemy("FarZombie").GetComponent<Rigidbody2D>();body.position=new Vector2(17,3);body.linearVelocity=Vector2.right*10;
            Physics2D.SyncTransforms();yield return new WaitForSeconds(.4f);
            Assert.That(body.position.x,Is.LessThan(17.6f));
        }
        [UnityTest] public IEnumerator MixedTypesSafeAreaAndHudRemainUsable()
        {
            Assert.That(Object.FindObjectsByType<SandboxEnemy>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<JumpingEnemy>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<ShootingEnemy>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            yield return null;
            Canvas.ForceUpdateCanvases();
            var controls=(RectTransform)arena.transform.Find("BattleControlsCanvas/SafeArea");
            var hud=Object.FindFirstObjectByType<HudPresenter>();
            var safe=(RectTransform)hud.transform.Find("CombatHUD/SafeArea");
            Vector2 minimum=new Vector2(Screen.safeArea.xMin/Screen.width,Screen.safeArea.yMin/Screen.height);
            Vector2 maximum=new Vector2(Screen.safeArea.xMax/Screen.width,Screen.safeArea.yMax/Screen.height);
            Assert.That(controls.anchorMin,Is.EqualTo(minimum));Assert.That(safe.anchorMin,Is.EqualTo(minimum));
            Assert.That(controls.anchorMax,Is.EqualTo(maximum));Assert.That(safe.anchorMax,Is.EqualTo(maximum));
            var corners=new Vector3[4];
            foreach(var button in controls.GetComponentsInChildren<UnityEngine.UI.Image>())
            {
                button.rectTransform.GetWorldCorners(corners);
                var buttonRect=new Rect(corners[0],corners[2]-corners[0]);
                for(int index=0;index<3;index++)
                {
                    ((RectTransform)safe.Find("Bar"+index)).GetWorldCorners(corners);
                    Assert.That(buttonRect.Overlaps(new Rect(corners[0],corners[2]-corners[0])),Is.False,"Control overlaps HUD bar");
                }
            }
        }
        [UnityTest] public IEnumerator TenMixedAttemptsClearProjectilesAndPreferDefeatOnFinalExit()
        {
            var ids=new System.Collections.Generic.HashSet<string>();
            for(int attempt=0;attempt<10;attempt++)
            {
                Assert.That(ids.Add(boot.Coordinator.RunId),Is.True);
                Assert.That(arena.Player.ReadSnapshot().Health,Is.EqualTo(100));
                Assert.That(arena.Player.ReadSnapshot().Magazine,Is.EqualTo(8));
                var oldHero=arena.Player;
                var shooter=Object.FindFirstObjectByType<ShootingEnemy>();
                Vector2 shooterPosition=shooter.transform.position;
                var request=new ProjectileSpawnRequest(shooterPosition,Vector2.up,1,10,.12f,~0,shooter.ActorId,CombatFaction.Enemy,new DamagePacket(shooter.ActorId,10));
                Assert.That(shooter.GetComponent<ProjectilePool>().TrySpawn(in request),Is.True);
                foreach(var target in arena.Combatants)
                    ((MonoBehaviour)target).GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));
                Assert.That(arena.RemainingEnemies,Is.Zero);
                arena.Player.transform.position=arena.ExitPosition;Physics2D.SyncTransforms();
                arena.TryExit();arena.TryExit();
                if(attempt==9)arena.Player.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));
                yield return Until(()=>boot.Coordinator.Phase==RunPhase.Home);
                Assert.That(boot.Coordinator.LastOutcome,Is.EqualTo(attempt==9?RunOutcome.Defeat:RunOutcome.Victory));
                Assert.That(oldHero==null,Is.True);
                Assert.That(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length,Is.Zero);
                Assert.That(Time.timeScale,Is.EqualTo(1));
                if(attempt<9)
                {
                    var home=Object.FindFirstObjectByType<HomeScreen>();home.PrimaryButton.onClick.Invoke();home.SecondaryButton.onClick.Invoke();
                    yield return Until(()=>boot.Coordinator.Phase==RunPhase.InArena);
                    arena=Object.FindFirstObjectByType<ArenaController>();
                }
            }
        }

        [UnityTest] public IEnumerator MixedSceneAllowsDodgingBothJumpAndActualShot()
        {
            foreach(var target in arena.Combatants)((MonoBehaviour)target).enabled=false;
            var jumper=Object.FindFirstObjectByType<JumpingEnemy>();
            jumper.transform.position=Vector2.zero;arena.Player.transform.position=new Vector2(-3,0);
            jumper.enabled=true;jumper.Initialize(Object.FindFirstObjectByType<TargetRegistry>(),arena.Player.transform);Physics2D.SyncTransforms();
            yield return Until(()=>jumper.Phase==JumpingEnemy.AttackPhase.Windup);
            arena.Player.transform.position=new Vector2(-3,3);Physics2D.SyncTransforms();
            yield return Until(()=>jumper.Phase==JumpingEnemy.AttackPhase.Recovery);
            yield return new WaitForSeconds(.1f);
            Assert.That(arena.Player.ReadSnapshot().Armor,Is.EqualTo(30));Assert.That(arena.Player.ReadSnapshot().Health,Is.EqualTo(100));
            jumper.enabled=false;
            var shooter=Object.FindFirstObjectByType<ShootingEnemy>();
            shooter.transform.position=Vector2.zero;arena.Player.transform.position=new Vector2(-5,0);
            shooter.enabled=true;shooter.Initialize(Object.FindFirstObjectByType<TargetRegistry>(),arena.Player.transform);Physics2D.SyncTransforms();
            int shots=shooter.ShotsFired;
            yield return Until(()=>shooter.ShotsFired>shots);
            arena.Player.transform.position=new Vector2(-5,3);Physics2D.SyncTransforms();yield return new WaitForSeconds(.8f);
            Assert.That(arena.Player.ReadSnapshot().Armor,Is.EqualTo(30));Assert.That(arena.Player.ReadSnapshot().Health,Is.EqualTo(100));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach(var existing in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))Object.Destroy(existing.gameObject);
            Time.timeScale=1;yield return null;
        }
        private static IEnumerator Until(Func<bool> predicate)
        {
            float timeout=Time.realtimeSinceStartup+15;
            while(!predicate()){Assert.That(Time.realtimeSinceStartup,Is.LessThan(timeout),"Scene transition timed out");yield return null;}
        }
    }
}
