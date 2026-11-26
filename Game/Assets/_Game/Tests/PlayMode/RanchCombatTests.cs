#if UNITY_EDITOR
using System;
using System.Collections;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Encounters;
using Game.Progression;
using Game.StreetMap;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Phase = Game.AI.RanchDeer.AttackPhase;

namespace Game.Tests
{
    public sealed class RanchCombatTests
    {
        private const string Prefabs = "Assets/_Game/Prefabs/Ranch/";
        private static readonly Vector2 Origin = new(2300, 2100);
        private GameObject root;
        private Rigidbody2D heroBody;
        private PlayerCombatFacade hero;
        private DamageReceiver heroDamage;
        private ActorMotor2D motor;
        private float previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1;
            root = new GameObject("Ranch combat fixture");
            var player = new GameObject("Hero");
            player.SetActive(false);
            player.transform.SetParent(root.transform);
            player.transform.position = Origin + Vector2.right * 4;
            var input = player.AddComponent<MovementTestInput>();
            motor = player.AddComponent<ActorMotor2D>();
            motor.Configure(input);
            heroBody = player.GetComponent<Rigidbody2D>();
            heroBody.gravityScale = 0;
            heroBody.freezeRotation = true;
            player.AddComponent<CircleCollider2D>().radius = .42f;
            player.AddComponent<ActorIdentity>();
            heroDamage = player.AddComponent<DamageReceiver>();
            heroDamage.Configure(100, 0, 0);
            hero = player.AddComponent<PlayerCombatFacade>();
            player.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = previousTimeScale;
            Object.DestroyImmediate(root);
        }

        [UnityTest]
        public IEnumerator WarningFreezesInPauseAndLocksDirectionThroughMissAndRecovery()
        {
            RanchDeer deer = CreateDeer();
            yield return Until(() => deer.Phase == Phase.Warning, "deer warning");
            Vector2 direction = deer.ChargeDirection;
            Assert.That(direction, Is.EqualTo(Vector2.right));
            Assert.That(deer.WarningVisible, Is.True);
            var marker = deer.transform.Find("Ranch deer charge lane").GetComponent<SpriteRenderer>();
            Assert.That(marker.bounds.size.y, Is.EqualTo(1.6f).Within(.01f));
            Assert.That(marker.bounds.size.x, Is.EqualTo(9.6f).Within(.01f));
            Vector2 start = deer.GetComponent<Rigidbody2D>().position;

            MoveHero(new Vector2(4, 3));
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(RanchDeer.WarningSeconds + .1f);
            Assert.That(deer.Phase, Is.EqualTo(Phase.Warning));
            Assert.That(deer.WarningVisible, Is.True);
            Assert.That(deer.ChargeDirection, Is.EqualTo(direction));
            Assert.That(Position(deer), Is.EqualTo(start));

            Time.timeScale = 1;
            yield return Until(() => deer.Phase == Phase.Charge, "deer charge");
            Assert.That(deer.WarningVisible, Is.False);
            yield return Until(() => Position(deer).x > start.x + 1, "first charge movement");
            Vector2 paused = Position(deer);
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(deer.Phase, Is.EqualTo(Phase.Charge));
            Assert.That(Position(deer), Is.EqualTo(paused));
            Time.timeScale = 1;
            yield return Until(() => deer.Phase == Phase.Recovery, "miss recovery");
            Assert.That(deer.ChargeDirection, Is.EqualTo(direction));
            Assert.That(Position(deer).y, Is.EqualTo(start.y).Within(.03f));
            Assert.That(heroDamage.Health, Is.EqualTo(100));
            Assert.That(Position(deer).x - start.x, Is.EqualTo(RanchDeer.ChargeDistance).Within(.08f));

            float initialHealth = deer.GetComponent<DamageReceiver>().Health;
            Assert.That(deer.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(900, 15)).Applied, Is.True);
            Assert.That(deer.GetComponent<DamageReceiver>().Health, Is.EqualTo(initialHealth - 15));
            var stopped = Position(deer);
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(RanchDeer.RecoverySeconds + .1f);
            Assert.That(deer.Phase, Is.EqualTo(Phase.Recovery));
            Time.timeScale = 1;
            yield return new WaitForSeconds(.9f);
            Assert.That(deer.Phase, Is.EqualTo(Phase.Recovery));
            Assert.That(Position(deer), Is.EqualTo(stopped));
            yield return Until(() => deer.Phase != Phase.Recovery, "end of recovery");
        }

        [UnityTest]
        public IEnumerator WorldStopsChargeAndShieldsHeroBehindWall()
        {
            RanchDeer deer = CreateDeer();
            yield return Until(() => deer.Phase == Phase.Warning, "deer warning");
            var wall = new GameObject("Barn wall");
            wall.transform.SetParent(root.transform);
            wall.transform.position = Origin + Vector2.right * 2.2f;
            wall.layer = LayerMask.NameToLayer("World");
            Assert.That(wall.layer, Is.GreaterThanOrEqualTo(0));
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.24f, 6);
            Physics2D.SyncTransforms();

            yield return Until(() => deer.Phase == Phase.Recovery, "wall recovery");
            float distance = Position(deer).x - Origin.x;
            Assert.That(distance, Is.InRange(1f, 1.29f));
            Assert.That(heroDamage.Health, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator ContactDamagesOnceAndDeerDiesThroughRegistry()
        {
            RanchDeer deer = CreateDeer();
            int hits = 0;
            heroDamage.DamageApplied += _ => hits++;
            yield return Until(() => deer.Phase == Phase.Recovery, "contact recovery");
            yield return new WaitForSeconds(.3f);
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(heroDamage.Health, Is.EqualTo(100 - RanchDeer.ChargeDamage));

            yield return Until(() => deer.WarningVisible, "next warning");
            int deaths = 0;
            deer.Died += () => deaths++;
            Assert.That(deer.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(900, 1000)).Killed, Is.True);
            Assert.That(deer.WarningVisible, Is.False);
            Assert.That(deer.GetComponent<Rigidbody2D>().simulated, Is.False);
            Assert.That(root.GetComponent<TargetRegistry>().Count, Is.Zero);
            Assert.That(deaths, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator VisibleTrapHoldsMovementThroughPauseThenReleasesAndOnlyFiresOnce()
        {
            var trap = Prefab<RanchTrap>("RanchTrap", Origin);
            var trigger = trap.GetComponent<BoxCollider2D>();
            var sprite = trap.GetComponent<SpriteRenderer>();
            var armedTint = sprite.color;
            Assert.That(sprite.enabled && sprite.sprite != null, Is.True);
            Assert.That(trigger.isTrigger, Is.True);
            Assert.That(trigger.size, Is.EqualTo(new Vector2(.94f, .58f)));

            MoveHero(Vector2.zero);
            yield return Until(() => trap.IsSpent, "trap triggers");
            var status = hero.GetComponent<ActorStatusController>();
            Assert.That(motor.Permissions.CanMove, Is.False);
            Assert.That(motor.Permissions.CanDodge, Is.False);
            Assert.That(motor.Permissions.CanAttack, Is.True);
            Assert.That(trigger.enabled, Is.False);
            Assert.That(sprite.color, Is.Not.EqualTo(armedTint));

            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(RanchTrap.HoldSeconds + .1f);
            Assert.That(status.Contains(trap.GetInstanceID(), RanchTrap.TrapId), Is.True);
            Time.timeScale = 1;
            yield return Until(() => motor.Permissions.CanMove, "hold releases");
            Assert.That(status.ActiveCount, Is.Zero);

            MoveHero(Vector2.up * 3);
            yield return new WaitForFixedUpdate();
            MoveHero(Vector2.zero);
            yield return new WaitForFixedUpdate();
            Assert.That(status.ActiveCount, Is.Zero);
            var fresh = Prefab<RanchTrap>("RanchTrap", Origin + Vector2.up * 3);
            Assert.That(fresh.IsSpent, Is.False);
            MoveHero(Vector2.up * 3);
            yield return Until(() => fresh.IsSpent, "fresh attempt trap triggers");
        }

        [Test]
        public void DeerTypeIsValidForEncounterAndBossLoot()
        {
            Assert.That(EnemyTypes.IsKnown(EnemyTypes.RanchDeer), Is.True);
            Assert.That(EnemyTypes.Radius(EnemyTypes.RanchDeer), Is.EqualTo(1.2f));
            Assert.That(EnemyTypes.Cost(EnemyTypes.RanchDeer), Is.EqualTo(9));
            Assert.That(CityEconomy.RollLoot(17, "ranch/5-1/barn/deer", EnemyTypes.RanchDeer).Crystals, Is.EqualTo(270));
        }

        private RanchDeer CreateDeer()
        {
            var deer = Prefab<RanchDeer>("RanchDeer", Origin);
            Assert.That(deer.gameObject.activeSelf, Is.False);
            deer.Initialize(root.AddComponent<TargetRegistry>(), hero);
            deer.gameObject.SetActive(true);
            Assert.That(deer.CollisionRadius, Is.EqualTo(.8f).Within(.001f));
            return deer;
        }

        private T Prefab<T>(string name, Vector2 position) where T : Component
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab");
            Assert.That(asset, Is.Not.Null);
            return Object.Instantiate(asset, position, Quaternion.identity, root.transform).GetComponent<T>();
        }

        private void MoveHero(Vector2 offset)
        {
            heroBody.position = Origin + offset;
            Physics2D.SyncTransforms();
        }

        private static Vector2 Position(RanchDeer deer) => deer.GetComponent<Rigidbody2D>().position;

        private static IEnumerator Until(Func<bool> condition, string step)
        {
            double limit = Time.realtimeSinceStartupAsDouble + 6;
            while (!condition() && Time.realtimeSinceStartupAsDouble < limit) yield return null;
            Assert.That(condition(), Is.True, step);
        }
    }
}
#endif
