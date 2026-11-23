using System.Collections.Generic;
using UnityEngine;

namespace Game.StreetMap
{
    public sealed class CityDistrictDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private string[] segmentIds;
        [SerializeField] private int[] bossSegments;
        public string DisplayName => displayName;
        public IReadOnlyList<string> SegmentIds => segmentIds;
        public IReadOnlyList<int> BossSegments => bossSegments;
    }
}
