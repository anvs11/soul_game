using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.UI;
using UnityEngine;
namespace Game.Sandbox
{
    public sealed class JumpingEnemyPreview : MonoBehaviour
    {
        [SerializeField] private PlayerCombatFacade hero;
        [SerializeField] private JumpingEnemy enemy;
        [SerializeField] private TargetRegistry registry;
        [SerializeField] private HudPresenter hud;
        public void Configure(PlayerCombatFacade h,JumpingEnemy e,TargetRegistry r,HudPresenter v){hero=h;enemy=e;registry=r;hud=v;}
        private void Start(){hero.GetComponent<AutoAimSelector>().Configure(registry);hero.SetControlsEnabled(true);enemy.Initialize(registry,hero.transform);hud.Bind(hero);hud.SetHint("Прыгун: WASD · Space · Q. Жёлтая линия фиксирует рывок. Диагностическая сцена без результата.");}
        private void OnDisable(){if(hud!=null)hud.Unbind();}
    }
}
