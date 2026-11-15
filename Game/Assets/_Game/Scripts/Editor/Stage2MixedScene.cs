using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Sandbox;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Game.Editor
{
    public static class Stage2MixedScene
    {
        public static void Apply()
        {
            FoundationScenes.EnsureEditorReady();
            var settings=AssetDatabase.LoadAssetAtPath<ShootingEnemyDefinition>("Assets/_Game/Prefabs/Actors/Stage2/ShootingEnemyDefinition.asset");
            settings.Windup=.35f;settings.Interval=.8f;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.OpenScene(ScenePaths.Asset(ScenePaths.Stage2CombatSandbox),OpenSceneMode.Single);
            var near=GameObject.Find("NearZombie");
            var hidden=Replace("HiddenZombie","JumpingEnemy");
            var far=Replace("FarZombie","ShootingEnemy");
            near.GetComponent<SpriteRenderer>().color=new Color(.9f,.2f,.15f);
            Marker(hidden,"JumpMark",new Vector2(0,.2f),45);
            Marker(far,"GunMarkL",new Vector2(-.2f,0),0);
            Marker(far,"GunMarkR",new Vector2(.2f,0),0);
            var arena=Object.FindFirstObjectByType<ArenaController>();
            arena.ConfigureMixed(Object.FindFirstObjectByType<PlayerCombatFacade>(),new MonoBehaviour[]{near.GetComponent<SandboxEnemy>(),hidden.GetComponent<JumpingEnemy>(),far.GetComponent<ShootingEnemy>()},Object.FindFirstObjectByType<TargetRegistry>(),Object.FindFirstObjectByType<HudPresenter>(),GameObject.Find("Exit").transform);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("S204_MIXED_SCENE_READY");
        }
        private static GameObject Replace(string name,string prefabName)
        {
            var old=GameObject.Find(name);
            if(old.GetComponent<SandboxEnemy>()==null)return old;
            var position=old.transform.position;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/Stage2/"+prefabName+".prefab");
            var replacement=(GameObject)PrefabUtility.InstantiatePrefab(prefab,old.scene);
            replacement.name=name;replacement.transform.position=position;
            Object.DestroyImmediate(old);
            return replacement;
        }
        private static void Marker(GameObject owner,string name,Vector2 position,float rotation)
        {
            if(owner.transform.Find(name)!=null)return;
            var marker=new GameObject(name).AddComponent<SpriteRenderer>();
            marker.transform.SetParent(owner.transform,false);marker.transform.localPosition=position;
            marker.transform.localScale=new Vector3(.12f,.45f,1);marker.transform.localRotation=Quaternion.Euler(0,0,rotation);
            marker.sprite=owner.GetComponent<SpriteRenderer>().sprite;marker.color=Color.white;marker.sortingOrder=2;
        }
    }
}
