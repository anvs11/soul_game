using System;
namespace Game.AI
{
    // State of one observer, advanced only with caller-supplied scaled time.
    public sealed class TargetAwareness
    {
        private readonly double detectSquared, loseSquared, memorySeconds;
        private double unseenSeconds;
        public bool HasTarget { get; private set; }
        public double SecondsWithoutSight => unseenSeconds;
        public TargetAwareness(float detectionRadius = 8f, float lossRadius = 11f, float memory = 2f)
        {
            if (!Positive(detectionRadius) || !Positive(lossRadius) || lossRadius < detectionRadius || !Positive(memory))
                throw new ArgumentOutOfRangeException(nameof(detectionRadius), "Expected finite 0 < detection <= loss and memory > 0.");
            detectSquared = (double)detectionRadius * detectionRadius;
            loseSquared = (double)lossRadius * lossRadius; memorySeconds = memory;
        }
        private static bool Positive(float n) => n > 0 && !float.IsInfinity(n) && !float.IsNaN(n);
        public bool Observe(float distanceSquared, bool visible, bool alive, float scaledDelta)
        {
            if (scaledDelta < 0 || float.IsInfinity(scaledDelta) || float.IsNaN(scaledDelta)) throw new ArgumentOutOfRangeException(nameof(scaledDelta));
            if (!alive) { Reset(); return false; }
            if (distanceSquared < 0 || float.IsInfinity(distanceSquared) || float.IsNaN(distanceSquared)) throw new ArgumentOutOfRangeException(nameof(distanceSquared));
            if (!HasTarget)
            {
                HasTarget = visible && distanceSquared <= detectSquared;
                unseenSeconds = 0;
            }
            else if (distanceSquared > loseSquared) Reset();
            else if (visible) unseenSeconds = 0;
            else
            {
                unseenSeconds += scaledDelta;
                if (unseenSeconds + 1e-7 >= memorySeconds) Reset();
            }
            return HasTarget;
        }
        public void Reset() { HasTarget = false; unseenSeconds = 0; }
    }
}
