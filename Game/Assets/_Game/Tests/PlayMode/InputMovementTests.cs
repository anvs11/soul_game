using System.Collections;
using Game.Actors;
using Game.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class InputMovementTests
    {
        [UnityTest]
        public IEnumerator GamepadPressIsOnePulseAndRepeatedReadsShareSnapshot()
        {
            var previousMode = InputSystem.settings.updateMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            var pad = InputSystem.AddDevice<Gamepad>();
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Player");
            map.AddAction("Move", InputActionType.Value, "<Gamepad>/leftStick");
            map.AddAction("Attack", InputActionType.Button, "<Gamepad>/buttonSouth");
            map.AddAction("SwitchWeapon", InputActionType.Button, "<Gamepad>/buttonNorth");
            map.AddAction("Interact", InputActionType.Button, "<Gamepad>/buttonWest");
            map.AddAction("Pause", InputActionType.Button, "<Gamepad>/start");
            asset.AddActionMap(map);
            asset.devices = new InputDevice[] { pad }; // Ignore physical gamepads during this test.
            var go = new GameObject("Test input");
            go.SetActive(false);
            var input = go.AddComponent<LocalInputSource>();
            input.Configure(asset);
            go.SetActive(true);
            try
            {
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North).WithButton(GamepadButton.South));
                InputSystem.Update();
                var first = input.ReadCommand();
                Assert.IsTrue(first.SwitchPressed);
                Assert.IsTrue(first.AttackHeld);
                go.SendMessage("OnApplicationFocus", true);
                go.SendMessage("OnApplicationPause", false);
                Assert.IsTrue(input.ReadCommand().AttackHeld, "Redundant lifecycle notifications must not reset held input.");
                Assert.IsTrue(input.ReadCommand().SwitchPressed, "Same frame is the same command snapshot.");
                yield return null;
                InputSystem.Update();
                Assert.IsFalse(input.ReadCommand().SwitchPressed, "Holding must not create another pulse.");
                Assert.IsTrue(input.ReadCommand().AttackHeld);
                yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState());
                InputSystem.Update();
                Assert.IsFalse(input.ReadCommand().AttackHeld);
                yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North));
                InputSystem.Update();
                Assert.IsTrue(input.ReadCommand().SwitchPressed, "A second press must produce a new pulse.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(asset);
                InputSystem.RemoveDevice(pad);
                InputSystem.settings.updateMode = previousMode;
            }
        }

        [UnityTest]
        public IEnumerator MotorPreservesAnalogInputAndLimitsDiagonalSpeed()
        {
            var go = CreateActor(out var input, out var body, out var motor);
            try
            {
                input.Command = new PlayerCommand(Vector2.right * .5f);
                yield return null;
                yield return new WaitForFixedUpdate();
                Assert.That(body.linearVelocity.magnitude, Is.EqualTo(2.5f).Within(.02f));
                input.Command = new PlayerCommand(Vector2.one);
                yield return null;
                yield return new WaitForFixedUpdate();
                Assert.That(body.linearVelocity.magnitude, Is.EqualTo(5f).Within(.02f));
                motor.SetControlsEnabled(false);
                input.Command = new PlayerCommand(Vector2.right, attackHeld: true, pausePressed: true);
                yield return null;
                Assert.IsTrue(motor.CurrentCommand.PausePressed);
                Assert.IsFalse(motor.CurrentCommand.AttackHeld);
                yield return new WaitForFixedUpdate();
                Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator MotorCannotPassThroughStaticWall()
        {
            var go = CreateActor(out var input, out var body, out _);
            // Keep the test away from any scene objects loaded by another test.
            body.position = new Vector2(1000, 1000);
            var wall = new GameObject("Test wall");
            wall.transform.position = new Vector3(1002, 1000, 0);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(1, 10);
            Physics2D.SyncTransforms();
            try
            {
                input.Command = new PlayerCommand(Vector2.right);
                yield return null;
                for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
                Assert.That(body.position.x, Is.GreaterThan(1000.5f), "Actor must actually move toward wall.");
                Assert.That(body.position.x, Is.LessThan(1001.1f), "Circle radius .5 must remain before wall at 1001.5.");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(wall); }
        }

        private static GameObject CreateActor(out MovementTestInput input, out Rigidbody2D body, out ActorMotor2D motor)
        {
            var go = new GameObject("Test actor");
            go.SetActive(false);
            body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            go.AddComponent<CircleCollider2D>().radius = .5f;
            input = go.AddComponent<MovementTestInput>();
            motor = go.AddComponent<ActorMotor2D>();
            motor.Configure(input);
            go.SetActive(true);
            return go;
        }
    }

    public sealed class MovementTestInput : MonoBehaviour, IPlayerInputSource
    {
        public PlayerCommand Command;
        public PlayerCommand ReadCommand() => Command;
    }
}
