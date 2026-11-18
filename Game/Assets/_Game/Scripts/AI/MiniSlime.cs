using Game.Combat;
using UnityEngine;

namespace Game.AI
{
    [RequireComponent(typeof(ProjectilePool))]
    public sealed class MiniSlime : EliteEnemy
    {
        private ProjectilePool pool;
        private float shot = .9f, hop = .3f, remaining;
        private Vector2 heading;
        private long attack;
        public override string EnemyTypeId => "summoned_mini_slime";
        protected override void Awake()
        {
            base.Awake();
            pool = GetComponent<ProjectilePool>();
        }
        protected override void Tick(float dt)
        {
            if (remaining > 0)
            {
                remaining -= dt;
                Travel(heading, 7f * dt, dt);
            }
            else if ((hop -= dt) <= 0 && ToHero.sqrMagnitude > 4)
            {
                hop = 1.1f;
                remaining = .2f;
                heading = ToHero.normalized;
            }
            if ((shot -= dt) > 0 || ToHero.sqrMagnitude > 49 || !Visible(Body.position, HeroPosition)) return;
            var request = new ProjectileSpawnRequest(Body.position, ToHero.normalized, 6f, 1.4f, .14f,
                ~0, ActorId, CombatFaction.Enemy, new DamagePacket(ActorId, 3f, attackId: ++attack));
            if (pool.TrySpawn(in request)) shot = 1.6f;
        }
        protected override void ClearEffects() => pool?.ClearActive();
    }
}
