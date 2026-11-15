using UnityEngine;
namespace Game.Actors
{
    public sealed class StunStars : MonoBehaviour
    {
        private LineRenderer[] stars;
        private Material material;
        public bool Visible => stars != null && stars[0].enabled;
        private void Build()
        {
            material = new Material(Shader.Find("Sprites/Default"));
            stars = new LineRenderer[4];
            for (int index = 0; index < stars.Length; index++)
            {
                var star = new GameObject("Stun star").AddComponent<LineRenderer>();
                star.transform.SetParent(transform, false);
                star.sharedMaterial = material;
                star.useWorldSpace = false;
                star.loop = true;
                star.widthMultiplier = .035f;
                star.startColor = star.endColor = Color.yellow;
                star.sortingOrder = 20;
                star.positionCount = 10;
                for (int point = 0; point < 10; point++)
                {
                    float angle = point * Mathf.PI / 5;
                    float radius = point % 2 == 0 ? .12f : .045f;
                    star.SetPosition(point, new Vector3(Mathf.Sin(angle), Mathf.Cos(angle)) * radius);
                }
                stars[index] = star;
            }
        }
        private void LateUpdate()
        {
            var motor = GetComponent<ActorMotor2D>();
            var health = GetComponent<DamageReceiver>();
            bool visible = motor != null && motor.isActiveAndEnabled && motor.StunRemaining > 0 && health != null && !health.IsDead;
            if (visible && stars == null) Build();
            if (stars == null) return;
            for (int index = 0; index < stars.Length; index++)
            {
                stars[index].enabled = visible;
                float angle = Time.time * 4 + index * Mathf.PI / 2;
                stars[index].transform.localPosition = new Vector3(Mathf.Cos(angle) * .55f, .75f + Mathf.Sin(angle) * .17f, 0);
            }
        }
        private void OnDisable() { if (stars != null) foreach (var star in stars) if (star != null) star.enabled = false; }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
