using System.IO;
using Game.Editor;
using NUnit.Framework;
namespace Game.Tests
{
    public sealed class Stage3BuildTests
    {
        [Test] public void SixRuntimeScenesStartWithBootstrapAndPreserveLegacyProfiles()
        {
            var scenes=BuildCommands.Stage3ScenePaths();
            Assert.That(scenes,Is.EqualTo(new[]{"Assets/_Game/Scenes/Bootstrap.unity","Assets/_Game/Scenes/Home.unity",
                "Assets/_Game/Scenes/CombatSandbox.unity","Assets/_Game/Scenes/Stage2CombatSandbox.unity",
                "Assets/_Game/Scenes/Stage2BossSandbox.unity","Assets/_Game/Scenes/Stage3StreetSandbox.unity"}));
            foreach(var path in scenes) Assert.That(File.Exists(path),Is.True,path);
        }
        [Test] public void Stage3SceneListCannotBeMutatedByCaller()
        {
            var paths=BuildCommands.Stage3ScenePaths(); paths[0]="preview";
            Assert.That(BuildCommands.Stage3ScenePaths()[0],Does.EndWith("Bootstrap.unity"));
        }
    }
}
