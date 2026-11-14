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
            registry = targets; player = playerTransform; IgnoreTargetContact(); ResetAttack();
            playerHealth = player != null ? player.GetComponent<DamageReceiver>() : null;
            if (isActiveAndEnabled && registry != null) { registry.Reactivated += RegisterAlive; RegisterAlive(); }
        }
        private void OnEnable()
        {
            EnsureReferences();
            playerHealth = player != null ? player.GetComponent<DamageReceiver>() : null;
            IgnoreTargetContact(); ResetAttack();
            health.DamageApplied += OnDamage;
            if (registry != null) registry.Reactivated += RegisterAlive;
            if (IsDead) ObserveDeath();
            else if (registry != null) registry.Register(this);
        }
        private void OnDisable()
        {
            RestoreContact(); ResetAttack();
            if (health != null) health.DamageApplied -= OnDamage;
            if (registry != null) { registry.Reactivated -= RegisterAlive; registry.Unregister(this); }
            if (body != null) body.linearVelocity = Vector2.zero;
        }
        private void RegisterAlive()
        {
            if (isActiveAndEnabled && !IsDead && registry != null) registry.Register(this);
        }
        public enum AttackPhase { Chase, Windup, Swing, Recover }
        public AttackPhase Phase { get; private set; }
        private float phaseTime;
        private Vector2 strikeDirection;
        private SpriteRenderer arm;
        private readonly System.Collections.Generic.List<RaycastHit2D> visionHits = new System.Collections.Generic.List<RaycastHit2D>();
        private readonly System.Collections.Generic.List<(Collider2D a, Collider2D b)> ignoredPairs = new System.Collections.Generic.List<(Collider2D, Collider2D)>();

        private void IgnoreTargetContact()
        {
            RestoreContact();
            if (player == null) return;
            foreach (var own in GetComponentsInChildren<Collider2D>())
                foreach (var target in player.GetComponentsInChildren<Collider2D>())
                    if (!Physics2D.GetIgnoreCollision(own, target))
                    { Physics2D.IgnoreCollision(own, target, true); ignoredPairs.Add((own, target)); }
        }
        private void RestoreContact()
        {
            foreach (var pair in ignoredPairs)
                if (pair.a != null && pair.b != null) Physics2D.IgnoreCollision(pair.a, pair.b, false);
            ignoredPairs.Clear();
        }
        private void ResetAttack()
        {
            Phase = AttackPhase.Chase; phaseTime = 0;
            if (arm != null) arm.enabled = false;
        }
        private bool ClearStrikePath(Vector2 offset)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(~0);
            Physics2D.Raycast(body.position, offset.normalized, filter, visionHits, offset.magnitude);
            foreach (var hit in visionHits)
                if (hit.collider != null && hit.collider.GetComponentInParent<ActorIdentity>() == null
                    && hit.collider.GetComponentInParent<DamageReceiver>() == null) return false;
            return true;
        }
        private void Strike()
        {
            Vector2 offset = (Vector2)player.position - body.position;
            if (offset.magnitude > 1.35f || Vector2.Angle(strikeDirection, offset) > 60 || !ClearStrikePath(offset)) return;
            var packet = new DamagePacket(ActorId, 10f);
            var result = playerHealth.ApplyDamage(in packet);
            if (result.Applied && !result.Killed)
                player.GetComponent<ActorMotor2D>()?.BeginKnockback(strikeDirection);
        }
        private void ShowArm(float angle)
        {
            if (arm == null)
            {
                var skin = GetComponent<SpriteRenderer>();
                if (skin == null || skin.sprite == null) return;
                arm = new GameObject("Melee arm").AddComponent<SpriteRenderer>();
                arm.transform.SetParent(transform, false); arm.sprite = skin.sprite;
                arm.color = new Color(1f, .65f, .15f); arm.sortingOrder = skin.sortingOrder + 1;
            }
            arm.enabled = true;
            Vector2 facing = Quaternion.Euler(0,0,angle) * strikeDirection;
            arm.transform.position = (Vector2)transform.position + facing * .55f;
            arm.transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(facing.y, facing.x)*Mathf.Rad2Deg);
            arm.transform.localScale = new Vector3(.8f,.2f,1);
        }
        private void FixedUpdate()
        {
            body.linearVelocity = Vector2.zero;
            if (IsDead || player == null || playerHealth == null || playerHealth.IsDead) { ResetAttack(); return; }
            Vector2 offset = (Vector2)player.position - body.position;
            if (Phase == AttackPhase.Chase)
            {
                if (offset.magnitude > 1.1f)
                    body.linearVelocity = offset.normalized * Mathf.Min(speed, Mathf.Max(0,offset.magnitude-1.1f)/Time.fixedDeltaTime);
                else if (ClearStrikePath(offset))
                { strikeDirection = offset.sqrMagnitude > .0001f ? offset.normalized : Vector2.down; Phase = AttackPhase.Windup; phaseTime=.4f; ShowArm(-100); }
                return;
            }
            phaseTime -= Time.fixedDeltaTime;
            if (Phase == AttackPhase.Windup)
            {
                ShowArm(Mathf.Lerp(-100,-45,1-Mathf.Clamp01(phaseTime/.4f)));
                if (phaseTime <= 0) { Strike(); Phase=AttackPhase.Swing; phaseTime=.12f; }
            }
            else if (Phase == AttackPhase.Swing)
            {
                ShowArm(Mathf.Lerp(-45,65,1-Mathf.Clamp01(phaseTime/.12f)));
                if (phaseTime <= 0) { Phase=AttackPhase.Recover; phaseTime=.6f; if(arm!=null)arm.enabled=false; }
            }
            else if (phaseTime <= 0) ResetAttack();
        }

        private void OnDamage(DamageResult result) { if (result.Killed) ObserveDeath(); }
        private void ObserveDeath()
        {
            if (deathSent) return;
            deathSent = true; ResetAttack();
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
            var renderer = GetComponent<SpriteRenderer>(); if (renderer != null) renderer.enabled = false;
            if (registry != null) registry.Unregister(this);
            Died?.Invoke();
        }

    }
}
