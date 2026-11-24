using Game.Combat;
using Game.Encounters;
using UnityEngine;

namespace Game.AI
{
    public sealed class ShieldShotgun : EliteEnemy
    {
        private enum Phase { Patrol, Warning, Charging, Burst }
        private Phase phase;
        private Vector2 facing = Vector2.left;
        private SpriteRenderer shield, lane;
        private readonly SpriteRenderer[] traces = new SpriteRenderer[5];
        private float timer, cooldown = .8f, flash;
        private int burstShots;
        private bool ramHit;
        public override string EnemyTypeId => EnemyTypes.ShieldShotgun;
        protected override float Threshold => 100f;
        public Vector2 Facing => facing;
        public int BurstShots => burstShots;
        protected override void Awake()
        {
            base.Awake();
            shield = CreateMarker("Shield direction", new Color(.25f, .8f, 1f));
            lane = CreateMarker("Ram lane", Color.red);
            for (int i = 0; i < traces.Length; i++) traces[i] = CreateMarker("Shotgun trace", Color.yellow);
        }
        public override float AdjustDamage(in DamagePacket packet)
        {
            float damage = base.AdjustDamage(in packet);
            if (packet.ProjectileHitNormal is Vector2 normal)
                return Vector2.Dot(facing, normal) > 0 ? 0f : damage;
            return HeroId != 0 && packet.SourceActorId == HeroId && Vector2.Dot(facing, ToHero) > 0 ? damage * .35f : damage;
        }
        protected override void BeginSpecial()
        {
            if (ToHero.sqrMagnitude > .001f) facing = ToHero.normalized;
            phase = Phase.Warning;
            SignalWarning();
            timer = .6f;
            ramHit = false;
        }
        protected override void Tick(float dt)
        {
            flash -= dt;
            if (flash <= 0) foreach (var ray in traces) ray.enabled = false;
            DrawLine(shield, Body.position + facing * .74f, facing, .16f, 1.18f, new Color(.25f, .8f, 1f));
            if (phase == Phase.Warning)
            {
                float length = WallDistance(Body.position, facing, 40, .7f);
                DrawLine(lane, Body.position + facing * length * .5f, facing, length, 3f, new Color(1f, .22f, .1f, .3f));
                if ((timer -= dt) <= 0)
                {
                    phase = Phase.Charging;
                    timer = 4f;
                }
                return;
            }
            if (phase == Phase.Charging)
            {
                float step = Travel(facing, 10f * dt, dt);
                if (!ramHit && HitsHero(Body.position, facing, step, 1.5f, out _) && Visible(Body.position, HeroPosition))
                {
                    ramHit = true;
                    if (HurtHero(16))
                    {
                        HeroMotor.BeginKnockback(facing, 4.5f);
                        HeroMotor.Stun(1f);
                    }
                }
                if (step + .001f < 10f * dt || (timer -= dt) <= 0)
                {
                    phase = Phase.Burst;
                    lane.enabled = false;
                    timer = 3f;
                    cooldown = .15f;
                    burstShots = 0;
                }
                return;
            }
            if (phase == Phase.Burst && (timer -= dt) <= 0) phase = Phase.Patrol;
            if (ToHero.sqrMagnitude > .001f)
            {
                float turn = Vector2.SignedAngle(facing, ToHero);
                facing = Rotate(facing, Mathf.Clamp(turn, -65f * dt, 65f * dt));
            }
            float distance = ToHero.magnitude;
            if (phase == Phase.Patrol && CoverBlocksHero)
                Pursue(1.5f * dt, dt);
            else if (distance > 2.6f && Vector2.Dot(facing, ToHero) > 0)
                Travel(facing, (phase == Phase.Burst ? 3.2f : 1.5f) * dt, dt);
            cooldown -= dt;
            if (cooldown > 0 || distance > 5f || Vector2.Dot(facing, ToHero) <= 0 || !Visible(Body.position, HeroPosition)) return;
            if (phase == Phase.Burst)
            {
                if (burstShots >= 5) return;
                Fire();
                burstShots++;
                cooldown = .4f;
            }
            else
            {
                if (distance <= 1f)
                {
                    if (HurtHero(8)) HeroMotor.BeginKnockback(ToHero.normalized, 1f);
                }
                else Fire();
                cooldown = 1.25f;
            }
        }
        private void Fire()
        {
            float damage = 0;
            float angle = Mathf.Clamp(Vector2.SignedAngle(facing, ToHero), -66f, 66f);
            for (int i = 0; i < traces.Length; i++)
            {
                Vector2 direction = Rotate(facing, angle + (i - 2) * 12f);
                float reach = WallDistance(Body.position, direction, 5f);
                if (HitsHero(Body.position, direction, reach, .09f, out float contact))
                {
                    damage += Mathf.Lerp(4.5f, 2.25f, contact / 5f);
                    reach = contact;
                }
                DrawLine(traces[i], Body.position + direction * reach * .5f, direction, reach, .055f, Color.yellow);
            }
            flash = .095f;
            if (damage > 0) HurtHero(damage);
        }
        private static Vector2 Rotate(Vector2 source, float degrees) => Quaternion.Euler(0, 0, degrees) * source;
        private static void DrawLine(SpriteRenderer marker, Vector2 center, Vector2 direction, float length, float width, Color color)
        {
            ShowMarker(marker, center, new Vector2(length, width), color);
            marker.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }
        protected override void ClearEffects()
        {
            if (shield != null) shield.enabled = false;
            if (lane != null) lane.enabled = false;
            foreach (var ray in traces) if (ray != null) ray.enabled = false;
        }
    }
}
