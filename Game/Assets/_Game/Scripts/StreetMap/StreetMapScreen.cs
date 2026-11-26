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
        internal IReadOnlyList<StreetMapLayout.Room> MapRooms => run.CurrentRequest.IsRanchPreview
            ? RanchLayout.Rooms : StreetMapLayout.Rooms;
        internal IReadOnlyList<RectInt> MapCorridors => run.CurrentRequest.IsRanchPreview
            ? RanchLayout.Corridors : StreetMapLayout.Corridors;
        internal Rect MiniMapBounds => run.CurrentRequest.IsRanchPreview ? RanchLayout.CameraBounds : StreetMapLayout.CameraBounds;
        public int ClearedCount => cleared.Count;
        public FixedMapPortal Portal {get;private set;}
        public StreetMiniMap MiniMap {get;private set;}
        internal CityRoomContours RoomContours { get; private set; }
        internal Rect WorldCameraBounds => run?.FullCityRoute != null && run.CitySegmentIndex >= 5 && !run.IsFinalCitySegment
            ? new Rect(-13, -14, 116, 56)
            : StreetMapLayout.CameraBounds;
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
        private SewerEntryScreen sewerEntry;
        private SewerStage sewerStage;
        private RanchStage ranchStage;
        private WeaponController sewerWeapon;
        private UnityEngine.UI.Button sewerRetry;
        private static Vector2 SewerHatch => StreetMapLayout.Rooms[4].Center + new Vector2(-5, -3);
        private int boundCitySegment;
        private bool exitDressed;
        internal EncounterPlan ActiveEncounter => run.FullCityRoute?.PlanAt(run.CitySegmentIndex) ?? run.CurrentRequest.FixedEncounter;
        internal CityFeedback Feedback { get; private set; }
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
            boundCitySegment = value.CitySegmentIndex;
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
            if (value.FullCityRoute != null)
            {
                RoomContours = new CityRoomContours(value.CitySegmentIndex, value.CurrentRequest.Seed);
                RoomContours.AddBoundaries(transform);
            }
            if (value.CurrentRequest.IsRanchPreview) CreateRanch(value.RunId);
            else CreateRooms(value.RunId);
            MiniMap=gameObject.AddComponent<StreetMiniMap>();MiniMap.Initialize(this);
            var portal=new GameObject("FinalPortal");portal.transform.SetParent(transform,false);
            Portal=portal.AddComponent<FixedMapPortal>();Portal.Initialize(player.GetComponent<SpriteRenderer>().sprite);
            if (value.CanEnterSewer) Portal.ReservedPosition = SewerHatch;
            controls = GetComponent<BattleControls>() ?? gameObject.AddComponent<BattleControls>();
            bool stage5 = !value.IsInSewer && (value.CurrentRequest.IsStage5Preview || value.CurrentRequest.IsStage5City);
            controls.Initialize(TogglePause, Resume, Leave, enableDodge: true, enableNpc: stage5);
            if (value.IsInSewer) BuildSewer();
            if (stage5)
            {
                preview = gameObject.AddComponent<Stage5PreviewController>();
                preview.Initialize(this, value, controls);
                if (value.CurrentRequest.IsStage5City)
                {
                    var art = new GameObject("City facades and street surface").AddComponent<CityStreetArt>();
                    art.transform.SetParent(transform, false);
                    if (value.CurrentRequest.IsFullCity && value.CitySegmentIndex >= 5)
                    {
                        art.Build(RoomContours, CityRegionRoute.DistrictAt(value.CitySegmentIndex), value.IsFinalCitySegment);
                    }
                    else art.Build(RoomContours);
                    if (value.FullCityRoute != null)
                        CityDistrictDressing.Build(preview, value.FullCityRoute.MapAt(value.CitySegmentIndex), value.CitySegmentIndex);
                    Feedback = gameObject.AddComponent<CityFeedback>();
                    Feedback.Bind(player);
                    foreach (var room in Rooms) room.CanBeginEncounter = () => !preview.HasPendingCheckpoint;
                    if (!value.CityRuntime.HasSeenTutorial)
                        gameObject.AddComponent<CityTutorial>().Initialize(this, value.CityRuntime, controls.SafeRoot);
                }
            }
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
            player.Died += OnPlayerDied;
            player.PauseRequested += TogglePause;
            player.InteractRequested += TryExit;
            run.Changed += OnPhaseChanged;
            hud.Bind(player);
            string seed = value.CurrentRequest.FixedEncounter == null ? "Тестовый маршрут" : "Seed " + value.CurrentRequest.Seed;
            hud.SetHint(value.CurrentRequest.IsRanchPreview ? "Ранчо · 5-1 · " + seed + " · E: подбор / выход" :
                value.IsInSewer ? value.CitySegmentId + " · Shift: кувырок · E: поиск / выход" : value.CurrentRequest.IsStage5City
                ? run.CitySegmentId + " · " + seed + " · Shift: кувырок · Esc: пауза"
                : "1-1 · " + seed + " · WASD / Space / Q · Shift: кувырок · E: подбор / портал · Esc: пауза");
            UpdateRoomLabel();
            OnPhaseChanged();
        }

        private void Update()
        {
            if (run == null || run.Phase != RunPhase.InArena || IsPaused || player.IsDead || heroCollider == null) return;
            if (sewerWeapon != null)
                controls.SetPassageLabel(sewerWeapon.TorchReadyMode ? "Факел" : sewerWeapon.State.IsMelee ? "Нож" : "Огонь");
            var mapRooms = MapRooms;
            for (int index = 0; index < mapRooms.Count; index++)
            {
                if (!StreetMapLayout.ContainsHero(mapRooms[index], heroCollider.bounds)) continue;
                bool roomChanged = CurrentRoomId != mapRooms[index].Id;
                CurrentRoomId = mapRooms[index].Id;
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

        private void CreateRanch(string runId)
        {
            ranchStage = RanchStage.Load();
            var prefabs = Resources.Load<FixedEncounterPrefabs>(FixedEncounterPrefabs.ResourceName);
            combatAssets = Resources.Load<Stage4CombatAssets>(Stage4CombatAssets.ResourceName);
            if (prefabs == null || !prefabs.IsComplete || combatAssets == null || !combatAssets.IsComplete)
                throw new InvalidOperationException("Missing Ranch encounters or supply prefabs.");
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("FixedStreetGeometry", StringComparison.Ordinal)) root.SetActive(false);
                var follow = root.GetComponent<BoundedFollowCamera>();
                if (follow != null) follow.Configure(player.transform, RanchLayout.CameraBounds);
            }
            var ranch = new RanchMapScene(transform, player.GetComponent<SpriteRenderer>().sprite);
            Rooms = ranch.Build(runId, ActiveEncounter, player, registry, prefabs, ranchStage);
            foreach (var room in Rooms) room.Cleared += OnRoomCleared;
        }
        private void CreateRooms(string runId)
        {
            if (Rooms != null) throw new InvalidOperationException("Load a fresh map for a new run.");
            bool seeded = run.CurrentRequest.FixedEncounter != null;
            var map = run.FullCityRoute?.MapAt(run.CitySegmentIndex) ??
                (seeded ? StreetMapCatalog.Create() : EncounterExample.CreateMap());
            var plan = ActiveEncounter ?? EncounterExample.CreateFixedPlan(run.CurrentRequest.Seed);
            var validation = seeded && run.FullCityRoute == null ? EncounterPlanValidator.ValidateStage(map, plan) : EncounterPlanValidator.Validate(map,plan);
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
                    var actor = spawn.EnemyTypeId == EnemyTypes.HeavyBoss
                        ? CityHeavyBoss.Create(actorRoot.transform, sprite, player.GetComponent<ProjectilePool>().Template,
                            run.IsInSewer ? 3 : CityRegionRoute.DistrictAt(run.CitySegmentIndex))
                        : run.IsInSewer ? prefabs.InstantiateActor(spawn.EnemyTypeId, actorRoot.transform)
                            : combatAssets.CreateElite(spawn.EnemyTypeId, actorRoot.transform);
                    EnableCoverRouting(actor);
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
                    else if (run.CurrentRequest.IsStage5City && marker.TryGetComponent<TextMesh>(out _))
                        marker.gameObject.SetActive(false);
                    else if (run.CurrentRequest.IsStage5City && marker.name.StartsWith("Wall_", StringComparison.Ordinal) &&
                        marker.TryGetComponent<SpriteRenderer>(out var wallRenderer))
                        wallRenderer.forceRenderingOff = true;
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
        private MonoBehaviour[] CreateActors(RoomPlan room, FixedEncounterPrefabs prefabs, Transform parent)
        {
            var actors = new List<MonoBehaviour>();
            foreach (var wave in room.Waves)
                foreach (var spawn in wave.Spawns)
                {
                    var actor = prefabs.InstantiateActor(spawn.EnemyTypeId, parent);
                    if (run.FullCityRoute != null)
                    {
                        int district = CityRegionRoute.DistrictAt(run.CitySegmentIndex);
                        EnableCoverRouting(actor);
                        if (actor is ShootingEnemy shooter) shooter.MarketVolley = district == 1;
                        if (actor is JumpingEnemy jumper) jumper.FlankInBuilding = district == 2;
                    }
                    actor.gameObject.SetActive(false);
                    actor.name = spawn.SpawnId;
                    actors.Add(actor);
                }
            return actors.ToArray();
        }

        private void EnableCoverRouting(MonoBehaviour actor)
        {
            if (!run.IsInSewer && run.CitySegmentIndex < 5) return;
            if (actor is EliteEnemy elite) elite.EnableCoverNavigation();
            var perception = actor.GetComponent<EnemyPerception>();
            if (perception == null && actor is SandboxEnemy)
                perception = actor.gameObject.AddComponent<EnemyPerception>();
            perception?.EnableCoverNavigation();
        }

        private void BuildSewer()
        {
            sewerStage = SewerStage.Load();
            sewerWeapon = player.GetComponent<WeaponController>();
            if (run.CityWeapon != CityFirearm.Pistol) sewerWeapon.EquipCityFamily(run.CityWeapon);
            else if (run.SewerStarter == "pistol-city") sewerWeapon.EquipCityStarter();
            sewerWeapon.LockSewerWeapon();
            player.RestoreForEncounter(run.PlayerTransfer.Value);
            controls.HideWeaponSwitch();
            Feedback = gameObject.AddComponent<CityFeedback>();
            Feedback.Bind(player);
            sewerRetry = Stage5PreviewView.Button("Повторить сохранение", controls.SafeRoot, Vector2.zero, run.RetryStage5Finish);
            sewerRetry.gameObject.SetActive(false);

            var art = new GameObject("Туннели канализации").AddComponent<CityStreetArt>();
            art.transform.SetParent(transform, false);
            art.Build(RoomContours, 3);
            var sprite = player.GetComponent<SpriteRenderer>().sprite;
            var light = new GameObject("Свет факела").AddComponent<SewerLighting>();
            light.transform.SetParent(transform, false);
            light.Initialize(player.transform, sprite);

            int segment = run.CitySegmentIndex;
            Stage5PreviewController.WorldLabel(transform, StreetMapLayout.Entry + Vector2.up * 2.3f, sewerStage.Section(segment));
            foreach (var room in run.FullCityRoute.MapAt(segment).Rooms)
            {
                if (room.RoomId == "entry") continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 offset = new Vector2(4.1f * side, (segment % 2 == 0 ? 2.8f : -2.8f) * side);
                    Vector2 size = segment % 2 == 0 ? new Vector2(1.05f, 3.1f) : new Vector2(2.7f, 1.05f);
                    Vector2 center = new Vector2(room.CenterX, room.CenterY) + offset;
                    if (!CityDistrictDressing.HasClearance(room, offset, size) || RoomContours.Overlaps(center, size)) continue;
                    var pipe = new GameObject("Труба и решётка") { layer = LayerMask.NameToLayer("World") };
                    pipe.transform.SetParent(transform, false);
                    pipe.transform.position = center;
                    var renderer = pipe.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.color = new Color(.26f,.35f,.33f);
                    renderer.sortingOrder = 0;
                    pipe.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
                    pipe.AddComponent<BoxCollider2D>().size = sprite.bounds.size;
                }
            }
        }
        private void OnRoomCleared(RoomController room)
        {
            if (run == null || room.RunId != run.RunId || !cleared.Add(room.RoomId)) return;
            preview?.CompleteRoom(room.RoomId);
            UpdateRoomLabel();
            if (ranchStage != null)
            {
                if (room.RoomId == RanchLayout.PenId) Drop(room, PickupKind.Ammo, RanchLayout.AmmoPosition, ranchStage.Ammo);
            }
            else if (run.IsInSewer)
            {
                int stage = run.CitySegmentIndex;
                Vector2 center = StreetMapLayout.Rooms[Array.IndexOf(Rooms, room) + 1].Center;
                if (room.RoomId == "combat_b" && !sewerWeapon.State.IsMelee && sewerStage.Ammo(stage) > 0)
                    Drop(room, PickupKind.Ammo, center, sewerStage.Ammo(stage));
                if (room.RoomId == "combat_c" && sewerStage.Armor(stage) > 0)
                    Drop(room, PickupKind.Armor, center, sewerStage.Armor(stage));
            }
            else if (combatAssets != null && preview == null)
            {
                int index = Array.IndexOf(Rooms, room);
                var layout = StreetMapLayout.Rooms[index + 1];
                Vector2 center = layout.Center;
                if (index < 3) Drop(room, (PickupKind)index, center);
                else
                {
                    Drop(room, PickupKind.Ammo, center + Vector2.left * 2f);
                    Drop(room, PickupKind.Health, center);
                    Drop(room, PickupKind.Armor, center + Vector2.right * 2f);
                }
            }
            RoomCleared?.Invoke(room.RunId,room.RoomId);
            if (ClearedCount == Rooms.Length && preview?.HasPendingCheckpoint != true && TryOpenExit())
            {
                DressCityExit();
                UpdateRoomLabel();
            }
        }

        private bool TryOpenExit()
        {
            if (ranchStage == null) return Portal.TryOpen(player);
            if (!Portal.IsOpen)
            {
                Portal.OpenAt(RanchLayout.ExitPosition);
                Stage5PreviewController.WorldLabel(transform, RanchLayout.ExitPosition + Vector2.up, "В убежище · E");
            }
            return true;
        }

        private void DressCityExit()
        {
            if (exitDressed || run.IsInSewer || run.FullCityRoute == null || run.CitySegmentIndex < 10) return;
            exitDressed = true;
            Vector2 location = Portal.transform.position;
            if (run.IsFinalCitySegment)
            {
                var car = StreetMapLayout.Rooms[4].Center + new Vector2(5, -3);
                preview.Tile("Машина эвакуации", car, new Vector2(2.5f, 1.15f), new Color(.32f,.43f,.45f), 1);
                preview.Tile("Лобовое стекло", car + Vector2.right * .26f, new Vector2(.60f,.85f), new Color(.17f,.24f,.29f), 2);
                Stage5PreviewController.WorldLabel(preview.transform, car + Vector2.up, "Эвакуация");
                if (run.CanEnterSewer)
                {
                    preview.Tile("Люк канализации", SewerHatch, new Vector2(1.35f, 1.1f), new Color(.24f,.35f,.33f), 2);
                    preview.Outline("Контур люка", SewerHatch, new Vector2(1.35f, 1.1f));
                    Stage5PreviewController.WorldLabel(preview.transform, SewerHatch + Vector2.up, "Канализация · E");
                    sewerEntry = new SewerEntryScreen(controls.SafeRoot, CityArmory.Name(run.CityWeapon), ConfirmSewer, CloseSewerEntry);
                }
                return;
            }
            bool lift = run.CitySegmentIndex == 13;
            if (lift)
            {
                preview.Tile("Двери лифта", location, new Vector2(1.75f, 1.2f), new Color(.40f, .49f, .54f), 2);
                preview.Tile("Створки лифта", location, new Vector2(.065f, 1.2f), new Color(.12f, .17f, .20f), 3);
                preview.Tile("Индикатор лифта", location + new Vector2(.74f, .69f), new Vector2(.17f, .13f), new Color(.46f, .94f, .66f), 3);
            }
            else
            {
                for (int step = 0; step < 4; step++)
                    preview.Tile("Ступень", location + Vector2.up * (.2f * step - .3f), new Vector2(1.6f, .11f),
                        new Color(.66f, .70f, .62f), 2);
            }
            string label = lift ? "Лифт · на крышу" : "Лестница · на этаж выше";
            Stage5PreviewController.WorldLabel(preview.transform, location + Vector2.up, label);
        }
        private void Drop(RoomController room, PickupKind kind, Vector2 position, int quantity = 0)
        {
            var pickup = combatAssets.CreatePickup(kind, position, transform);
            if (quantity > 0) pickup.Configure(kind, quantity);
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
                if (pickup.Kind == PickupKind.Ammo && (snapshot.ReserveAmmo >= WeaponRuntimeState.PickupReserveLimit || run.IsInSewer && snapshot.IsMelee) ||
                    pickup.Kind == PickupKind.Health && snapshot.Health >= snapshot.MaxHealth ||
                    pickup.Kind == PickupKind.Armor && snapshot.Armor >= snapshot.MaxArmor) continue;
                Vector2 offset = (Vector2)pickup.transform.position - position;
                float range = offset.magnitude;
                if (range > distance || Physics2D.Raycast(position, offset.normalized, range, LayerMask.GetMask("World"))) continue;
                nearest = pickup;
                distance = range;
            }
            if (nearest == null || nearest.TryCollect(player) <= 0) return false;
            if (ranchStage != null) UpdateRoomLabel();
            return true;
        }
        private void UpdateRoomLabel()
        {
            if (ranchStage != null)
            {
                string ranchText = $"{RanchLayout.RoomLabel(CurrentRoomId)} · Встречи {ClearedCount}/{Rooms.Length}";
                if (Portal != null && Portal.IsOpen) ranchText += "\nВернуться в убежище · E рядом с выходом";
                else if (pickups.Exists(pickup => pickup != null && !pickup.IsConsumed))
                    ranchText += "\nПатроны на дороге к амбару · E";
                hud.SetEncounter(ranchText);
                return;
            }
            if (run.CurrentRequest.IsStage5City)
            {
                var label = run.IsInSewer ? (CurrentRoomId == "elite" && run.IsLastCitySegment ? "Иловый затворник" : sewerStage.Section(run.CitySegmentIndex)) :
                    CityDistrictDressing.RoomName(CityRegionRoute.DistrictAt(run.CitySegmentIndex), CurrentRoomId) ?? (CurrentRoomId switch
                {
                    "combat_a" => "Первый перекрёсток",
                    "combat_b" => "Переулок",
                    "combat_c" => "Двор",
                    "elite" => "Финальный бой",
                    _ => "Вход на улицу"
                });
                string cityText = $"{label}\nВстречи {ClearedCount}/4";
                foreach (var room in Rooms)
                {
                    if (room.RoomId != CurrentRoomId) continue;
                    cityText += room.State.Phase switch
                    {
                        RoomPhase.Cleared => " · Проходы открыты",
                        RoomPhase.Fighting or RoomPhase.Waiting => $" · Врагов {room.State.AliveCount + room.State.PendingCount}",
                        _ => ""
                    };
                    break;
                }
                if (ClearedCount == 4)
                {
                    string travel = run.IsInSewer ? (run.IsLastCitySegment ? "Убежище и награда" : "Следующий затвор · " + SewerStage.IdAt(run.CitySegmentIndex + 1)) :
                        !run.CurrentRequest.IsFullCity ? "Вернуться в убежище" : run.IsFinalCitySegment
                        ? (run.CanEnterSewer ? "Эвакуация или люк канализации" : "Эвакуация с крыши") : run.IsLastCitySegment ? "Завершить проверку" :
                        (run.CitySegmentIndex == 13 ? "Лифт" : run.CitySegmentIndex >= 10 ? "Лестница" : "Дальше") +
                            " · " + CityRegionRoute.IdAt(run.CitySegmentIndex + 1);
                    cityText += Portal != null && Portal.IsOpen ? $"\n{travel} · E рядом" : "\nНажмите E для размещения портала";
                    if (!string.IsNullOrEmpty(run.Error)) cityText += "\n" + run.Error;
                }
                hud.SetEncounter(cityText);
                return;
            }
            string text="1-1 · "+CurrentRoomId+" · пройдено "+ClearedCount+"/4";
            if (CurrentRoomId == "elite" && combatAssets == null) text += "\nПроверочный бой";
            else if (combatAssets != null && preview == null && cleared.Contains(CurrentRoomId)) text += "\nE рядом с предметом: подобрать";
            if (Rooms!=null) foreach (var room in Rooms)
                if (room.RoomId==CurrentRoomId)
                    text+="\n"+(room.State.Phase==RoomPhase.Cleared ? "Проходы открыты" : "Живых: "+room.State.AliveCount+" · ожидается: "+room.State.PendingCount);
            if(ClearedCount==4)text+="\n"+(Portal!=null && Portal.IsOpen?"Портал открыт · подойдите и нажмите E":"Нажмите E для размещения портала");
            hud.SetEncounter(text);
        }
        public void TryExit()
        {
            if(run==null || run.Phase!=RunPhase.InArena || IsPaused || player.IsDead || sewerEntry?.IsOpen == true || run.Stage5FinishBlocked)return;
            if (preview?.RetryCheckpoint() == true) return;
            if (sewerEntry != null && Vector2.Distance(player.transform.position, SewerHatch) <= 1.5f)
            {
                controls.ClearHeld();
                Time.timeScale = 0;
                sewerEntry.Show(true);
                return;
            }
            if ((preview != null && preview.TryUseInteract()) || TryPickup() || ClearedCount != Rooms.Length) return;
            if(!TryOpenExit()){UpdateRoomLabel();return;}
            DressCityExit();
            UpdateRoomLabel();
            if(Vector2.Distance(player.transform.position,Portal.transform.position)>1.5f)return;
            if (run.CurrentRequest.IsFullCity && !run.IsLastCitySegment)
                run.AdvanceCity(player.ReadSnapshot, preview?.CarriedMedkits ?? 0);
            else run.FinishRun(RunOutcome.Victory);
        }
        private void ConfirmSewer(bool knife)
        {
            if (sewerEntry?.IsOpen != true) return;
            sewerEntry.Show(false);
            run.EnterSewer(knife, player.ReadSnapshot);
        }

        private void CloseSewerEntry()
        {
            if (sewerEntry?.IsOpen != true) return;
            sewerEntry.Show(false);
            Time.timeScale = 1;
        }
        private void CancelRooms()
        { if (Rooms!=null) foreach (var room in Rooms) if (room!=null) room.Cancel(); }
        private void OnPlayerDied() { CancelRooms(); run?.FinishRun(RunOutcome.Defeat); }
        public void TogglePause() { if (IsPaused) Resume(); else Pause(); }
        public void Pause()
        {
            if (run == null || run.Phase != RunPhase.InArena || player.IsDead || sewerEntry?.IsOpen == true || run.Stage5FinishBlocked) return;
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
            if (sewerRetry != null) sewerRetry.gameObject.SetActive(run.Stage5FinishBlocked);
            if (run.Stage5FinishBlocked && run.Phase == RunPhase.InArena)
            {
                player.SetControlsEnabled(false);
                controls.ClearHeld();
                Time.timeScale = 0;
                return;
            }
            bool active = run != null && run.Phase == RunPhase.InArena && !player.IsDead;
            player.SetControlsEnabled(active);
            if (active)
            {
                if (!string.IsNullOrEmpty(run.Error)) UpdateRoomLabel();
                return;
            }
            if (run.Phase == RunPhase.ReturningHome && run.LastOutcome.HasValue ||
                run.Phase == RunPhase.LoadingArena && run.CurrentRequest.IsFullCity && run.CitySegmentIndex != boundCitySegment)
                preview?.Finish();
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
