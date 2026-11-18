using System;
using System.Collections.Generic;
using Game.Contracts;

namespace Game.Actors
{
    public enum ActorStatusKind { Root, Bind, Stun, Poison, Slow }

    // A key denotes one application/cast, not a repeating trigger callback.
    public sealed class ActorStatusEffect
    {
        public int SourceId { get; }
        public string EffectId { get; }
        public ActorStatusKind Kind { get; }
        public float Duration { get; }
        public bool UntilSourceDeath { get; }
        public float SlowMultiplier { get; }
        public float PoisonDamage { get; }
        public float TickInterval { get; }
        public ActorStatusEffect(int sourceId, string effectId, ActorStatusKind kind, float duration,
            bool untilSourceDeath = false, float slowMultiplier = .5f, float poisonDamage = 0, float tickInterval = 1)
        {
            if (string.IsNullOrWhiteSpace(effectId)) throw new ArgumentException("Expected a cast/application Id.", nameof(effectId));
            if (!Enum.IsDefined(typeof(ActorStatusKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Finite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
            if (untilSourceDeath && kind != ActorStatusKind.Poison && kind != ActorStatusKind.Slow)
                throw new ArgumentException("Only poison/slow may last until source death.");
            if (!Finite(slowMultiplier) || slowMultiplier <= 0 || slowMultiplier > 1) throw new ArgumentOutOfRangeException(nameof(slowMultiplier));
            if (!Finite(poisonDamage) || poisonDamage < 0) throw new ArgumentOutOfRangeException(nameof(poisonDamage));
            if (!Finite(tickInterval) || tickInterval < .02f) throw new ArgumentOutOfRangeException(nameof(tickInterval));
            SourceId = sourceId; EffectId = effectId; Kind = kind; Duration = duration;
            UntilSourceDeath = untilSourceDeath; SlowMultiplier = slowMultiplier; PoisonDamage = poisonDamage; TickInterval = tickInterval;
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // Pure state: no Unity clock, scene, random source or damage calculation.
    public sealed class ActorStatusState
    {
        private sealed class ActiveEffect
        {
            public ActorStatusEffect Definition;
            public double Remaining, TickElapsed;
        }
        public const int MaximumKeysPerEncounter = 256;
        public const int MaximumTicksPerAdvance = 8;
        private readonly List<ActiveEffect> active = new List<ActiveEffect>();
        private readonly HashSet<(int, string)> seen = new HashSet<(int, string)>();
        private float legacyStunRemaining;
        public int Count => active.Count;
        internal void RefreshLegacyStun(float seconds) => legacyStunRemaining = Math.Max(legacyStunRemaining, seconds);
        public int RecordedKeys => seen.Count;
        public bool TryApply(ActorStatusEffect effect)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));
            var key = (effect.SourceId, effect.EffectId);
            if (seen.Contains(key) || seen.Count >= MaximumKeysPerEncounter) return false;
            seen.Add(key);
            active.Add(new ActiveEffect { Definition = effect, Remaining = effect.Duration });
            return true;
        }
        public ActorActionPermissions Permissions
        {
            get
            {
                bool move = legacyStunRemaining <= 0, attack = move, dodge = move;
                foreach (var item in active)
                {
                    if (item.Remaining <= 0 && !item.Definition.UntilSourceDeath) continue;
                    switch (item.Definition.Kind)
                    {
                        case ActorStatusKind.Root: move = dodge = false; break;
                        case ActorStatusKind.Bind:
                        case ActorStatusKind.Stun: move = attack = dodge = false; break;
                    }
                }
                return new ActorActionPermissions(move, attack, dodge, true);
            }
        }
        public float MovementMultiplier
        {
            get
            {
                float result = 1;
                foreach (var item in active)
                    if (item.Definition.Kind == ActorStatusKind.Slow) result = Math.Min(result, item.Definition.SlowMultiplier);
                return result;
            }
        }
        public float Remaining(ActorStatusKind kind)
        {
            float result = kind == ActorStatusKind.Stun ? legacyStunRemaining : 0;
            foreach (var item in active) if (item.Definition.Kind == kind) result = Math.Max(result, (float)item.Remaining);
            return result;
        }
        public bool Contains(int sourceId, string effectId)
        {
            foreach (var item in active) if (item.Definition.SourceId == sourceId && item.Definition.EffectId == effectId) return true;
            return false;
        }
        public int ReleaseSource(int sourceId) => active.RemoveAll(item => item.Definition.SourceId == sourceId && item.Definition.UntilSourceDeath);
        public void Advance(float deltaTime, Action<int, float> poisonTick = null)
        {
            if (!ActorStatusEffect.Finite(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (deltaTime == 0) return;
            legacyStunRemaining = Math.Max(0, legacyStunRemaining - deltaTime);
            // Iterate backwards; a lethal tick may synchronously Reset via the existing facade.
            for (int index = active.Count - 1; index >= 0; index--)
            {
                if (index >= active.Count) continue;
                var item = active[index]; var effect = item.Definition;
                double elapsed = effect.UntilSourceDeath ? deltaTime : Math.Min(deltaTime, Math.Max(0, item.Remaining));
                if (!effect.UntilSourceDeath) item.Remaining = Math.Max(0, item.Remaining - deltaTime);
                if (effect.Kind == ActorStatusKind.Poison)
                {
                    item.TickElapsed += elapsed;
                    int ticks = 0;
                    while (item.TickElapsed + 1e-7 >= effect.TickInterval && ticks++ < MaximumTicksPerAdvance)
                    {
                        item.TickElapsed = Math.Max(0, item.TickElapsed - effect.TickInterval);
                        poisonTick?.Invoke(effect.SourceId, effect.PoisonDamage);
                        if (!active.Contains(item)) break;
                    }
                }
                // Retain capped poison debt for a later Advance, never an unbounded frame loop.
                if (active.Contains(item) && !effect.UntilSourceDeath && item.Remaining <= 0 &&
                    (effect.Kind != ActorStatusKind.Poison || item.TickElapsed + 1e-7 < effect.TickInterval)) active.Remove(item);
            }
        }
        public void Reset() { active.Clear(); seen.Clear(); legacyStunRemaining = 0; }
    }
}
