using System.Collections;
using Game.Actors;
using Game.Combat;
using Game.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Game.Tests
{
    public sealed class PlayerHealthTests
    {
        private static GameObject Create(out PlayerCombatFacade facade, out DamageReceiver receiver,
            out MovementTestInput input, out ActorMotor2D motor)
        {
            var go = new GameObject("Health test player"); go.SetActive(false);
            input = go.AddComponent<MovementTestInput>();
            motor = go.AddComponent<ActorMotor2D>(); motor.Configure(input);
            go.GetComponent<Rigidbody2D>().gravityScale = 0;
            receiver = go.AddComponent<DamageReceiver>();
            facade = go.AddComponent<PlayerCombatFacade>();
            return go;
        }
        [UnityTest] public IEnumerator SnapshotDeathAndPauseRespectLifecycle()
        {
            var go = Create(out var facade, out var receiver, out var input, out var motor);
            try
            {
                var initial = facade.ReadSnapshot(); // Before Awake / OnEnable.
                Assert.That(initial.Health, Is.EqualTo(100));
                Assert.That(initial.Armor, Is.EqualTo(30));
                Assert.That(initial.Stamina, Is.EqualTo(100));
                int deaths = 0, pauses = 0, interactions = 0;
                facade.Died += () => { deaths++; Assert.IsTrue(facade.ReadSnapshot().IsDead); };
                facade.PauseRequested += () => pauses++;
                facade.InteractRequested += () => interactions++;
                go.SetActive(true);
                facade.SetControlsEnabled(false);
                input.Command = new PlayerCommand(Vector2.right, attackHeld: true, interactPressed: true, pausePressed: true);
                yield return null;
                input.Command = default;
                Assert.That(pauses, Is.EqualTo(1)); Assert.That(interactions, Is.Zero);
                Assert.That(motor.CurrentCommand.Move, Is.EqualTo(Vector2.zero));
                receiver.ApplyDamage(new DamagePacket(1, 1000));
                Assert.That(deaths, Is.EqualTo(1));
                facade.SetControlsEnabled(true);
                input.Command = new PlayerCommand(Vector2.right, attackHeld: true);
                yield return null;
                yield return new WaitForFixedUpdate();
                Assert.That(go.GetComponent<Rigidbody2D>().linearVelocity, Is.EqualTo(Vector2.zero));
                receiver.ApplyDamage(new DamagePacket(1, 1000));
                go.SetActive(false); go.SetActive(true);
                Assert.That(deaths, Is.EqualTo(1)); Assert.IsTrue(facade.IsDead);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [UnityTest] public IEnumerator ReceiverProtectsForHalfASecondAndDoesNotRegenerate()
        {
            var go = Create(out var facade, out var receiver, out _, out _);
            try
            {
                go.SetActive(true);
                int changes = 0; facade.Changed += _ => changes++;
                Assert.IsTrue(receiver.ApplyDamage(new DamagePacket(1, 10)).Applied);
                Assert.IsFalse(receiver.ApplyDamage(new DamagePacket(1, 10)).Applied);
                Assert.That(receiver.Armor, Is.EqualTo(20));
                Assert.That(changes, Is.EqualTo(1));
                yield return new WaitForSeconds(.55f);
                Assert.That(receiver.Armor, Is.EqualTo(20));
                Assert.IsTrue(receiver.ApplyDamage(new DamagePacket(1, 25)).Applied);
                Assert.That(receiver.Armor, Is.Zero); Assert.That(receiver.Health, Is.EqualTo(95));
                Assert.That(changes, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
