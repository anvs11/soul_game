using System;
using System.Linq;
using Game.Progression;
using UnityEngine;

namespace Game.StreetMap
{
    public sealed class CityRegionDefinition : ScriptableObject
    {
        [SerializeField] private CityDistrictDefinition[] districts;
        private static CityRegionDefinition cached;

        internal static CityRegionDefinition Load()
        {
            if (cached != null) return cached;
            var region = Resources.Load<CityRegionDefinition>("Data/Regions/CityRegion");
            if (region == null || region.districts == null || region.districts.Length != 3 ||
                region.districts.Any(district => district == null || string.IsNullOrWhiteSpace(district.DisplayName) ||
                    district.SegmentIds?.Count != 5 || district.BossSegments == null ||
                    district.BossSegments.Count == 0 || district.BossSegments.Any(i => i < 1 || i > 5)) ||
                !region.districts.SelectMany(district => district.SegmentIds).SequenceEqual(CityEconomy.FullRoute))
                throw new InvalidOperationException("Каталог CITY должен содержать три района по пять уникальных сегментов.");
            return cached = region;
        }

        public CityDistrictDefinition DistrictAt(int segmentIndex) => districts[segmentIndex / 5];
        public bool HasBoss(int segmentIndex) => DistrictAt(segmentIndex).BossSegments.Contains(segmentIndex % 5 + 1);
    }
}
