using System;
using UnityEngine;

namespace Game.StreetMap
{
    internal sealed class SewerLighting : MonoBehaviour
    {
        private Transform hero;
        private Material darkness;
        private static readonly int Lantern = Shader.PropertyToID("_Lantern");

        internal void Initialize(Transform target, Sprite sprite)
        {
            hero = target;
            var shader = Resources.Load<Shader>("SewerDarkness");
            if (shader == null) throw new InvalidOperationException("Missing sewer darkness shader.");
            darkness = new Material(shader);
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = darkness;
            renderer.sortingOrder = 100;
            var bounds = StreetMapLayout.CameraBounds;
            transform.position = bounds.center;
            transform.localScale = new Vector3((bounds.width + 40) / sprite.bounds.size.x,
                (bounds.height + 40) / sprite.bounds.size.y, 1);
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (hero != null && darkness != null) darkness.SetVector(Lantern, hero.position);
        }

        private void OnDestroy()
        {
            if (darkness != null) Destroy(darkness);
        }
    }
}
