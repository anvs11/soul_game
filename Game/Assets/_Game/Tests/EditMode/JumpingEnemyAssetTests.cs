using System;
using Game.AI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Game.Tests
{
    public sealed class JumpingEnemyAssetTests
    {
        [Test] public void DefinitionRejectsInvalidParameters()
        {
            var value=ScriptableObject.CreateInstance<JumpingEnemyDefinition>();
            try {value.Validate();value.DashDuration=float.NaN;Assert.Throws<ArgumentOutOfRangeException>(()=>value.Validate());}
            finally {UnityEngine.Object.DestroyImmediate(value);}
        }
        [Test] public void PrefabHasIndependentBehaviorAndPreviewIsNotInBuildSettings()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/Stage2/JumpingEnemy.prefab");
            Assert.IsNotNull(prefab);Assert.IsNotNull(prefab.GetComponent<JumpingEnemy>());Assert.IsNotNull(prefab.GetComponent<EnemyPerception>());
            Assert.IsNull(prefab.GetComponent<SandboxEnemy>());
            var data=new SerializedObject(prefab.GetComponent<JumpingEnemy>());
            Assert.IsNotNull(data.FindProperty("definition").objectReferenceValue);
            foreach(var scene in EditorBuildSettings.scenes)Assert.That(scene.path,Does.Not.Contain("JumpingEnemyPreview"));
        }
    }
}
