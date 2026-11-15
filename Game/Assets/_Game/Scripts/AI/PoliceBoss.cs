using System;
using System.Collections.Generic;
using Game.Actors;
using Game.Combat;
using UnityEngine;
namespace Game.AI
{
    [RequireComponent(typeof(Rigidbody2D), typeof(ActorIdentity), typeof(DamageReceiver))]
    public sealed class PoliceBoss : MonoBehaviour, IEnemyTarget
    {
        [SerializeField] private PoliceBossDefinition definition;
        [SerializeField] private ProjectilePool pool;
        [SerializeField] private GameObject minionPrefab;
        [SerializeField] private LayerMask worldMask;
        private Rigidbody2D body;
        private DamageReceiver health, targetHealth;
        private TargetRegistry registry;
        private Transform player, visual;
        private SpriteRenderer warning;
        private Vector2 rollDirection;
        private float timer, shotTimer;
        private int rounds, attackLevel;
        private bool summonedSecondStage, summonedThirdStage;
        public int SecondStageSummons { get; private set; }
        public int ThirdStageSummons { get; private set; }
        private bool hitDuringRoll, deathSent, started;
        private readonly List<ShieldMinion> minions = new List<ShieldMinion>();
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
        private readonly List<(Collider2D a, Collider2D b)> ignored = new List<(Collider2D, Collider2D)>();
        public enum AttackPhase { Waiting, Windup, Roll, Burst, Reload, Spin, Dead }
        public AttackPhase Phase { get; private set; }
        public int Level { get; private set; } = 1;
        public int PhaseTransitions { get; private set; }
        public int ShotsFired { get; private set; }
        public int LivingMinions { get { minions.RemoveAll(m => m == null || m.IsDead); return minions.Count; } }
        public float SpinDegrees { get; private set; }
        public int ActorId => GetComponent<ActorIdentity>().ActorId;
        public bool IsDead => GetComponent<DamageReceiver>().IsDead;
        public event Action Died;
        public void Configure(PoliceBossDefinition value, ProjectilePool projectiles, GameObject summons, LayerMask world)
        {
            if (isActiveAndEnabled || started) throw new InvalidOperationException("Configure before first activation.");
            if (value == null || projectiles == null || summons == null) throw new ArgumentNullException(nameof(value));
            value.Validate(); definition = value; pool = projectiles; minionPrefab = summons; worldMask = world;
        }
        private void Ensure()
        {
            body = GetComponent<Rigidbody2D>(); health = GetComponent<DamageReceiver>(); visual = transform.Find("Police body");
        }
        public void Initialize(TargetRegistry targets, Transform target)
        {
            Ensure(); Detach(); CleanupOwned(); RestoreContacts(); registry = targets; player = target;
            IgnoreContacts();
            targetHealth = player != null ? player.GetComponent<DamageReceiver>() : null;
            BeginWindup();
            if (isActiveAndEnabled) { if (registry != null) registry.Reactivated += Register; Register(); }
        }
        private void OnEnable()
        {
            Ensure(); IgnoreContacts(); definition.Validate(); started = true; health.DamageApplied += OnDamage;
            if (registry != null) registry.Reactivated += Register;
            if (IsDead) { Phase = AttackPhase.Dead; return; }
            Register(); BeginWindup();
        }
        private void Register() { if (registry != null && isActiveAndEnabled && !IsDead) registry.Register(this); }
        private void Detach() { if (registry != null) { registry.Reactivated -= Register; registry.Unregister(this); } }
        private void BeginWindup()
        {
            Phase = IsDead ? AttackPhase.Dead : AttackPhase.Windup; timer = definition.Windup;
            body.linearVelocity = Vector2.zero; hitDuringRoll = false; SpinDegrees = 0;
            rollDirection = player != null ? ((Vector2)player.position - body.position).normalized : Vector2.down;
            if (rollDirection.sqrMagnitude < .001f) rollDirection = Vector2.down;
            ShowWarning(true);
            if (visual != null) { visual.localRotation = Quaternion.identity; visual.GetComponent<SpriteRenderer>().color = Color.yellow; }
        }
        private void FixedUpdate()
        {
            body.linearVelocity = Vector2.zero;
            if (IsDead) return;
            if (player == null || targetHealth == null || !targetHealth.isActiveAndEnabled || targetHealth.IsDead)
            { CleanupOwned(); ShowWarning(false); Phase = AttackPhase.Waiting; return; }
            if (Phase == AttackPhase.Waiting) BeginWindup();
            if (Phase == AttackPhase.Roll) { Roll(); return; }
            if (Phase == AttackPhase.Burst)
            {
                shotTimer -= Time.fixedDeltaTime;
                if (shotTimer <= 0 && FirePair(((Vector2)player.position - body.position).normalized))
                { rounds--; shotTimer = definition.PairInterval; if (rounds == 0) Reload(); }
                return;
            }
            if (Phase == AttackPhase.Spin)
            {
                float delta = Mathf.Min(Time.fixedDeltaTime, timer);
                SpinDegrees = Mathf.Min(1080, SpinDegrees + 1080 * delta / definition.SpinDuration);
                if (visual != null) visual.localRotation = Quaternion.Euler(0, 0, SpinDegrees);
                shotTimer -= delta;
                if (shotTimer <= 0)
                { float angle = SpinDegrees * Mathf.Deg2Rad; if (FirePair(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)))) shotTimer = definition.SpinInterval; }
                timer -= delta;
                if (timer <= 0) Reload();
                return;
            }
            timer -= Time.fixedDeltaTime;
            if (timer > 0) return;
            if (Phase == AttackPhase.Reload) { BeginWindup(); return; }
            ShowWarning(false);
            attackLevel = Level;
            if (attackLevel == 2 && !summonedSecondStage)
            {
                summonedSecondStage = true;
                SecondStageSummons = SpawnMinions(2);
            }
            Phase = AttackPhase.Roll; timer = definition.RollDuration;
            if (visual != null) visual.GetComponent<SpriteRenderer>().color = new Color(.25f, .45f, .7f);
        }
        private void Roll()
        {
            float distance = definition.RollSpeed * Mathf.Min(timer, Time.fixedDeltaTime);
            var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(worldMask);
            body.Cast(rollDirection, filter, hits, distance + .02f);
            bool blocked = false;
            foreach (var hit in hits)
                if (hit.collider != null && hit.collider.GetComponentInParent<ActorIdentity>() == null)
                { distance = Mathf.Min(distance, Mathf.Max(0, hit.distance - .02f)); blocked = true; }
            if (!hitDuringRoll)
            {
                filter.SetLayerMask(~0);
                Physics2D.CircleCast(body.position, 2, rollDirection, filter, hits, distance);
                foreach (var hit in hits)
                    if (hit.collider != null && hit.collider.GetComponentInParent<DamageReceiver>() == targetHealth)
                    {
                        hitDuringRoll = true;
                        var result = targetHealth.ApplyDamage(new DamagePacket(ActorId, definition.RollDamage));
                        if (result.Applied && !result.Killed) player.GetComponent<ActorMotor2D>()?.BeginKnockback(rollDirection, 2);
                        break;
                    }
            }
            body.linearVelocity = rollDirection * (distance / Time.fixedDeltaTime);
            if (visual != null) visual.Rotate(0, 0, 720 * Time.fixedDeltaTime);
            timer -= Time.fixedDeltaTime;
            if (timer > 0 && !blocked) return;
            if (visual != null) visual.localRotation = Quaternion.identity;
            if (attackLevel == 3 && !summonedThirdStage)
            {
                summonedThirdStage = true;
                ThirdStageSummons = SpawnMinions(4);
            }
            if (attackLevel == 3) { Phase = AttackPhase.Spin; timer = definition.SpinDuration; shotTimer = 0; SpinDegrees = 0; }
            else { Phase = AttackPhase.Burst; rounds = definition.MagazinePerGun; shotTimer = 0; }
        }
        private bool FirePair(Vector2 direction)
        {
            if (pool.ActiveCount + 2 > definition.PoolLimit) return false;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            for (int gun = 0; gun < 2; gun++)
            {
                Vector2 muzzle = body.position + perpendicular * (gun == 0 ? -1.2f : 1.2f);
                Vector2 shotDirection = Phase == AttackPhase.Spin ? direction : ((Vector2)player.position - muzzle).normalized;
                var request = new ProjectileSpawnRequest(muzzle, shotDirection,
                    definition.ShotSpeed, definition.ShotLifetime, .12f, ~0, ActorId, CombatFaction.Enemy, new DamagePacket(ActorId, definition.ShotDamage, protectionSecondsOverride: .1f));
                if (!pool.TrySpawn(in request)) return false;
                ShotsFired++;
            }
            return true;
        }
        private void Reload() { Phase = AttackPhase.Reload; timer = definition.Reload; if (visual != null) visual.localRotation = Quaternion.identity; }
        private int SpawnMinions(int count)
        {
            int available = definition.SummonLimit - LivingMinions;
            int spawned = 0;
            for (int candidate = 0; candidate < 24 && spawned < Mathf.Min(count, available); candidate++)
            {
                float angle = (45 + (candidate % 4) * 90 + ((candidate / 4) % 3) * 30) * Mathf.Deg2Rad;
                float radius = candidate < 12 ? 3 : 4.5f;
                Vector2 position = body.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (Physics2D.OverlapCircle(position, .45f, worldMask) != null || Vector2.Distance(position, player.position) < 1) continue;
                if (minions.Exists(minion => minion != null && Vector2.Distance(minion.transform.position, position) < 1)) continue;
                var instance = Instantiate(minionPrefab, position, Quaternion.identity);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
                var minion = instance.GetComponent<ShieldMinion>(); minion.Initialize(registry, player); instance.SetActive(true); minions.Add(minion); spawned++;
            }
            return spawned;
        }
        private void OnDamage(DamageResult result)
        {
            if (result.Killed) { Die(); return; }
            int level = health.Health <= health.MaxHealth / 3 ? 3 : health.Health <= health.MaxHealth * 2 / 3 ? 2 : 1;
            while (Level < level) { Level++; PhaseTransitions++; }
        }
        private void CleanupOwned()
        {
            if (pool != null) { pool.enabled = false; pool.enabled = true; }
            foreach (var minion in minions) if (minion != null) { minion.gameObject.SetActive(false); Destroy(minion.gameObject); }
            minions.Clear();
        }
        private void Die()
        {
            if (deathSent) return;
            deathSent = true; ShowWarning(false); Phase = AttackPhase.Dead; body.linearVelocity = Vector2.zero; body.simulated = false;
            CleanupOwned(); Detach(); foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
            if (visual != null) visual.gameObject.SetActive(false); Died?.Invoke();
        }
        private void ShowWarning(bool show)
        {
            if (show && warning == null && visual != null)
            {
                warning = new GameObject("Roll direction").AddComponent<SpriteRenderer>();
                warning.transform.SetParent(transform, false); warning.sprite = visual.GetComponent<SpriteRenderer>().sprite;
                warning.color = new Color(1, .5f, .1f, .7f); warning.sortingOrder = 3;
            }
            if (warning == null) return;
            warning.enabled = show;
            if (!show) return;
            float length = definition.RollSpeed * definition.RollDuration;
            warning.transform.localScale = new Vector3(length, .12f, 1);
            warning.transform.position = body.position + rollDirection * length * .5f;
            warning.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(rollDirection.y, rollDirection.x) * Mathf.Rad2Deg);
        }
        private void IgnoreContacts()
        {
            RestoreContacts();
            if (player == null) return;
            foreach (var a in GetComponentsInChildren<Collider2D>())
                foreach (var b in player.GetComponentsInChildren<Collider2D>())
                    if (!Physics2D.GetIgnoreCollision(a, b))
                    { Physics2D.IgnoreCollision(a, b, true); ignored.Add((a, b)); }
        }
        private void RestoreContacts() { foreach (var pair in ignored) if (pair.a != null && pair.b != null) Physics2D.IgnoreCollision(pair.a, pair.b, false); ignored.Clear(); }
        private void OnDisable() { ShowWarning(false); RestoreContacts(); Detach(); CleanupOwned(); if (health != null) health.DamageApplied -= OnDamage; if (body != null) body.linearVelocity = Vector2.zero; }
    }
}
