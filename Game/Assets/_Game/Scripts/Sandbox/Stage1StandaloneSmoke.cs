#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Actors;
using Game.Combat;
using Game.Core;
using Game.Input;
using Game.Sandbox;
using Game.UI;
using UnityEngine;
namespace Game.Sandbox
{
    public sealed class StandaloneSmokeInput : MonoBehaviour,IPlayerInputSource
    {
        public PlayerCommand Command;
        public PlayerCommand ReadCommand()=>Command;
    }
    public sealed class Stage1StandaloneSmoke : MonoBehaviour
    {
        [Serializable] private sealed class Result { public bool passed; public int victories,defeats,loggedErrors; public string error,unity,platform; }
        private readonly Result result=new Result();
        private string folder;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Launch()
        {
            if(Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(),"--stage1-smoke")<0)return;
            var root=new GameObject("Standalone verification"); DontDestroyOnLoad(root); root.AddComponent<Stage1StandaloneSmoke>();
        }
        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;
        private void OnLog(string message, string stack, LogType type)
        {
            if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) result.loggedErrors++;
        }
        private void Start()
        {
            Application.runInBackground=true;
            folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"smoke-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);StartCoroutine(Guarded());
        }
        private IEnumerator Guarded()
        {
            var run=Run();
            while(true)
            {
                bool more; object current;
                try { more=run.MoveNext(); current=more?run.Current:null; }
                catch(Exception exception) { Finish(exception.ToString());yield break; }
                if(!more)break;
                yield return current;
            }
            Finish(null);
        }
        private static void Require(bool valid,string message) { if(!valid)throw new InvalidOperationException(message); }
        private IEnumerator Run()
        {
            GameBootstrap boot=null; float deadline=Time.realtimeSinceStartup+20;
            while((boot=FindFirstObjectByType<GameBootstrap>())==null || boot.Coordinator==null || boot.Coordinator.Phase!=RunPhase.Home)
            {Require(Time.realtimeSinceStartup<deadline,"Bootstrap did not reach Home");yield return null;}
            var ids=new HashSet<string>();
            for(int index=0;index<11;index++)
            {
                var home=FindFirstObjectByType<HomeScreen>();
                if(home.IsShowingResult)home.PrimaryButton.onClick.Invoke();
                home.PrimaryButton.onClick.Invoke();deadline=Time.realtimeSinceStartup+15;
                while(boot.Coordinator.Phase!=RunPhase.InArena){Require(Time.realtimeSinceStartup<deadline,"Arena load timeout");yield return null;}
                Require(ids.Add(boot.Coordinator.RunId),"Run ID repeated");
                var arena=FindFirstObjectByType<ArenaController>();
                Require(arena!=null && arena.RemainingEnemies==3,"Wrong arena population");
                Require(arena.Player.ReadSnapshot().Magazine==8 && arena.Player.ReadSnapshot().Health==100,"Resources not fresh");
                var motor=arena.Player.GetComponent<ActorMotor2D>();motor.enabled=false;
                var input=arena.Player.gameObject.AddComponent<StandaloneSmokeInput>();motor.Configure(input);motor.enabled=true;
                if(index==0)
                {
                    float wait=Time.realtimeSinceStartup+.15f;while(Time.realtimeSinceStartup<wait)yield return null;
                    if(Array.IndexOf(Environment.GetCommandLineArgs(),"--stage1-screenshot")>=0)
                        ScreenCapture.CaptureScreenshot(Path.Combine(folder,"arena.png"));
                    arena.Pause();var position=arena.Player.transform.position;wait=Time.realtimeSinceStartup+.2f;
                    while(Time.realtimeSinceStartup<wait)yield return null;
                    Require(Time.timeScale==0 && arena.Player.transform.position==position,"Pause did not freeze player");
                    arena.Controls.ResumeButton.onClick.Invoke();Require(Time.timeScale==1,"Resume failed");
                }
                if(index<10)
                {
                    input.Command=new PlayerCommand(Vector2.zero,attackHeld:true);deadline=Time.realtimeSinceStartup+20;
                    while(arena.RemainingEnemies>0)
                    {Require(Time.realtimeSinceStartup<deadline && !arena.Player.IsDead,"Real combat did not win");yield return null;}
                    input.Command=default;Require(arena.Player.ReadSnapshot().Magazine<8 || arena.Player.ReadSnapshot().ReserveAmmo<16,"No real ammo consumed");
                    deadline=Time.realtimeSinceStartup+10;
                    while(Vector2.Distance(arena.Player.transform.position,arena.ExitPosition)>1)
                    {Require(Time.realtimeSinceStartup<deadline,"Could not reach exit");input.Command=new PlayerCommand(((Vector2)(arena.ExitPosition-arena.Player.transform.position)).normalized);yield return null;}
                    input.Command=new PlayerCommand(Vector2.zero,interactPressed:true);
                }
                deadline=Time.realtimeSinceStartup+40;
                while(boot.Coordinator.Phase!=RunPhase.Home)
                {Require(Time.realtimeSinceStartup<deadline,"Result timeout");yield return null;}
                Require(FindFirstObjectByType<HomeScreen>().IsShowingResult,"Result screen absent");
                Require(Time.timeScale==1,"Time scale leaked");
                Require(FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length==0,"Projectile leaked across scenes");
                Require(FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None).Length==1,"Service root duplicated");
                Require(boot.Coordinator.LastOutcome==(index<10?RunOutcome.Victory:RunOutcome.Defeat),"Unexpected outcome");
                if(index<10)result.victories++;else result.defeats++;
                Debug.Log("STAGE1_STANDALONE_CYCLE "+index+" "+boot.Coordinator.LastOutcome);
            }
        }
        private void Finish(string error)
        {
            if(error==null && result.loggedErrors>0) error="Player logged errors: "+result.loggedErrors;
            result.passed=error==null;result.error=error??"";result.unity=Application.unityVersion;result.platform=Application.platform.ToString();
            File.WriteAllText(Path.Combine(folder,"result.json"),JsonUtility.ToJson(result,true));
            Debug.Log("STAGE1_STANDALONE_RESULT "+JsonUtility.ToJson(result));Application.Quit(result.passed?0:1);
        }
    }
}
#endif
