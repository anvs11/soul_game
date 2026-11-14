using System;
using Game.Sandbox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Game.Editor
{
    public static class BoundaryFix
    {
        public static void Apply()
        {
            FoundationScenes.EnsureEditorReady();
            foreach(var path in new[]{"Assets/_Game/Scenes/CombatSandbox.unity","Assets/_Game/Scenes/Stage1ArenaPreview.unity"})
            {
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                foreach(var name in new[]{"North","South","East","West"})
                {
                    GameObject wall=null;
                    foreach(var root in scene.GetRootGameObjects())if(root.name==name)wall=root;
                    if(wall==null || wall.GetComponent<BoxCollider2D>()==null)throw new InvalidOperationException("Missing boundary: "+name);
                    var collider=wall.GetComponent<BoxCollider2D>(); collider.size=Vector2.one;collider.offset=Vector2.zero;collider.isTrigger=false;
                    var scale=wall.transform.localScale;scale.z=1;wall.transform.localScale=scale; EditorUtility.SetDirty(collider);
                }
                foreach(var root in scene.GetRootGameObjects())
                    foreach(var camera in root.GetComponentsInChildren<Camera>())
                        if(camera.GetComponent<ArenaCameraFit>()==null)camera.gameObject.AddComponent<ArenaCameraFit>();
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Scene save failed: "+path);
            }
            Debug.Log("BOUNDARIES_FIXED physical size=1, z scale=1; scene GUIDs preserved");
        }
    }
}
