using System;
using Game.Actors;
using Game.Combat;
using NUnit.Framework;
namespace Game.Tests
{
    public sealed class HealthStateTests
    {
        [Test] public void ExactArmorHitDoesNotDamageHealth()
        {
            var state = new HealthState(100, 30);
            var result = state.ApplyDamage(new DamagePacket(1, 30));
            Assert.That(state.CurrentArmor, Is.Zero); Assert.That(state.CurrentHealth, Is.EqualTo(100));
            Assert.That(result.HealthLost, Is.Zero); Assert.IsFalse(result.Killed);
        }
        [Test] public void OverflowPassesFromArmorToHealth()
        {
            var state = new HealthState(100, 5);
            var result = state.ApplyDamage(new DamagePacket(1, 12));
            Assert.That(state.CurrentArmor, Is.Zero);
            Assert.That(state.CurrentHealth, Is.EqualTo(93));
            Assert.That(result.ArmorLost, Is.EqualTo(5));
            Assert.That(result.HealthLost, Is.EqualTo(7));
            Assert.IsTrue(result.Applied); Assert.IsFalse(result.Killed);
        }
        [Test] public void LethalDamageClampsAndCannotKillTwice()
        {
            var state = new HealthState();
            Assert.IsTrue(state.ApplyDamage(new DamagePacket(1, 1000)).Killed);
            Assert.That(state.CurrentHealth, Is.Zero);
            Assert.That(state.CurrentArmor, Is.Zero);
            var repeated = state.ApplyDamage(new DamagePacket(1, 1000));
            Assert.IsFalse(repeated.Applied); Assert.IsFalse(repeated.Killed);
        }
        [Test] public void ZeroDamageDoesNotChangeState()
        {
            var state = new HealthState();
            Assert.IsFalse(state.ApplyDamage(new DamagePacket(1, 0)).Applied);
            Assert.That(state.CurrentHealth, Is.EqualTo(100));
            Assert.That(state.CurrentArmor, Is.EqualTo(30));
        }
        [Test] public void InvalidNumbersAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamagePacket(1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamagePacket(1, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamagePacket(1, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthState(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthState(100, -1));
        }
    }
}
