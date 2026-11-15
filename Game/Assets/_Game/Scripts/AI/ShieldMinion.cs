using System;
using Game.Actors;
using Game.Combat;
using UnityEngine;
namespace Game.AI
{
    [RequireComponent(typeof(SandboxEnemy))]
    public sealed class ShieldMinion : MonoBehaviour, IEnemyTarget
    {
        private SandboxEnemy movement;
        private DamageReceiver health;
        private TargetRegistry registry;
        private float rise;
        private bool deathSent;
        public int ActorId => GetComponent<ActorIdentity>().ActorId;
        public bool IsDead => GetComponent<DamageReceiver>().IsDead;
        public event Action Died;
        public void Initialize(TargetRegistry targets, Transform player)
        {
            Detach(); registry = targets;
            movement = GetComponent<SandboxEnemy>(); movement.enabled = false;
            movement.Initialize(null, player); rise = .8f;
            if (isActiveAndEnabled) { if (registry != null) registry.Reactivated += Register; Register(); }
        }
        private void OnEnable()
        {
            health = GetComponent<DamageReceiver>(); movement = GetComponent<SandboxEnemy>();
            movement.enabled = false; rise = .8f; health.DamageApplied += OnDamage;
            if (registry != null) registry.Reactivated += Register;
            if (IsDead) Die(); else Register();
        }
        private void Register() { if (registry != null && isActiveAndEnabled && !IsDead) registry.Register(this); }
        private void Detach() { if (registry != null) { registry.Reactivated -= Register; registry.Unregister(this); } }
        private void Update()
        {
            if (IsDead) return;
            if (rise > 0)
            {
                rise -= Time.deltaTime;
                foreach (var skin in GetComponentsInChildren<SpriteRenderer>())
                { var color = skin.color; color.a = Mathf.Clamp01(1 - rise / .8f); skin.color = color; }
                if (rise <= 0) movement.enabled = true;
            }
        }
        private void OnDamage(DamageResult result) { if (result.Killed) Die(); }
        private void Die()
        {
            if (deathSent) return;
            deathSent = true; Detach(); movement.enabled = false;
            Died?.Invoke(); gameObject.SetActive(false); Destroy(gameObject);
        }
        private void OnDisable()
        {
            Detach(); if (health != null) health.DamageApplied -= OnDamage;
            if (movement != null) movement.enabled = false;
        }
    }
}
