using Game.Editor;
using NUnit.Framework;
namespace Game.Tests
{
    public sealed class Stage2BuildTests
    {
        [Test] public void Stage2ContainsExactlyFourRuntimeScenesInOrder()
        {
            Assert.That(BuildCommands.Stage2ScenePaths(), Is.EqualTo(new[] {
                "Assets/_Game/Scenes/Bootstrap.unity", "Assets/_Game/Scenes/Home.unity",
                "Assets/_Game/Scenes/Stage2CombatSandbox.unity", "Assets/_Game/Scenes/Stage2BossSandbox.unity" }));
        }
        [Test] public void CallerCannotChangeLaterBuildSceneLists()
        {
            var first = BuildCommands.Stage2ScenePaths(); first[0] = "Assets/_Game/Scenes/PoliceBossPreview.unity";
            Assert.That(BuildCommands.Stage2ScenePaths()[0], Is.EqualTo("Assets/_Game/Scenes/Bootstrap.unity"));
        }
    }
}
