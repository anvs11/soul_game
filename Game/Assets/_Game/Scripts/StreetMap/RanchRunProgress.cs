using System;
using System.Collections.Generic;
using System.Linq;
using Game.Actors;
using Game.Combat;
using Game.Contracts;
using Game.Core;
using Game.Progression;
using UnityEngine;

namespace Game.StreetMap
{
    internal sealed class RanchRunProgress : IDisposable
    {
        private sealed class Pickup
        {
            public string Id;
            public string Room;
            public ProgressionLootKind Kind;
            public GameObject Visual;
        }

        private readonly string runId;
        private readonly CityProgressionSession session;
        private readonly PlayerCombatFacade hero;
        private readonly RoomController[] rooms;
        private readonly Transform parent;
        private readonly Sprite sprite;
        private readonly Action changed;
        private readonly List<Pickup> drops = new();
        private RoomController pending;
        private long ammoAttempt;

        internal bool HasPendingSave => pending != null;
        internal string Notice { get; private set; }
        internal ProgressionSnapshot Snapshot => session.Snapshot;

        internal RanchRunProgress(RunCoordinator run, PlayerCombatFacade hero, RoomController[] rooms,
            Transform parent, Action changed)
        {
            runId = run.RunId;
            session = run.CityRuntime.Session;
            this.hero = hero;
            this.rooms = rooms;
            this.parent = parent;
            this.changed = changed;
            sprite = hero.GetComponent<SpriteRenderer>().sprite;
            if (Snapshot.Run.StartingWeaponId == "pistol-city")
                hero.GetComponent<WeaponController>().EquipCityStarter();
            hero.BindProgressionWeaponIds(runId + "/firearm", runId + "/blade");
            session.BindCombat(hero);

            foreach (var room in rooms)
            {
                room.EnemyDefeated += RecordDeath;
                room.CanPublishClear = ConfirmClear;
            }
        }

        private ProgressionCommandKey Key(string action) => new(runId, action);

        private void RecordDeath(RoomController room, string spawn, string type, Vector2 position)
        {
            if (!RanchProgression.TryCheckpoint(room.RoomId, out var checkpoint)) return;
            var result = session.ConfirmEnemyDeath(Key("death/" + spawn), checkpoint, spawn, type);
            if (result.Status != ProgressionStatus.Applied) return;
            foreach (var drop in result.Snapshot.Run.Loot.Where(item => item.SpawnId == spawn))
            {
                var visual = new GameObject(drop.Kind switch
                {
                    ProgressionLootKind.Coins => "Монеты ранчо",
                    ProgressionLootKind.Crystals => "Кристаллы ранчо",
                    _ => "Патроны ранчо"
                });
                visual.transform.SetParent(parent, false);
                float offset = drop.Kind == ProgressionLootKind.Coins ? -.21f :
                    drop.Kind == ProgressionLootKind.Crystals ? .21f : 0;
                visual.transform.position = position + Vector2.right * offset;
                visual.transform.localScale = new Vector3(.30f, .30f, 1);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 3;
                renderer.color = drop.Kind switch
                {
                    ProgressionLootKind.Coins => new Color(.99f, .76f, .19f),
                    ProgressionLootKind.Crystals => new Color(.64f, .43f, .95f),
                    _ => new Color(.92f, .89f, .67f)
                };
                drops.Add(new Pickup { Id = drop.PickupId, Room = room.RoomId, Kind = drop.Kind, Visual = visual });
            }
            changed();
        }

        private bool ConfirmClear(RoomController room)
        {
            if (!RanchProgression.TryCheckpoint(room.RoomId, out var checkpoint)) return false;
            var result = session.ClearCheckpoint(Key("clear/" + checkpoint.StageId + "/" + room.RoomId), checkpoint);
            if (result.Failure != ProgressionFailure.None)
            {
                pending = room;
                Notice = Stage5Runtime.Describe(result.Failure);
                changed();
                return false;
            }

            pending = null;
            Notice = null;
            for (int i = drops.Count - 1; i >= 0; --i)
                if (drops[i].Room == room.RoomId && drops[i].Kind == ProgressionLootKind.Crystals) Remove(i);
            changed();
            return true;
        }

        internal void RetryCheckpoint() => pending?.PublishClear();

        internal void CollectNearbyCurrency()
        {
            if (HasPendingSave) return;
            bool collected = false;
            for (int i = drops.Count - 1; i >= 0; --i)
            {
                var drop = drops[i];
                if (drop.Kind == ProgressionLootKind.Ammo || !Near(drop.Visual.transform.position, 1.2f)) continue;
                var result = session.CollectLoot(Key("currency/" + drop.Id), drop.Id);
                if (result.Failure != ProgressionFailure.None) continue;
                Remove(i);
                collected = true;
            }
            if (collected) changed();
        }

        internal bool TryCollectAmmo()
        {
            for (int i = drops.Count - 1; i >= 0; --i)
            {
                var drop = drops[i];
                if (drop.Kind != ProgressionLootKind.Ammo || !Near(drop.Visual.transform.position, 1.5f)) continue;
                var result = session.CollectLoot(Key("ammo/" + ++ammoAttempt), drop.Id);
                if (result.Failure != ProgressionFailure.None) return false;
                if (!result.Snapshot.Run.Loot.Any(item => item.PickupId == drop.Id)) Remove(i);
                changed();
                return true;
            }
            return false;
        }

        private bool Near(Vector2 point, float radius)
        {
            Vector2 origin = hero.transform.position;
            Vector2 offset = point - origin;
            return offset.sqrMagnitude <= radius * radius &&
                !Physics2D.Raycast(origin, offset.normalized, offset.magnitude, LayerMask.GetMask("World"));
        }

        private void Remove(int index)
        {
            var visual = drops[index].Visual;
            visual.SetActive(false);
            UnityEngine.Object.Destroy(visual);
            drops.RemoveAt(index);
        }

        public void Dispose()
        {
            foreach (var room in rooms)
            {
                if (room == null) continue;
                room.EnemyDefeated -= RecordDeath;
                room.CanPublishClear = null;
            }
        }
    }
}
