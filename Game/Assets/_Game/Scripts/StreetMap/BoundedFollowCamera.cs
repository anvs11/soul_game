using UnityEngine;

namespace Game.StreetMap
{
    [RequireComponent(typeof(Camera))]
    public sealed class BoundedFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Rect bounds;
        private Camera view;
        public Rect Bounds => bounds;
        public void Configure(Transform hero, Rect mapBounds) { target = hero; bounds = mapBounds; }
        private void Awake() => view = GetComponent<Camera>();
        private void LateUpdate()
        {
            if (target == null) return;
            if (view == null) view = GetComponent<Camera>();
            Vector2 point = Clamp(target.position, bounds, view.orthographicSize, view.aspect);
            transform.position = new Vector3(point.x, point.y, -10);
        }
        public static Vector2 Clamp(Vector2 point, Rect mapBounds, float halfHeight, float aspect)
        {
            float halfWidth = halfHeight * aspect;
            float x = mapBounds.width <= halfWidth * 2 ? mapBounds.center.x : Mathf.Clamp(point.x, mapBounds.xMin + halfWidth, mapBounds.xMax - halfWidth);
            float y = mapBounds.height <= halfHeight * 2 ? mapBounds.center.y : Mathf.Clamp(point.y, mapBounds.yMin + halfHeight, mapBounds.yMax - halfHeight);
            return new Vector2(x, y);
        }
    }
}
