#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using Game.Actors;
using Game.Combat;
using Game.Core;
using Game.Street;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Game.Sandbox
{
    // Explicit opt-in automated Player smoke, never a normal-input playthrough.
    public sealed class Stage3PlayerValidation : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Launch()
        {
            if(Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(),"--stage3-smoke")<0) return;
            var go=new GameObject("Stage3 automated Player validation"); DontDestroyOnLoad(go); go.AddComponent<Stage3PlayerValidation>();
        }
        private IEnumerator Start()
        {
            Debug.Log("S308_DEVICE model="+SystemInfo.deviceModel+" os="+SystemInfo.operatingSystem+" cpu="+SystemInfo.processorType+" gpu="+SystemInfo.graphicsDeviceName+" guid="+Application.buildGUID);
            // Scene zero is checked in the packaged Player, not inferred from builder arguments.
            for(int i=0;i<SceneManager.sceneCountInBuildSettings;i++) Debug.Log("S308_SCENE index="+i+" path="+SceneUtility.GetScenePathByBuildIndex(i));
            if(SceneManager.sceneCountInBuildSettings!=6 || SceneUtility.GetScenePathByBuildIndex(0)!="Assets/_Game/Scenes/Bootstrap.unity") { Fail("scene manifest mismatch"); yield break; }
            float started=Time.realtimeSinceStartup;
            foreach(int seed in new[]{0,123})
            {
                yield return WaitFor(()=>Boot()!=null && Boot().Coordinator.Phase==RunPhase.Home);
                var home=FindFirstObjectByType<HomeScreen>(); if(home.IsShowingResult) home.PrimaryButton.onClick.Invoke();
                home.SeedInput.text=seed.ToString(System.Globalization.CultureInfo.InvariantCulture); home.StreetButton.onClick.Invoke();
                yield return WaitFor(()=>Boot().Coordinator.Phase==RunPhase.InArena);
                var run=Boot().Coordinator;
                for(int index=1;index<=5;index++)
                {
                    var arena=FindFirstObjectByType<ArenaController>();
                    var actual=FindFirstObjectByType<StreetSceneLoader>().CurrentModuleId;
                    var expected=run.StreetGeneration.Layout.Segments[index-1].ModuleId;
                    if(actual!=expected || run.SegmentIndex!=index || run.StreetGeneration.Layout.Seed!=seed) { Fail("seed/segment mismatch"); yield break; }
                    Debug.Log("S308_SEGMENT seed="+seed+" G="+run.StreetGeneration.Layout.GeneratorVersion+" C="+run.StreetGeneration.Layout.ContentVersion+" index="+index+" module="+actual);
                    for(int frame=0;frame<30;frame++) yield return null;
                    foreach(var target in arena.Combatants) ((MonoBehaviour)target).GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));
                    if(index<5) { arena.Player.transform.position=arena.ExitPosition; Physics2D.SyncTransforms(); arena.TryExit(); arena.TryExit();
                        yield return WaitFor(()=>run.Phase==RunPhase.InArena && run.SegmentIndex==index+1); }
                    else yield return WaitFor(()=>run.Phase==RunPhase.Home);
                }
                if(run.LastOutcome!=RunOutcome.Victory) { Fail("expected victory"); yield break; }
            }
            while(Time.realtimeSinceStartup-started<7) yield return null;
            Debug.Log("S308_SMOKE_OK automated seeds0,123; not manual combat"); Application.Quit(0);
        }
        private static GameBootstrap Boot()=>FindFirstObjectByType<GameBootstrap>();
        private static IEnumerator WaitFor(Func<bool> predicate)
        {
            float deadline=Time.realtimeSinceStartup+20;
            while(!predicate()) { if(Time.realtimeSinceStartup>deadline) { Fail("transition timeout"); yield break; } yield return null; }
        }
        private static void Fail(string reason) { Debug.LogError("S308_SMOKE_FAILED "+reason); Application.Quit(2); }
    }
}
#endif
