#if UNITY_EDITOR
using System.Collections;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Input;
using Game.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class Stage1PreviewSmokeTests
    {
        private const string ScenePath = "Assets/_Game/Scenes/Stage1ArenaPreview.unity";

        [UnityTest]
        public IEnumerator PreviewLoadsRealActorsAndHudTracksCombat()
        {
            Time.timeScale = 1;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerCombatFacade>();
            var hud = Object.FindFirstObjectByType<HudPresenter>();
            Assert.That(Object.FindObjectsByType<PlayerCombatFacade>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<SandboxEnemy>(FindObjectsSortMode.None).Length, Is.EqualTo(3));
            Assert.That(Object.FindObjectsByType<TargetRegistry>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(hud.IsBound, Is.True);
            Assert.That(hud.LastSnapshot.Health, Is.EqualTo(100));
            Assert.That(hud.LastSnapshot.Armor, Is.EqualTo(30));
            Assert.That(hud.LastSnapshot.Magazine, Is.EqualTo(8));
            Assert.That(hud.LastSnapshot.ReserveAmmo, Is.EqualTo(16));

            // Feed the same command boundary as hardware; do not mutate weapon state.
            var motor = player.GetComponent<ActorMotor2D>();
            motor.enabled = false;
            var input = player.gameObject.AddComponent<MovementTestInput>();
            motor.Configure(input);
            motor.enabled = true;
            var damage = new DamagePacket(-1, 10);
            player.GetComponent<DamageReceiver>().ApplyDamage(in damage);
            Assert.That(hud.LastSnapshot.Armor, Is.EqualTo(20));

            input.Command = new PlayerCommand(Vector2.zero, switchPressed: true);
            yield return null;
            input.Command = default;
            yield return null;
            Assert.That(hud.LastSnapshot.IsMelee, Is.True);
            input.Command = new PlayerCommand(Vector2.zero, attackHeld: true);
            yield return new WaitForSeconds(.1f);
            input.Command = default;
            yield return null;
            Assert.That(hud.LastSnapshot.Stamina, Is.LessThan(100));

            input.Command = new PlayerCommand(Vector2.zero, switchPressed: true);
            yield return null;
            input.Command = default;
            yield return null;
            Assert.That(hud.LastSnapshot.IsMelee, Is.False);
            input.Command = new PlayerCommand(Vector2.zero, attackHeld: true);
            yield return new WaitForSeconds(.3f);
            input.Command = default;
            yield return null;
            Assert.That(hud.LastSnapshot.Magazine, Is.LessThan(8));
            Assert.That(hud.LastSnapshot.Magazine, Is.EqualTo(player.ReadSnapshot().Magazine));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            var preview = SceneManager.GetSceneByPath(ScenePath);
            if (!preview.IsValid() || !preview.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("PreviewTestCleanup"));
            yield return SceneManager.UnloadSceneAsync(preview);
        }
    }
}
#endif
