namespace Game.Combat
{
    public readonly struct DamageResult
    {
        public bool Applied { get; }
        public float ArmorLost { get; }
        public float HealthLost { get; }
        public bool Killed { get; }
        public DamageResult(float armorLost, float healthLost, bool killed)
        {
            ArmorLost = armorLost; HealthLost = healthLost;
            Applied = armorLost > 0 || healthLost > 0; Killed = killed;
        }
    }
}
