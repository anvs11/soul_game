using System;
using Game.Combat;
using Game.Contracts;
using Game.Input;
using UnityEngine;
namespace Game.Actors
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DamageReceiver), typeof(ActorMotor2D))]
    public sealed class PlayerCombatFacade : MonoBehaviour, IPlayerCombatSource
    {
        private DamageReceiver receiver;
        private ActorMotor2D motor;
        private WeaponController weapon;
        private bool controlsEnabled = true;
        private bool deathSent;
        public bool IsDead { get { EnsureReferences(); return receiver.IsDead; } }
        public event Action<CombatSnapshot> Changed;
        public event Action Died;
        public event Action InteractRequested;
        public event Action PauseRequested;
        private void EnsureReferences()
        {
            if (receiver == null) receiver = GetComponent<DamageReceiver>();
            if (motor == null) motor = GetComponent<ActorMotor2D>();
            if (weapon == null) weapon = GetComponent<WeaponController>();
        }
        private void OnEnable()
        {
            EnsureReferences();
            receiver.DamageApplied += OnDamage;
            motor.CommandSampled += OnCommand;
            if (weapon != null) weapon.Changed += PublishState;
            SetControlsEnabled(controlsEnabled);
            PublishState();
        }
        private void OnDisable()
        {
            if (receiver != null) receiver.DamageApplied -= OnDamage;
            if (weapon != null) { weapon.Changed -= PublishState; weapon.SetControlsEnabled(false); }
            if (motor != null) { motor.CommandSampled -= OnCommand; motor.SetControlsEnabled(false); }
        }
        public CombatSnapshot ReadSnapshot()
        {
            EnsureReferences();
            var resources = weapon != null ? weapon.State : null;
            return new CombatSnapshot(receiver.Health, receiver.MaxHealth, receiver.Armor, receiver.MaxArmor,
                resources?.Stamina ?? 100f, resources?.MaxStamina ?? 100f, resources?.Magazine ?? 0, resources?.MagazineCapacity ?? 1,
                resources?.ReserveAmmo ?? 0, resources?.IsMelee ?? false, resources?.IsReloading ?? false, receiver.IsDead);
        }
        public void RestoreForEncounter(CombatSnapshot snapshot)
        {
            EnsureReferences();
            if (deathSent || snapshot.IsDead) throw new InvalidOperationException("Cannot transfer a dead hero.");
            if (snapshot.MaxHealth != receiver.MaxHealth || snapshot.MaxArmor != receiver.MaxArmor ||
                snapshot.MagazineCapacity != weapon.State.MagazineCapacity || snapshot.MaxStamina != weapon.State.MaxStamina)
                throw new InvalidOperationException("Encounter transfer maxima differ from the destination actor.");
            receiver.RestoreForEncounter(snapshot.Health, snapshot.Armor);
            weapon.State.RestoreForEncounter(snapshot.Magazine, snapshot.ReserveAmmo, snapshot.Stamina, snapshot.IsMelee);
            motor.ResetForEncounter();
            PublishState();
        }
        public void SetControlsEnabled(bool enabled)
        {
            EnsureReferences(); controlsEnabled = enabled;
            motor.SetControlsEnabled(enabled && !receiver.IsDead && isActiveAndEnabled);
            if (weapon != null)
            {
                bool wasReloading = weapon.State.IsReloading;
                weapon.SetControlsEnabled(enabled && !receiver.IsDead && isActiveAndEnabled);
                if (wasReloading && !weapon.State.IsReloading) Changed?.Invoke(ReadSnapshot());
            }
        }
        private void OnDamage(DamageResult result) => PublishState();
        private void PublishState()
        {
            bool notifyDeath = receiver.IsDead && !deathSent;
            if (notifyDeath) deathSent = true;
            if (receiver.IsDead) { motor.SetControlsEnabled(false); if (weapon != null) weapon.SetControlsEnabled(false); }
            Changed?.Invoke(ReadSnapshot());
            if (notifyDeath) Died?.Invoke();
        }
        private void OnCommand(PlayerCommand command)
        {
            if (command.PausePressed) PauseRequested?.Invoke();
            if (controlsEnabled && !IsDead && command.InteractPressed) InteractRequested?.Invoke();
        }
    }
}
