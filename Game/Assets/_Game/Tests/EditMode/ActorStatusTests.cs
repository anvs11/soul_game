using System;
using Game.Actors;
using Game.Input;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ActorStatusTests
    {
        [TestCase(ActorStatusKind.Root, false, true, false)]
        [TestCase(ActorStatusKind.Bind, false, false, false)]
        [TestCase(ActorStatusKind.Stun, false, false, false)]
        [TestCase(ActorStatusKind.Poison, true, true, true)]
        [TestCase(ActorStatusKind.Slow, true, true, true)]
        public void PermissionsPreserveResponseWhereContractAllows(ActorStatusKind kind, bool move, bool attack, bool dodge)
        {
            var state = new ActorStatusState(); Assert.IsTrue(state.TryApply(new ActorStatusEffect(1, "cast", kind, 3)));
            Assert.That(state.Permissions.CanMove, Is.EqualTo(move)); Assert.That(state.Permissions.CanAttack, Is.EqualTo(attack));
            Assert.That(state.Permissions.CanDodge, Is.EqualTo(dodge)); Assert.IsTrue(state.Permissions.CanSwitchWeapon);
            state.Advance(3); Assert.IsTrue(state.Permissions.CanMove); Assert.IsTrue(state.Permissions.CanAttack);
        }
        [Test] public void SourcesAndEffectsAreIndependentAndExpiredFogCannotRefresh()
        {
            var state = new ActorStatusState();
            var fog = new ActorStatusEffect(10, "fog-stun", ActorStatusKind.Stun, 2);
            Assert.IsTrue(state.TryApply(fog)); state.Advance(1);
            Assert.IsFalse(state.TryApply(fog)); Assert.That(state.Remaining(ActorStatusKind.Stun), Is.EqualTo(1));
            Assert.IsTrue(state.TryApply(new ActorStatusEffect(11, "fog-stun", ActorStatusKind.Stun, 2)));
            Assert.IsTrue(state.TryApply(new ActorStatusEffect(10, "root", ActorStatusKind.Root, 3)));
            state.Advance(1); Assert.IsFalse(state.Contains(10, "fog-stun")); Assert.IsFalse(state.TryApply(fog));
            Assert.IsFalse(state.Permissions.CanAttack, "Second source still stuns.");
            state.Advance(1); Assert.IsTrue(state.Permissions.CanAttack); Assert.IsFalse(state.Permissions.CanMove, "Independent root remains.");
            state.Advance(1); Assert.IsTrue(state.Permissions.CanMove);
        }
        [Test] public void SourceDeathOnlyRemovesItsPersistentPoisonAndSlow()
        {
            var state = new ActorStatusState();
            foreach (int source in new[] { 10, 11 })
            {
                state.TryApply(new ActorStatusEffect(source, "poison", ActorStatusKind.Poison, 1, true, poisonDamage: 2));
                state.TryApply(new ActorStatusEffect(source, "slow", ActorStatusKind.Slow, 1, true, slowMultiplier: source == 10 ? .25f : .5f));
                state.TryApply(new ActorStatusEffect(source, "stun", ActorStatusKind.Stun, 2));
            }
            Assert.That(state.MovementMultiplier, Is.EqualTo(.25));
            Assert.That(state.ReleaseSource(10), Is.EqualTo(2)); Assert.That(state.ReleaseSource(10), Is.Zero);
            Assert.That(state.MovementMultiplier, Is.EqualTo(.5)); Assert.IsTrue(state.Contains(10, "stun"));
            int ticks = 0; state.Advance(5, (source, amount) => { Assert.That(source, Is.EqualTo(11)); Assert.That(amount, Is.EqualTo(2)); ticks++; });
            Assert.That(ticks, Is.EqualTo(5)); Assert.IsTrue(state.Permissions.CanAttack); Assert.That(state.Count, Is.EqualTo(2));
            state.ReleaseSource(11); Assert.That(state.MovementMultiplier, Is.EqualTo(1)); Assert.That(state.Count, Is.Zero);
        }
        [Test] public void PauseExpiryTickDebtAndLethalResetAreFinite()
        {
            var state = new ActorStatusState();
            state.TryApply(new ActorStatusEffect(1, "poison", ActorStatusKind.Poison, 20, poisonDamage: 1));
            int ticks = 0; state.Advance(0, (_, __) => ticks++); Assert.That(ticks, Is.Zero);
            state.Advance(20, (_, __) => ticks++); Assert.That(ticks, Is.EqualTo(8));
            state.Advance(.01f, (_, __) => ticks++); Assert.That(ticks, Is.EqualTo(16));
            state.Advance(.01f, (_, __) => ticks++); Assert.That(ticks, Is.EqualTo(20)); Assert.That(state.Count, Is.Zero);
            state.Reset(); state.TryApply(new ActorStatusEffect(1, "lethal", ActorStatusKind.Poison, 20, poisonDamage: 99));
            state.Advance(20, (_, __) => state.Reset()); Assert.That(state.Count, Is.Zero); Assert.That(state.RecordedKeys, Is.Zero);
        }
        [Test] public void BadDataAndFullKeyBudgetCannotMutateAcceptedState()
        {
            var state = new ActorStatusState();
            Assert.Throws<ArgumentException>(() => new ActorStatusEffect(1, " ", ActorStatusKind.Root, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActorStatusEffect(1, "bad", ActorStatusKind.Root, float.NaN));
            Assert.Throws<ArgumentException>(() => new ActorStatusEffect(1, "bad", ActorStatusKind.Stun, 2, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActorStatusEffect(1, "bad", ActorStatusKind.Slow, 2, slowMultiplier: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Advance(float.PositiveInfinity));
            for (int i = 0; i < ActorStatusState.MaximumKeysPerEncounter; i++) Assert.IsTrue(state.TryApply(new ActorStatusEffect(i, "root", ActorStatusKind.Root, 3)));
            state.Advance(3); Assert.IsFalse(state.TryApply(new ActorStatusEffect(999, "new", ActorStatusKind.Root, 3))); Assert.That(state.Count, Is.Zero);
            state.Reset(); Assert.IsTrue(state.TryApply(new ActorStatusEffect(999, "new", ActorStatusKind.Root, 3)));
        }
        [Test] public void OldCommandCallsRemainCompatibleAndDodgeIsLastOptionalArgument()
        {
            var old = new PlayerCommand(Vector2.right, true, true, true, true);
            Assert.IsFalse(old.DodgePressed); Assert.IsTrue(old.PausePressed); Assert.IsTrue(old.AttackHeld);
            var next = new PlayerCommand(Vector2.left, true, true, true, true, true);
            Assert.IsTrue(next.DodgePressed); Assert.That(next.Move, Is.EqualTo(Vector2.left));
        }
    }
}
