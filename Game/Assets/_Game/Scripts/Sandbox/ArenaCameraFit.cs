using UnityEngine;
namespace Game.Sandbox
{
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCameraFit : MonoBehaviour
    {
        [SerializeField] private Vector2 halfExtents = new Vector2(12,8);
        public void Configure(Vector2 value) { halfExtents = value; }
        private void LateUpdate()
        {
            var view=GetComponent<Camera>();
            view.orthographic=true;
            view.orthographicSize=Mathf.Max(halfExtents.y,halfExtents.x/Mathf.Max(.1f,view.aspect));
        }
    }
}
