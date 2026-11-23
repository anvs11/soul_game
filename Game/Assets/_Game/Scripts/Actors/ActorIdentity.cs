using System.Threading;
using UnityEngine;

namespace Game.Actors
{
    [DisallowMultipleComponent]
    public sealed class ActorIdentity : MonoBehaviour
    {
        [SerializeField] private Game.Combat.CombatFaction faction = Game.Combat.CombatFaction.Player;
        public Game.Combat.CombatFaction Faction => faction;
        internal void SetFaction(Game.Combat.CombatFaction value) => faction = value;
        private static int nextId;
        private int actorId;
        // Process-local identity, deliberately not serialized into duplicated prefabs.
        public int ActorId => actorId != 0 ? actorId : (actorId = Interlocked.Increment(ref nextId));
        private void Awake() { _ = ActorId; }
    }
}
