using System;

namespace Game.Combat
{
    internal sealed class CityAmmoBackpack
    {
        // 72 volume units fit one 8-, 18-, or 6-round ammo pack exactly.
        private const int PackVolume = 72;
        private const int Capacity = 4 * PackVolume;
        private readonly int[] rounds = new int[3];

        internal static int PackSize(CityFirearm family) => family switch
        {
            CityFirearm.SubmachineGun => 18,
            CityFirearm.Carbine => 6,
            _ => 8
        };

        private static int Volume(CityFirearm family) => PackVolume / PackSize(family);
        private int UsedVolume => rounds[0] * Volume(CityFirearm.Pistol) +
            rounds[1] * Volume(CityFirearm.SubmachineGun) + rounds[2] * Volume(CityFirearm.Carbine);

        internal float Packs => (float)UsedVolume / PackVolume;
        internal int Count(CityFirearm family) => rounds[(int)family];

        internal int Store(CityFirearm family, int offered)
        {
            int accepted = Math.Min(offered, (Capacity - UsedVolume) / Volume(family));
            rounds[(int)family] += accepted;
            return accepted;
        }

        internal int Take(CityFirearm family, Func<int, int> addReserve)
        {
            int index = (int)family;
            if (rounds[index] == 0) return 0;
            int accepted = addReserve(rounds[index]);
            rounds[index] -= accepted;
            return accepted;
        }

        internal void Clear() => Array.Clear(rounds, 0, rounds.Length);
    }
}
