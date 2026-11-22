using System;
using System.Collections.Generic;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Encounters;
using UnityEngine;

namespace Game.StreetMap
{
    public sealed class RoomController : MonoBehaviour
    {
        private sealed class Binding { public IEnemyTarget Target; public Action Died; }
        private readonly Dictionary<string, Binding> bindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
        private readonly Dictionary<string, MonoBehaviour> primaryActors = new Dictionary<string, MonoBehaviour>(StringComparer.Ordinal);
        private readonly List<Collider2D> occupants = new List<Collider2D>();
        private EncounterRoom room;
        private PlayerCombatFacade hero;
        private Collider2D[] heroColliders;
        private DoorController[] doors;
        private bool clearPublished;
        public RoomEncounterState State { get; private set; }
        public string RunId { get; private set; }
        public string RoomId => room.RoomId;
        public IReadOnlyList<DoorController> Doors => doors;
        public event Action<RoomController> Cleared;
        internal Func<bool> CanBeginEncounter { get; set; }
        public event Action<RoomController, string, string, Vector2> EnemyDefeated;

        public void Configure(string runId, EncounterRoom description, RoomPlan plan, PlayerCombatFacade player, TargetRegistry registry, MonoBehaviour[] actors, DoorController[] gates, Stage4CombatAssets combatAssets = null)
        {
            if (State != null) throw new InvalidOperationException("A room belongs to one run only.");
            if (string.IsNullOrWhiteSpace(runId) || description == null || plan == null || description.RoomId != plan.RoomId || player == null || registry == null || actors == null || gates == null || gates.Length == 0)
                throw new ArgumentException("Room requires run/plan/player/registry/actors/doors.");
            var validation=EncounterPlanValidator.Validate(new EncounterMap("runtime_room",1,new[]{description}),new EncounterPlan(0,1,1,"runtime_room",1,new[]{plan}));
            if (!validation.IsSuccess) throw new ArgumentException(validation.Diagnostic);
            int index = 0;
            foreach (var wave in plan.Waves)
                foreach (var spawn in wave.Spawns)
                {
                    if (index >= actors.Length || !MatchesType(actors[index], spawn.EnemyTypeId))
                        throw new ArgumentException("Missing actor for " + spawn.SpawnId + ": " + spawn.EnemyTypeId);
                    if (primaryActors.ContainsValue(actors[index])) throw new ArgumentException("Each primary SpawnId needs a unique actor.");
                    var actor = actors[index++];
                    primaryActors.Add(spawn.SpawnId, actor);
                    if (actor is SlimeGrenadier grenadier)
                    {
                        if (combatAssets == null) throw new ArgumentException("Grenadier requires summon prefabs.");
                        grenadier.ConfigureSummons(spawn.ReservedSummons,
                            slot => SpawnSlime(grenadier, spawn.SpawnId, slot, combatAssets, registry),
                            () => CancelUnmaterializedSummons(spawn.SpawnId));
                    }
                }
            if (index != actors.Length) throw new ArgumentException("Actor count does not match plan.");
            room = description; hero = player; doors = (DoorController[])gates.Clone(); RunId = runId;
            heroColliders = hero.GetComponentsInChildren<Collider2D>();
            foreach (var actor in actors)
            {
                actor.gameObject.SetActive(false);
                switch (actor)
                {
                    case SandboxEnemy melee: melee.Initialize(registry, hero.transform); break;
                    case JumpingEnemy jumper: jumper.Initialize(registry, hero.transform); break;
                    case ShootingEnemy shooter: shooter.Initialize(registry, hero.transform); break;
                    case EliteEnemy elite: elite.Initialize(registry, hero); break;
                }
            }
            State = new RoomEncounterState(plan);
        }
        private static bool MatchesType(MonoBehaviour actor, string type) => actor != null &&
            (type == EnemyTypes.Melee && actor is SandboxEnemy ||
             type == EnemyTypes.Jumper && actor is JumpingEnemy ||
             type == EnemyTypes.Shooter && actor is ShootingEnemy ||
             actor is EliteEnemy elite && elite.EnemyTypeId == type);
        private bool SpawnSlime(SlimeGrenadier owner, string ownerId, int slot, Stage4CombatAssets assets, TargetRegistry registry)
        {
            if (State == null || !State.IsActive || slot < 0 || slot >= 4) return false;
            Vector2 origin = owner.GetComponent<Rigidbody2D>().position;
            Physics2D.SyncTransforms();
            int obstructed = 0;
            foreach (Vector2 point in StreetMapCatalog.SummonCandidates(origin, slot))
            {
                if (Mathf.Abs(point.x - room.CenterX) > room.Width / 2f - .5f ||
                    Mathf.Abs(point.y - room.CenterY) > room.Height / 2f - .5f || !SpawnIsFree(point, .32f)) { obstructed++; continue; }
                Vector2 delta = point - origin;
                if (Physics2D.CircleCast(origin, .28f, delta.normalized, delta.magnitude, LayerMask.GetMask("World"))) { obstructed++; continue; }
                var child = assets.CreateSlime(transform);
                child.name = ownerId + "/summon/" + slot;
                child.Initialize(registry, hero);
                PlaceInactiveActor(child, point);
                child.gameObject.SetActive(true);
                if (TryMaterializeSummon(ownerId, slot, child)) return true;
                Debug.Log($"Summon {ownerId}/{slot} rejected at {point}; phase={State.Phase}, targetActive={child.isActiveAndEnabled}, targetDead={child.IsDead}");
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
                return false;
            }
            Debug.Log($"Summon {ownerId}/{slot} lacked safe placement at {origin}; room={room.RoomId}; blocked={obstructed}");
            return false;
        }
        private void FixedUpdate()
        {
            if (State == null || hero == null) return;
            if (hero.IsDead) { Cancel(); return; }
            if (State.Phase == RoomPhase.Dormant)
            {
                if (CanBeginEncounter?.Invoke() == false) return;
                if (!HeroFullyInside()) return;
                foreach (var door in doors) if (!door.CanClose()) return;
                foreach (var door in doors)
                    if (!door.SetClosed(true)) { foreach (var opened in doors) opened.SetClosed(false); return; }
                State.Begin();
                PublishClear();
                // Do not charge the entry tick to the first wave's delay.
                SpawnDueWave();
                return;
            }
            State.Tick(Time.fixedDeltaTime);
            SpawnDueWave();
        }
        private bool HeroFullyInside()
        {
            bool found = false;
            foreach (var collider in heroColliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                found = true; var b = collider.bounds;
                if (b.min.x <= room.CenterX-room.Width/2+.1f || b.max.x >= room.CenterX+room.Width/2-.1f ||
                    b.min.y <= room.CenterY-room.Height/2+.1f || b.max.y >= room.CenterY+room.Height/2-.1f) return false;
            }
            return found;
        }
        private void SpawnDueWave()
        {
            if (!State.IsWaveDue) return;
            var wave = State.Plan.Waves[State.WaveIndex];
            foreach (var spawn in wave.Spawns)
            {
                if (!State.CanMaterialize(spawn.SpawnId)) continue;
                var actor = primaryActors[spawn.SpawnId];
                if (actor == null || ((IEnemyTarget)actor).IsDead) continue;
                var point = EncounterPlanValidator.FindPoint(room,spawn.PointId);
                var position = new Vector2(room.CenterX+point.X,room.CenterY+point.Y);
                if (!SpawnIsFree(position,EnemyTypes.Radius(spawn.EnemyTypeId))) continue;
                PlaceInactiveActor(actor, position);
                if (Attach(spawn.SpawnId,(IEnemyTarget)actor)) actor.gameObject.SetActive(true);
            }
        }
        private static void PlaceInactiveActor(Component actor, Vector2 point)
        {
            // Unity can initialize a disabled prefab's body from the Transform on first activation.
            actor.transform.position = point;
            actor.GetComponent<Rigidbody2D>().position = point;
        }
        private bool SpawnIsFree(Vector2 position,float radius)
        {
            Physics2D.SyncTransforms();
            var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(~0);
            Physics2D.OverlapCircle(position,radius,filter,occupants);
            foreach (var collider in occupants)
                if (collider != null && (collider.gameObject.layer == LayerMask.NameToLayer("World") || collider.GetComponentInParent<ActorIdentity>() != null)) return false;
            return true;
        }
        private bool Attach(string id,IEnemyTarget target)
        {
            if (!State.CanMaterialize(id)) return false;
            foreach (var binding in bindings.Values) if (ReferenceEquals(binding.Target,target)) return false;
            Action died = () => ObserveDeath(id);
            if (!State.MarkMaterialized(id)) return false;
            bindings.Add(id,new Binding {Target=target,Died=died}); target.Died += died;
            return true;
        }
        private void ObserveDeath(string id)
        {
            if (!bindings.TryGetValue(id,out var binding) || !(binding.Target is MonoBehaviour component) || component == null || !binding.Target.IsDead) return;
            if (!State.ConfirmDeath(id)) return;
            binding.Target.Died -= binding.Died;
            if (EnemyDefeated != null)
            {
                string type = component switch
                {
                    EliteEnemy elite => elite.EnemyTypeId,
                    JumpingEnemy _ => EnemyTypes.Jumper,
                    ShootingEnemy _ => EnemyTypes.Shooter,
                    _ => EnemyTypes.Melee
                };
                EnemyDefeated(this, id, type, component.transform.position);
            }
            PublishClear();
        }
        public bool TryMaterializeSummon(string ownerSpawnId,int index,IEnemyTarget target)
        {
            if (State == null || target == null || !(target is MonoBehaviour component) || component == null || !component.isActiveAndEnabled || target.IsDead || index < 0) return false;
            string id=ownerSpawnId+"/summon/"+index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Attach(id,target);
        }
        public int CancelUnmaterializedSummons(string ownerSpawnId)
        {
            if (State == null) return 0;
            int count=State.CancelUnmaterializedSummons(ownerSpawnId); PublishClear(); return count;
        }
        private void PublishClear()
        {
            if (clearPublished || State.Phase != RoomPhase.Cleared) return;
            clearPublished=true;
            foreach (var door in doors) door.SetClosed(false);
            Cleared?.Invoke(this);
        }
        public void Cancel()
        {
            if (State == null) return;
            State.Cancel();
            foreach (var binding in bindings.Values)
            {
                if (!(binding.Target is MonoBehaviour component) || component == null) continue;
                binding.Target.Died -= binding.Died;
                component.gameObject.SetActive(false);
            }
        }
        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();
    }
}
