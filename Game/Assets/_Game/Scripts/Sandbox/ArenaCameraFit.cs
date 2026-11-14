using UnityEngine;
namespace Game.Sandbox
{
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCameraFit : MonoBehaviour
    {
        private void LateUpdate()
        {
            var view=GetComponent<Camera>();
            view.orthographic=true;
            view.orthographicSize=Mathf.Max(8,12/Mathf.Max(.1f,view.aspect));
        }
    }
}
