using System;
using Game.Input;
using UnityEngine;

namespace Game.Actors
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class ActorMotor2D : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField, Min(0f)] private float speed = 5f;
        private Rigidbody2D body;
        private IPlayerInputSource source;
        private bool controlsEnabled = true;
        public PlayerCommand CurrentCommand { get; private set; }
        public event Action<PlayerCommand> CommandSampled;

        private void Awake() => body = GetComponent<Rigidbody2D>();
        private void OnEnable()
        {
            source = inputSource as IPlayerInputSource;
            if (source == null)
            {
                Debug.LogError("ActorMotor2D requires an IPlayerInputSource component.", this);
                enabled = false;
            }
        }

        public void Configure(MonoBehaviour provider)
        {
            if (isActiveAndEnabled) throw new InvalidOperationException("Disable motor before configuring it.");
            if (!(provider is IPlayerInputSource)) throw new ArgumentException("Expected IPlayerInputSource.", nameof(provider));
            inputSource = provider;
        }

        private void Update()
        {
            var command = source.ReadCommand();
            CurrentCommand = controlsEnabled ? command : new PlayerCommand(Vector2.zero, pausePressed: command.PausePressed);
            CommandSampled?.Invoke(CurrentCommand);
        }

        private void FixedUpdate()
        {
            body.linearVelocity = controlsEnabled
                ? Vector2.ClampMagnitude(CurrentCommand.Move, 1f) * speed : Vector2.zero;
        }

        public void SetControlsEnabled(bool value)
        {
            controlsEnabled = value;
            CurrentCommand = default;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void OnDisable()
        {
            CurrentCommand = default;
            if (body != null) body.linearVelocity = Vector2.zero;
        }
    }
}
