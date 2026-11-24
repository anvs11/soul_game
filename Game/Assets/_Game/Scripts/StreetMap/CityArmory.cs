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
            internal GameObject SearchOutline;
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
            CityFirearm districtWeapon = run.CitySegmentIndex >= 10 ? CityFirearm.Carbine :
                run.CitySegmentIndex >= 5 ? CityFirearm.SubmachineGun : CityFirearm.Pistol;
            if (run.CitySegmentIndex is 5 or 10)
            {
                Add(districtWeapon, entry + new Vector2(-3.4f, -1.3f), true);
                AddPoliceCorpse(districtWeapon, StreetMapLayout.Rooms[1].Center + new Vector2(1.6f, -3f));
            }
            if (run.CurrentRequest.VisualPreview == CityVisualPreview.None) return;
            Add(run.CityWeapon, entry + new Vector2(3.5f, -1.2f), false);
            for (int i = 1; i <= 3; i++)
                Add(districtWeapon, StreetMapLayout.Rooms[i].Center + new Vector2(1.1f, -4.6f), false);
        }

        private void AddPoliceCorpse(CityFirearm family, Vector2 location)
        {
            stock.Add(new Stock
            {
                Family = family,
                Marker = Stage5Supplies.DrawBody(view, "Тело полицейского с патронами", location, new Color(.27f,.36f,.51f)),
                SearchOutline = view.Outline("Контур тела полицейского", location, new Vector2(.62f,.98f)),
                Remaining = CityAmmoBackpack.PackSize(family)
            });
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

        private void Add(CityFirearm family, Vector2 position, bool weapon, int? rounds = null)
        {
            var marker = view.Tile(weapon ? Name(family) : (rounds.HasValue ? "Патроны · " : "Ящик · ") + Ammo(family), position,
                weapon ? new Vector2(.92f,.42f) : new Vector2(.55f,.42f),
                weapon ? new Color(.88f,.67f,.33f) : new Color(.43f,.57f,.40f), 1);
            if (rounds.HasValue)
                view.Outline("Контур патронов", position, new Vector2(.55f,.42f)).transform.SetParent(marker.transform, true);
            stock.Add(new Stock { Family = family, Marker = marker, Weapon = weapon,
                Remaining = rounds ?? CityAmmoBackpack.PackSize(family) });
        }

        internal void DropAmmo(CityFirearm family, Vector2 position, int rounds) => Add(family, position, false, rounds);

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
            if (item.SearchOutline != null) return "E · Обыскать полицейского";
            return item.Weapon ? "E · Взять " + Name(item.Family) : item.Family == run.CityWeapon
                ? "E · Подобрать патроны " + Ammo(item.Family)
                : "E · В рюкзак · Патроны " + Ammo(item.Family);
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
            else if (item.SearchOutline != null)
            {
                int added = run.CityWeapon == item.Family ? screen.Player.TryAddReserve(item.Remaining) : 0;
                int remaining = item.Remaining - added;
                item.SearchOutline.SetActive(false);
                stock.Remove(item);
                if (remaining > 0)
                    DropAmmo(item.Family, item.Marker.transform.position, remaining);
                if (added > 0) screen.Feedback?.Play(CityCue.Pickup);
                notice = added > 0 ? $"{Ammo(item.Family)} · +{added}" : $"Патроны {Ammo(item.Family)} на земле";
            }
            else
            {
                bool toBackpack = run.CityWeapon != item.Family;
                int added = toBackpack ? run.CityAmmo.Store(item.Family, item.Remaining) : screen.Player.TryAddReserve(item.Remaining);
                item.Remaining -= added;
                if (item.Remaining == 0) item.Marker.SetActive(false);
                notice = added > 0 ? $"{Ammo(item.Family)} · +{added}" + (toBackpack ? " в рюкзак" : "") :
                    toBackpack ? "В рюкзаке нет места для патронов" : "Резерв патронов полон";
                if (added > 0) screen.Feedback?.Play(CityCue.Pickup);
            }
            return true;
        }
    }
}
