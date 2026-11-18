using System.Collections;
using Game.Actors;
using Game.Combat;
using Game.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class MovementStatusTests
    {
        private GameObject root, hero;
        private ActorMotor2D motor;
        private ActorStatusController status;
        private PlayerDodgeController dodge;
        private Rigidbody2D body;
        private OneShotWeaponInput input;
        private PlayerCombatFacade facade;
        private WeaponController weapon;
        private WeaponDefinition definition;
        private DamageReceiver receiver;
        private readonly Vector2 origin = new Vector2(12000, 12000);
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1;
            root = new GameObject("S405A0 fixture"); hero = new GameObject("Synthetic hero"); hero.SetActive(false); hero.transform.SetParent(root.transform);
            hero.transform.position = origin;
            input = hero.AddComponent<OneShotWeaponInput>(); motor = hero.AddComponent<ActorMotor2D>(); motor.Configure(input);
            body = hero.GetComponent<Rigidbody2D>(); body.gravityScale = 0; body.freezeRotation = true; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            hero.AddComponent<CircleCollider2D>().radius = .4f;
            var template = new GameObject("Synthetic projectile template"); template.SetActive(false); template.transform.SetParent(root.transform);
            var pool = hero.AddComponent<ProjectilePool>(); pool.Configure(template.AddComponent<Projectile>(), 16);
            weapon = hero.AddComponent<WeaponController>();
            definition = ScriptableObject.CreateInstance<WeaponDefinition>(); weapon.Configure(definition, pool);
            receiver = hero.GetComponent<DamageReceiver>(); receiver.Configure(100, 30, 0);
            facade = hero.AddComponent<PlayerCombatFacade>(); dodge = hero.AddComponent<PlayerDodgeController>();
            hero.SetActive(true); yield return null;
            status = hero.GetComponent<ActorStatusController>(); Physics2D.SyncTransforms();
        }
        private IEnumerator Command(PlayerCommand command)
        { input.Next = command; yield return null; yield return null; }
        private GameObject Wall(Vector2 offset, Vector2 size)
        {
            var wall = new GameObject("Solid wall"); wall.transform.SetParent(root.transform); wall.transform.position = origin + offset;
            wall.layer = LayerMask.NameToLayer("World"); wall.AddComponent<BoxCollider2D>().size = size; Physics2D.SyncTransforms(); return wall;
        }
        [UnityTest] public IEnumerator ZeroDirectionUsesDownThenLastMovementAndOldAim()
        {
            Assert.IsTrue(dodge.TryRequestDodge(Vector2.zero)); Assert.That(dodge.Direction, Is.EqualTo(Vector2.down));
            yield return new WaitForSeconds(.25f); Assert.That(body.position.y, Is.EqualTo(origin.y - 2.4f).Within(.08));
            motor.ResetForEncounter();
            yield return Command(new PlayerCommand(Vector2.left)); yield return Command(default);
            Assert.IsTrue(dodge.TryRequestDodge(Vector2.zero)); Assert.That(dodge.Direction, Is.EqualTo(Vector2.left));
            motor.ResetForEncounter(); // Direction memory resets, weapon aim remains left.
            Assert.IsTrue(dodge.TryRequestDodge(Vector2.zero)); Assert.That(dodge.Direction, Is.EqualTo(weapon.AimDirection));
        }
        [UnityTest] public IEnumerator EdgeCommandMovesExactlyOneFiniteDodgeAndAllowsAttackWithoutImmunity()
        {
            int magazine = weapon.State.Magazine; bool sampledDodge = false;
            motor.CommandSampled += command => sampledDodge |= command.DodgePressed;
            yield return Command(new PlayerCommand(Vector2.right, attackHeld: true, dodgePressed: true));
            Assert.IsTrue(sampledDodge, "The accepted edge remains visible in the sampled command.");
            Assert.That(weapon.State.Magazine, Is.EqualTo(magazine - 1)); Assert.That(dodge.CooldownRemaining, Is.GreaterThan(0));
            Assert.IsTrue(receiver.ApplyDamage(new DamagePacket(44, 10)).Applied); Assert.That(receiver.Armor, Is.EqualTo(20));
            yield return new WaitForSeconds(1);
            Assert.That(body.position.x - origin.x, Is.EqualTo(2.4f).Within(.08)); Assert.IsFalse(dodge.IsDodging); Assert.That(dodge.CooldownRemaining, Is.Zero);
            yield return new WaitForSeconds(.1f); Assert.That(body.position.x - origin.x, Is.EqualTo(2.4f).Within(.08));
        }
        [UnityTest] public IEnumerator WallsStopDiagonalMotionAndBlockedRequestSpendsNothing()
        {
            Wall(new Vector2(1, 0), new Vector2(.2f, 8));
            float stamina = weapon.State.Stamina; Assert.IsTrue(dodge.TryRequestDodge(Vector2.one));
            yield return new WaitForSeconds(.25f); Assert.That(body.position.x, Is.LessThanOrEqualTo(origin.x + .51f)); Assert.IsFalse(dodge.IsDodging);
            motor.ResetForEncounter(); body.position = origin + new Vector2(.49f, 0); yield return new WaitForFixedUpdate(); Physics2D.SyncTransforms();
            Assert.IsFalse(dodge.TryRequestDodge(Vector2.right)); Assert.That(dodge.CooldownRemaining, Is.Zero); Assert.That(weapon.State.Stamina, Is.EqualTo(stamina));
        }
        [UnityTest] public IEnumerator RootBindStunRejectWithoutSpendingAndPreservePauseAndSwitch()
        {
            int pauses = 0; facade.PauseRequested += () => pauses++;
            foreach (var kind in new[] { ActorStatusKind.Root, ActorStatusKind.Bind, ActorStatusKind.Stun })
            {
                motor.ResetForEncounter(); weapon.State.SetSelected(true);
                Assert.IsTrue(status.TryApply(new ActorStatusEffect(12, kind.ToString(), kind, 3)));
                float stamina = weapon.State.Stamina; int ammo = weapon.State.Magazine;
                Assert.IsFalse(dodge.TryRequestDodge(Vector2.up)); Assert.That(dodge.CooldownRemaining, Is.Zero);
                yield return Command(new PlayerCommand(Vector2.right, attackHeld: true, pausePressed: true, dodgePressed: true));
                Assert.That(weapon.State.Magazine, Is.EqualTo(kind == ActorStatusKind.Root ? ammo - 1 : ammo));
                Assert.That(weapon.State.Stamina, Is.EqualTo(stamina)); Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
                yield return Command(new PlayerCommand(Vector2.zero, switchPressed: true)); Assert.IsTrue(weapon.State.IsMelee);
            }
            Assert.That(pauses, Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator NewRestrictionCancelsDodgeAndKnockbackRetainsWallLimitedPriority()
        {
            Wall(new Vector2(1, 0), new Vector2(.2f, 8));
            Assert.IsTrue(dodge.TryRequestDodge(Vector2.up));
            Assert.IsTrue(status.TryApply(new ActorStatusEffect(1, "root", ActorStatusKind.Root, 3))); Assert.IsFalse(dodge.IsDodging);
            float cooldown = dodge.CooldownRemaining; Assert.IsFalse(dodge.TryRequestDodge(Vector2.down)); Assert.That(dodge.CooldownRemaining, Is.EqualTo(cooldown));
            motor.BeginKnockback(Vector2.right, 4, .15f); Assert.IsFalse(dodge.TryRequestDodge(Vector2.up));
            yield return Command(new PlayerCommand(Vector2.zero, attackHeld: true)); Assert.That(weapon.State.Magazine, Is.EqualTo(7));
            yield return new WaitForSeconds(.25f); Assert.That(body.position.x, Is.GreaterThan(origin.x)); Assert.That(body.position.x, Is.LessThanOrEqualTo(origin.x + .51f));
            Assert.IsTrue(motor.Permissions.CanAttack); Assert.IsFalse(motor.Permissions.CanMove);
        }
        [UnityTest] public IEnumerator PoisonAndSlowAllowResponseAndSourceRemovalIsIndependent()
        {
            motor.enabled = false; var held = hero.AddComponent<MovementTestInput>(); motor.Configure(held); motor.enabled = true;
            Assert.IsTrue(status.TryApply(new ActorStatusEffect(21, "poison", ActorStatusKind.Poison, 1, true, poisonDamage: 2, tickInterval: .05f)));
            Assert.IsTrue(status.TryApply(new ActorStatusEffect(21, "slow", ActorStatusKind.Slow, 1, true, slowMultiplier: .25f)));
            Assert.IsTrue(status.TryApply(new ActorStatusEffect(22, "slow", ActorStatusKind.Slow, 1, true, slowMultiplier: .5f)));
            int changes = 0; receiver.DamageApplied += _ => changes++;
            held.Command = new PlayerCommand(Vector2.right, attackHeld: true); yield return null;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.x, Is.EqualTo(5.5f * .25f).Within(.01));
            held.Command = default;
            yield return new WaitForSeconds(.12f); Assert.That(changes, Is.GreaterThanOrEqualTo(2)); Assert.That(receiver.Armor, Is.LessThan(30));
            Assert.That(weapon.State.Magazine, Is.EqualTo(7)); Assert.IsTrue(motor.Permissions.CanAttack); Assert.IsTrue(motor.Permissions.CanDodge);
            Assert.That(status.ReleaseSource(21), Is.EqualTo(2)); Assert.That(status.MovementMultiplier, Is.EqualTo(.5));
            int oldChanges = changes; yield return new WaitForSeconds(.12f); Assert.That(changes, Is.EqualTo(oldChanges)); Assert.IsTrue(status.Contains(22, "slow"));
        }
        [UnityTest] public IEnumerator PauseFreezesStatusAndDodgeButRequestsRemainAvailable()
        {
            status.TryApply(new ActorStatusEffect(1, "slow", ActorStatusKind.Slow, 3));
            Assert.IsTrue(dodge.TryRequestDodge(Vector2.right)); yield return new WaitForFixedUpdate();
            Time.timeScale = 0; yield return null; var position = body.position; float cooldown = dodge.CooldownRemaining;
            int pauses = 0; facade.PauseRequested += () => pauses++;
            yield return Command(new PlayerCommand(Vector2.left, attackHeld: true, pausePressed: true, dodgePressed: true));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(pauses, Is.EqualTo(1)); Assert.That(dodge.CooldownRemaining, Is.EqualTo(cooldown)); Assert.That(body.position, Is.EqualTo(position));
            Assert.IsFalse(motor.Permissions.CanAttack); Assert.That(weapon.State.Magazine, Is.EqualTo(8)); Assert.That(status.ActiveCount, Is.EqualTo(1));
            Time.timeScale = 1; yield return new WaitForSeconds(.3f); Assert.IsFalse(dodge.IsDodging); Assert.That(body.position.x, Is.GreaterThan(position.x));
        }
        [UnityTest] public IEnumerator DeathClearsAllTransientStateAndCannotBeReenabled()
        {
            status.TryApply(new ActorStatusEffect(1, "poison", ActorStatusKind.Poison, 3, true, poisonDamage: 1));
            dodge.TryRequestDodge(Vector2.right); receiver.ApplyDamage(new DamagePacket(2, 999));
            Assert.IsFalse(dodge.IsDodging); Assert.That(dodge.CooldownRemaining, Is.Zero); Assert.That(status.ActiveCount, Is.Zero);
            facade.SetControlsEnabled(true); Assert.IsFalse(dodge.TryRequestDodge(Vector2.right)); Assert.IsFalse(motor.Permissions.CanAttack);
            int pauses = 0; facade.PauseRequested += () => pauses++;
            yield return Command(new PlayerCommand(Vector2.up, attackHeld: true, pausePressed: true, dodgePressed: true));
            Assert.That(pauses, Is.EqualTo(1)); Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.IsFalse(status.TryApply(new ActorStatusEffect(1, "later", ActorStatusKind.Stun, 2)));
        }
        [UnityTest] public IEnumerator DisableAndTransitionResetWithoutCleansingOnRepeatedEnable()
        {
            dodge.TryRequestDodge(Vector2.right); float cooldown = dodge.CooldownRemaining;
            facade.SetControlsEnabled(true); Assert.That(dodge.CooldownRemaining, Is.EqualTo(cooldown));
            status.TryApply(new ActorStatusEffect(1, "root", ActorStatusKind.Root, 3));
            facade.SetControlsEnabled(false); Assert.That(status.ActiveCount, Is.Zero); Assert.That(dodge.CooldownRemaining, Is.Zero);
            Assert.IsFalse(dodge.TryRequestDodge(Vector2.right)); facade.SetControlsEnabled(true);
            status.TryApply(new ActorStatusEffect(1, "root", ActorStatusKind.Root, 3)); hero.SetActive(false); hero.SetActive(true); yield return null;
            Assert.That(status.ActiveCount, Is.Zero); Assert.That(dodge.CooldownRemaining, Is.Zero);
            facade.SetControlsEnabled(true); Assert.IsTrue(dodge.TryRequestDodge(Vector2.up));
            motor.ResetForEncounter(); status.TryApply(new ActorStatusEffect(2, "slow", ActorStatusKind.Slow, 3));
            facade.RestoreForEncounter(facade.ReadSnapshot()); Assert.That(status.ActiveCount, Is.Zero); Assert.That(dodge.CooldownRemaining, Is.Zero);
        }
        [UnityTest] public IEnumerator UnloadDoesNotSendDeathOrKeepSubscriptionsAndEffects()
        {
            var scene = SceneManager.CreateScene("S405 unload"); hero.transform.SetParent(null); SceneManager.MoveGameObjectToScene(hero, scene);
            int deaths = 0; facade.Died += () => deaths++;
            status.TryApply(new ActorStatusEffect(1, "poison", ActorStatusKind.Poison, 3, true, poisonDamage: 2)); dodge.TryRequestDodge(Vector2.right);
            yield return SceneManager.UnloadSceneAsync(scene); yield return null;
            Assert.That(deaths, Is.Zero); Assert.IsTrue(hero == null); Assert.IsTrue(status == null); Assert.IsTrue(dodge == null);
        }
        [UnityTest] public IEnumerator BindAndStunPreventKnifeCostWhileRootAllowsMelee()
        {
            foreach (var kind in new[] { ActorStatusKind.Root, ActorStatusKind.Bind, ActorStatusKind.Stun })
            {
                motor.ResetForEncounter(); weapon.State.RestoreForEncounter(8, 16, 100, true);
                status.TryApply(new ActorStatusEffect(1, "knife/" + kind, kind, 3));
                yield return Command(new PlayerCommand(Vector2.zero, attackHeld: true));
                Assert.That(weapon.State.Stamina, Is.EqualTo(kind == ActorStatusKind.Root ? 80 : 100));
                Assert.That(weapon.State.Magazine, Is.EqualTo(8)); Assert.That(weapon.State.ReserveAmmo, Is.EqualTo(16));
                Assert.IsFalse(dodge.TryRequestDodge(Vector2.up)); Assert.That(dodge.CooldownRemaining, Is.Zero);
            }
        }
        [UnityTest] public IEnumerator DodgeAllowsSwitchAndDoesNotRequireStamina()
        {
            weapon.State.RestoreForEncounter(8, 16, 0, true);
            Assert.IsTrue(dodge.TryRequestDodge(Vector2.right)); Assert.That(weapon.State.Stamina, Is.Zero);
            yield return Command(new PlayerCommand(Vector2.zero, switchPressed: true, attackHeld: true));
            Assert.IsFalse(weapon.State.IsMelee); Assert.That(weapon.State.Magazine, Is.EqualTo(7));
            Assert.That(dodge.CooldownRemaining, Is.GreaterThan(0));
        }
        [UnityTest] public IEnumerator PoisonDeathClearsItsOwnTickLoopAndEmitsOneDeath()
        {
            int deaths = 0; facade.Died += () => deaths++;
            status.TryApply(new ActorStatusEffect(10, "lethal", ActorStatusKind.Poison, 3, true, poisonDamage: 999, tickInterval: .02f));
            yield return new WaitForSeconds(.1f);
            Assert.That(deaths, Is.EqualTo(1)); Assert.That(status.ActiveCount, Is.Zero); Assert.IsTrue(receiver.IsDead);
            yield return new WaitForSeconds(.1f); Assert.That(deaths, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator InvalidDirectionAndRepeatedRequestsDoNotRestartCooldown()
        {
            Assert.IsFalse(dodge.TryRequestDodge(new Vector2(float.NaN, 0))); Assert.IsFalse(dodge.TryRequestDodge(new Vector2(float.MaxValue, 0)));
            Assert.That(dodge.CooldownRemaining, Is.Zero); Assert.IsTrue(dodge.TryRequestDodge(Vector2.right));
            float before = dodge.CooldownRemaining; Assert.IsFalse(dodge.TryRequestDodge(Vector2.left)); Assert.That(dodge.CooldownRemaining, Is.EqualTo(before));
            yield return new WaitForSeconds(.3f); before = dodge.CooldownRemaining; Assert.IsFalse(dodge.TryRequestDodge(Vector2.left)); Assert.That(dodge.CooldownRemaining, Is.EqualTo(before));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { Time.timeScale = 1; Object.Destroy(root); Object.Destroy(definition); yield return null; }
    }
}
