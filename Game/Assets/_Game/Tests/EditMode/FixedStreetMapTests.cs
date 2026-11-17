using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Editor;
using Game.StreetMap;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Game.Tests
{
    public sealed class FixedStreetMapTests
    {
        [Test]
        public void Geography_HasFiveStableRoomsAndEveryFloorCellIsConnected()
        {
            Assert.That(StreetMapLayout.Rooms.Select(r => r.Id), Is.EqualTo(new[] { "entry", "combat_a", "combat_b", "combat_c", "elite" }));
            var floor = new HashSet<Vector2Int>(StreetMapLayout.WalkableCells());
            var reached = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            var start = Vector2Int.FloorToInt(StreetMapLayout.Entry);
            reached.Add(start); queue.Enqueue(start);
            var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (floor.Contains(next) && reached.Add(next)) queue.Enqueue(next);
                }
            }
            Assert.That(reached.SetEquals(floor), Is.True, "No isolated room or corridor tiles are allowed.");
            Assert.That(StreetMapLayout.Corridors.Count, Is.EqualTo(4));
            foreach (var corridor in StreetMapLayout.Corridors) Assert.That(Mathf.Min(corridor.width, corridor.height), Is.EqualTo(4));
            Assert.That(StreetMapLayout.Rooms[4].Bounds.size, Is.EqualTo(new Vector2Int(28, 24)));
            Assert.That(StreetMapLayout.Rooms[0].SpawnPoints, Is.Empty);
            Assert.That(floor.Count, Is.GreaterThan(1800));
        }

        [Test]
        public void RoomVisit_RequiresEntireColliderPastThresholdIncludingDiagonalEntry()
        {
            var room = StreetMapLayout.Rooms[1];
            Assert.That(StreetMapLayout.ContainsHero(room, new Bounds(new Vector3(13.2f, 0), Vector3.one)), Is.False);
            Assert.That(StreetMapLayout.ContainsHero(room, new Bounds(new Vector3(13.7f, -7.3f), Vector3.one)), Is.True);
            Assert.That(StreetMapLayout.ContainsHero(room, new Bounds(new Vector3(13.7f, -7.6f), Vector3.one)), Is.False);
        }

        [TestCase(16f / 9f)]
        [TestCase(9f / 16f)]
        [TestCase(4f)]
        public void Camera_ClampsViewportAtEachMapEdge(float aspect)
        {
            var bounds = StreetMapLayout.CameraBounds;
            foreach (var target in new[] { new Vector2(-999, -999), new Vector2(999, 999), bounds.center })
            {
                var center = BoundedFollowCamera.Clamp(target, bounds, 7.5f, aspect);
                Assert.That(center.x - 7.5f * aspect, Is.GreaterThanOrEqualTo(bounds.xMin - .001f));
                Assert.That(center.x + 7.5f * aspect, Is.LessThanOrEqualTo(bounds.xMax + .001f));
                Assert.That(center.y - 7.5f, Is.GreaterThanOrEqualTo(bounds.yMin));
                Assert.That(center.y + 7.5f, Is.LessThanOrEqualTo(bounds.yMax));
            }
            Assert.That(BoundedFollowCamera.Clamp(Vector2.one * 999, bounds, 100, 1), Is.EqualTo(bounds.center), "Oversized viewport centers without an inverted clamp.");
        }

        [Test]
        public void FixedProfile_UsesExistingCoordinatorFreshRunAndDefeatPriority()
        {
            var flow = new ImmediateFlow();
            using (var run = new RunCoordinator(flow))
            {
                run.Begin(); run.StartRun(RunRequest.FixedStreet1_1(42));
                Assert.That(flow.LastScene, Is.EqualTo(ScenePaths.Stage4StreetMap));
                Assert.That(run.CurrentRequest.IsFixedStreet1_1, Is.True);
                Assert.That(run.CurrentRequest.IsStreetRun, Is.False);
                Assert.That(run.CurrentRequest.Seed, Is.EqualTo(42));
                Assert.That(run.StreetRoute, Is.Null);
                string id = run.RunId;
                run.FinishRun(RunOutcome.Victory); run.FinishRun(RunOutcome.Defeat); run.ResolvePendingFinish();
                Assert.That(run.LastOutcome, Is.EqualTo(RunOutcome.Defeat));
                Assert.That(flow.LastScene, Is.EqualTo(ScenePaths.Home));
                run.StartRun(RunRequest.FixedStreet1_1());
                Assert.That(run.RunId, Is.Not.EqualTo(id));
                Assert.That(run.LastOutcome, Is.Null);
            }
        }

        [Test]
        public void NewAssets_HaveSingleFlowCameraEventSystemAndRealWorldWalls()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Stage4MapAssets.GeometryPath);
            Assert.That(prefab, Is.Not.Null, "Run Game/Stage 4/Create S4-02 fixed map first.");
            var walls = prefab.GetComponentsInChildren<BoxCollider2D>();
            Assert.That(walls.Length, Is.GreaterThan(250));
            foreach (var wall in walls)
            {
                Assert.That(wall.isTrigger, Is.False);
                Assert.That(wall.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("World")));
                Assert.That(StreetMapLayout.IsWalkable(Vector2Int.FloorToInt(wall.transform.position)), Is.False);
            }
            var scene = EditorSceneManager.OpenScene(Stage4MapAssets.ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.Sum(r => r.GetComponentsInChildren<FlowScreen>(true).Length), Is.EqualTo(1));
                Assert.That(roots.Sum(r => r.GetComponentsInChildren<GameBootstrap>(true).Length), Is.Zero);
                Assert.That(roots.Sum(r => r.GetComponentsInChildren<Camera>(true).Length), Is.EqualTo(1));
                Assert.That(roots.Sum(r => r.GetComponentsInChildren<EventSystem>(true).Length), Is.EqualTo(1));
                Assert.That(roots.Sum(r => r.GetComponentsInChildren<InputSystemUIInputModule>(true).Length), Is.EqualTo(1));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == Stage4MapAssets.ScenePath), Is.True);
        }
        private sealed class ImmediateFlow : ISceneFlow
        {
            public string LastScene;
            public bool TryLoad(string name, System.Action<bool, string> completed)
            { LastScene = name; completed(true, null); return true; }
        }
    }
}
