using System.Collections;
using System.Collections.Generic;
using Game.Actors;
using Game.Combat;
using Game.Contracts;
using Game.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class CityProgressionCombatTests
    {
        [UnityTest]
        public IEnumerator PlayerConsumesProgressionModifiersInMovementFireAndMelee()
        {
            var hero = new GameObject("S5 combat hero");
            hero.SetActive(false);
            var target = new GameObject("S5 combat target");
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 1;
                hero.transform.position = new Vector2(10000, 10000);
                var input = hero.AddComponent<MovementTestInput>();
                var motor = hero.AddComponent<ActorMotor2D>();
                motor.Configure(input);
                var body = hero.GetComponent<Rigidbody2D>();
                body.gravityScale = 0;
                var pool = hero.AddComponent<ProjectilePool>();
                var weapon = hero.AddComponent<WeaponController>();
                weapon.Configure(definition, pool);
                var facade = hero.AddComponent<PlayerCombatFacade>();
                facade.BindProgressionWeaponIds("pistol", "knife");
                hero.SetActive(true);
                yield return null;

                var state = weapon.State;
                Assert.That(facade.EquippedWeaponId, Is.EqualTo("pistol"));
                var modules = new Dictionary<string, ProgressionModuleKind>
                {
                    ["pistol"] = ProgressionModuleKind.FasterFire,
                    ["knife"] = ProgressionModuleKind.SharpenedBlade
                };
                var beneficial = new[] { new ProgressionEffectSnapshot("haste", ProgressionEffectKind.Beneficial) };
                var harmful = new[] { new ProgressionEffectSnapshot("slow", ProgressionEffectKind.Harmful) };
                input.Command = new PlayerCommand(Vector2.right);
                yield return null;
                yield return new WaitForFixedUpdate();
                float baseSpeed = body.linearVelocity.x;
                Assert.That(baseSpeed, Is.GreaterThan(0));
                facade.SetProgressionModifiers(modules, beneficial);
                yield return new WaitForFixedUpdate();
                yield return null;
                Assert.That(body.linearVelocity.x, Is.EqualTo(baseSpeed * 1.2f).Within(.01f));
                facade.SetProgressionModifiers(modules, harmful);
                yield return new WaitForFixedUpdate();
                yield return null;
                Assert.That(body.linearVelocity.x, Is.EqualTo(baseSpeed * .8f).Within(.01f));
                input.Command = default;
                yield return null;
                yield return new WaitForFixedUpdate();

                var spawner = new AcceptProjectile();
                ProjectileSpawnRequest shot = default;
                Assert.That(state.TryFire(spawner, in shot), Is.True);
                state.Advance(definition.ShotInterval * .9f);
                Assert.That(state.TryFire(spawner, in shot), Is.True);

                state.SetSelected(false);
                Assert.That(facade.EquippedWeaponId, Is.EqualTo("knife"));
                target.transform.position = hero.transform.position + Vector3.right;
                target.AddComponent<CircleCollider2D>().radius = .1f;
                var receiver = target.AddComponent<DamageReceiver>();
                float startingArmor = receiver.Armor;
                Physics2D.SyncTransforms();
                var melee = hero.GetComponent<MeleeAttack>();
                Assert.That(melee.TryAttack(state, hero.transform.position, Vector2.right, 1, CombatFaction.Player,
                    ~0, 1), Is.True);
                Assert.That(receiver.Armor, Is.EqualTo(startingArmor - MeleeAttack.Damage * 1.25f).Within(.01f));

                modules.Clear();
                facade.SetProgressionModifiers(modules, System.Array.Empty<ProgressionEffectSnapshot>());
                input.Command = new PlayerCommand(Vector2.right);
                yield return null;
                yield return new WaitForFixedUpdate();
                Assert.That(body.linearVelocity.x, Is.EqualTo(baseSpeed).Within(.01f));
                state.SetSelected(true);
                state.Advance(1);
                Assert.That(state.TryFire(spawner, in shot), Is.True);
                state.Advance(definition.ShotInterval * .9f);
                Assert.That(state.TryFire(spawner, in shot), Is.False);
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(hero);
                Object.DestroyImmediate(definition);
            }
        }

        private sealed class AcceptProjectile : IProjectileSpawner
        {
            public bool TrySpawn(in ProjectileSpawnRequest request) => true;
        }
    }
}
