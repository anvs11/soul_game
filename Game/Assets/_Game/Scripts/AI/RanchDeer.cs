using Game.Encounters;
using UnityEngine;

namespace Game.AI
{
    [RequireComponent(typeof(CircleCollider2D), typeof(SpriteRenderer))]
    public sealed class RanchDeer : EliteEnemy
    {
        public enum AttackPhase { Approach, Warning, Charge, Recovery }

        public const float WarningSeconds = .9f;
        public const float RecoverySeconds = 1.1f;
        public const float ChargeDistance = 8f;
        public const float ChargeSpeed = 13f;
        public const float ChargeDamage = 24f;
        private const float TriggerRange = 7f;
        private const float PursuitSpeed = 2.35f;
        private const float StopGap = .02f;

        [SerializeField] private Sprite warningSprite;
        private CircleCollider2D shape;
        private SpriteRenderer warning;
        private Vector2 direction;
        private float remaining;
        private float pathLeft;

        public override string EnemyTypeId => EnemyTypes.RanchDeer;
        public AttackPhase Phase { get; private set; }
        public bool WarningVisible => warning != null && warning.enabled;
        public Vector2 ChargeDirection => direction;
        public float CollisionRadius => shape.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y));

        protected override void Awake()
        {
            base.Awake();
            shape = GetComponent<CircleCollider2D>();
            warning = CreateMarker("Ranch deer charge lane", new Color(1f, .52f, .13f, .68f));
            warning.sprite = warningSprite != null ? warningSprite : Skin.sprite;
        }

        protected override void Tick(float dt)
        {
            switch (Phase)
            {
                case AttackPhase.Approach:
                    Approach(dt);
                    break;
                case AttackPhase.Warning:
                    remaining -= dt;
                    if (remaining <= .0001f)
                    {
                        warning.enabled = false;
                        Phase = AttackPhase.Charge;
                    }
                    break;
                case AttackPhase.Charge:
                    Charge(dt);
                    break;
                case AttackPhase.Recovery:
                    remaining -= dt;
                    if (remaining <= .0001f) Phase = AttackPhase.Approach;
                    break;
            }
        }

        private void Approach(float dt)
        {
            Vector2 offset = ToHero;
            if (offset.sqrMagnitude > TriggerRange * TriggerRange || CoverBlocksHero ||
                !Visible(Body.position, HeroPosition))
            {
                Pursue(Mathf.Min(PursuitSpeed * dt, offset.magnitude), dt);
                return;
            }

            direction = offset.sqrMagnitude > .0001f ? offset.normalized : Vector2.right;
            float radius = CollisionRadius;
            float length = WallDistance(Body.position, direction, ChargeDistance, radius);
            if (length <= StopGap) return;

            Phase = AttackPhase.Warning;
            remaining = WarningSeconds;
            pathLeft = ChargeDistance;
            Skin.flipX = direction.x < 0;
            warning.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            // Include the swept body's front and rear in the visible lane.
            float visibleLength = length + radius * 2;
            if (warning.sprite != null)
            {
                Vector2 spriteSize = warning.sprite.bounds.size;
                ShowMarker(warning, Body.position + direction * (length * .5f),
                    new Vector2(visibleLength / spriteSize.x, radius * 2 / spriteSize.y), warning.color);
            }
            SignalWarning();
        }

        private void Charge(float dt)
        {
            float step = Mathf.Min(pathLeft, ChargeSpeed * dt);
            float safeStep = WallDistance(Body.position, direction, step, CollisionRadius);
            bool hitHero = HitsHero(Body.position, direction, safeStep, CollisionRadius, out float heroDistance);
            if (hitHero)
            {
                // Stop on contact, even if the hero's existing protection rejected the damage.
                Travel(direction, Mathf.Max(0, heroDistance - StopGap), dt);
                Recover();
                HurtHero(ChargeDamage);
                return;
            }

            float travelled = Travel(direction, safeStep, dt);
            pathLeft -= travelled;
            if (step - travelled > .0001f || pathLeft <= .0001f) Recover();
        }

        private void Recover()
        {
            Phase = AttackPhase.Recovery;
            remaining = RecoverySeconds;
        }

        protected override void ClearEffects()
        {
            if (warning != null) warning.enabled = false;
            Phase = AttackPhase.Approach;
            remaining = pathLeft = 0;
        }
    }
}
