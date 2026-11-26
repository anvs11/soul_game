using System;
using System.Collections.Generic;
using System.Linq;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Encounters;
using UnityEngine;

namespace Game.StreetMap
{
    internal sealed class RanchMapScene
    {
        private static readonly Color Ground = new(.40f, .37f, .24f);
        private static readonly Color Grass = new(.26f, .31f, .18f);
        private static readonly Color Fence = new(.37f, .23f, .14f);
        private static readonly Color Wood = new(.48f, .34f, .21f);
        private readonly Transform root;
        private readonly Sprite tile;
        private readonly int worldLayer;

        internal RanchMapScene(Transform parent, Sprite tile)
        {
            this.tile = tile;
            worldLayer = LayerMask.NameToLayer("World");
            root = new GameObject("Ranch 5-1").transform;
            root.SetParent(parent, false);
        }

        internal RoomController[] Build(string runId, EncounterPlan plan, PlayerCombatFacade hero,
            TargetRegistry registry, FixedEncounterPrefabs enemies, RanchStage catalog)
        {
            BuildGround();
            var map = RanchLayout.CreateMap();
            var check = EncounterPlanValidator.Validate(map, plan);
            if (!check.IsSuccess) throw new InvalidOperationException(check.Diagnostic);

            var result = new RoomController[map.Rooms.Count - 1];
            for (int i = 0; i < result.Length; i++)
            {
                int index = i + 1;
                var descriptor = map.Rooms[index];
                var layout = RanchLayout.Rooms[index];
                var encounter = plan.Rooms.Single(room => room.RoomId == descriptor.RoomId);
                var room = new GameObject(descriptor.RoomId + "_Encounter").AddComponent<RoomController>();
                room.transform.SetParent(root, false);
                var containers = new GameObject("EncounterActors");
                containers.transform.SetParent(room.transform, false);
                containers.SetActive(false);
                var actors = encounter.Waves.SelectMany(wave => wave.Spawns).Select(spawn =>
                {
                    MonoBehaviour actor = spawn.EnemyTypeId == EnemyTypes.RanchDeer
                        ? UnityEngine.Object.Instantiate(catalog.Deer, containers.transform)
                        : enemies.InstantiateActor(spawn.EnemyTypeId, containers.transform);
                    actor.name = spawn.SpawnId;
                    if (actor is RanchDeer deer) deer.EnableCoverNavigation();
                    else
                    {
                        var perception = actor.GetComponent<EnemyPerception>();
                        if (perception == null && actor is SandboxEnemy) perception = actor.gameObject.AddComponent<EnemyPerception>();
                        perception?.EnableCoverNavigation();
                    }
                    return actor;
                }).ToArray();
                var gates = new List<DoorController> { Gate(layout, layout.Bounds.xMin, "Вход") };
                if (index < map.Rooms.Count - 1)
                    gates.Add(Gate(layout, layout.Bounds.xMax, "Дальше"));
                room.Configure(runId, descriptor, encounter, hero, registry, actors, gates.ToArray());
                // The next room becomes accessible only after the current room clears.
                if (gates.Count == 2) gates[1].SetClosed(true);
                containers.SetActive(true);
                result[i] = room;
            }

            UnityEngine.Object.Instantiate(catalog.Trap, RanchLayout.TrapPosition, Quaternion.identity, root);
            Physics2D.SyncTransforms();
            return result;
        }

        private void BuildGround()
        {
            Place("Ranch surroundings", RanchLayout.CameraBounds.center, RanchLayout.CameraBounds.size, Grass, -9);
            foreach (var corridor in RanchLayout.Corridors)
            {
                Place("Дорога", corridor.center, corridor.size, Ground, -4);
                foreach (int sign in new[] { -1, 1 })
                    Wall("Ограда дороги", corridor.center + Vector2.up * (sign * corridor.height / 2f),
                        new Vector2(corridor.width, RanchLayout.WallWidth), Fence);
            }

            for (int i = 0; i < RanchLayout.Rooms.Count; i++)
            {
                var room = RanchLayout.Rooms[i];
                var bounds = room.Bounds;
                bool barn = room.Id == RanchLayout.BarnId;
                Place(RanchLayout.RoomLabel(room.Id), bounds.center, bounds.size, barn ? Wood : Ground, -4);
                Wall("Северная ограда", new Vector2(bounds.center.x, bounds.yMax), new Vector2(bounds.width, RanchLayout.WallWidth), Fence);
                Wall("Южная ограда", new Vector2(bounds.center.x, bounds.yMin), new Vector2(bounds.width, RanchLayout.WallWidth), Fence);
                Side(bounds, bounds.xMin, i > 0);
                Side(bounds, bounds.xMax, i < RanchLayout.Rooms.Count - 1);
                Stage5PreviewController.WorldLabel(root, new Vector2(bounds.xMin + 3.3f, 1.45f), RanchLayout.RoomLabel(room.Id));

                if (barn)
                {
                    for (int x = bounds.xMin + 1; x < bounds.xMax; x++)
                        Place("Доски амбара", new Vector2(x, 0), new Vector2(.065f, bounds.height), Fence, -3);
                }
                else
                {
                    Wall("Сено", new Vector2(bounds.center.x - 1.1f, bounds.yMin + 1.3f),
                        new Vector2(2, .85f), new Color(.62f, .48f, .25f));
                    if (room.Id == RanchLayout.PenId)
                        Wall("Перегородка загона", new Vector2(51, 6.55f), new Vector2(2, .65f), Fence);
                }
            }
        }

        private void Side(RectInt bounds, int x, bool opening)
        {
            float halfOpening = RanchLayout.PassageWidth / 2f;
            if (!opening)
            {
                Wall("Торцевая ограда", new Vector2(x, bounds.center.y), new Vector2(RanchLayout.WallWidth, bounds.height), Fence);
                return;
            }

            float length = bounds.yMax - halfOpening;
            foreach (int sign in new[] { -1, 1 })
                Wall("Стойки ворот", new Vector2(x, sign * (halfOpening + length / 2)),
                    new Vector2(RanchLayout.WallWidth, length), Fence);
        }

        private DoorController Gate(StreetMapLayout.Room room, int x, string label)
        {
            var gate = new GameObject(label + " · " + RanchLayout.RoomLabel(room.Id)).AddComponent<DoorController>();
            gate.transform.SetParent(root, false);
            gate.Configure(tile, new Vector2(x, 0), new Vector2(RanchLayout.WallWidth, RanchLayout.PassageWidth));
            return gate;
        }

        private void Wall(string name, Vector2 position, Vector2 size, Color color)
        {
            var visual = Place(name, position, size, color, 1);
            visual.layer = worldLayer;
            visual.AddComponent<BoxCollider2D>().size = Vector2.one;
        }

        private GameObject Place(string name, Vector2 position, Vector2 size, Color color, int layer)
        {
            var visual = new GameObject(name);
            visual.transform.SetParent(root, false);
            visual.transform.position = position;
            visual.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = tile;
            renderer.color = color;
            renderer.sortingOrder = layer;
            return visual;
        }
    }
}
