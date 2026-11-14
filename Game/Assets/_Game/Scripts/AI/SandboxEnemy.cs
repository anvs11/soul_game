using System;
using Game.Actors;
using Game.Combat;
using Game.Contracts;
using UnityEngine;
namespace Game.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(ActorIdentity), typeof(DamageReceiver))]
    public sealed class SandboxEnemy : MonoBehaviour, IDefeatable
    {
        [SerializeField] private TargetRegistry registry;
        [SerializeField] private Transform player;
        [SerializeField, Min(0)] private float speed = 2.5f;
        private DamageReceiver health, playerHealth;
        private Rigidbody2D body;
        private ActorIdentity identity;
        private bool deathSent;
        private double nextContact;
        public event Action Died;
        public bool IsDead { get { EnsureReferences(); return health.IsDead; } }
        public int ActorId { get { EnsureReferences(); return identity.ActorId; } }
        private void EnsureReferences()
        {
            if (health == null) health = GetComponent<DamageReceiver>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (identity == null) identity = GetComponent<ActorIdentity>();
        }
        public void Initialize(TargetRegistry targets, Transform playerTransform)
        {
            if (registry != null) { registry.Reactivated -= RegisterAlive; registry.Unregister(this); }
            registry = targets; player = playerTransform;
            playerHealth = player != null ? player.GetComponent<DamageReceiver>() : null;
            if (isActiveAndEnabled && registry != null) { registry.Reactivated += RegisterAlive; RegisterAlive(); }
        }
        private void OnEnable()
        {
            EnsureReferences();
            playerHealth = player != null ? player.GetComponent<DamageReceiver>() : null;
            health.DamageApplied += OnDamage;
            if (registry != null) registry.Reactivated += RegisterAlive;
            if (IsDead) ObserveDeath();
            else if (registry != null) registry.Register(this);
        }
        private void OnDisable()
        {
            if (health != null) health.DamageApplied -= OnDamage;
            if (registry != null) { registry.Reactivated -= RegisterAlive; registry.Unregister(this); }
            if (body != null) body.linearVelocity = Vector2.zero;
        }
        private void RegisterAlive()
        {
            if (isActiveAndEnabled && !IsDead && registry != null) registry.Register(this);
        }
        private void FixedUpdate()
        {
            body.linearVelocity = !IsDead && player != null && playerHealth != null && !playerHealth.IsDead
                ? ((Vector2)player.position - body.position).normalized * speed : Vector2.zero;
        }
        private void OnDamage(DamageResult result) { if (result.Killed) ObserveDeath(); }
        private void ObserveDeath()
        {
            if (deathSent) return;
            deathSent = true;
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
            var renderer = GetComponent<SpriteRenderer>(); if (renderer != null) renderer.enabled = false;
            if (registry != null) registry.Unregister(this);
            Died?.Invoke();
        }
        private void OnCollisionEnter2D(Collision2D collision) => Contact(collision);
        private void OnCollisionStay2D(Collision2D collision) => Contact(collision);
        private void Contact(Collision2D collision)
        {
            if (IsDead || player == null || playerHealth == null || playerHealth.IsDead || Time.timeAsDouble < nextContact) return;
            var receiver = collision.collider.GetComponentInParent<DamageReceiver>();
            if (receiver != playerHealth) return;
            nextContact = Time.timeAsDouble + 1d;
            var packet = new DamagePacket(ActorId, 10f);
            receiver.ApplyDamage(in packet);
        }
    }
}
