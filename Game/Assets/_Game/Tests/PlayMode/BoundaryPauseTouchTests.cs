using System;
using System.Collections;
using Game.Actors;
using Game.AI;
using Game.Core;
using Game.Input;
using Game.Sandbox;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Game.Tests
{
    public sealed class BoundaryPauseTouchTests
    {
        private GameBootstrap bootstrap;
        private ArenaController arena;
        private int basePads;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        [UnitySetUp] public IEnumerator Setup()
        {
            background=InputSystem.settings.backgroundBehavior;
            editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach(var existing in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))Object.Destroy(existing.gameObject);
            Time.timeScale=1;yield return null;
            yield return SceneManager.LoadSceneAsync(ScenePaths.Bootstrap);
            yield return Until(()=> (bootstrap=Object.FindFirstObjectByType<GameBootstrap>())!=null && bootstrap.Coordinator.Phase==RunPhase.Home);
            basePads=Gamepad.all.Count;
            Object.FindFirstObjectByType<HomeScreen>().PrimaryButton.onClick.Invoke();
            yield return Until(()=>bootstrap.Coordinator.Phase==RunPhase.InArena);
            arena=Object.FindFirstObjectByType<ArenaController>();yield return null;
        }
        [UnityTest] public IEnumerator ActualSceneWallsStopMovementKnockbackAndEnemy()
        {
            foreach(var name in new[]{"North","South","West","East"})
            {
                var box=GameObject.Find(name).GetComponent<BoxCollider2D>();
                Assert.That(box.size,Is.EqualTo(Vector2.one));Assert.That(box.isTrigger,Is.False);
            }
            var enemies=Object.FindObjectsByType<SandboxEnemy>(FindObjectsSortMode.None);
            foreach(var enemy in enemies)enemy.enabled=false;
            var motor=arena.Player.GetComponent<ActorMotor2D>();motor.enabled=false;
            var input=arena.Player.gameObject.AddComponent<MovementTestInput>();motor.Configure(input);motor.enabled=true;
            var body=arena.Player.GetComponent<Rigidbody2D>();
            foreach(var direction in new[]{Vector2.right,Vector2.left,Vector2.up,Vector2.down})
            {
                body.position=new Vector2(direction.x*10,direction.y*6); Physics2D.SyncTransforms();
                input.Command=new PlayerCommand(direction);
                yield return new WaitForSeconds(.4f);
                Assert.That(Mathf.Abs(body.position.x),Is.LessThan(10.41f));Assert.That(Mathf.Abs(body.position.y),Is.LessThan(6.41f));
                input.Command=default;yield return null;
                motor.BeginKnockback(direction,3,.15f);yield return new WaitForSeconds(.25f);
                Assert.That(Mathf.Abs(body.position.x),Is.LessThan(10.41f));Assert.That(Mathf.Abs(body.position.y),Is.LessThan(6.41f));
            }
            body.position=new Vector2(-15,0); // Deliberately place target outside to drive an enemy at the real wall.
            var attacker=enemies[0];attacker.GetComponent<Rigidbody2D>().position=new Vector2(-10,0);attacker.enabled=true;
            Physics2D.SyncTransforms();yield return new WaitForSeconds(1);
            Assert.That(attacker.transform.position.x,Is.GreaterThan(-10.42f));
        }
        [UnityTest] public IEnumerator PauseFreezesCombatSubmitCannotLeaveAndRestartResetsTime()
        {
            var motor=arena.Player.GetComponent<ActorMotor2D>();motor.enabled=false;
            var input=arena.Player.gameObject.AddComponent<MovementTestInput>();motor.Configure(input);motor.enabled=true;
            input.Command=new PlayerCommand(Vector2.right,attackHeld:true);yield return new WaitForSeconds(.1f);
            arena.TogglePause();var before=arena.Player.ReadSnapshot();var position=arena.Player.transform.position;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(Time.timeScale,Is.Zero);Assert.That(arena.Player.transform.position,Is.EqualTo(position));
            Assert.That(arena.Player.ReadSnapshot().Magazine,Is.EqualTo(before.Magazine));
            Assert.That(EventSystem.current.sendNavigationEvents,Is.False);
            EventSystem.current.SetSelectedGameObject(arena.Controls.HomeButton.gameObject);
            var pad=InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
                Assert.That(arena.IsPaused,Is.True);Assert.That(bootstrap.Coordinator.Phase,Is.EqualTo(RunPhase.InArena));
            }
            finally {InputSystem.RemoveDevice(pad);}
            arena.Controls.ResumeButton.onClick.Invoke();Assert.That(Time.timeScale,Is.EqualTo(1));
            input.Command=default;yield return null;
            arena.TogglePause();arena.Controls.HomeButton.onClick.Invoke();arena.Leave();
            yield return Until(()=>bootstrap.Coordinator.Phase==RunPhase.Home);
            Assert.That(bootstrap.Coordinator.LastOutcome,Is.EqualTo(RunOutcome.Abandoned));Assert.That(Time.timeScale,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Game.Combat.Projectile>(FindObjectsSortMode.None).Length,Is.Zero);
            Assert.That(Gamepad.all.Count,Is.EqualTo(basePads),"Virtual gamepad must be removed on scene unload.");
            var home=Object.FindFirstObjectByType<HomeScreen>();home.PrimaryButton.onClick.Invoke();home.PrimaryButton.onClick.Invoke();
            yield return Until(()=>bootstrap.Coordinator.Phase==RunPhase.InArena);
            Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(Object.FindFirstObjectByType<ArenaController>().Player.IsDead,Is.False);
        }
        [UnityTest] public IEnumerator TwoPointersMoveAndAttackIndependentlyAndFocusClearsButtons()
        {
            var controls=arena.Controls;var stick=controls.Stick;
            Canvas.ForceUpdateCanvases();
            var center=RectTransformUtility.WorldToScreenPoint(null,stick.transform.position);
            var movePointer=new PointerEventData(EventSystem.current){pointerId=1,position=center};
            var attackPointer=new PointerEventData(EventSystem.current){pointerId=2,position=Vector2.zero};
            var input=arena.Player.GetComponent<LocalInputSource>();input.SendMessage("OnApplicationFocus",true);
            stick.OnPointerDown(movePointer);movePointer.position=center+Vector2.right*90;stick.OnDrag(movePointer);
            controls.Attack.OnPointerDown(attackPointer);yield return null;yield return null;
            var command=input.ReadCommand();Assert.That(command.Move.x,Is.GreaterThan(.1f));Assert.That(command.AttackHeld,Is.True);
            controls.Attack.OnPointerUp(attackPointer);yield return null;yield return null;
            command=input.ReadCommand();Assert.That(command.Move.x,Is.GreaterThan(.1f));Assert.That(command.AttackHeld,Is.False);
            controls.PauseControl.OnPointerDown(new PointerEventData(EventSystem.current){pointerId=3});
            yield return null;yield return null;Assert.That(arena.IsPaused,Is.True);
            controls.ResumeButton.onClick.Invoke();yield return null;
            controls.Attack.OnPointerDown(attackPointer);yield return null;
            arena.SendMessage("OnApplicationFocus",false);input.SendMessage("OnApplicationFocus",false);
            yield return null;input.SendMessage("OnApplicationFocus",true);arena.Resume();yield return null;yield return null;
            command=input.ReadCommand();Assert.That(command.Move.sqrMagnitude,Is.LessThan(.001f));Assert.That(command.AttackHeld,Is.False);
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator DeathWhilePausedCannotResumeOrReactivateCombat()
        {
            arena.Pause();
            var death=new Game.Combat.DamagePacket(-1,999);
            arena.Player.GetComponent<DamageReceiver>().ApplyDamage(in death);
            arena.Resume();
            Assert.That(arena.Player.IsDead,Is.True);
            Assert.That(arena.Player.GetComponent<ActorMotor2D>().CurrentCommand.AttackHeld,Is.False);
            yield return Until(()=>bootstrap.Coordinator.Phase==RunPhase.Home);
            Assert.That(bootstrap.Coordinator.LastOutcome,Is.EqualTo(RunOutcome.Defeat));
            Assert.That(Time.timeScale,Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<HomeScreen>().IsShowingResult,Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach(var existing in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))Object.Destroy(existing.gameObject);
            Time.timeScale=1;yield return null;
            var empty=SceneManager.CreateScene("BoundaryControlsCleanup");SceneManager.SetActiveScene(empty);
            foreach(var name in new[]{ScenePaths.Bootstrap,ScenePaths.Home,ScenePaths.CombatSandbox})
            {var scene=SceneManager.GetSceneByName(name);if(scene.IsValid() && scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
        }
        private static IEnumerator Until(Func<bool> predicate)
        {
            float deadline=Time.realtimeSinceStartup+15;
            while(!predicate()){if(Time.realtimeSinceStartup>deadline)Assert.Fail("Scene transition timeout");yield return null;}
        }
    }
}
