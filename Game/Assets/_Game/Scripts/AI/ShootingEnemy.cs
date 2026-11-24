using System;
using Game.Actors;
using Game.Combat;
using UnityEngine;
namespace Game.AI
{
    [RequireComponent(typeof(Rigidbody2D),typeof(ActorIdentity),typeof(DamageReceiver))]
    [RequireComponent(typeof(EnemyPerception))]
    public sealed class ShootingEnemy : MonoBehaviour,IEnemyTarget
    {
        [SerializeField] private ShootingEnemyDefinition definition;
        [SerializeField] private ProjectilePool pool;
        [SerializeField] private LayerMask damageMask=~0;
        private Rigidbody2D body;
        private DamageReceiver health,targetHealth;
        private ActorIdentity identity;
        private EnemyPerception perception;
        private Transform target;
        private TargetRegistry registry;
        private SpriteRenderer skin;
        private float remaining;
        private bool started,deathSent;
        public enum AttackPhase { Waiting,Windup,Recovery,Dead }
        public AttackPhase Phase { get; private set; }
        public int ShotsFired { get; private set; }
        public int ActorId { get { Ensure();return identity.ActorId; } }
        public bool IsDead { get { Ensure();return health.IsDead; } }
        public event Action Died;
        private void Ensure()
        {
            body=GetComponent<Rigidbody2D>();health=GetComponent<DamageReceiver>();
            identity=GetComponent<ActorIdentity>();perception=GetComponent<EnemyPerception>();skin=GetComponent<SpriteRenderer>();
        }
        public void Configure(ShootingEnemyDefinition value,ProjectilePool projectiles,LayerMask world,LayerMask damage)
        {
            if(isActiveAndEnabled||started)throw new InvalidOperationException("Configure before activation.");
            if(value==null||projectiles==null)throw new ArgumentNullException(nameof(value));
            value.Validate();definition=value;pool=projectiles;damageMask=damage;
            GetComponent<EnemyPerception>().Configure(8,11,2,world);
        }
        public void Initialize(TargetRegistry targets,Transform player)
        {
            Ensure();Detach();ClearShots();registry=targets;target=player;
            targetHealth=target!=null?target.GetComponent<DamageReceiver>():null;
            ResetAttack();if(isActiveAndEnabled){if(registry!=null)registry.Reactivated+=Register;Register();}
        }
        private void OnEnable()
        {
            Ensure();definition.Validate();started=true;ResetAttack();health.DamageApplied+=OnDamage;
            if(registry!=null)registry.Reactivated+=Register;
            if(IsDead)Die();else Register();
        }
        private void Register(){if(registry!=null&&isActiveAndEnabled&&!IsDead)registry.Register(this);}
        private void Detach(){if(registry!=null){registry.Reactivated-=Register;registry.Unregister(this);}}
        private void ClearShots(){if(pool!=null)pool.ClearActive();}
        internal bool MarketVolley { get; set; }
        private void ResetAttack(){remaining=0;Phase=IsDead?AttackPhase.Dead:AttackPhase.Waiting;body.linearVelocity=Vector2.zero;perception.ResetMemory();if(skin!=null)skin.color=Color.cyan;}
        private void OnDisable(){Detach();ClearShots();if(health!=null)health.DamageApplied-=OnDamage;if(body!=null)ResetAttack();}
        private void FixedUpdate()
        {
            body.linearVelocity=Vector2.zero;
            if(IsDead)return;
            if(target==null||targetHealth==null||!targetHealth.isActiveAndEnabled||targetHealth.IsDead)
            {ResetAttack();ClearShots();return;}
            bool detected=perception.Tick(target,targetHealth,Time.fixedDeltaTime);
            Vector2 offset=(Vector2)target.position-body.position;
            if(Phase==AttackPhase.Recovery)
            {remaining-=Time.fixedDeltaTime;if(remaining<=0)Phase=AttackPhase.Waiting;return;}
            if(!detected||!perception.HasLineOfSight||offset.magnitude>definition.FireRange)
            {
                Phase=AttackPhase.Waiting;remaining=0;
                if(skin!=null)skin.color=Color.cyan;
                if(detected)body.linearVelocity=perception.Approach((perception.PursuitPosition-body.position).normalized)*definition.Speed;
                return;
            }
            if(Phase==AttackPhase.Waiting)
            {
                if(offset.magnitude<definition.MinimumRange){body.linearVelocity=-offset.normalized*definition.Speed;return;}
                if(offset.magnitude>definition.PreferredRange){body.linearVelocity=perception.Approach(offset.normalized)*definition.Speed;return;}
                Phase=AttackPhase.Windup;remaining=definition.Windup;
                if(skin!=null)skin.color=Color.yellow;
                return;
            }
            remaining-=Time.fixedDeltaTime;
            if(remaining>0)return;
            if (MarketVolley) { Fire(offset, -14); Fire(offset, 14); }
            else Fire(offset, 0);
            Phase=AttackPhase.Recovery;remaining=definition.Interval;
            if(skin!=null)skin.color=Color.cyan;
        }
        private void Fire(Vector2 offset, float angle)
        {
            Vector2 direction = Quaternion.Euler(0, 0, angle) * offset.normalized;
            var request = new ProjectileSpawnRequest(body.position, direction, definition.ProjectileSpeed,
                definition.ProjectileLifetime, .12f, damageMask, ActorId, identity.Faction,
                new DamagePacket(ActorId, definition.Damage));
            if (pool.TrySpawn(in request)) ShotsFired++;
        }
        private void OnDamage(DamageResult result){if(result.Killed)Die();}
        private void Die()
        {
            if(deathSent)return;deathSent=true;ResetAttack();Phase=AttackPhase.Dead;ClearShots();Detach();
            body.simulated=false;foreach(var collider in GetComponentsInChildren<Collider2D>())collider.enabled=false;
            foreach(var renderer in GetComponentsInChildren<SpriteRenderer>(true))renderer.enabled=false;
            Died?.Invoke();
        }
    }
}
