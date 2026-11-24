using System;
using System.Collections.Generic;
using Game.Actors;
using Game.Combat;
using UnityEngine;

namespace Game.AI
{
    [RequireComponent(typeof(ActorIdentity), typeof(DamageReceiver), typeof(Rigidbody2D))]
    public abstract class EliteEnemy : MonoBehaviour, IEnemyTarget, IDamageAdjustment
    {
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private TargetRegistry registry;
        private PlayerCombatFacade hero;
        private DamageReceiver heroDamage;
        private DamageReceiver health;
        private CoverNavigation coverRoute;
        private bool dead;
        private long attackId;
        protected Rigidbody2D Body { get; private set; }
        protected SpriteRenderer Skin { get; private set; }
        protected Vector2 HeroPosition => heroDamage.transform.position;
        protected Vector2 ToHero => HeroPosition - Body.position;
        protected ActorStatusController HeroStatuses { get; private set; }
        protected ActorMotor2D HeroMotor { get; private set; }
        protected int HeroId { get; private set; }
        protected int World => LayerMask.GetMask("World");
        protected bool Targetable { get; private set; } = true;
        public bool CanReceiveHits => Targetable;
        public int ActorId => GetComponent<ActorIdentity>().ActorId;
        public bool IsDead => Health.IsDead;
        private DamageReceiver Health => health != null ? health : health = GetComponent<DamageReceiver>();
        public abstract string EnemyTypeId { get; }
        public bool SpecialUsed { get; private set; }
        public event Action Died;
        internal static event Action<EliteEnemy> WarningStarted;
        protected void SignalWarning() => WarningStarted?.Invoke(this);
        protected virtual float Threshold => 0;

        public void Initialize(TargetRegistry targets, PlayerCombatFacade player)
        {
            registry = targets;
            hero = player;
            heroDamage = player.GetComponent<DamageReceiver>();
            HeroMotor = player.GetComponent<ActorMotor2D>();
            HeroStatuses = player.GetComponent<ActorStatusController>();
            HeroId = player.GetComponent<ActorIdentity>().ActorId;
        }
        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Skin = GetComponent<SpriteRenderer>();
        }
        private void OnEnable()
        {
            Health.DamageApplied += OnDamage;
            if (registry != null) registry.Reactivated += Register;
            Register();
        }
        private void Register()
        {
            if (isActiveAndEnabled && !IsDead && Targetable) registry?.Register(this);
        }
        protected void SetTargetable(bool value)
        {
            Targetable = value;
            if (value) Register();
            else registry?.Unregister(this);
        }
        private void OnDamage(DamageResult result)
        {
            if (!result.Killed || dead) return;
            dead = true;
            registry?.Unregister(this);
            StopEffects();
            Body.simulated = false;
            if (Skin != null) Skin.enabled = false;
            Died?.Invoke();
        }
        private void OnDisable()
        {
            Health.DamageApplied -= OnDamage;
            if (registry != null) registry.Reactivated -= Register;
            registry?.Unregister(this);
            StopEffects();
        }
        private void StopEffects()
        {
            HeroStatuses?.ReleaseSource(ActorId);
            if (Body != null) Body.linearVelocity = Vector2.zero;
            ClearEffects();
        }
        protected virtual void ClearEffects() { }
        private void FixedUpdate()
        {
            Body.linearVelocity = Vector2.zero;
            if (dead || hero == null || hero.IsDead) return;
            if (!SpecialUsed && Threshold > 0 && Health.Health <= Threshold)
            {
                SpecialUsed = true;
                BeginSpecial();
            }
            Tick(Time.fixedDeltaTime);
        }
        protected virtual void BeginSpecial() { }
        protected abstract void Tick(float dt);
        public virtual float AdjustDamage(in DamagePacket packet) => Targetable ? packet.Amount : 0;

        protected bool Visible(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            return delta.sqrMagnitude <= .0001f || !Physics2D.Raycast(from, delta.normalized, delta.magnitude, World);
        }
        protected float WallDistance(Vector2 origin, Vector2 direction, float length, float radius = 0)
        {
            var hit = radius > 0 ? Physics2D.CircleCast(origin, radius, direction, length, World) :
                Physics2D.Raycast(origin, direction, length, World);
            return hit.collider != null ? Mathf.Max(0, hit.distance - .02f) : length;
        }
        protected float Travel(Vector2 direction, float distance, float dt)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(World);
            Body.Cast(direction, filter, hits, distance + .02f);
            foreach (var hit in hits) distance = Mathf.Min(distance, Mathf.Max(0, hit.distance - .02f));
            Body.linearVelocity = direction * (distance / dt);
            return distance;
        }

        internal void EnableCoverNavigation() => coverRoute = new CoverNavigation(GetComponent<Rigidbody2D>());

        protected bool CoverBlocksHero => coverRoute != null && !Visible(Body.position, HeroPosition);

        protected void Pursue(float distance, float dt) =>
            Travel(coverRoute?.Heading(HeroPosition) ?? ToHero.normalized, distance, dt);
        protected bool IsFree(Vector2 position, float radius)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(~0);
            Physics2D.OverlapCircle(position, radius, filter, overlaps);
            foreach (var hit in overlaps)
                if (hit.GetComponentInParent<ActorIdentity>() != GetComponent<ActorIdentity>() &&
                    ((World & (1 << hit.gameObject.layer)) != 0 || hit.GetComponentInParent<ActorIdentity>() != null))
                    return false;
            return true;
        }
        protected bool HitsHero(Vector2 from, Vector2 direction, float length, float radius, out float distance)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(~0);
            if (radius > 0) Physics2D.CircleCast(from, radius, direction, filter, hits, length);
            else Physics2D.Raycast(from, direction, filter, hits, length);
            distance = float.PositiveInfinity;
            foreach (var hit in hits)
                if (hit.collider != null && hit.collider.GetComponentInParent<DamageReceiver>() == heroDamage)
                    distance = Mathf.Min(distance, hit.distance);
            return !float.IsPositiveInfinity(distance);
        }
        protected bool HurtHero(float damage)
        {
            var packet = new DamagePacket(ActorId, damage, attackId: ++attackId);
            return heroDamage != null && heroDamage.ApplyDamage(in packet).Applied;
        }
        protected bool HeroWithin(Vector2 point, float radius) =>
            (HeroPosition - point).sqrMagnitude <= radius * radius && Visible(point, HeroPosition);

        protected SpriteRenderer CreateMarker(string label, Color color, bool circular = false)
        {
            var marker = new GameObject(label).AddComponent<SpriteRenderer>();
            marker.transform.SetParent(transform, false);
            marker.sprite = (circular ? Resources.Load<Sprite>("Stage4AreaMarker") : null) ?? Skin.sprite;
            marker.color = color;
            marker.sortingOrder = Skin.sortingOrder - 1;
            marker.enabled = false;
            return marker;
        }
        protected static void ShowMarker(SpriteRenderer marker, Vector2 position, Vector2 size, Color color)
        {
            marker.transform.position = position;
            marker.transform.localScale = size;
            marker.color = color;
            marker.enabled = true;
        }
    }
}
