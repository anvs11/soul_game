using Game.Actors;
using UnityEngine;

namespace Game.StreetMap
{
    [RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class RanchTrap : MonoBehaviour
    {
        public const string TrapId = "ranch_trap";
        public const float HoldSeconds = .8f;
        private static readonly Color SpentTint = new Color(.48f, .47f, .44f, .75f);
        public bool IsSpent { get; private set; }

        private void OnTriggerEnter2D(Collider2D other) => TryCatch(other);
        private void OnTriggerStay2D(Collider2D other) => TryCatch(other);

        private void TryCatch(Collider2D other)
        {
            if (IsSpent || !isActiveAndEnabled || Time.timeScale <= 0) return;
            var hero = other.GetComponentInParent<PlayerCombatFacade>();
            if (hero == null || hero.IsDead || !hero.TryGetComponent(out ActorStatusController status)) return;
            var effect = new ActorStatusEffect(GetInstanceID(), TrapId, ActorStatusKind.Root, HoldSeconds);
            if (!status.TryApply(effect)) return;

            IsSpent = true;
            GetComponent<BoxCollider2D>().enabled = false;
            GetComponent<SpriteRenderer>().color = SpentTint;
        }
    }
}
