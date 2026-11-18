using System;
using Game.AI;
using Game.StreetMap;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class FixedEncounterPrefabSetup
    {
        public static void Create()
        {
            const string path = "Assets/_Game/Resources/FixedEncounterPrefabs.asset";
            if (System.IO.File.Exists(path) || System.IO.File.Exists(path + ".meta"))
                throw new InvalidOperationException("Existing prefab references preserved.");
            var asset = ScriptableObject.CreateInstance<FixedEncounterPrefabs>();
            asset.Configure(Load<SandboxEnemy>("SandboxEnemy"), Load<JumpingEnemy>("Stage2/JumpingEnemy"), Load<ShootingEnemy>("Stage2/ShootingEnemy"));
            if (!asset.IsComplete) throw new InvalidOperationException("Missing fixed encounter prefab.");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
        }
        private static T Load<T>(string name) where T : Component =>
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/" + name + ".prefab")?.GetComponent<T>();
    }
}
