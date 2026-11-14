using Game.Combat;
using NUnit.Framework;
using UnityEngine;
namespace Game.Tests
{
    public sealed class MeleeResourceTests
    {
        private sealed class Spawner : IProjectileSpawner { public bool TrySpawn(in ProjectileSpawnRequest request) => true; }
        private WeaponDefinition definition;
        private WeaponRuntimeState state;
        [SetUp] public void Setup() { definition = ScriptableObject.CreateInstance<WeaponDefinition>(); state = new WeaponRuntimeState(definition); }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(definition);
        private void ExhaustKnife()
        {
            state.SetSelected(false);
            for (int i = 0; i < 5; i++)
            { Assert.IsTrue(state.TryMeleeAttack()); state.Advance(.45f); }
        }
        [Test] public void FiveSwingsExhaustAndRestingOnKnifeRestoresStamina()
        {
            ExhaustKnife(); Assert.That(state.Stamina, Is.Zero);
            Assert.IsFalse(state.TryMeleeAttack()); state.Advance(100);
            Assert.That(state.Stamina, Is.EqualTo(100)); Assert.IsTrue(state.TryMeleeAttack());
        }
        [Test] public void KnifeRestIsSlowerAndHeldAttackPreventsRecovery()
        {
            ExhaustKnife();
            state.Advance(100, attackHeld: true);
            Assert.That(state.Stamina, Is.Zero);
            state.Advance(.8f);
            Assert.That(state.Stamina, Is.Zero.Within(.001f));
            state.Advance(1);
            Assert.That(state.Stamina, Is.EqualTo(10).Within(.001f));
            state.SetSelected(true); state.Advance(1);
            Assert.That(state.Stamina, Is.EqualTo(35).Within(.001f));
        }
        [Test] public void PistolRestoresTwentyPerPointEightSecondsAndClampsAtMaximum()
        {
            ExhaustKnife(); state.SetSelected(true); state.Advance(.8f);
            Assert.That(state.Stamina, Is.EqualTo(20).Within(.001f));
            state.SetSelected(false); Assert.IsTrue(state.TryMeleeAttack());
            Assert.That(state.Stamina, Is.Zero);
            state.SetSelected(true); state.Advance(100); Assert.That(state.Stamina, Is.EqualTo(100));
        }
        [Test] public void EmptyPistolStillRestoresKnifeResourceWithoutInventingAmmo()
        {
            var spawner = new Spawner();
            for (int mag = 0; mag < 3; mag++)
            {
                for (int shot = 0; shot < 8; shot++)
                { Assert.IsTrue(state.TryFire(spawner, default)); if (shot < 7) state.Advance(.25f); }
                if (mag < 2) { state.Advance(0); state.Advance(1); }
            }
            ExhaustKnife(); state.SetSelected(true); state.Advance(.8f);
            Assert.That(state.Magazine, Is.Zero); Assert.That(state.ReserveAmmo, Is.Zero);
            state.SetSelected(false); Assert.IsTrue(state.TryMeleeAttack());
            Assert.That(state.Magazine, Is.Zero); Assert.That(state.ReserveAmmo, Is.Zero);
        }
        [Test] public void SwitchingDoesNotBypassSwingIntervalOrCreateAmmo()
        {
            state.SetSelected(false); Assert.IsTrue(state.TryMeleeAttack());
            Assert.IsFalse(state.TryMeleeAttack());
            state.SetSelected(true); state.SetSelected(false); Assert.IsFalse(state.TryMeleeAttack());
            state.Advance(.45f); Assert.IsTrue(state.TryMeleeAttack());
            Assert.That(state.Magazine, Is.EqualTo(8)); Assert.That(state.ReserveAmmo, Is.EqualTo(16));
            state.SetSelected(true); Assert.IsFalse(state.TryMeleeAttack());
        }
    }
}
