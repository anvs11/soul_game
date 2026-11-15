using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Game.Sandbox
{
    public sealed class HazardBossPreview : MonoBehaviour
    {
        [SerializeField] private PlayerCombatFacade hero;
        [SerializeField] private PoliceBoss boss;
        [SerializeField] private ExplosiveBarrel barrel;
        [SerializeField] private TargetRegistry registry;
        [SerializeField] private HudPresenter hud;
        public void Configure(PlayerCombatFacade h, PoliceBoss b, ExplosiveBarrel c, TargetRegistry r, HudPresenter view)
        { hero = h; boss = b; barrel = c; registry = r; hud = view; }
        private void Start()
        {
            hero.GetComponent<AutoAimSelector>().Configure(registry); hero.SetControlsEnabled(true); hud.Bind(hero);
            if (boss != null) boss.Initialize(registry, hero.transform);
            hud.SetHint("WASD / Space / Q · Esc пауза · F6 треть HP боссу или активация бочки");
            hero.PauseRequested += Pause;
        }
        public static float BossDiagnosticDamage(float maxHealth) => maxHealth / 3f;
        private void Pause() { Time.timeScale = Time.timeScale > 0 ? 0 : 1; }
        private void Update()
        {
            if (boss != null) hud.SetEncounter("Полицейский · HP " + boss.GetComponent<DamageReceiver>().Health + " · стадия " + boss.Level);
            if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame && Time.timeScale > 0)
            {
                var receiver = boss != null ? boss.GetComponent<DamageReceiver>() : barrel.GetComponent<DamageReceiver>();
                receiver.ApplyDamage(new DamagePacket(-1, boss != null ? BossDiagnosticDamage(receiver.MaxHealth) : 150));
            }
        }
        private void OnDisable() { if (hero != null) hero.PauseRequested -= Pause; if (hud != null) hud.Unbind(); Time.timeScale = 1; }
    }
}
