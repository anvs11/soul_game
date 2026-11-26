using System;
using UnityEngine;

namespace Game.StreetMap
{
    public sealed class SewerStage : ScriptableObject
    {
        public const int SegmentCount = 5;
        [SerializeField] private string[] sectionNames;
        [SerializeField] private int[] ammoBySection;
        [SerializeField] private int[] armorBySection;
        internal static string IdAt(int index) => $"4-{index + 1}";
        internal string Section(int index) => sectionNames[index];
        internal int Ammo(int index) => ammoBySection[index];
        internal int Armor(int index) => armorBySection[index];

        internal static SewerStage Load()
        {
            var stage = Resources.Load<SewerStage>("Data/Stages/SewerStage");
            if (stage == null || stage.sectionNames?.Length != SegmentCount ||
                stage.ammoBySection?.Length != SegmentCount || stage.armorBySection?.Length != SegmentCount)
                throw new InvalidOperationException("Missing sewer section catalog.");
            return stage;
        }
    }
}
