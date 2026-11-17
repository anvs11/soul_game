using System.Collections.Generic;
using Game.Actors;
using UnityEngine;

namespace Game.StreetMap
{
    [RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class DoorController : MonoBehaviour
    {
        private BoxCollider2D gate;
        private SpriteRenderer skin;
        private Vector2 dimensions;
        private readonly List<Collider2D> occupants = new List<Collider2D>();
        public bool IsClosed => gate != null && gate.enabled;
        public Bounds GateBounds => new Bounds(transform.position, dimensions);
        public void Configure(Sprite sprite, Vector2 center, Vector2 size)
        {
            transform.position = center;
            dimensions = size; transform.localScale = new Vector3(size.x,size.y,1);
            gate = GetComponent<BoxCollider2D>(); gate.size = Vector2.one; gate.isTrigger = false; gate.enabled = false;
            gameObject.layer = LayerMask.NameToLayer("World");
            skin = GetComponent<SpriteRenderer>(); skin.sprite = sprite; skin.sortingOrder = 2;
            SetClosed(false);
        }
        public bool CanClose()
        {
            Physics2D.SyncTransforms();
            var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(~0);
            Physics2D.OverlapBox(transform.position, dimensions + Vector2.one * .1f, 0, filter, occupants);
            foreach (var occupant in occupants)
                if (occupant != null && occupant != gate && (occupant.GetComponentInParent<ActorIdentity>() != null || occupant.GetComponentInParent<DamageReceiver>() != null)) return false;
            return true;
        }
        public bool SetClosed(bool closed)
        {
            if (closed && !IsClosed && !CanClose()) return false;
            gate.enabled = closed;
            skin.color = closed ? new Color(.85f, .26f, .18f) : new Color(.2f, .7f, .6f, .16f);
            Physics2D.SyncTransforms();
            return true;
        }
    }
}
