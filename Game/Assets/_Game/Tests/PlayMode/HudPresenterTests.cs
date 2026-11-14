using System;
using Game.Contracts;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class HudPresenterTests
    {
        private sealed class Source : IPlayerCombatSource
        {
            public CombatSnapshot Value;
            public bool IsDead => Value.IsDead;
            public event Action<CombatSnapshot> Changed;
            public event Action Died { add { } remove { } }
            public event Action InteractRequested { add { } remove { } }
            public event Action PauseRequested { add { } remove { } }
            public void SetControlsEnabled(bool enabled) { }
            public CombatSnapshot ReadSnapshot() => Value;
            public void Publish(float health)
            {
                Value = new CombatSnapshot(health, 100, 30, 30, 100, 100, 8, 8, 16, false, false, false);
                Changed?.Invoke(Value);
            }
        }
        [Test]
        public void BindReadsImmediatelyAndSwitchingSourceDetachesPrevious()
        {
            var root = new GameObject("HUD test");
            try
            {
                var hud = root.AddComponent<HudPresenter>();
                var first = new Source(); first.Publish(100);
                var second = new Source(); second.Publish(80);
                hud.Bind(first);
                Assert.That(hud.LastSnapshot.Health, Is.EqualTo(100));
                hud.Bind(second);
                first.Publish(10);
                Assert.That(hud.LastSnapshot.Health, Is.EqualTo(80));
                second.Publish(70);
                Assert.That(hud.LastSnapshot.Health, Is.EqualTo(70));
                hud.Unbind(); second.Publish(20);
                Assert.That(hud.IsBound, Is.False);
                Assert.That(hud.LastSnapshot.Health, Is.EqualTo(70));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void DisablingHudDetachesSource()
        {
            var root = new GameObject("HUD disabled test");
            try
            {
                var hud = root.AddComponent<HudPresenter>();
                var source = new Source(); source.Publish(100);
                hud.Bind(source); root.SetActive(false); source.Publish(15);
                Assert.That(hud.IsBound, Is.False);
                Assert.That(hud.LastSnapshot.Health, Is.EqualTo(100));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
