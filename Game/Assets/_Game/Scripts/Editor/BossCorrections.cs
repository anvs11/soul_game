using Game.Actors;
using Game.AI;
using UnityEditor;
using UnityEngine;
namespace Game.Editor
{
    public static class BossCorrections
    {
        public static void Apply()
        {
            FoundationScenes.EnsureEditorReady();
            var settings=AssetDatabase.LoadAssetAtPath<PoliceBossDefinition>("Assets/_Game/Prefabs/Actors/Stage2/PoliceBossDefinition.asset");
            settings.ShotDamage=15;settings.SummonLimit=6;EditorUtility.SetDirty(settings);
            const string path="Assets/_Game/Prefabs/Actors/Player.prefab";
            var hero=PrefabUtility.LoadPrefabContents(path);
            var motor=new SerializedObject(hero.GetComponent<ActorMotor2D>());
            motor.FindProperty("speed").floatValue=5.5f;motor.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(hero,path);PrefabUtility.UnloadPrefabContents(hero);AssetDatabase.SaveAssets();
            Debug.Log("BOSS_CORRECTIONS_READY damage15, speed5.5, summon cap6");
        }
    }
}
