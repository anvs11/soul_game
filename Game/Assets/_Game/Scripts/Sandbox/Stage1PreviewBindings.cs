using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.UI;
using UnityEngine;

namespace Game.Sandbox
{
    public sealed class Stage1PreviewBindings : MonoBehaviour
    {
        [SerializeField] private PlayerCombatFacade player;
        [SerializeField] private SandboxEnemy[] enemies;
        [SerializeField] private TargetRegistry registry;
        [SerializeField] private HudPresenter hud;
        public void Configure(PlayerCombatFacade hero, SandboxEnemy[] targets, TargetRegistry targetRegistry, HudPresenter view)
        { player = hero; enemies = targets; registry = targetRegistry; hud = view; }
        private void Start()
        {
            if (player == null || registry == null || hud == null || enemies == null || enemies.Length != 3)
            { Debug.LogError("S1-03: assign player, registry, HUD and exactly three enemies.", this); enabled = false; return; }
            foreach (var enemy in enemies)
                if (enemy == null) { player.SetControlsEnabled(false); Debug.LogError("S1-03: missing enemy.", this); enabled = false; return; }
            player.GetComponent<AutoAimSelector>().Configure(registry);
            foreach (var enemy in enemies) enemy.Initialize(registry, player.transform);
            hud.Bind(player);
        }
        private void OnDestroy() { if (hud != null) hud.Unbind(); }
    }
}
