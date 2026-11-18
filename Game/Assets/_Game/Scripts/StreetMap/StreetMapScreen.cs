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
        public bool IsCombatReady => false; // S4-02 geometry, not the completed Stage 4 slice.
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
            controls.Initialize(TogglePause, Resume, Leave);
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
            player.Died += OnPlayerDied;
            player.PauseRequested += TogglePause;
            player.InteractRequested += TryExit;
            run.Changed += OnPhaseChanged;
            hud.Bind(player);
            string seed = value.CurrentRequest.FixedEncounter == null ? "Тестовый маршрут" : "Seed " + value.CurrentRequest.Seed;
            hud.SetHint("1-1 · " + seed + " · WASD / Space / Q · E: портал · Esc: пауза\nВ финальной комнате проверочный бой.");
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
            if (seeded)
            {
                var planned = FindRoom(plan, "elite").Waves[0];
                var points = new List<SpawnPoint>();
                var spawns = new List<SpawnPlan>();
                foreach (var spawn in planned.Spawns)
                {
                    var point = EncounterPlanValidator.FindPoint(map.Rooms[4], spawn.PointId);
                    points.Add(new SpawnPoint(point.PointId, point.X, point.Y, point.ClearanceRadius, new[] { EnemyTypes.Melee }));
                    spawns.Add(new SpawnPlan(spawn.SpawnId, EnemyTypes.Melee, spawn.PointId));
                }
                description4 = new EncounterRoom("elite", elite.Center.x, elite.Center.y, 28, 24,
                    new RoomBudget(3, 3, 3, 1), points);
                plan4 = new RoomPlan("elite", new[] { new WavePlan(planned.WaveId, planned.DelaySeconds, spawns) });
            }
            MonoBehaviour[] actors4;
            if (seeded) actors4 = CreateActors(plan4, prefabs, actorRoot.transform);
            else
            {
                actors4 = new MonoBehaviour[2];
                for (int i = 0; i < actors4.Length; i++) actors4[i] = Instantiate(enemies[0], actorRoot.transform);
            }
            var finalGate=new GameObject("EliteDoor").AddComponent<DoorController>();finalGate.transform.SetParent(transform,false);finalGate.Configure(sprite,new Vector2(69,24),new Vector2(.5f,4));
            var finalRoom=new GameObject("EliteDiagnostic").AddComponent<RoomController>();finalRoom.transform.SetParent(transform,false);
            finalRoom.Configure(runId,description4,plan4,player,registry,actors4,new[]{finalGate});finalRoom.Cleared+=OnRoomCleared;Rooms[3]=finalRoom;
            actorRoot.SetActive(true);
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
            UpdateRoomLabel();
            RoomCleared?.Invoke(room.RunId,room.RoomId); // Future rewards subscribe here, no reward system yet.
            if(ClearedCount==4)Portal.TryOpen(player);
        }
        private void UpdateRoomLabel()
        {
            string text="1-1 · "+CurrentRoomId+" · пройдено "+ClearedCount+"/4";
            if(CurrentRoomId=="elite")text+="\nПроверочный бой, НЕ мини-боссы";
            if (Rooms!=null) foreach (var room in Rooms)
                if (room.RoomId==CurrentRoomId)
                    text+="\n"+(room.State.Phase==RoomPhase.Cleared ? "Проходы открыты" : "Живых: "+room.State.AliveCount+" · ожидается: "+room.State.PendingCount);
            if(ClearedCount==4)text+="\n"+(Portal!=null && Portal.IsOpen?"Портал открыт · подойдите и нажмите E":"Нажмите E для размещения портала");
            hud.SetEncounter(text);
        }
        public void TryExit()
        {
            if(run==null || run.Phase!=RunPhase.InArena || IsPaused || player.IsDead || ClearedCount!=4)return;
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
