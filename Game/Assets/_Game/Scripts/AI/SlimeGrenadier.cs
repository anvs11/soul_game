using System;
using Game.Encounters;
using UnityEngine;

namespace Game.AI
{
    public sealed class SlimeGrenadier : EliteEnemy
    {
        private sealed class Grenade
        {
            public SpriteRenderer Marker;
            public Vector2 Position, Direction;
            public float Range, Timer, TickTimer;
            public int Phase;
            public bool Large;
        }
        private readonly Grenade[] grenades = new Grenade[3];
        private float cooldown = .7f;
        private bool largePending;
        private int slots;
        private Func<int, bool> summon;
        private Action closeSummonReservations;
        public override string EnemyTypeId => EnemyTypes.SlimeGrenadier;
        protected override float Threshold => 100f;
        public int SummonAttempts { get; private set; }
        public int ActiveGrenades { get { int count = 0; foreach (var grenade in grenades) if (grenade != null && grenade.Phase != 0) count++; return count; } }
        public void ConfigureSummons(int count, Func<int, bool> spawnSlot, Action closeReservations)
        {
            slots = Mathf.Clamp(count, 0, 4);
            summon = spawnSlot;
            closeSummonReservations = closeReservations;
        }
        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < grenades.Length; i++)
                grenades[i] = new Grenade { Marker = CreateMarker("Acid grenade", Color.green, circular: true) };
        }
        protected override void BeginSpecial()
        {
            largePending = true;
            for (int i = 0; i < slots; i++)
            {
                SummonAttempts++;
                summon?.Invoke(i);
            }
            closeSummonReservations?.Invoke();
        }
        protected override void Tick(float dt)
        {
            foreach (var grenade in grenades) Advance(grenade, dt);
            cooldown -= dt;
            if (largePending || cooldown <= 0)
            {
                if (TryThrow(largePending))
                {
                    largePending = false;
                    cooldown = 2.5f;
                }
            }
            if (ToHero.magnitude > 5f || CoverBlocksHero) Pursue(1.8f * dt, dt);
        }
        private bool TryThrow(bool large)
        {
            foreach (var grenade in grenades)
            {
                if (grenade.Phase != 0) continue;
                grenade.Large = large;
                grenade.Phase = 1;
                grenade.Position = Body.position;
                grenade.Direction = ToHero.sqrMagnitude > .001f ? ToHero.normalized : Vector2.down;
                grenade.Range = Mathf.Min(8f, ToHero.magnitude);
                grenade.Timer = 0;
                return true;
            }
            return false;
        }
        private void Advance(Grenade grenade, float dt)
        {
            if (grenade.Phase == 0) return;
            float radius = grenade.Large ? 2.8f : 1.6f;
            if (grenade.Phase == 1)
            {
                float intended = Mathf.Min(grenade.Range, 9f * dt);
                float step = WallDistance(grenade.Position, grenade.Direction, intended, .12f);
                grenade.Position += grenade.Direction * step;
                grenade.Range -= step;
                ShowMarker(grenade.Marker, grenade.Position, Vector2.one * .35f, Color.green);
                if (step + .001f < intended || grenade.Range < .001f)
                {
                    grenade.Phase = 2;
                    SignalWarning();
                    grenade.Timer = 1f;
                }
                return;
            }
            grenade.Timer -= dt;
            if (grenade.Phase == 2)
            {
                ShowMarker(grenade.Marker, grenade.Position, Vector2.one * radius * 2,
                    new Color(.8f, 1f, .12f, .22f + .22f * Mathf.PingPong(grenade.Timer * 4, 1)));
                if (grenade.Timer > 0) return;
                if (HeroWithin(grenade.Position, radius)) HurtHero(grenade.Large ? 22 : 12);
                grenade.Phase = 3;
                grenade.Timer = grenade.Large ? 4f : 3f;
                grenade.TickTimer = 1f;
                return;
            }
            float poolRadius = grenade.Large ? 2f : 1.15f;
            ShowMarker(grenade.Marker, grenade.Position, Vector2.one * poolRadius * 2, new Color(.3f, .85f, .05f, .45f));
            grenade.TickTimer -= dt;
            if (grenade.TickTimer <= 0)
            {
                grenade.TickTimer += 1f;
                if (HeroWithin(grenade.Position, poolRadius)) HurtHero(3);
            }
            if (grenade.Timer <= 0)
            {
                grenade.Phase = 0;
                grenade.Marker.enabled = false;
            }
        }
        protected override void ClearEffects()
        {
            largePending = false;
            foreach (var grenade in grenades)
            {
                if (grenade == null) continue;
                grenade.Phase = 0;
                grenade.Marker.enabled = false;
            }
        }
    }
}
