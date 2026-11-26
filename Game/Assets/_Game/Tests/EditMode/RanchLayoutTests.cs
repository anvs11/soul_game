using System.Linq;
using Game.Encounters;
using Game.StreetMap;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class RanchLayoutTests
    {
        [Test]
        public void OneHundredRanchSeedsAreValidRepeatableAndDistinct()
        {
            var map = RanchLayout.CreateMap();
            Assert.That(map.MapId, Is.EqualTo("ranch/5-1"));
            Assert.That(RanchLayout.Corridors.Select(corridor => corridor.height), Is.All.EqualTo(4));
            var fingerprints = Enumerable.Range(-50, 100).Select(seed =>
            {
                var plan = RanchLayout.Plan(seed);
                Assert.That(EncounterPlanValidator.Validate(map, plan).IsSuccess, Is.True);
                Assert.That(plan.Rooms[0].Waves, Is.Empty);
                foreach (var room in plan.Rooms.Skip(1).Take(2))
                {
                    var enemies = room.Waves.Single().Spawns;
                    Assert.That(enemies.Count, Is.InRange(4, 6));
                    Assert.That(enemies.Select(enemy => enemy.PointId).Distinct().Count(), Is.EqualTo(enemies.Count));
                    Assert.That(enemies.Select(enemy => enemy.EnemyTypeId).Distinct().Count(), Is.EqualTo(3));
                }
                var deer = plan.Rooms[3].Waves.Single().Spawns.Single();
                Assert.That(deer.EnemyTypeId, Is.EqualTo(EnemyTypes.RanchDeer));
                Assert.That(deer.SpawnId, Is.EqualTo("ranch/5-1/barn/deer"));
                string fingerprint = Fingerprint(plan);
                Assert.That(Fingerprint(RanchLayout.Plan(seed)), Is.EqualTo(fingerprint));
                return fingerprint;
            }).ToArray();
            Assert.That(fingerprints.Distinct().Count(), Is.GreaterThan(2));
        }

        private static string Fingerprint(EncounterPlan plan) => string.Join("|", plan.Rooms
            .SelectMany(room => room.Waves)
            .SelectMany(wave => wave.Spawns)
            .Select(spawn => spawn.SpawnId + ":" + spawn.EnemyTypeId + ":" + spawn.PointId));
    }
}
