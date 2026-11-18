using Game.StreetMap;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MapCatalogAssetTests
    {
        [TestCase("Assets/_Game/Prefabs/Actors/Player.prefab",.45f)]
        [TestCase("Assets/_Game/Prefabs/Actors/SandboxEnemy.prefab",.6f)]
        [TestCase("Assets/_Game/Prefabs/Actors/Stage2/JumpingEnemy.prefab",.6f)]
        [TestCase("Assets/_Game/Prefabs/Actors/Stage2/ShootingEnemy.prefab",.6f)]
        public void ExportClearance_EnclosesExistingPhysicalCollider(string path,float clearance)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Assert.That(prefab,Is.Not.Null);
            var colliders=prefab.GetComponentsInChildren<Collider2D>();Assert.That(colliders.Length,Is.GreaterThan(0));
            foreach(var collider in colliders)
            {
                if(collider.isTrigger)continue;
                Assert.That(collider,Is.TypeOf<CircleCollider2D>(),"Update exporter certification if actor shape changes.");
                var circle=(CircleCollider2D)collider;var scale=circle.transform.lossyScale;
                Assert.That((circle.radius+circle.offset.magnitude)*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y)),Is.LessThanOrEqualTo(clearance));
            }
        }
        [Test]public void ExpandedCatalog_UsesAllOrdinaryTypesAndKeepsHeroEntrySeparate()
        {
            var map=StreetMapCatalog.Create();Assert.That(map.MapVersion,Is.EqualTo(3));
            for(int i=1;i<4;i++)
            {Assert.That(map.Rooms[i].Budget.MinEnemies,Is.EqualTo(6));Assert.That(map.Rooms[i].Budget.MaxEnemies,Is.EqualTo(8));foreach(var point in map.Rooms[i].Points)Assert.That(point.AllowedEnemyTypeIds.Count,Is.EqualTo(point.IsHeroEntry?0:3));}
        }
    }
}
