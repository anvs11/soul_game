using UnityEngine;

namespace Game.Input
{
    public readonly struct PlayerCommand
    {
        public Vector2 Move { get; }
        public bool AttackHeld { get; }
        public bool SwitchPressed { get; }
        public bool InteractPressed { get; }
        public bool PausePressed { get; }

        public PlayerCommand(Vector2 move, bool attackHeld = false, bool switchPressed = false,
            bool interactPressed = false, bool pausePressed = false)
        {
            Move = Vector2.ClampMagnitude(move, 1f);
            AttackHeld = attackHeld;
            SwitchPressed = switchPressed;
            InteractPressed = interactPressed;
            PausePressed = pausePressed;
        }
    }

    public interface IPlayerInputSource
    {
        PlayerCommand ReadCommand();
    }
}
