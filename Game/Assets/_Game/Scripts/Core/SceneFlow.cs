using System;
using System.Collections;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    [DisallowMultipleComponent]
    public sealed class SceneFlow : MonoBehaviour, ISceneFlow
    {
        private RunCoordinator coordinator;
        private bool loading;

        public void Initialize(RunCoordinator value)
        {
            if (coordinator != null) throw new InvalidOperationException("SceneFlow already initialized.");
            coordinator = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool TryLoad(string sceneName, Action<bool, string> completed)
        {
            if (loading || coordinator == null || completed == null) return false;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                completed(false, "Scene is missing from Build Settings: " + sceneName);
                return true;
            }

            loading = true;
            Time.timeScale = 1f;
            StartCoroutine(Load(sceneName, completed));
            return true;
        }

        private IEnumerator Load(string sceneName, Action<bool, string> completed)
        {
            AsyncOperation operation = null;
            string error = null;
            try { operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); }
            catch (Exception exception) { error = exception.Message; }

            if (operation == null)
            {
                loading = false;
                completed(false, error ?? "Unity did not create a scene load operation.");
                yield break;
            }

            while (!operation.isDone) yield return null;
            var scene = SceneManager.GetSceneByName(sceneName);
            try
            {
                FlowScreen screen = null;
                int count = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var candidate in root.GetComponentsInChildren<FlowScreen>(true))
                    {
                        screen = candidate;
                        count++;
                    }
                }

                if (count != 1)
                    throw new InvalidOperationException("Expected exactly one FlowScreen in " + sceneName);
                screen.Bind(coordinator);
            }
            catch (Exception exception) { error = exception.Message; }

            loading = false;
            if (error != null) Debug.LogError("SceneFlow: " + error);
            completed(error == null, error);
        }
    }
}
