using Game.Combat;
using UnityEngine;

namespace Game.Actors
{
    internal sealed class CityBurn : MonoBehaviour
    {
        private DamageReceiver receiver;
        private SpriteRenderer glow;
        private int sourceId, ticks;
        private float untilTick;

        internal void Ignite(int source)
        {
            sourceId = source;
            if (ticks == 0) untilTick = .56f;
            ticks = 3;
            if (receiver == null) receiver = GetComponent<DamageReceiver>();
            if (glow == null)
            {
                glow = new GameObject("Горение").AddComponent<SpriteRenderer>();
                glow.transform.SetParent(transform, false);
                glow.sprite = Resources.Load<Sprite>("Stage4AreaMarker");
                glow.sortingOrder = 4;
                glow.color = new Color(1f, .4f, .15f, .68f);
            }
            glow.enabled = true;
        }

        private void FixedUpdate()
        {
            if (ticks == 0) return;
            if (receiver.IsDead) { OnDisable(); return; }
            untilTick -= Time.fixedDeltaTime;
            if (untilTick > 0) return;
            --ticks;
            untilTick += .56f;
            receiver.ApplyDamage(new DamagePacket(sourceId, 2f, protectionSecondsOverride: 0));
            if (ticks == 0 || receiver.IsDead) OnDisable();
        }

        private void OnDisable()
        {
            ticks = 0;
            if (glow != null) glow.enabled = false;
        }
    }
}
