using Game.AI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Game.Tests
{
    public sealed class HazardBossAssetTests
    {
        [Test] public void PoliceSilhouetteIsFiveTimesHeroAndPrefabsAreIndependent()
        {
            var boss=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/Stage2/PoliceBoss.prefab");
            var hero=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/Player.prefab");
            Assert.That(boss.transform.Find("Police body").localScale.y/hero.transform.localScale.y,Is.EqualTo(5).Within(.01f));
            Assert.IsNull(boss.GetComponent<SandboxEnemy>());
            var barrel=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/Stage2/ExplosiveBarrel.prefab");
            Assert.IsNull(barrel.GetComponent<Game.Combat.IEnemyTarget>());
            foreach(var scene in EditorBuildSettings.scenes)Assert.That(scene.path,Does.Not.Contain("PoliceBossPreview").And.Not.Contain("BarrelPreview"));
        }
        [Test] public void DefinitionRejectsInvalidTimersAndPool()
        {
            var value=ScriptableObject.CreateInstance<PoliceBossDefinition>();
            try {value.Validate();value.Reload=float.NaN;Assert.Throws<System.ArgumentOutOfRangeException>(()=>value.Validate());value.Reload=2;value.PoolLimit=1;Assert.Throws<System.ArgumentOutOfRangeException>(()=>value.Validate());}
            finally {Object.DestroyImmediate(value);}
        }
    }
}
