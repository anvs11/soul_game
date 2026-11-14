using Game.Combat;
using NUnit.Framework;
using UnityEngine;
namespace Game.Tests
{
    public sealed class WeaponRuntimeTests
    {
        private sealed class Spawner : IProjectileSpawner
        {
            public bool Accept = true;
            public bool TrySpawn(in ProjectileSpawnRequest request) => Accept;
        }
        private WeaponDefinition definition;
        private WeaponRuntimeState state;
        private readonly Spawner spawner = new Spawner();
        [SetUp] public void Setup() { definition = ScriptableObject.CreateInstance<WeaponDefinition>(); state = new WeaponRuntimeState(definition); spawner.Accept = true; }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(definition);
        private void EmptyMagazine()
        {
            for (int i = 0; i < 8; i++) { Assert.IsTrue(state.TryFire(spawner, default)); if (i < 7) state.Advance(.25f); }
        }
        [Test] public void ReloadRejectsFireAndCompletionCannotTransferTwice()
        {
            EmptyMagazine(); state.Advance(0);
            Assert.IsFalse(state.TryFire(spawner, default)); Assert.That(state.ReserveAmmo, Is.EqualTo(16));
            state.Advance(1); state.Advance(100);
            Assert.That(state.Magazine, Is.EqualTo(8)); Assert.That(state.ReserveAmmo, Is.EqualTo(8));
            Assert.IsFalse(state.IsReloading);
        }
        [Test] public void EightShotsThenFullReloadTransfersOnlyAtCompletion()
        {
            EmptyMagazine(); Assert.That(state.Magazine, Is.Zero); Assert.That(state.ReserveAmmo, Is.EqualTo(16));
            state.Advance(0); Assert.IsTrue(state.IsReloading);
            state.Advance(.5f); Assert.That(state.ReserveAmmo, Is.EqualTo(16)); Assert.That(state.Magazine, Is.Zero);
            state.Advance(.5f); Assert.That(state.Magazine, Is.EqualTo(8)); Assert.That(state.ReserveAmmo, Is.EqualTo(8));
        }
        [Test] public void RejectedSpawnDoesNotSpendAmmoOrStartCooldown()
        {
            spawner.Accept = false; Assert.IsFalse(state.TryFire(spawner, default));
            Assert.That(state.Magazine, Is.EqualTo(8));
            spawner.Accept = true; Assert.IsTrue(state.TryFire(spawner, default));
            Assert.IsFalse(state.TryFire(spawner, default)); Assert.That(state.Magazine, Is.EqualTo(7));
        }
        [Test] public void HolsteringCancelsReloadWithoutResourceTransfer()
        {
            EmptyMagazine(); state.Advance(0); state.Advance(.75f);
            state.SetSelected(false); Assert.IsFalse(state.IsReloading); state.Advance(2);
            Assert.That(state.ReserveAmmo, Is.EqualTo(16));
            state.SetSelected(true); state.Advance(0); state.Advance(.5f);
            Assert.That(state.Magazine, Is.Zero); state.Advance(.5f);
            Assert.That(state.Magazine, Is.EqualTo(8)); Assert.That(state.ReserveAmmo, Is.EqualTo(8));
        }
        [Test] public void AllTwentyFourRoundsAreFiniteAndDefinitionIsSharedSafely()
        {
            for (int magazine = 0; magazine < 3; magazine++)
            { EmptyMagazine(); if (magazine < 2) { state.Advance(0); state.Advance(1); } }
            state.Advance(10); Assert.That(state.Magazine, Is.Zero); Assert.That(state.ReserveAmmo, Is.Zero);
            Assert.IsFalse(state.IsReloading); Assert.IsFalse(state.TryFire(spawner, default));
            var second = new WeaponRuntimeState(definition);
            Assert.That(second.Magazine, Is.EqualTo(8)); Assert.That(second.ReserveAmmo, Is.EqualTo(16));
        }
    }
}
