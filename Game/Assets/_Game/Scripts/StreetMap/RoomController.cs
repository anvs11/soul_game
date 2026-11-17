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
        private readonly Dictionary<string, SandboxEnemy> primaryActors = new Dictionary<string, SandboxEnemy>(StringComparer.Ordinal);
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

        public void Configure(string runId, EncounterRoom description, RoomPlan plan, PlayerCombatFacade player, TargetRegistry registry, SandboxEnemy[] actors, DoorController[] gates)
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
                    if (spawn.EnemyTypeId != EnemyTypes.Melee || EncounterPlanValidator.FindPoint(description,spawn.PointId) == null || index >= actors.Length || actors[index] == null)
                        throw new ArgumentException("S4-04A runtime supports fixed ordinary melee only.");
                    if (primaryActors.ContainsValue(actors[index])) throw new ArgumentException("Each primary SpawnId needs a unique actor.");
                    primaryActors.Add(spawn.SpawnId,actors[index++]);
                }
            if (index != actors.Length) throw new ArgumentException("Actor count does not match plan.");
            room = description; hero = player; doors = (DoorController[])gates.Clone(); RunId = runId;
            heroColliders = hero.GetComponentsInChildren<Collider2D>();
            foreach (var actor in actors) { actor.gameObject.SetActive(false); actor.Initialize(registry, hero.transform); }
            State = new RoomEncounterState(plan);
        }
        private void FixedUpdate()
        {
            if (State == null || hero == null) return;
            if (hero.IsDead) { Cancel(); return; }
            if (State.Phase == RoomPhase.Dormant)
            {
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
                if (actor == null || actor.IsDead) continue; // Missing/disabled is never a kill.
                var point = EncounterPlanValidator.FindPoint(room,spawn.PointId);
                var position = new Vector2(room.CenterX+point.X,room.CenterY+point.Y);
                if (!SpawnIsFree(position,EnemyTypes.Radius(spawn.EnemyTypeId))) continue;
                actor.GetComponent<Rigidbody2D>().position = position;
                Attach(spawn.SpawnId,actor);
                actor.gameObject.SetActive(true);
            }
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
            if (State.ConfirmDeath(id)) { binding.Target.Died -= binding.Died; PublishClear(); }
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
