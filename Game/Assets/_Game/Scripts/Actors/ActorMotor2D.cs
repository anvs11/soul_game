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
        [SerializeField, Min(0f)] private float speed = 5.5f;
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

        public float StunRemaining { get; private set; }
        public void Stun(float seconds)
        {
            if (seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!controlsEnabled || !isActiveAndEnabled) return;
            StunRemaining = Mathf.Max(StunRemaining, seconds);
            if (GetComponent<StunStars>() == null) gameObject.AddComponent<StunStars>();
        }
        private void Update()
        {
            StunRemaining = Mathf.Max(0, StunRemaining - Time.deltaTime);
            var command = source.ReadCommand();
            CurrentCommand = controlsEnabled && Time.timeScale > 0 ? command : new PlayerCommand(Vector2.zero, pausePressed: command.PausePressed);
            CommandSampled?.Invoke(CurrentCommand);
        }

        private float knockRemaining, knockSpeed;
        private Vector2 knockDirection;
        private readonly System.Collections.Generic.List<RaycastHit2D> knockHits = new System.Collections.Generic.List<RaycastHit2D>();
        public bool IsKnockedBack => knockRemaining > 0;
        public void BeginKnockback(Vector2 direction, float distance = 1f, float duration = .15f)
        {
            if (!isActiveAndEnabled || !controlsEnabled || direction.sqrMagnitude < .0001f) return;
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0 || float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0)
                throw new ArgumentOutOfRangeException(nameof(distance));
            knockDirection = direction.normalized; knockSpeed = distance/duration; knockRemaining=duration;
        }
        private void FixedUpdate()
        {
            if (controlsEnabled && knockRemaining > 0)
            {
                float step = knockSpeed * Mathf.Min(Time.fixedDeltaTime,knockRemaining);
                var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(~0);
                body.Cast(knockDirection,filter,knockHits,step+.02f);
                foreach (var hit in knockHits)
                    if (hit.collider != null && hit.collider.GetComponentInParent<ActorIdentity>() == null)
                        step = Mathf.Min(step,Mathf.Max(0,hit.distance-.02f));
                body.linearVelocity = knockDirection * (step/Time.fixedDeltaTime);
                knockRemaining = Mathf.Max(0,knockRemaining-Time.fixedDeltaTime);
                return;
            }
            body.linearVelocity = controlsEnabled && StunRemaining <= 0 ? Vector2.ClampMagnitude(CurrentCommand.Move,1f)*speed : Vector2.zero;
        }

        public void SetControlsEnabled(bool value)
        {
            controlsEnabled = value;
            CurrentCommand = default;
            knockRemaining = 0;
            StunRemaining = 0;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void OnDisable()
        {
            CurrentCommand = default;
            knockRemaining = 0;
            StunRemaining = 0;
            if (body != null) body.linearVelocity = Vector2.zero;
        }
    }
}
