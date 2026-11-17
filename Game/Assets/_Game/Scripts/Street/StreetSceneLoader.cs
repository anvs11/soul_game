using System;
using Game.Actors;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Sandbox;
using Game.UI;
using UnityEngine;
namespace Game.Street
{
    [DefaultExecutionOrder(-100)]
    public sealed class StreetSceneLoader : MonoBehaviour
    {
        [SerializeField] private StreetModuleDefinition[] catalog;
        public string CurrentModuleId { get; private set; }
        public void Configure(StreetModuleDefinition[] modules) => catalog = modules;
        private void Awake()
        {
            var boot = FindFirstObjectByType<GameBootstrap>();
            if (boot == null || !boot.Coordinator.CurrentRequest.IsStreetRun)
                throw new InvalidOperationException("Open StreetRun through Bootstrap and Home.");
            var run = boot.Coordinator;
            CurrentModuleId = run.StreetRoute.ModuleIds[run.SegmentIndex - 1];
            StreetModuleDefinition definition = null;
            foreach (var candidate in catalog)
                if (candidate != null && candidate.Id == CurrentModuleId) definition = candidate;
            if (definition == null) throw new InvalidOperationException("Missing street module " + CurrentModuleId);
            var module = Instantiate(definition.Prefab).GetComponent<StreetModule>();
            foreach (var perception in module.GetComponentsInChildren<EnemyPerception>())
            {
                perception.enabled = false;
                perception.Configure(10.5f, 14f, 2f, LayerMask.GetMask("World"));
                perception.enabled = true;
            }
            module.Exit.gameObject.AddComponent<StreetExitPortal>().Initialize(definition);
            var hero = FindFirstObjectByType<PlayerCombatFacade>();
            hero.GetComponent<Rigidbody2D>().position = module.Entry.position;
            hero.transform.position = module.Entry.position;
            GetComponent<ArenaController>().ConfigureMixed(hero, module.Enemies,
                FindFirstObjectByType<TargetRegistry>(), FindFirstObjectByType<HudPresenter>(), module.Exit);
        }
    }
}
