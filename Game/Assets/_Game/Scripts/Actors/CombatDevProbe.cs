using Game.Input;
using UnityEngine;

namespace Game.Actors
{
    // Development scene diagnostics; not the game HUD.
    public sealed class CombatDevProbe : MonoBehaviour
    {
        [SerializeField] private ActorMotor2D motor;
        [SerializeField] private DamageReceiver target;
        private int switches, interactions, pauses;
        private void OnEnable() { if (motor != null) motor.CommandSampled += Sample; }
        private void OnDisable() { if (motor != null) motor.CommandSampled -= Sample; }
        private void Sample(PlayerCommand command)
        {
            if (command.SwitchPressed) switches++;
            if (command.InteractPressed) interactions++;
            if (command.PausePressed) pauses++;
        }
        private void OnGUI()
        {
            if (motor == null) return;
            GUILayout.BeginArea(new Rect(12, 12, 620, 390), GUI.skin.box);
            GUILayout.Label("CombatDev_A: pistol + knife; Q switches the two slots");
            GUILayout.Label("WASD / arrows / left stick: move. Space / South: attack selected weapon.");
            GUILayout.Label("Q / North: switch pistol / knife. E / West: interact. Esc / Start: pause request only.");
            GUILayout.Label($"Move: {motor.CurrentCommand.Move}  Attack held: {motor.CurrentCommand.AttackHeld}");
            GUILayout.Label($"Requests — Switch: {switches}  Interact: {interactions}  Pause: {pauses}");
            GUILayout.Label($"Position: {motor.transform.position:F2}  Speed: {motor.GetComponent<Rigidbody2D>().linearVelocity.magnitude:F2}");
            var facade = motor.GetComponent<PlayerCombatFacade>();
            var receiver = motor.GetComponent<DamageReceiver>();
            if (facade != null && receiver != null)
            {
                var snapshot = facade.ReadSnapshot();
                GUILayout.Label($"Health {snapshot.Health}/{snapshot.MaxHealth}  Armor {snapshot.Armor}/{snapshot.MaxArmor}  Dead {snapshot.IsDead}");
                GUILayout.Label(snapshot.IsMelee
                    ? $"Knife — stamina {snapshot.Stamina:F1}/{snapshot.MaxStamina}"
                    : $"Pistol {snapshot.Magazine}/{snapshot.ReserveAmmo}  Reload {snapshot.IsReloading}  Stamina {snapshot.Stamina:F1}");
                var selector = motor.GetComponent<Game.Combat.AutoAimSelector>();
                var weapon = motor.GetComponent<Game.Combat.WeaponController>();
                if (selector != null && weapon != null) GUILayout.Label($"Aim target: {(selector.CurrentTarget != null ? selector.CurrentTarget.ActorId.ToString() : "none")}  Direction: {weapon.AimDirection}");
                var melee = motor.GetComponent<Game.Combat.MeleeAttack>();
                if (melee != null) GUILayout.Label($"Last swing targets: {melee.LastSwingTargetCount}");
                if (target != null) GUILayout.Label($"Temporary target: HP {target.Health}, armor {target.Armor}");
                if (GUILayout.Button("Test damage 12 (0.5s protection)")) receiver.ApplyDamage(new Game.Combat.DamagePacket(0, 12));
                if (GUILayout.Button("Test lethal damage")) receiver.ApplyDamage(new Game.Combat.DamagePacket(0, 1000));
                if (GUILayout.Button("Block controls (Esc still requests pause)")) facade.SetControlsEnabled(false);
                if (GUILayout.Button("Enable controls (cannot revive)")) facade.SetControlsEnabled(true);
            }
            GUILayout.EndArea();
        }
    }
}
