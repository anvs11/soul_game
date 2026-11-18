using System;
using Game.Contracts;
using UnityEngine;

namespace Game.Combat
{
    public enum PickupKind { Ammo, Health, Armor }

    public sealed class RoomPickup : MonoBehaviour
    {
        [SerializeField] private PickupKind kind;
        [SerializeField] private int amount;
        private bool collecting;
        public string PickupId { get; private set; }
        public float Remaining { get; private set; }
        public PickupKind Kind => kind;
        public bool IsConsumed => Remaining <= 0;

        public void Configure(PickupKind type, int quantity)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            kind = type;
            amount = quantity;
        }
        public void Initialize(string runId, string roomId)
        {
            if (PickupId != null || string.IsNullOrEmpty(runId) || string.IsNullOrEmpty(roomId))
                throw new InvalidOperationException("A pickup belongs to one run and room.");
            PickupId = runId + "/" + roomId + "/" + kind;
            Remaining = amount;
        }
        public float TryCollect(IRunResourceSink receiver)
        {
            if (!isActiveAndEnabled || PickupId == null || receiver == null || Time.timeScale <= 0 || IsConsumed || collecting)
                return 0;
            collecting = true;
            try
            {
                float accepted;
                switch (kind)
                {
                    case PickupKind.Ammo: accepted = receiver.TryAddReserve((int)Remaining); break;
                    case PickupKind.Health: accepted = receiver.TryHeal(Remaining); break;
                    default: accepted = receiver.TryRepair(Remaining); break;
                }
                Remaining = Mathf.Max(0, Remaining - accepted);
                if (IsConsumed) gameObject.SetActive(false);
                return accepted;
            }
            finally { collecting = false; }
        }
    }
}
