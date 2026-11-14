using System.Collections.Generic;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Contracts;
using Game.Core;
using Game.UI;
using UnityEngine;

namespace Game.Sandbox
{
    public sealed class ArenaController : FlowScreen
    {
        [SerializeField] private PlayerCombatFacade player;
        [SerializeField] private SandboxEnemy[] enemies;
        [SerializeField] private TargetRegistry registry;
        [SerializeField] private HudPresenter hud;
        [SerializeField] private Transform exit;
        private readonly HashSet<IDefeatable> alive = new HashSet<IDefeatable>();
        private RunCoordinator run;
        private BattleControls controls;
        public bool IsPaused { get; private set; }
        public BattleControls Controls => controls;
        public void TogglePause() { if (IsPaused) Resume(); else Pause(); }
        public void Pause() { if(run==null || run.Phase!=RunPhase.InArena || player.IsDead)return; IsPaused=true; controls?.SetPaused(true); Time.timeScale=0; }
        public void Resume() { if(!IsPaused || run==null || run.Phase!=RunPhase.InArena || player.IsDead)return; IsPaused=false; controls?.SetPaused(false); Time.timeScale=1; }
        public void Leave() { if(run==null || run.Phase!=RunPhase.InArena)return; controls?.ClearHeld(); IsPaused=false; Time.timeScale=1; player.SetControlsEnabled(false); run.FinishRun(RunOutcome.Abandoned); }
        private void OnApplicationFocus(bool value) { if(!value)Pause(); }
        private void OnApplicationPause(bool value) { if(value)Pause(); }
        public int RemainingEnemies => alive.Count;
        public Vector3 ExitPosition => exit.position;
        public PlayerCombatFacade Player => player;
        protected override bool ShowView => false;
        protected override string Title => "";
        protected override string Description => "";
        protected override string ButtonLabel => "";
        protected override bool CanAct(RunCoordinator value) => false;
        protected override void Act(RunCoordinator value) { }
        protected override string Message(RunCoordinator value) => "";
        public void Configure(PlayerCombatFacade hero, SandboxEnemy[] targets, TargetRegistry targetsRegistry, HudPresenter view, Transform door)
        { player = hero; enemies = targets; registry = targetsRegistry; hud = view; exit = door; }
        protected override void OnBound(RunCoordinator value)
        {
            DetachCombat();
            if (player == null || enemies == null || enemies.Length != 3 || registry == null || hud == null || exit == null)
                throw new System.InvalidOperationException("Arena requires player, three enemies, registry, HUD and exit.");
            foreach (var enemy in enemies)
                if (enemy == null || !alive.Add(enemy)) throw new System.InvalidOperationException("Unique enemy references required.");
            run = value;
            player.GetComponent<AutoAimSelector>().Configure(registry);
            foreach (var enemy in enemies) { enemy.Initialize(registry, player.transform); enemy.Died += OnEnemyDied; }
            controls=gameObject.GetComponent<BattleControls>() ?? gameObject.AddComponent<BattleControls>();
            controls.Initialize(TogglePause,Resume,Leave);
            if(UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents=false;
            player.PauseRequested += TogglePause;
            player.Died += OnPlayerDied;
            player.InteractRequested += TryExit;
            run.Changed += OnPhaseChanged;
            hud.SetHint("WASD: движение · Space: атака · Q: оружие · E: выход после зачистки");
            hud.Bind(player);
            OnPhaseChanged();
        }
        private void OnEnemyDied()
        {
            foreach (var enemy in enemies) if (enemy != null && enemy.IsDead) alive.Remove(enemy);
            if (exit != null) exit.GetComponent<SpriteRenderer>().color = alive.Count == 0 ? Color.green : Color.red;
        }
        private void OnPlayerDied() => run?.FinishRun(RunOutcome.Defeat);
        public void TryExit()
        {
            if (run == null || run.Phase != RunPhase.InArena || IsPaused || player.IsDead || alive.Count != 0) return;
            if (Vector2.Distance(player.transform.position, exit.position) <= 1.5f) run.FinishRun(RunOutcome.Victory);
        }
        private void OnPhaseChanged()
        {
            bool active = run != null && run.Phase == RunPhase.InArena;
            if (player != null) player.SetControlsEnabled(active);
            if (!active && enemies != null)
                foreach (var enemy in enemies) if (enemy != null) enemy.enabled = false;

            if (active && enemies != null)
                foreach (var enemy in enemies) if (enemy != null && !enemy.IsDead) enemy.enabled = true;
        }
        private void DetachCombat()
        {
            controls?.ClearHeld(); IsPaused=false; Time.timeScale=1;
            if (run != null) run.Changed -= OnPhaseChanged;
            if (player != null) { player.PauseRequested -= TogglePause; player.Died -= OnPlayerDied; player.InteractRequested -= TryExit; player.SetControlsEnabled(false); }
            if (enemies != null) foreach (var enemy in enemies) if (enemy != null) enemy.Died -= OnEnemyDied;
            if (hud != null) hud.Unbind();
            alive.Clear(); run = null;
        }
        protected override void OnDisable() { DetachCombat(); base.OnDisable(); }
        protected override void OnDestroy() { DetachCombat(); base.OnDestroy(); }
        private void OnGUI()
        {
            if (run == null || run.Phase != RunPhase.InArena) return;
            GUI.Label(new Rect(Screen.width / 2 - 200, 15, 500, 35),
                alive.Count > 0 ? "Зомби осталось: " + alive.Count : "Выход открыт: подойдите к зелёному квадрату и нажмите E");
        }
    }
}
