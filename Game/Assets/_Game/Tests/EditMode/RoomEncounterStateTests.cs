using Game.Encounters;
using Game.StreetMap;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class RoomEncounterStateTests
    {
        [Test]
        public void DelayedWave_AliveZeroKeepsFutureObligationsAndCannotRestart()
        {
            var state=new RoomEncounterState(EncounterExample.OrdinaryPlan("combat_b",true));
            Assert.That(state.PendingCount,Is.EqualTo(2)); Assert.That(state.Begin(),Is.True);
            Assert.That(state.MarkMaterialized("combat_b/0"),Is.True);
            Assert.That(state.ConfirmDeath("combat_b/0"),Is.True);
            Assert.That(state.Phase,Is.EqualTo(RoomPhase.Waiting));
            Assert.That(state.AliveCount,Is.Zero); Assert.That(state.PendingCount,Is.EqualTo(1));
            Assert.That(state.ConfirmDeath("combat_b/0"),Is.False);
            Assert.That(state.MarkMaterialized("combat_b/1"),Is.False);
            state.Tick(0); Assert.That(state.DelayRemaining,Is.EqualTo(2));
            state.Tick(1.9f); Assert.That(state.MarkMaterialized("combat_b/1"),Is.False);
            state.Tick(.2f); Assert.That(state.MarkMaterialized("combat_b/1"),Is.True);
            Assert.That(state.ConfirmDeath("combat_b/1"),Is.True);
            Assert.That(state.Phase,Is.EqualTo(RoomPhase.Cleared));
            Assert.That(state.PendingCount+state.AliveCount,Is.Zero);
            Assert.That(state.Begin(),Is.False); Assert.That(state.ConfirmDeath("combat_b/1"),Is.False);
        }
        [Test]
        public void CancelDuringGap_KeepsUnfulfilledCountAndNeverMaterializesLater()
        {
            var state=new RoomEncounterState(EncounterExample.OrdinaryPlan("r",true)); state.Begin();
            state.MarkMaterialized("r/0"); state.ConfirmDeath("r/0"); state.Cancel(); state.Tick(100);
            Assert.That(state.Phase,Is.EqualTo(RoomPhase.Cancelled));
            Assert.That(state.PendingCount,Is.EqualTo(1));
            Assert.That(state.MarkMaterialized("r/1"),Is.False); Assert.That(state.Begin(),Is.False);
        }
        [Test]
        public void ReservedChildren_ArePendingUntilMaterializedAndOwnerDeathDoesNotKillLiveChild()
        {
            var spawn=new SpawnPlan("owner",EnemyTypes.SlimeGrenadier,"p",2);
            var state=new RoomEncounterState(new RoomPlan("r",new[]{new WavePlan("w",0,new[]{spawn})}));
            state.Begin(); Assert.That(state.PendingCount,Is.EqualTo(3));
            Assert.That(state.CancelUnmaterializedSummons("owner"),Is.Zero,"Cannot cancel a future owner's reservations.");
            Assert.That(state.MarkMaterialized(spawn.SummonId(0)),Is.False);
            state.MarkMaterialized("owner");
            Assert.That(state.MarkMaterialized(spawn.SummonId(0)),Is.True);
            Assert.That(state.MarkMaterialized(spawn.SummonId(0)),Is.False);
            Assert.That(state.MarkMaterialized("owner/summon/99"),Is.False);
            state.ConfirmDeath("owner");
            Assert.That(state.PendingCount,Is.Zero); Assert.That(state.AliveCount,Is.EqualTo(1));
            Assert.That(state.Phase,Is.EqualTo(RoomPhase.Fighting));
            Assert.That(state.ConfirmDeath(spawn.SummonId(0)),Is.True);
            Assert.That(state.Phase,Is.EqualTo(RoomPhase.Cleared));
        }
        [Test]
        public void IndependentOwners_CannotCancelEachOthersObligations()
        {
            var state=new RoomEncounterState(new RoomPlan("r",new[]{new WavePlan("w",0,new[]
            {new SpawnPlan("a",EnemyTypes.SlimeGrenadier,"p",2),new SpawnPlan("b",EnemyTypes.SlimeGrenadier,"q",2)})}));
            state.Begin(); state.MarkMaterialized("a"); state.MarkMaterialized("b");
            Assert.That(state.CancelUnmaterializedSummons("missing"),Is.Zero);
            state.ConfirmDeath("a");
            Assert.That(state.PendingCount,Is.EqualTo(2));
            state.MarkMaterialized("b/summon/0");
            Assert.That(state.CancelUnmaterializedSummons("b"),Is.EqualTo(1));
            Assert.That(state.CancelUnmaterializedSummons("b"),Is.Zero);
            state.ConfirmDeath("b"); Assert.That(state.AliveCount,Is.EqualTo(1));
            state.ConfirmDeath("b/summon/0"); Assert.That(state.Phase,Is.EqualTo(RoomPhase.Cleared));
        }
    }
}
