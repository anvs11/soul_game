using System.Collections;
using Game.Actors;
using Game.Combat;
using Game.Contracts;
using Game.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Game.Tests
{
    public sealed class MeleeAttackTests
    {
        [UnityTest] public IEnumerator SectorDeduplicatesCollidersAndWallBlocksSwing()
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            var state = new WeaponRuntimeState(definition); state.SetSelected(false);
            var attacker = new GameObject("Melee test attacker");
            var melee = attacker.AddComponent<MeleeAttack>();
            var target = new GameObject("Two collider target");
            Vector2 origin = new Vector2(2000, 2000);
            target.transform.position = origin + Vector2.right;
            target.AddComponent<CircleCollider2D>().radius = .1f;
            target.AddComponent<BoxCollider2D>().size = Vector2.one * .2f;
            var receiver = target.AddComponent<DamageReceiver>();
            var wall = new GameObject("Melee test wall");
            wall.transform.position = origin + Vector2.right * .5f;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.1f, 2);
            int applied = 0; receiver.DamageApplied += _ => applied++;
            try
            {
                Physics2D.SyncTransforms();
                Assert.IsTrue(melee.TryAttack(state, origin, Vector2.right, 1, CombatFaction.Player, ~0, 1));
                Assert.That(receiver.Armor, Is.EqualTo(30));
                Object.DestroyImmediate(wall); Physics2D.SyncTransforms(); state.Advance(.45f);
                // Opposite direction misses even though both colliders are inside the radius.
                Assert.IsTrue(melee.TryAttack(state, origin, Vector2.left, 1, CombatFaction.Player, ~0, 2));
                Assert.That(applied, Is.Zero); state.Advance(.45f);
                Vector2 outsideSector = new Vector2(Mathf.Cos(60 * Mathf.Deg2Rad), Mathf.Sin(60 * Mathf.Deg2Rad));
                Assert.IsTrue(melee.TryAttack(state, origin, outsideSector, 1, CombatFaction.Player, ~0, 3));
                Assert.That(applied, Is.Zero); state.Advance(.45f);
                Vector2 insideSector = new Vector2(Mathf.Cos(40 * Mathf.Deg2Rad), Mathf.Sin(40 * Mathf.Deg2Rad));
                Assert.IsTrue(melee.TryAttack(state, origin, insideSector, 1, CombatFaction.Player, ~0, 4));
                Assert.That(melee.LastSwingTargetCount, Is.EqualTo(1), "Even rejected repeated damage must not be attempted twice.");
                Assert.That(applied, Is.EqualTo(1)); Assert.That(receiver.Armor, Is.EqualTo(15));
                Assert.That(state.Stamina, Is.EqualTo(20));
                yield return new WaitForSeconds(.55f);
                state.Advance(.55f); target.transform.position = origin + Vector2.right * 3;
                Physics2D.SyncTransforms();
                Assert.IsTrue(melee.TryAttack(state, origin, Vector2.right, 1, CombatFaction.Player, ~0, 5));
                Assert.That(applied, Is.EqualTo(1)); // Outside radius.
            }
            finally
            {
                if (wall != null) Object.DestroyImmediate(wall);
                Object.DestroyImmediate(target); Object.DestroyImmediate(attacker); Object.DestroyImmediate(definition);
            }
        }
        [UnityTest] public IEnumerator ControllerSwitchPublishesRealResourceAndRespectsBlocking()
        {
            var go = new GameObject("Weapon integration test"); go.SetActive(false);
            var input = go.AddComponent<OneShotWeaponInput>();
            var motor = go.AddComponent<ActorMotor2D>(); motor.Configure(input);
            go.GetComponent<Rigidbody2D>().gravityScale = 0;
            var pool = go.AddComponent<ProjectilePool>();
            var controller = go.AddComponent<WeaponController>();
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>(); controller.Configure(definition, pool);
            var facade = go.AddComponent<PlayerCombatFacade>();
            int changes = 0; CombatSnapshot last = default;
            facade.Changed += value => { changes++; last = value; };
            try
            {
                Assert.That(facade.ReadSnapshot().Stamina, Is.EqualTo(100));
                go.SetActive(true);
                input.Next = new PlayerCommand(Vector2.zero, attackHeld: true, switchPressed: true);
                yield return null; yield return null;
                Assert.IsTrue(last.IsMelee); Assert.That(last.Stamina, Is.EqualTo(80)); Assert.Greater(changes, 1);
                Assert.That(last.Magazine, Is.EqualTo(8)); Assert.That(last.ReserveAmmo, Is.EqualTo(16));
                facade.SetControlsEnabled(false);
                input.Next = new PlayerCommand(Vector2.zero, attackHeld: true, switchPressed: true);
                yield return null; yield return null;
                Assert.IsTrue(facade.ReadSnapshot().IsMelee); Assert.That(facade.ReadSnapshot().Stamina, Is.EqualTo(80));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(definition); }
        }
    }
    public sealed class OneShotWeaponInput : MonoBehaviour, IPlayerInputSource
    {
        public PlayerCommand Next;
        public PlayerCommand ReadCommand() { var result = Next; Next = default; return result; }
    }
}
