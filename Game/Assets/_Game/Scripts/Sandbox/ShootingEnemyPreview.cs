using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.UI;
using UnityEngine;
namespace Game.Sandbox
{
    public sealed class ShootingEnemyPreview : MonoBehaviour
    {
        [SerializeField] private PlayerCombatFacade hero;
        [SerializeField] private ShootingEnemy enemy;
        [SerializeField] private TargetRegistry registry;
        [SerializeField] private HudPresenter hud;
        public void Configure(PlayerCombatFacade h,ShootingEnemy e,TargetRegistry r,HudPresenter v){hero=h;enemy=e;registry=r;hud=v;}
        private void Start(){hero.GetComponent<AutoAimSelector>().Configure(registry);hero.SetControlsEnabled(true);enemy.Initialize(registry,hero.transform);hud.Bind(hero);hud.SetHint("Стрелок · WASD / Space / Q · жёлтый цвет означает подготовку выстрела");}
        private void OnDisable(){if(hud!=null)hud.Unbind();}
    }
}
