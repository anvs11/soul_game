using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    [DisallowMultipleComponent]
    public sealed class LocalInputSource : MonoBehaviour, IPlayerInputSource
    {
        [SerializeField] private InputActionAsset actions;
        private InputActionAsset ownedActions;
        private InputActionMap map;
        private InputAction move, attack, switchWeapon, interact, pause;
        private int sampledFrame = -1;
        private PlayerCommand command;
        private bool focused = true;
        private bool suspended;

        private void OnEnable()
        {
            if (actions == null)
            {
                Debug.LogError("LocalInputSource requires Player.inputactions.", this);
                enabled = false;
                return;
            }
            ownedActions = Instantiate(actions);
            ownedActions.devices = actions.devices;
            map = ownedActions.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            attack = map.FindAction("Attack", true);
            switchWeapon = map.FindAction("SwitchWeapon", true);
            interact = map.FindAction("Interact", true);
            pause = map.FindAction("Pause", true);
            sampledFrame = -1;
            RefreshEnabled();
        }

        // Configure while disabled, so every player owns an independent action instance.
        public void Configure(InputActionAsset asset)
        {
            if (isActiveAndEnabled) throw new InvalidOperationException("Disable input before configuring it.");
            actions = asset;
        }

        public PlayerCommand ReadCommand()
        {
            if (!isActiveAndEnabled || map == null || !map.enabled) return default;
            if (sampledFrame == Time.frameCount) return command;
            sampledFrame = Time.frameCount;
            command = new PlayerCommand(move.ReadValue<Vector2>(), attack.IsPressed(),
                switchWeapon.WasPressedThisFrame(), interact.WasPressedThisFrame(), pause.WasPressedThisFrame());
            return command;
        }

        private void OnApplicationFocus(bool value) { if (focused == value) return; focused = value; RefreshEnabled(); }
        private void OnApplicationPause(bool value) { if (suspended == value) return; suspended = value; RefreshEnabled(); }
        private void RefreshEnabled()
        {
            if (map == null) return;
            map.Disable();
            command = default;
            sampledFrame = -1;
            if (focused && !suspended && isActiveAndEnabled) map.Enable();
        }

        private void OnDisable()
        {
            map?.Disable();
            map = null;
            command = default;
            sampledFrame = -1;
            if (ownedActions != null) Destroy(ownedActions);
            ownedActions = null;
        }
    }
}
