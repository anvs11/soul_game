using Game.Actors;
using Game.AI;
using Game.Contracts;
using Game.Core;
using Game.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    internal enum CityCue { Hit, Hurt, Empty, Reload, Pickup, Warning, Victory, Evacuation }

    internal sealed class CityFeedback : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private static readonly AudioClip[] Clips = new AudioClip[8];
        private readonly double[] nextCue = new double[8];
        private AudioSource source;
        private PlayerCombatFacade hero;
        private ActorMotor2D motor;
        private CombatSnapshot previous;
        private Image resultFade;
        private float fadeRemaining;

        internal void Bind(PlayerCombatFacade player)
        {
            hero = player;
            if (!Application.isBatchMode && Camera.main != null && FindFirstObjectByType<AudioListener>() == null)
                Camera.main.gameObject.AddComponent<AudioListener>();
            motor = hero.GetComponent<ActorMotor2D>();
            previous = hero.ReadSnapshot();
            hero.Changed += OnCombatChanged;
            motor.CommandSampled += OnCommand;
            DamageReceiver.Hit += OnHit;
            EliteEnemy.WarningStarted += OnWarning;
        }

        private void OnCombatChanged(CombatSnapshot snapshot)
        {
            if (snapshot.IsReloading && !previous.IsReloading) Play(CityCue.Reload);
            previous = snapshot;
        }

        private void OnCommand(PlayerCommand command)
        {
            if (Time.timeScale <= 0 || !motor.Permissions.CanAttack || !command.AttackHeld) return;
            if (!previous.IsDead && !previous.IsMelee && previous.Magazine == 0 && previous.ReserveAmmo == 0)
                Play(CityCue.Empty, .37);
        }

        private void OnHit(DamageReceiver target)
        {
            if (target.gameObject.scene != gameObject.scene) return;
            Play(target.gameObject == hero.gameObject ? CityCue.Hurt : CityCue.Hit, .12);
        }

        private void OnWarning(EliteEnemy elite)
        {
            if (elite.gameObject.scene == gameObject.scene) Play(CityCue.Warning, .28);
        }

        internal void Play(CityCue cue, double spacing = .10)
        {
            if (Application.isBatchMode) return;
            int index = (int)cue;
            double now = Time.unscaledTimeAsDouble;
            if (now < nextCue[index]) return;
            nextCue[index] = now + spacing;
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0;
                source.volume = .50f;
            }
            if (Clips[index] == null) Clips[index] = CreateCue(cue);
            source.PlayOneShot(Clips[index]);
        }

        internal void ShowResult(RunOutcome outcome, RectTransform canvas)
        {
            if (outcome == RunOutcome.Victory) { Play(CityCue.Victory); return; }
            if (outcome != RunOutcome.Defeat) return;
            Play(CityCue.Evacuation);
            var layer = new GameObject("Затемнение эвакуации", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)layer.transform;
            rect.SetParent(canvas, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            resultFade = layer.GetComponent<Image>();
            resultFade.color = Color.black;
            fadeRemaining = .9f;
        }

        private void Update()
        {
            if (resultFade == null) return;
            fadeRemaining -= Time.unscaledDeltaTime;
            resultFade.color = new Color(0, 0, 0, Mathf.Clamp01(fadeRemaining / .68f));
            if (fadeRemaining <= 0) Destroy(resultFade.gameObject);
        }

        private static AudioClip CreateCue(CityCue cue)
        {
            var (seconds, low, high, noise, beats) = cue switch
            {
                CityCue.Hit => (.12f, 165f, 75f, .65f, 1),
                CityCue.Hurt => (.23f, 91f, 45f, .34f, 1),
                CityCue.Empty => (.09f, 1250f, 240f, .78f, 1),
                CityCue.Reload => (.32f, 430f, 370f, .70f, 3),
                CityCue.Pickup => (.22f, 580f, 1080f, .02f, 2),
                CityCue.Warning => (.46f, 420f, 285f, .08f, 2),
                CityCue.Victory => (.74f, 392f, 782f, .02f, 4),
                _ => (.91f, 55f, 113f, .22f, 1)
            };
            var samples = new float[Mathf.CeilToInt(seconds * SampleRate)];
            uint random = 8;
            float phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / samples.Length;
                float note = Mathf.Floor(time * beats) / Mathf.Max(1, beats - 1);
                float frequency = Mathf.Lerp(low, high, beats > 1 ? note : time);
                phase += frequency * (2f * Mathf.PI / SampleRate);
                random = unchecked(random * 1664525 + 1013904223);
                float texture = ((random >> 16) / 32768f - 1f) * noise;
                float pulse = 1f - time * beats % 1f;
                float envelope = Mathf.Min(i / 112f, 1f) * (1f - time) * Mathf.Lerp(.3f, 1f, pulse);
                samples[i] = .36f * envelope * (Mathf.Sin(phase) * (1f - noise) +
                    .25f * Mathf.Sin(phase * 2) + texture);
            }
            var clip = AudioClip.Create("City " + cue, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (hero != null) hero.Changed -= OnCombatChanged;
            if (motor != null) motor.CommandSampled -= OnCommand;
            DamageReceiver.Hit -= OnHit;
            EliteEnemy.WarningStarted -= OnWarning;
        }
    }
}
