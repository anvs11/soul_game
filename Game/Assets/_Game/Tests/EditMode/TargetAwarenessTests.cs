using System;
using Game.AI;
using NUnit.Framework;
namespace Game.Tests
{
    public sealed class TargetAwarenessTests
    {
        [Test] public void AcquisitionIncludesExactlyEightButNotBeyondOrHidden()
        {
            var state = new TargetAwareness();
            Assert.IsFalse(state.Observe(64.001f, true, true, 0));
            Assert.IsFalse(state.Observe(64, false, true, 0));
            Assert.IsTrue(state.Observe(64, true, true, 0));
        }
        [Test] public void TrackingIncludesElevenButLosesImmediatelyBeyond()
        {
            var state = new TargetAwareness(); state.Observe(64, true, true, 0);
            Assert.IsTrue(state.Observe(121, true, true, 0));
            Assert.IsFalse(state.Observe(121.001f, true, true, 0));
            Assert.IsFalse(state.Observe(100, true, true, 0), "Lost targets must re-enter detection radius.");
        }
        [Test] public void TwoSecondsWithoutSightLosesTargetAndZeroDeltaFreezesMemory()
        {
            var state = new TargetAwareness(); state.Observe(64, true, true, 0);
            Assert.IsTrue(state.Observe(64, false, true, 1));
            for (int i = 0; i < 100; i++) Assert.IsTrue(state.Observe(64, false, true, 0));
            Assert.That(state.SecondsWithoutSight, Is.EqualTo(1));
            Assert.IsTrue(state.Observe(64, false, true, .99f));
            Assert.IsFalse(state.Observe(64, false, true, .01f));
        }
        [Test] public void VisibilityResetsMemoryAndDeathOrResetClearsTracking()
        {
            var state = new TargetAwareness(); state.Observe(64, true, true, 0);
            state.Observe(64, false, true, 1.5f); state.Observe(64, true, true, .1f);
            Assert.That(state.SecondsWithoutSight, Is.Zero);
            Assert.IsTrue(state.Observe(64, false, true, 1.5f));
            Assert.IsFalse(state.Observe(64, true, false, 0));
            state.Observe(64, true, true, 0); state.Reset(); Assert.IsFalse(state.HasTarget);
        }
        [Test] public void InvalidParametersAndTimeAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TargetAwareness(8, 7, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TargetAwareness(8, 11, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TargetAwareness(float.NaN));
            var state = new TargetAwareness();
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Observe(1, true, true, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Observe(float.NaN, true, true, 0));
        }
    }
}
