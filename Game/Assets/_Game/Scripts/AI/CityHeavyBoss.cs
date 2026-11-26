using Game.Actors;
using Game.Combat;
using Game.Encounters;
using UnityEngine;

namespace Game.AI
{
    public sealed class CityHeavyBoss : EliteEnemy
    {
        private readonly Vector2[] directions = new Vector2[12];
        private readonly SpriteRenderer[] telegraphs = new SpriteRenderer[12];
        private ProjectilePool pool;
        private int district, attack, directionCount;
        private float remaining = .7f;
        private bool warning;
        public override string EnemyTypeId => EnemyTypes.HeavyBoss;

        internal static CityHeavyBoss Create(Transform parent, Sprite sprite, Projectile template, int district)
        {
            var actor = new GameObject("City boss");
            actor.SetActive(false);
            actor.transform.SetParent(parent, false);
            var skin = actor.AddComponent<SpriteRenderer>();
            skin.sprite = sprite;
            skin.color = district switch { 0 => new Color(.85f,.32f,.16f), 1 => new Color(.8f,.51f,.19f),
                3 => new Color(.48f,.75f,.31f), _ => new Color(.62f,.43f,.83f) };
            skin.sortingOrder = 3;
            var body = actor.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.freezeRotation = true;
            actor.AddComponent<CircleCollider2D>().radius = .83f;
            actor.AddComponent<ActorIdentity>().SetFaction(CombatFaction.Enemy);
            actor.AddComponent<DamageReceiver>().Configure(district == 3 ? 300 : 310 + district * 45, 0, .09f);
            var boss = actor.AddComponent<CityHeavyBoss>();
            boss.district = district;
            boss.pool = actor.AddComponent<ProjectilePool>();
            boss.pool.Configure(template, 40);
            return boss;
        }

        protected override void Tick(float dt)
        {
            remaining -= dt;
            if (remaining > 0)
            {
                if (!warning && (ToHero.magnitude > 5.3f || CoverBlocksHero)) Pursue(dt * 1.8f, dt);
                return;
            }
            var health = GetComponent<DamageReceiver>();
            bool enraged = health.Health <= health.MaxHealth * .5f;
            if (warning)
            {
                warning = false;
                for (int i = 0; i < directionCount; i++)
                {
                    telegraphs[i].enabled = false;
                    var request = new ProjectileSpawnRequest(Body.position, directions[i], 8.5f + district,
                        2.6f, .18f, World | (1 << HeroMotor.gameObject.layer), ActorId, CombatFaction.Enemy,
                        new DamagePacket(ActorId, 9 + 2 * district, attackId: attack));
                    pool.TrySpawn(in request);
                }
                remaining = district == 3 ? (enraged ? 1.12f : 1.65f) : enraged ? .86f : 1.25f;
                return;
            }
            bool fan = district == 3 ? attack % (enraged ? 2 : 3) == 0 : district != 2 || attack % 2 == 0;
            if (fan && !Visible(Body.position, HeroPosition))
            {
                if (district == 3) Pursue(dt * 1.8f, dt);
                else Travel(Vector2.Perpendicular(ToHero.normalized), dt * 1.8f, dt);
                return;
            }
            warning = true;
            attack++;
            remaining = .85f;
            directionCount = fan ? (district == 3 && enraged ? 5 : 3) : 12;
            Vector2 toward = ToHero.sqrMagnitude > .01f ? ToHero.normalized : Vector2.down;
            for (int i = 0; i < directionCount; i++)
            {
                float angle = fan ? (i - (directionCount - 1) * .5f) * (district == 1 ? 19f : 11f)
                    : (i + .5f) * 30f + (district == 3 && attack % 2 == 0 ? 15f : 0f);
                Vector2 direction = Quaternion.Euler(0, 0, angle) * toward;
                directions[i] = direction;
                float length = WallDistance(Body.position, direction, 16f, .18f);
                telegraphs[i] ??= CreateMarker("City boss warning", Color.yellow);
                var marker = telegraphs[i];
                marker.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                float scale = Mathf.Max(.01f, marker.sprite.bounds.size.x);
                ShowMarker(marker, Body.position + direction * length * .5f, new Vector2(length / scale, .13f / scale),
                    fan ? new Color(1f,.57f,.16f,.88f) : new Color(1f,.88f,.3f,.82f));
            }
            SignalWarning();
        }

        protected override void ClearEffects()
        {
            if (pool != null) pool.ClearActive();
            foreach (var marker in telegraphs) if (marker != null) marker.enabled = false;
        }
    }
}
