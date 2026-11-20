using System;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Encounters;
using System.Collections.Generic;
using Game.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.StreetMap
{
    public sealed class StreetMapScreen : FlowScreen
    {
        [SerializeField] private PlayerCombatFacade player;
        [SerializeField] private SandboxEnemy[] enemies;
        [SerializeField] private TargetRegistry registry;
        [SerializeField] private HudPresenter hud;
        private RunCoordinator run;
        private BattleControls controls;
        private Collider2D heroCollider;
        private readonly bool[] visited = new bool[5];
        private readonly HashSet<string> cleared = new HashSet<string>(StringComparer.Ordinal);
        public RoomController[] Rooms { get; private set; }
        public int ClearedCount => cleared.Count;
        public FixedMapPortal Portal {get;private set;}
        public StreetMiniMap MiniMap {get;private set;}
        private int lastStatus=-1;
        public event Action<string,string> RoomCleared;
        public PlayerCombatFacade Player => player;
        public bool IsPaused { get; private set; }
        public string CurrentRoomId { get; private set; } = "entry";
        public bool HasVisited(int index) => visited[index];
        private Stage4CombatAssets combatAssets;
        private readonly List<RoomPickup> pickups = new List<RoomPickup>();
        public bool IsCombatReady => combatAssets != null;
        private Stage5PreviewController preview;
        internal Vector2 MiniMapPosition => preview != null ? preview.MapPosition : (Vector2)player.transform.position;
        internal void ReleaseHeldControls() => controls.ClearHeld();
        protected override bool ShowView => false;
        protected override string Title => "";
        protected override string Description => "";
        protected override string ButtonLabel => "";
        protected override bool CanAct(RunCoordinator value) => false;
        protected override void Act(RunCoordinator value) { }
        protected override string Message(RunCoordinator value) => "";

        public void Configure(PlayerCombatFacade hero, SandboxEnemy[] targets, TargetRegistry targetsRegistry, HudPresenter view)
        { player = hero; enemies = targets; registry = targetsRegistry; hud = view; }

        protected override void OnBound(RunCoordinator value)
        {
            DetachCombat();
            if (!value.CurrentRequest.IsFixedStreet1_1 || player == null || registry == null || hud == null || enemies == null || enemies.Length != 6)
                throw new InvalidOperationException("FixedStreet1_1 requires its hero, registry, HUD and six diagnostic melee enemies.");
            run = value;
            cleared.Clear();
            Array.Clear(visited, 0, visited.Length);
            visited[0] = true;
            CurrentRoomId = "entry";
            heroCollider = player.GetComponent<Collider2D>();
            if (player.GetComponent<PlayerDodgeController>() == null)
                player.gameObject.AddComponent<PlayerDodgeController>();
            player.SetControlsEnabled(false);
            player.GetComponent<AutoAimSelector>().Configure(registry);
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] == null) throw new InvalidOperationException("Missing diagnostic enemy.");
                enemies[i].gameObject.SetActive(false);
            }
            CreateRooms(value.RunId);
            MiniMap=gameObject.AddComponent<StreetMiniMap>();MiniMap.Initialize(this);
            var portal=new GameObject("FinalPortal");portal.transform.SetParent(transform,false);
            Portal=portal.AddComponent<FixedMapPortal>();Portal.Initialize(player.GetComponent<SpriteRenderer>().sprite);
            controls = GetComponent<BattleControls>() ?? gameObject.AddComponent<BattleControls>();
            controls.Initialize(TogglePause, Resume, Leave, enableDodge: true, enableNpc: value.CurrentRequest.IsStage5Preview);
            if (value.CurrentRequest.IsStage5Preview)
            {
                preview = gameObject.AddComponent<Stage5PreviewController>();
                preview.Initialize(this, value, controls);
            }
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
            player.Died += OnPlayerDied;
            player.PauseRequested += TogglePause;
            player.InteractRequested += TryExit;
            run.Changed += OnPhaseChanged;
            hud.Bind(player);
            string seed = value.CurrentRequest.FixedEncounter == null ? "Тестовый маршрут" : "Seed " + value.CurrentRequest.Seed;
            hud.SetHint("1-1 · " + seed + " · WASD / Space / Q · Shift: кувырок · E: подбор / портал · Esc: пауза");
            UpdateRoomLabel();
            OnPhaseChanged();
        }

        private void Update()
        {
            if (run == null || run.Phase != RunPhase.InArena || IsPaused || player.IsDead || heroCollider == null) return;
            for (int index = 0; index < StreetMapLayout.Rooms.Count; index++)
            {
                if (!StreetMapLayout.ContainsHero(StreetMapLayout.Rooms[index], heroCollider.bounds)) continue;
                bool roomChanged = CurrentRoomId != StreetMapLayout.Rooms[index].Id;
                CurrentRoomId = StreetMapLayout.Rooms[index].Id;
                if (!visited[index])
                {
                    visited[index] = true;
                }
                if (roomChanged) UpdateRoomLabel();
                int status=ClearedCount*10000;
                foreach(var room in Rooms)status+=room.State.PendingCount*100+room.State.AliveCount*10+(int)room.State.Phase;
                if(lastStatus!=status){lastStatus=status;UpdateRoomLabel();}
                return;
            }
        }
        private void CreateRooms(string runId)
        {
            if (Rooms != null) throw new InvalidOperationException("Load a fresh map for a new run.");
            bool seeded = run.CurrentRequest.FixedEncounter != null;
            var map = seeded ? StreetMapCatalog.Create() : EncounterExample.CreateMap();
            var plan = run.CurrentRequest.FixedEncounter ?? EncounterExample.CreateFixedPlan(run.CurrentRequest.Seed);
            var validation = seeded ? EncounterPlanValidator.ValidateStage(map, plan) : EncounterPlanValidator.Validate(map,plan);
            if (!validation.IsSuccess) throw new InvalidOperationException(validation.Diagnostic);
            var prefabs = seeded ? Resources.Load<FixedEncounterPrefabs>(FixedEncounterPrefabs.ResourceName) : null;
            if (seeded && (prefabs == null || !prefabs.IsComplete)) throw new InvalidOperationException("Missing encounter prefabs.");
            combatAssets = seeded ? Resources.Load<Stage4CombatAssets>(Stage4CombatAssets.ResourceName) : null;
            if (seeded && (combatAssets == null || !combatAssets.IsComplete)) throw new InvalidOperationException("Missing elite and reward prefabs.");
            if (seeded) HideDiagnosticMarkers();
            var actorRoot = new GameObject("EncounterActors");
            actorRoot.SetActive(false);
            actorRoot.transform.SetParent(transform, false);
            var positions = new[]
            {
                new[] {new Vector2(13,0),new Vector2(23,8)},
                new[] {new Vector2(23,16),new Vector2(33,24)},
                new[] {new Vector2(41,24),new Vector2(61,24)}
            };
            Rooms = new RoomController[4];
            var sprite = player.GetComponent<SpriteRenderer>().sprite;
            for (int index=0;index<3;index++)
            {
                var description=map.Rooms[index+1]; var geometry=StreetMapLayout.Rooms[index+1];
                if (geometry.Id != description.RoomId || geometry.Center != new Vector2(description.CenterX,description.CenterY) || geometry.Bounds.width != description.Width || geometry.Bounds.height != description.Height)
                    throw new InvalidOperationException("Map/contract geometry mismatch.");
                var gates=new DoorController[2];
                for (int side=0;side<2;side++)
                {
                    var gate=new GameObject(description.RoomId+"_Door_"+side).AddComponent<DoorController>();
                    gate.transform.SetParent(transform,false);
                    bool horizontal = index==0 ? side==1 : index==1 && side==0;
                    gate.Configure(sprite,positions[index][side],horizontal ? new Vector2(4,.5f) : new Vector2(.5f,4));
                    gates[side]=gate;
                }
                var room=new GameObject(description.RoomId+"_Encounter").AddComponent<RoomController>();
                room.transform.SetParent(transform,false);
                var roomPlan = FindRoom(plan, description.RoomId);
                MonoBehaviour[] actors = seeded ? CreateActors(roomPlan, prefabs, actorRoot.transform) :
                    new MonoBehaviour[] { enemies[index*2], enemies[index*2+1] };
                room.Configure(runId,description,roomPlan,player,registry,actors,gates);
                room.Cleared+=OnRoomCleared; Rooms[index]=room;
            }
            var elite=StreetMapLayout.Rooms[4];
            var diagnosticPoints=new[]{new SpawnPoint("diagnostic_0",-7,4,.6f,new[]{EnemyTypes.Melee}),new SpawnPoint("diagnostic_1",7,4,.6f,new[]{EnemyTypes.Melee})};
            var description4=new EncounterRoom("elite",elite.Center.x,elite.Center.y,28,24,new RoomBudget(2,2,2,1),diagnosticPoints);
            var plan4=new RoomPlan("elite",new[]{new WavePlan("diagnostic",0,new[]{new SpawnPlan("elite/diagnostic/0",EnemyTypes.Melee,"diagnostic_0"),new SpawnPlan("elite/diagnostic/1",EnemyTypes.Melee,"diagnostic_1")})});
            MonoBehaviour[] actors4;
            if (seeded)
            {
                description4 = map.Rooms[4];
                plan4 = FindRoom(plan, "elite");
                var actors = new List<MonoBehaviour>();
                foreach (var wave in plan4.Waves)
                foreach (var spawn in wave.Spawns)
                {
                    var actor = combatAssets.CreateElite(spawn.EnemyTypeId, actorRoot.transform);
                    actor.name = spawn.SpawnId;
                    actors.Add(actor);
                }
                actors4 = actors.ToArray();
            }
            else
            {
                actors4 = new MonoBehaviour[2];
                for (int i = 0; i < actors4.Length; i++) actors4[i] = Instantiate(enemies[0], actorRoot.transform);
            }
            var finalGate=new GameObject("EliteDoor").AddComponent<DoorController>();finalGate.transform.SetParent(transform,false);finalGate.Configure(sprite,new Vector2(69,24),new Vector2(.5f,4));
            var finalRoom=new GameObject(seeded ? "EliteEncounter" : "EliteDiagnostic").AddComponent<RoomController>();finalRoom.transform.SetParent(transform,false);
            finalRoom.Configure(runId,description4,plan4,player,registry,actors4,new[]{finalGate},combatAssets);finalRoom.Cleared+=OnRoomCleared;Rooms[3]=finalRoom;
            actorRoot.SetActive(true);
        }
        private void HideDiagnosticMarkers()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (!root.name.StartsWith("FixedStreetGeometry", StringComparison.Ordinal)) continue;
                foreach (var marker in root.GetComponentsInChildren<Transform>(true))
                {
                    if (marker.name == "Placeholder_NOT_AN_ENEMY" || marker.name == "EliteWarning_label")
                        marker.gameObject.SetActive(false);
                    else if (marker.name == "elite_label" && marker.TryGetComponent<TextMesh>(out var label))
                        label.text = "ELITE";
                }
            }
        }
        private static RoomPlan FindRoom(EncounterPlan plan, string id)
        {
            foreach (var room in plan.Rooms) if (room.RoomId == id) return room;
            throw new InvalidOperationException("Missing room plan: " + id);
        }
        private static MonoBehaviour[] CreateActors(RoomPlan room, FixedEncounterPrefabs prefabs, Transform parent)
        {
            var actors = new List<MonoBehaviour>();
            foreach (var wave in room.Waves)
                foreach (var spawn in wave.Spawns)
                {
                    var actor = prefabs.InstantiateActor(spawn.EnemyTypeId, parent);
                    actor.gameObject.SetActive(false);
                    actor.name = spawn.SpawnId;
                    actors.Add(actor);
                }
            return actors.ToArray();
        }
        private void OnRoomCleared(RoomController room)
        {
            if (run == null || room.RunId != run.RunId || !cleared.Add(room.RoomId)) return;
            preview?.CompleteRoom(room.RoomId);
            UpdateRoomLabel();
            if (combatAssets != null)
            {
                int index = Array.IndexOf(Rooms, room);
                Vector2 center = StreetMapLayout.Rooms[index + 1].Center;
                if (index < 3) Drop(room, (PickupKind)index, center);
                else
                {
                    Drop(room, PickupKind.Ammo, center + Vector2.left * 2f);
                    Drop(room, PickupKind.Health, center);
                    Drop(room, PickupKind.Armor, center + Vector2.right * 2f);
                }
            }
            RoomCleared?.Invoke(room.RunId,room.RoomId);
            if(ClearedCount==4)Portal.TryOpen(player);
        }
        private void Drop(RoomController room, PickupKind kind, Vector2 position)
        {
            if (preview != null && kind == PickupKind.Ammo) return;
            var pickup = combatAssets.CreatePickup(kind, position, transform);
            pickup.Initialize(room.RunId, room.RoomId);
            pickups.Add(pickup);
        }
        private bool TryPickup()
        {
            var position = (Vector2)player.transform.position;
            RoomPickup nearest = null;
            float distance = 1.5f;
            var snapshot = player.ReadSnapshot();
            foreach (var pickup in pickups)
            {
                if (pickup == null || pickup.IsConsumed) continue;
                if (pickup.Kind == PickupKind.Ammo && snapshot.ReserveAmmo >= WeaponRuntimeState.PickupReserveLimit ||
                    pickup.Kind == PickupKind.Health && snapshot.Health >= snapshot.MaxHealth ||
                    pickup.Kind == PickupKind.Armor && snapshot.Armor >= snapshot.MaxArmor) continue;
                Vector2 offset = (Vector2)pickup.transform.position - position;
                float range = offset.magnitude;
                if (range > distance || Physics2D.Raycast(position, offset.normalized, range, LayerMask.GetMask("World"))) continue;
                nearest = pickup;
                distance = range;
            }
            return nearest != null && nearest.TryCollect(player) > 0;
        }
        private void UpdateRoomLabel()
        {
            string text="1-1 · "+CurrentRoomId+" · пройдено "+ClearedCount+"/4";
            if (CurrentRoomId == "elite" && combatAssets == null) text += "\nПроверочный бой";
            else if (combatAssets != null && cleared.Contains(CurrentRoomId)) text += "\nE рядом с предметом: подобрать";
            if (Rooms!=null) foreach (var room in Rooms)
                if (room.RoomId==CurrentRoomId)
                    text+="\n"+(room.State.Phase==RoomPhase.Cleared ? "Проходы открыты" : "Живых: "+room.State.AliveCount+" · ожидается: "+room.State.PendingCount);
            if(ClearedCount==4)text+="\n"+(Portal!=null && Portal.IsOpen?"Портал открыт · подойдите и нажмите E":"Нажмите E для размещения портала");
            hud.SetEncounter(text);
        }
        public void TryExit()
        {
            if(run==null || run.Phase!=RunPhase.InArena || IsPaused || player.IsDead)return;
            if ((preview != null && preview.TryUseInteract()) || TryPickup() || ClearedCount != 4) return;
            if(!Portal.TryOpen(player)){UpdateRoomLabel();return;}
            UpdateRoomLabel();
            if(Vector2.Distance(player.transform.position,Portal.transform.position)>1.5f)return;
            run.FinishRun(RunOutcome.Victory);
        }
        private void CancelRooms()
        { if (Rooms!=null) foreach (var room in Rooms) if (room!=null) room.Cancel(); }
        private void OnPlayerDied() { CancelRooms(); run?.FinishRun(RunOutcome.Defeat); }
        public void TogglePause() { if (IsPaused) Resume(); else Pause(); }
        public void Pause()
        {
            if (run == null || run.Phase != RunPhase.InArena || player.IsDead) return;
            IsPaused = true; controls.SetPaused(true); Time.timeScale = 0;
        }
        public void Resume()
        {
            if (!IsPaused || run == null || run.Phase != RunPhase.InArena || player.IsDead) return;
            controls.SetPaused(false); IsPaused = false; Time.timeScale = 1;
        }
        public void Leave()
        {
            if (run == null || run.Phase != RunPhase.InArena) return;
            controls.ClearHeld(); player.SetControlsEnabled(false); IsPaused = false; Time.timeScale = 1;
            CancelRooms();
            run.FinishRun(RunOutcome.Abandoned);
        }
        private void OnApplicationFocus(bool focused) { if (!focused) Pause(); }
        private void OnApplicationPause(bool paused) { if (paused) Pause(); }
        private void OnPhaseChanged()
        {
            bool active = run != null && run.Phase == RunPhase.InArena && !player.IsDead;
            player.SetControlsEnabled(active);
            if (active) return;
            if (run.Phase == RunPhase.ReturningHome && run.LastOutcome.HasValue) preview?.Finish();
            if (run.Phase != RunPhase.LoadingArena) CancelRooms();
            controls?.ClearHeld(); IsPaused = false; Time.timeScale = 1;
            foreach (var enemy in enemies) if (enemy != null) enemy.gameObject.SetActive(false);
            player.GetComponent<ProjectilePool>()?.ClearActive();
        }
        private void DetachCombat()
        {
            controls?.ClearHeld(); IsPaused = false; Time.timeScale = 1;
            if (run != null) run.Changed -= OnPhaseChanged;
            if (Rooms!=null) foreach (var room in Rooms) if (room!=null) { room.Cleared-=OnRoomCleared; room.Cancel(); }
            if (player != null)
            {
                player.Died -= OnPlayerDied;
                player.PauseRequested -= TogglePause;
                player.InteractRequested -= TryExit;
                player.SetControlsEnabled(false);
                player.GetComponent<ProjectilePool>()?.ClearActive();
            }
            if (enemies != null) foreach (var enemy in enemies) if (enemy != null) enemy.gameObject.SetActive(false);
            if (hud != null) hud.Unbind();
            run = null;
        }
        protected override void OnDisable() { DetachCombat(); base.OnDisable(); }
        protected override void OnDestroy() { DetachCombat(); base.OnDestroy(); }
    }
}
