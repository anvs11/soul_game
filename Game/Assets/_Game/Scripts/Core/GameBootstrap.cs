using UnityEngine;

namespace Game.Core
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SceneFlow))]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static GameBootstrap active;
        public RunCoordinator Coordinator { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active = null;

        private void Awake()
        {
            if (active != null && active != this)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }

            active = this;
            DontDestroyOnLoad(gameObject);
            Time.timeScale = 1f;
            var sceneFlow = GetComponent<SceneFlow>();
            Coordinator = new RunCoordinator(sceneFlow);
            sceneFlow.Initialize(Coordinator);
        }

        private void Start() => Coordinator?.Begin();
        private void LateUpdate() => Coordinator?.ResolvePendingFinish();

        private void OnDestroy()
        {
            if (active != this) return;
            Coordinator?.Dispose();
            active = null;
            Time.timeScale = 1f;
        }
    }
}
