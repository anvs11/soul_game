using System.Collections.Generic;
using Game.Combat;
using Game.Core;
using Game.UI;
using UnityEngine;

namespace Game.StreetMap
{
    internal sealed class CityArmory
    {
        private sealed class Stock
        {
            internal CityFirearm Family;
            internal GameObject Marker;
            internal int Remaining;
            internal bool Weapon;
        }

        private readonly List<Stock> stock = new();
        private readonly Stage5PreviewController view;
        private readonly StreetMapScreen screen;
        private readonly RunCoordinator run;

        internal CityArmory(Stage5PreviewController view, StreetMapScreen screen, RunCoordinator run)
        {
            this.view = view;
            this.screen = screen;
            this.run = run;
            var entry = StreetMapLayout.Entry;
            if (run.CitySegmentIndex is 5 or 10)
                Add(run.CitySegmentIndex == 5 ? CityFirearm.SubmachineGun : CityFirearm.Carbine,
                    entry + new Vector2(-3.4f, -1.3f), true);
            Add(run.CityWeapon, entry + new Vector2(3.5f, -1.2f), false);
            CityFirearm districtWeapon = run.CitySegmentIndex >= 10 ? CityFirearm.Carbine :
                run.CitySegmentIndex >= 5 ? CityFirearm.SubmachineGun : CityFirearm.Pistol;
            for (int i = 1; i <= 3; i++)
            {
                Vector2 location = StreetMapLayout.Rooms[i].Center + new Vector2(1.1f, -4.6f);
                Add(districtWeapon, location, false);
                if (run.CityWeapon != districtWeapon) Add(run.CityWeapon, location + Vector2.right * 1.6f, false);
            }
        }

        internal static string Name(CityFirearm family) => family switch
        {
            CityFirearm.Carbine => "Карабин",
            CityFirearm.SubmachineGun => "Пистолет-пулемёт",
            _ => "Пистолет"
        };

        internal static string Ammo(CityFirearm family) => family switch
        {
            CityFirearm.Carbine => "5,56 мм",
            CityFirearm.SubmachineGun => ".45",
            _ => "9 мм"
        };

        private void Add(CityFirearm family, Vector2 position, bool weapon)
        {
            var marker = view.Tile(weapon ? Name(family) : "Ящик · " + Ammo(family), position,
                weapon ? new Vector2(.92f,.42f) : new Vector2(.55f,.42f),
                weapon ? new Color(.88f,.67f,.33f) : new Color(.43f,.57f,.40f), 1);
            stock.Add(new Stock { Family = family, Marker = marker, Weapon = weapon,
                Remaining = family switch { CityFirearm.Carbine => 8, CityFirearm.SubmachineGun => 16, _ => 8 } });
        }

        private Stock Nearby(Vector2 position)
        {
            Stock nearest = null;
            float best = .9f * .9f;
            foreach (var item in stock)
            {
                if (!item.Marker.activeSelf) continue;
                Vector2 delta = (Vector2)item.Marker.transform.position - position;
                if (delta.sqrMagnitude >= best ||
                    Physics2D.Raycast(position, delta.normalized, delta.magnitude, LayerMask.GetMask("World"))) continue;
                nearest = item;
                best = delta.sqrMagnitude;
            }
            return nearest;
        }

        internal string Prompt(Vector2 position)
        {
            var item = Nearby(position);
            if (item == null) return null;
            return item.Weapon ? "E · Взять " + Name(item.Family) : "E · Боеприпасы " + Ammo(item.Family);
        }

        internal bool Use(Vector2 position, out string notice)
        {
            notice = null;
            var item = Nearby(position);
            if (item == null) return false;
            if (item.Weapon)
            {
                view.EquipCityFirearm(item.Family);
                item.Marker.SetActive(false);
                screen.Feedback?.Play(CityCue.Pickup);
                notice = Name(item.Family) + " · " + Ammo(item.Family);
            }
            else if (run.CityWeapon != item.Family)
                notice = "Нужен калибр " + Ammo(run.CityWeapon) + " · здесь " + Ammo(item.Family);
            else
            {
                int added = screen.Player.TryAddReserve(item.Remaining);
                item.Remaining -= added;
                if (item.Remaining == 0) item.Marker.SetActive(false);
                notice = added > 0 ? Ammo(item.Family) + " · +" + added : "Резерв патронов полон";
                if (added > 0) screen.Feedback?.Play(CityCue.Pickup);
            }
            return true;
        }
    }
}
