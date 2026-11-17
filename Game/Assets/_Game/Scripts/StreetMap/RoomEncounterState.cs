using System;
using System.Collections.Generic;
using Game.Encounters;

namespace Game.StreetMap
{
    public enum RoomPhase { Dormant, Waiting, Fighting, Cleared, Cancelled }

    // Finite obligations, independent of Unity callbacks, scene lifetime or object visibility.
    public sealed class RoomEncounterState
    {
        private enum SlotPhase { Pending, Alive, Resolved, Cancelled }
        private sealed class Slot
        {
            public string Id, Owner;
            public int Wave;
            public SlotPhase Phase;
        }
        private readonly Dictionary<string, Slot> slots = new Dictionary<string, Slot>(StringComparer.Ordinal);
        private readonly RoomPlan plan;
        public RoomPhase Phase { get; private set; }
        public int WaveIndex { get; private set; }
        public float DelayRemaining { get; private set; }
        public int PendingCount { get; private set; }
        public int AliveCount { get; private set; }
        public bool IsActive => Phase == RoomPhase.Waiting || Phase == RoomPhase.Fighting;
        public bool IsWaveDue => Phase == RoomPhase.Waiting && DelayRemaining <= 0;
        public RoomPlan Plan => plan;

        public RoomEncounterState(RoomPlan plan)
        {
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
            for (int index = 0; index < plan.Waves.Count; index++)
                foreach (var spawn in plan.Waves[index].Spawns)
                {
                    AddSlot(spawn.SpawnId, null, index);
                    for (int child = 0; child < spawn.ReservedSummons; child++) AddSlot(spawn.SummonId(child), spawn.SpawnId, index);
                }
        }
        private void AddSlot(string id, string owner, int wave)
        {
            if (slots.ContainsKey(id)) throw new ArgumentException("Duplicate required SpawnId: " + id);
            slots.Add(id, new Slot { Id = id, Owner = owner, Wave = wave }); PendingCount++;
        }
        public bool Begin()
        {
            if (Phase != RoomPhase.Dormant) return false;
            Phase = plan.Waves.Count == 0 ? RoomPhase.Cleared : RoomPhase.Waiting;
            if (plan.Waves.Count > 0) DelayRemaining = plan.Waves[0].DelaySeconds;
            return true;
        }
        public void Tick(float scaledDeltaTime)
        {
            if (float.IsNaN(scaledDeltaTime) || float.IsInfinity(scaledDeltaTime) || scaledDeltaTime < 0) throw new ArgumentOutOfRangeException(nameof(scaledDeltaTime));
            if (Phase == RoomPhase.Waiting) DelayRemaining = Math.Max(0, DelayRemaining - scaledDeltaTime);
        }
        public bool CanMaterialize(string id)
        {
            if (!IsActive || !slots.TryGetValue(id, out var slot) || slot.Phase != SlotPhase.Pending || slot.Wave != WaveIndex) return false;
            if (slot.Owner == null) return IsWaveDue;
            return slots[slot.Owner].Phase == SlotPhase.Alive;
        }
        public bool MarkMaterialized(string id)
        {
            if (!CanMaterialize(id)) return false;
            var slot = slots[id]; slot.Phase = SlotPhase.Alive; PendingCount--; AliveCount++;
            bool anyPrimaryPending = false;
            foreach (var other in slots.Values)
                if (other.Wave == WaveIndex && other.Owner == null && other.Phase == SlotPhase.Pending) anyPrimaryPending = true;
            if (!anyPrimaryPending) Phase = RoomPhase.Fighting;
            return true;
        }
        public bool ConfirmDeath(string id)
        {
            if (!IsActive || !slots.TryGetValue(id, out var slot) || slot.Phase != SlotPhase.Alive) return false;
            slot.Phase = SlotPhase.Resolved; AliveCount--;
            // A real owner death explicitly cancels only its unused reservations.
            if (slot.Owner == null) CancelUnmaterializedSummons(id);
            AdvanceIfComplete();
            return true;
        }
        public int CancelUnmaterializedSummons(string ownerId)
        {
            if (!IsActive || !slots.TryGetValue(ownerId, out var owner) || owner.Owner != null || owner.Phase == SlotPhase.Pending) return 0;
            int cancelled = 0;
            foreach (var slot in slots.Values)
                if (slot.Owner == ownerId && slot.Phase == SlotPhase.Pending)
                { slot.Phase = SlotPhase.Cancelled; PendingCount--; cancelled++; }
            AdvanceIfComplete();
            return cancelled;
        }
        private void AdvanceIfComplete()
        {
            if (!IsActive) return;
            foreach (var slot in slots.Values)
                if (slot.Wave == WaveIndex && (slot.Phase == SlotPhase.Pending || slot.Phase == SlotPhase.Alive)) return;
            WaveIndex++;
            if (WaveIndex >= plan.Waves.Count)
            {
                if (PendingCount != 0 || AliveCount != 0) throw new InvalidOperationException("Room obligations are inconsistent.");
                Phase = RoomPhase.Cleared;
            }
            else { Phase = RoomPhase.Waiting; DelayRemaining = plan.Waves[WaveIndex].DelaySeconds; }
        }
        public void Cancel()
        {
            if (Phase == RoomPhase.Cleared || Phase == RoomPhase.Cancelled) return;
            Phase = RoomPhase.Cancelled;
            DelayRemaining = 0; // Counters retained for diagnosis, never interpreted as kills.
        }
    }
}
