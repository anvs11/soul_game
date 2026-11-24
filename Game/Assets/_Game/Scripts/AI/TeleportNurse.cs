using Game.Actors;
using Game.Encounters;
using UnityEngine;

namespace Game.AI
{
    public sealed class TeleportNurse : EliteEnemy
    {
        private enum Phase { Patrol, Aim, Shot, Combo, Recover, Slime }
        private static readonly Color Pink = new Color(1f, .28f, .65f);
        private const float BindDuration = 1f;
        private const float ComboRecovery = 1.7f;
        private Phase phase;
        private float timer = .6f, scratch, range, mistTime;
        private Vector2 bolt, heading;
        private SpriteRenderer marker, mist;
        private Color normal;
        private int attack;
        public override string EnemyTypeId => EnemyTypes.TeleportNurse;
        protected override float Threshold => 60f;
        public bool IsSlime => phase == Phase.Slime;
        protected override void Awake()
        {
            base.Awake();
            marker = CreateMarker("Pink projectile", Pink);
            mist = CreateMarker("Pink mist", Pink, circular: true);
            normal = Skin.color;
        }
        protected override void BeginSpecial()
        {
            marker.enabled = false;
            phase = Phase.Slime;
            timer = 2.7f;
            SetTargetable(false);
            Skin.color = new Color(.95f, .4f, .85f, .45f);
        }
        protected override void Tick(float dt)
        {
            if (mistTime > 0 && (mistTime -= dt) <= 0) mist.enabled = false;
            timer -= dt;
            switch (phase)
            {
                case Phase.Slime:
                    TickSlime(dt);
                    break;
                case Phase.Aim:
                    if (timer <= 0) { phase = Phase.Shot; bolt = Body.position; range = 5f; }
                    break;
                case Phase.Shot:
                    TickShot(dt);
                    break;
                case Phase.Combo:
                    if (timer <= 0)
                    {
                        phase = Phase.Recover;
                        timer = ComboRecovery;
                        Skin.color = normal;
                        break;
                    }
                    if ((scratch -= dt) <= 0)
                    {
                        scratch += .5f;
                        if (HeroWithin(Body.position, 1.5f)) HurtHero(4);
                    }
                    break;
                case Phase.Recover:
                    if (timer <= 0) phase = Phase.Patrol;
                    break;
                default:
                    if (ToHero.magnitude > 3.8f || CoverBlocksHero) Pursue(2.7f * dt, dt);
                    if (timer > 0 || ToHero.magnitude > 5.5f || !Visible(Body.position, HeroPosition)) break;
                    heading = ToHero.sqrMagnitude > .001f ? ToHero.normalized : Vector2.down;
                    phase = Phase.Aim;
                    SignalWarning();
                    timer = .25f;
                    ShowMarker(marker, Body.position + heading * .7f, Vector2.one * .35f, Pink);
                    break;
            }
        }
        private void TickSlime(float dt)
        {
            if (HeroWithin(Body.position, 1f))
            {
                SetTargetable(true);
                Apply("mist_stun", ActorStatusKind.Stun, 2f);
                Apply("mist_poison", ActorStatusKind.Poison, 1, true, poison: 2);
                Apply("mist_slow", ActorStatusKind.Slow, 1, true, slow: .65f);
                mistTime = 1f;
                ShowMarker(mist, Body.position, Vector2.one * 2f, new Color(1, .24f, .61f, .36f));
                BeginCombo();
            }
            else if (timer <= 0)
            {
                SetTargetable(true);
                BeginPatrol();
            }
            else Travel(ToHero.normalized, 7f * dt, dt);
        }
        private void TickShot(float dt)
        {
            float requested = Mathf.Min(range, 11f * dt);
            float step = WallDistance(bolt, heading, requested, .13f);
            bool direct = HitsHero(bolt, heading, step, .13f, out float distance);
            bolt += heading * (direct ? distance : step);
            range -= step;
            ShowMarker(marker, bolt, new Vector2(.38f, .26f), Pink);
            if (!direct && range > .001f && step + .001f >= requested) return;
            marker.enabled = false;
            if (direct) Apply("bind_" + ++attack, ActorStatusKind.Bind, BindDuration);
            TeleportNear(direct ? HeroPosition : bolt, direct);
            if (HeroWithin(Body.position, 1.5f))
            {
                if (direct)
                {
                    HurtHero(5);
                    Apply("syringe_poison_" + attack, ActorStatusKind.Poison, 4f, poison: 2);
                    Apply("syringe_slow_" + attack, ActorStatusKind.Slow, 4f, slow: .75f);
                }
                BeginCombo();
            }
            else BeginPatrol();
        }
        private void TeleportNear(Vector2 point, bool direct)
        {
            // Search one destination and a bounded ring. A world sweep prevents jumping through a door.
            for (int i = direct ? 1 : 0; i <= 8; i++)
            {
                float angle = (i - 1) * Mathf.PI / 4;
                Vector2 candidate = i == 0 ? point : point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.1f;
                Vector2 delta = candidate - Body.position;
                if (delta.sqrMagnitude < .001f || WallDistance(Body.position, delta.normalized, delta.magnitude, .43f) + .001f < delta.magnitude ||
                    !IsFree(candidate, .5f)) continue;
                Body.position = candidate;
                return;
            }
        }
        private void BeginCombo()
        {
            phase = Phase.Combo;
            timer = 1.5f;
            scratch = 0;
            Skin.color = Pink;
        }
        private void BeginPatrol()
        {
            phase = Phase.Patrol;
            timer = 1.2f;
            Skin.color = normal;
        }
        private void Apply(string id, ActorStatusKind kind, float duration, bool persistent = false, float slow = .5f, float poison = 0)
        {
            HeroStatuses.TryApply(new ActorStatusEffect(ActorId, id, kind, duration, persistent, slow, poison));
        }
        protected override void ClearEffects()
        {
            if (marker != null) marker.enabled = false;
            if (mist != null) mist.enabled = false;
            if (!Targetable)
            {
                // OnDisable/Death has already removed the entry from the registry.
                SetTargetable(true);
                BeginPatrol();
            }
        }
    }
}
