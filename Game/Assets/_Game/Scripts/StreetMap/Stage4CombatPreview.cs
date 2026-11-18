using System.Collections.Generic;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Encounters;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.StreetMap
{
    public sealed class Stage4CombatPreview : MonoBehaviour
    {
        [SerializeField] private PlayerCombatFacade hero;
        private GameObject actors;
        private TargetRegistry registry;
        private Stage4CombatAssets assets;
        private readonly List<RoomPickup> pickups = new List<RoomPickup>();
        private int scenario;

        public void Configure(PlayerCombatFacade player) => hero = player;
        private void Start()
        {
            assets = Resources.Load<Stage4CombatAssets>(Stage4CombatAssets.ResourceName);
            registry = GetComponent<TargetRegistry>();
            hero.GetComponent<AutoAimSelector>().Configure(registry);
            hero.gameObject.AddComponent<PlayerDodgeController>();
            hero.InteractRequested += Collect;
            hero.PauseRequested += TogglePause;
            var hud = gameObject.AddComponent<HudPresenter>();
            hud.Bind(hero);
            hud.SetHint("Тренировка · Shift: кувырок · E: подобрать · Esc: пауза");
            StartScenario(0);
        }
        private void OnGUI()
        {
            if (assets == null) return;
            GUILayout.BeginArea(new Rect(Screen.width - 224, 12, 212, 175), GUI.skin.box);
            if (GUILayout.Button("Гранатомётчик")) StartScenario(0);
            if (GUILayout.Button("Щитоносец")) StartScenario(1);
            if (GUILayout.Button("Медсестра")) StartScenario(2);
            if (GUILayout.Button("Группа 2+1")) StartScenario(3);
#if UNITY_EDITOR
            if (GUILayout.Button("Начать заново"))
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(gameObject.scene.path,
                    new LoadSceneParameters(LoadSceneMode.Single));
#endif
            GUILayout.EndArea();
        }
        private void StartScenario(int index)
        {
            if (hero.IsDead) return;
            if (actors != null) { actors.SetActive(false); Destroy(actors); }
            pickups.Clear();
            actors = new GameObject("Combat preview actors");
            actors.transform.SetParent(transform, false);
            scenario++;
            var types = new[] { EnemyTypes.SlimeGrenadier, EnemyTypes.ShieldShotgun, EnemyTypes.TeleportNurse };
            if (index == 3)
            {
                Spawn(types[0], new Vector2(-4, 2));
                Spawn(types[1], new Vector2(0, 3));
                Spawn(types[0], new Vector2(4, 2));
            }
            else Spawn(types[index], new Vector2(0, 3));
            for (int i = 0; i < 3; i++)
            {
                var pickup = assets.CreatePickup((PickupKind)i, new Vector2(i * 2 - 2, -3), actors.transform);
                pickup.Initialize("preview", "scenario_" + scenario);
                pickups.Add(pickup);
            }
        }
        private void Spawn(string id, Vector2 position)
        {
            var elite = assets.CreateElite(id, actors.transform);
            elite.transform.position = position;
            elite.Initialize(registry, hero);
            if (elite is SlimeGrenadier grenadier)
                grenadier.ConfigureSummons(4, index =>
                {
                    var child = assets.CreateSlime(actors.transform);
                    child.transform.position = position + new Vector2(index - 1.5f, -1.6f);
                    child.Initialize(registry, hero);
                    child.gameObject.SetActive(true);
                    return true;
                }, null);
            elite.gameObject.SetActive(true);
        }
        private void Collect()
        {
            foreach (var pickup in pickups)
                if (pickup != null && Vector2.Distance(hero.transform.position, pickup.transform.position) <= 1.5f &&
                    pickup.TryCollect(hero) > 0) return;
        }
        private void TogglePause() => Time.timeScale = Time.timeScale > 0 ? 0 : 1;
        private void OnDestroy()
        {
            if (hero != null)
            {
                hero.InteractRequested -= Collect;
                hero.PauseRequested -= TogglePause;
            }
            Time.timeScale = 1f;
        }
    }
}
