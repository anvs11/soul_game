using System;
using System.Collections.Generic;
using Game.Actors;
using Game.Combat;
using UnityEngine;
namespace Game.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D),typeof(ActorIdentity),typeof(DamageReceiver))]
    [RequireComponent(typeof(EnemyPerception))]
    public sealed class JumpingEnemy : MonoBehaviour,IEnemyTarget
    {
        [SerializeField] private JumpingEnemyDefinition definition;
        [SerializeField] private LayerMask worldMask=~0,damageMask=~0;
        private Rigidbody2D body;
        private DamageReceiver health,targetHealth;
        private ActorIdentity identity;
        private EnemyPerception perception;
        private TargetRegistry registry;
        private Transform player;
        private float timer;
        private bool consumed,deathSent,started;
        private SpriteRenderer skin,signal;
        private readonly List<RaycastHit2D> hits=new List<RaycastHit2D>();
        private readonly List<(Collider2D a,Collider2D b)> pairs=new List<(Collider2D,Collider2D)>();
        public enum AttackPhase { Idle,Chase,Windup,Dash,Recovery,Dead }
        public AttackPhase Phase { get; private set; }
        public Vector2 DashDirection { get; private set; }
        public int ActorId { get { Ensure();return identity.ActorId; } }
        public bool IsDead { get { Ensure();return health.IsDead; } }
        public event Action Died;
        private void Ensure()
        {
            if(body==null)body=GetComponent<Rigidbody2D>();
            if(health==null)health=GetComponent<DamageReceiver>();
            if(identity==null)identity=GetComponent<ActorIdentity>();
            if(perception==null)perception=GetComponent<EnemyPerception>();
            if(skin==null)skin=GetComponent<SpriteRenderer>();
        }
        public void Configure(JumpingEnemyDefinition value,LayerMask world,LayerMask damage)
        {
            if(isActiveAndEnabled||started)throw new InvalidOperationException("Configure before first activation.");
            if(value==null)throw new ArgumentNullException(nameof(value));value.Validate();
            definition=value;worldMask=world;damageMask=damage;
        }
        public void Initialize(TargetRegistry targets,Transform target)
        {
            Ensure();Unregister();RestoreContacts();registry=targets;player=target;
            targetHealth=player!=null?player.GetComponent<DamageReceiver>():null;
            ResetAttack();IgnoreContacts();
            if(isActiveAndEnabled){if(registry!=null)registry.Reactivated+=Register;Register();}
        }
        private void OnEnable()
        {
            Ensure();if(definition==null)throw new InvalidOperationException("Missing jumping enemy definition.");
            definition.Validate();started=true;
            health.DamageApplied+=OnDamage;ResetAttack();IgnoreContacts();
            if(registry!=null)registry.Reactivated+=Register;
            if(IsDead)Die();else Register();
        }
        private void Register(){if(registry!=null && isActiveAndEnabled && !IsDead)registry.Register(this);}
        private void Unregister(){if(registry!=null){registry.Reactivated-=Register;registry.Unregister(this);}}
        private void OnDisable(){Unregister();RestoreContacts();if(health!=null)health.DamageApplied-=OnDamage;ResetAttack();}
        private void ResetAttack()
        {
            Ensure();timer=0;consumed=false;DashDirection=Vector2.zero;
            Phase=IsDead?AttackPhase.Dead:AttackPhase.Idle;
            body.linearVelocity=Vector2.zero;perception.ResetMemory();ShowSignal(false);
        }
        private void IgnoreContacts()
        {
            if(player==null)return;
            foreach(var a in GetComponentsInChildren<Collider2D>())foreach(var b in player.GetComponentsInChildren<Collider2D>())
                if(!Physics2D.GetIgnoreCollision(a,b)){Physics2D.IgnoreCollision(a,b,true);pairs.Add((a,b));}
        }
        private void RestoreContacts(){foreach(var p in pairs)if(p.a!=null&&p.b!=null)Physics2D.IgnoreCollision(p.a,p.b,false);pairs.Clear();}
        private void ShowSignal(bool show)
        {
            if(show && signal==null && skin!=null && skin.sprite!=null)
            {
                signal=new GameObject("Jump direction").AddComponent<SpriteRenderer>();signal.transform.SetParent(transform,false);
                signal.sprite=skin.sprite;signal.sortingOrder=skin.sortingOrder+1;signal.color=Color.yellow;
            }
            if(signal==null)return;signal.enabled=show;
            if(show){signal.transform.localScale=new Vector3(definition.TriggerRange,.1f,1);
                signal.transform.position=(Vector2)transform.position+DashDirection*definition.TriggerRange*.5f;
                signal.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(DashDirection.y,DashDirection.x)*Mathf.Rad2Deg);}
        }
        private void FixedUpdate()
        {
            body.linearVelocity=Vector2.zero;
            if(IsDead)return;
            if(player==null||targetHealth==null||!targetHealth.isActiveAndEnabled||targetHealth.IsDead){ResetAttack();return;}
            bool detected=perception.Tick(player,targetHealth,Time.fixedDeltaTime);
            if(Phase==AttackPhase.Idle||Phase==AttackPhase.Chase)
            {
                Phase=detected?AttackPhase.Chase:AttackPhase.Idle;
                if(!detected)return;
                Vector2 offset=perception.PursuitPosition-body.position;
                if(perception.HasLineOfSight && offset.magnitude<=definition.TriggerRange)
                {
                    DashDirection=offset.sqrMagnitude>.0001f?offset.normalized:Vector2.down;
                    Phase=AttackPhase.Windup;timer=definition.Windup;consumed=false;ShowSignal(true);
                }
                else body.linearVelocity=offset.normalized*Mathf.Min(definition.ChaseSpeed,offset.magnitude/Time.fixedDeltaTime);
                return;
            }
            if(Phase==AttackPhase.Dash){DashStep();return;}
            timer-=Time.fixedDeltaTime;
            if(Phase==AttackPhase.Windup)
            {
                ShowSignal(true);
                if(timer<=0){Phase=AttackPhase.Dash;timer=definition.DashDuration;ShowSignal(false);}
            }
            else if(timer<=0)Phase=AttackPhase.Idle;
            if(skin!=null)skin.color=Phase==AttackPhase.Windup?Color.yellow:Phase==AttackPhase.Recovery?Color.gray:new Color(.8f,.2f,.9f);
        }
        private void DashStep()
        {
            float distance=definition.DashSpeed*Mathf.Min(timer,Time.fixedDeltaTime);
            var filter=new ContactFilter2D{useTriggers=false};filter.SetLayerMask(worldMask);
            body.Cast(DashDirection,filter,hits,distance+.02f);
            bool blocked=false;
            foreach(var hit in hits)
                if(hit.collider!=null && hit.collider.GetComponentInParent<ActorIdentity>()==null)
                {distance=Mathf.Min(distance,Mathf.Max(0,hit.distance-.02f));blocked=true;}
            if(!consumed)
            {
                filter.SetLayerMask(damageMask);
                Physics2D.CircleCast(body.position,definition.HitRadius,DashDirection,filter,hits,distance);
                foreach(var hit in hits)
                {
                    if(hit.collider==null || hit.collider.GetComponentInParent<DamageReceiver>()!=targetHealth)continue;
                    var targetIdentity=hit.collider.GetComponentInParent<ActorIdentity>();
                    if(targetIdentity!=null && targetIdentity.Faction==identity.Faction)continue;
                    consumed=true;
                    var result=targetHealth.ApplyDamage(new DamagePacket(ActorId,definition.Damage));
                    if(result.Applied&&!result.Killed)player.GetComponent<ActorMotor2D>()?.BeginKnockback(DashDirection,definition.Knockback);
                    break;
                }
            }
            body.linearVelocity=DashDirection*(distance/Time.fixedDeltaTime);
            timer-=Time.fixedDeltaTime;
            if(timer<=0||blocked){Phase=AttackPhase.Recovery;timer=definition.Recovery;}
        }
        private void OnDamage(DamageResult result){if(result.Killed)Die();}
        private void Die()
        {
            if(deathSent)return;deathSent=true;ResetAttack();Phase=AttackPhase.Dead;
            body.simulated=false;foreach(var c in GetComponentsInChildren<Collider2D>())c.enabled=false;
            if(skin!=null)skin.enabled=false;Unregister();Died?.Invoke();
        }
    }
}
