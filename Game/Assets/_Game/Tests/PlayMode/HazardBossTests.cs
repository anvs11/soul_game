#if UNITY_EDITOR
using System.Collections;
using Game.Actors;
using Game.AI;
using Game.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
namespace Game.Tests
{
    public sealed class HazardBossTests
    {
        private const string Folder="Assets/_Game/Prefabs/Actors/Stage2/";
        private readonly Vector2 origin=new Vector2(23000,23000);
        private GameObject root,hero;
        private PoliceBoss boss;
        private TargetRegistry registry;
        private GameObject Spawn(string name,Vector2 offset)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab"));
            go.transform.SetParent(root.transform);go.transform.position=origin+offset;return go;
        }
        private GameObject Target(Vector2 offset,float hp=1000,float armor=0)
        {
            var go=new GameObject("Blast target");go.SetActive(false);go.transform.SetParent(root.transform);go.transform.position=origin+offset;
            go.AddComponent<ActorIdentity>();go.AddComponent<CircleCollider2D>().radius=.3f;go.AddComponent<BoxCollider2D>().size=Vector2.one*.5f;
            go.AddComponent<DamageReceiver>().Configure(hp,armor,0);go.SetActive(true);return go;
        }
        [UnitySetUp] public IEnumerator Setup()
        {root=new GameObject("Hazard test root");registry=root.AddComponent<TargetRegistry>();Time.timeScale=1;yield return null;}
        [UnityTest] public IEnumerator BarrelDamagesMultipleReceiversOnceWithoutChainAndStunsHero()
        {
            hero=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/Player.prefab"));hero.transform.SetParent(root.transform);hero.transform.position=origin+Vector2.right*1.2f;
            var motor=hero.GetComponent<ActorMotor2D>();motor.enabled=false;var input=hero.AddComponent<MovementTestInput>();motor.Configure(input);motor.enabled=true;
            var enemy=Target(Vector2.left*1.2f);var body=enemy.AddComponent<Rigidbody2D>();body.gravityScale=0;
            var far=Target(Vector2.right*10);var barrel=Spawn("ExplosiveBarrel",Vector2.zero).GetComponent<ExplosiveBarrel>();
            var second=Spawn("ExplosiveBarrel",Vector2.up*2).GetComponent<ExplosiveBarrel>();Physics2D.SyncTransforms();
            var packet=new DamagePacket(-1,10);barrel.GetComponent<DamageReceiver>().ApplyDamage(packet);barrel.GetComponent<DamageReceiver>().ApplyDamage(packet);
            Assert.That(barrel.ExplosionCount,Is.EqualTo(1));Assert.That(second.ExplosionCount,Is.Zero);Assert.That(registry.Count,Is.Zero);
            Assert.That(hero.GetComponent<DamageReceiver>().Armor,Is.Zero);Assert.That(hero.GetComponent<DamageReceiver>().Health,Is.EqualTo(80));
            Assert.That(enemy.GetComponent<DamageReceiver>().Health,Is.EqualTo(950));Assert.That(far.GetComponent<DamageReceiver>().Health,Is.EqualTo(1000));
            yield return new WaitForSeconds(.3f);Assert.That(enemy.transform.position.x,Is.LessThan(origin.x-3));
            Assert.That(motor.StunRemaining,Is.GreaterThan(1.5f));Assert.That(hero.GetComponent<StunStars>().Visible,Is.True);
            var position=hero.transform.position;input.Command=new Game.Input.PlayerCommand(Vector2.up);
            Time.timeScale=0;float remaining=motor.StunRemaining;yield return new WaitForSecondsRealtime(.15f);Assert.That(motor.StunRemaining,Is.EqualTo(remaining));Time.timeScale=1;
            yield return new WaitForSeconds(.3f);Assert.That(Vector2.Distance(position,hero.transform.position),Is.LessThan(.03f));
            yield return new WaitForSeconds(1.6f);Assert.That(motor.StunRemaining,Is.Zero);Assert.That(hero.GetComponent<StunStars>().Visible,Is.False);
        }
        [UnityTest] public IEnumerator WallBlocksBlastAndStunDiesWithHero()
        {
            var victim=Target(Vector2.right*2);var wall=new GameObject("Blast cover");wall.transform.SetParent(root.transform);wall.layer=LayerMask.NameToLayer("World");wall.transform.position=origin+Vector2.right;wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,5);
            var barrel=Spawn("ExplosiveBarrel",Vector2.zero);Physics2D.SyncTransforms();barrel.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,10));Assert.That(victim.GetComponent<DamageReceiver>().Health,Is.EqualTo(1000));
            hero=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Actors/Player.prefab"),root.transform);hero.transform.position=origin+Vector2.left*5;
            hero.GetComponent<ActorMotor2D>().Stun(2);yield return null;hero.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));yield return null;
            Assert.That(hero.GetComponent<ActorMotor2D>().StunRemaining,Is.Zero);Assert.That(hero.GetComponent<StunStars>().Visible,Is.False);
        }
        private void Boss()
        {hero=Target(Vector2.right*12,10000);boss=Spawn("PoliceBoss",Vector2.zero).GetComponent<PoliceBoss>();boss.Initialize(registry,hero.transform);Physics2D.SyncTransforms();}
        [UnityTest] public IEnumerator ExactThresholdsAndSkippedThresholdsAreMonotonic()
        {
            Boss();var hp=boss.GetComponent<DamageReceiver>();hp.ApplyDamage(new DamagePacket(-1,200));Assert.That(boss.Level,Is.EqualTo(2));hp.ApplyDamage(new DamagePacket(-1,200));Assert.That(boss.Level,Is.EqualTo(3));Assert.That(boss.PhaseTransitions,Is.EqualTo(2));
            int deaths=0;boss.Died+=()=>deaths++;hp.ApplyDamage(new DamagePacket(-1,1000));hp.ApplyDamage(new DamagePacket(-1,1000));Assert.That(deaths,Is.EqualTo(1));Assert.That(registry.Count,Is.Zero);
            var other=Spawn("PoliceBoss",Vector2.up*15).GetComponent<PoliceBoss>();other.Initialize(registry,hero.transform);other.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,450));Assert.That(other.Level,Is.EqualTo(3));Assert.That(other.PhaseTransitions,Is.EqualTo(2));yield return null;
        }
        [UnityTest] public IEnumerator PhaseThreeSummonsFourFixedShieldsAndSpinsExactlyThreeTimes()
        {
            Boss();boss.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,450));
            float deadline=Time.time+4;while(boss.Phase!=PoliceBoss.AttackPhase.Spin){Assert.That(Time.time,Is.LessThan(deadline));yield return null;}
            Assert.That(boss.LivingMinions,Is.EqualTo(4));
            foreach(var minion in Object.FindObjectsByType<ShieldMinion>(FindObjectsSortMode.None))Assert.That(minion.GetComponent<DamageReceiver>().MaxArmor,Is.EqualTo(15));
            yield return new WaitForSeconds(3.1f);Assert.That(boss.SpinDegrees,Is.EqualTo(1080).Within(.1f));Assert.That(boss.ShotsFired,Is.GreaterThan(10));Assert.That(boss.LivingMinions,Is.LessThanOrEqualTo(4));
            boss.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));yield return null;Assert.That(boss.LivingMinions,Is.Zero);Assert.That(boss.GetComponent<ProjectilePool>().ActiveCount,Is.Zero);
        }
        [UnityTest] public IEnumerator PauseFullPoolDisableAndDeathDuringPreparationAreSafe()
        {
            Boss();var pool=boss.GetComponent<ProjectilePool>();
            var shot=new ProjectileSpawnRequest(origin+Vector2.up*100,Vector2.up,.01f,100,.12f,~0,boss.ActorId,CombatFaction.Enemy,new DamagePacket(boss.ActorId,10));
            for(int i=0;i<128;i++)Assert.That(pool.TrySpawn(in shot),Is.True);
            Time.timeScale=0;var position=boss.transform.position;yield return new WaitForSecondsRealtime(.2f);Assert.That(boss.Phase,Is.EqualTo(PoliceBoss.AttackPhase.Windup));Assert.That(boss.transform.position,Is.EqualTo(position));Time.timeScale=1;
            yield return new WaitForSeconds(2);Assert.That(boss.ShotsFired,Is.Zero);boss.enabled=false;Assert.That(pool.ActiveCount,Is.Zero);Assert.That(registry.Count,Is.Zero);
            boss.enabled=true;Assert.That(boss.Phase,Is.EqualTo(PoliceBoss.AttackPhase.Windup));boss.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,1000));yield return new WaitForSeconds(1);Assert.That(boss.ShotsFired,Is.Zero);Assert.That(boss.Phase,Is.EqualTo(PoliceBoss.AttackPhase.Dead));
        }
        [UnityTest] public IEnumerator RollStopsAtWorldWallAndUnloadDoesNotEmitDeath()
        {
            Boss();var wall=new GameObject("Roll wall");wall.transform.SetParent(root.transform);wall.layer=LayerMask.NameToLayer("World");wall.transform.position=origin+Vector2.right*4;wall.AddComponent<BoxCollider2D>().size=new Vector2(.5f,10);Physics2D.SyncTransforms();
            yield return new WaitForSeconds(1.5f);Assert.That(boss.transform.position.x,Is.LessThan(origin.x+2));int deaths=0;boss.Died+=()=>deaths++;Object.Destroy(boss.gameObject);yield return null;Assert.That(deaths,Is.Zero);Assert.That(registry.Count,Is.Zero);
        }
        [UnityTest] public IEnumerator SecondPhaseRaisesTwoAndFiresEightRoundsFromEachGun()
        {
            Boss();boss.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,200));
            float deadline=Time.time+3;
            while(boss.Phase!=PoliceBoss.AttackPhase.Roll){Assert.That(Time.time,Is.LessThan(deadline));yield return null;}
            Assert.That(boss.LivingMinions,Is.EqualTo(2));
            deadline=Time.time+4;
            while(boss.Phase!=PoliceBoss.AttackPhase.Reload){Assert.That(Time.time,Is.LessThan(deadline));yield return null;}
            Assert.That(boss.ShotsFired,Is.EqualTo(16));Assert.That(boss.LivingMinions,Is.LessThanOrEqualTo(4));
        }
        [UnityTest] public IEnumerator RealSceneUnloadCleansSummonsBulletsAndBlastWithoutDeaths()
        {
            Boss();var scene=UnityEngine.SceneManagement.SceneManager.CreateScene("Hazard unload "+System.Guid.NewGuid().ToString("N"));
            boss.transform.SetParent(null);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(boss.gameObject,scene);
            boss.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,450));float deadline=Time.time+4;
            while(boss.Phase!=PoliceBoss.AttackPhase.Spin){Assert.That(Time.time,Is.LessThan(deadline));yield return null;}
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            var summons=Object.FindObjectsByType<ShieldMinion>(FindObjectsSortMode.None);Assert.That(summons.Length,Is.EqualTo(4));
            var bullets=new System.Collections.Generic.List<Projectile>();foreach(var bullet in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))if(bullet.gameObject.scene==scene)bullets.Add(bullet);Assert.That(bullets.Count,Is.GreaterThan(0));
            var barrel=Spawn("ExplosiveBarrel",Vector2.up*10);barrel.transform.SetParent(null);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(barrel,scene);
            barrel.GetComponent<DamageReceiver>().ApplyDamage(new DamagePacket(-1,10));int deaths=0;boss.Died+=()=>deaths++;
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
            Assert.That(deaths,Is.Zero);Assert.That(registry.Count,Is.Zero);Assert.That(barrel==null,Is.True);
            foreach(var summon in summons)Assert.That(summon==null,Is.True);foreach(var bullet in bullets)Assert.That(bullet==null,Is.True);
        }
        [UnityTest] public IEnumerator RealProjectileTriggersSingleExplosionWithExactRadiusBoundary()
        {
            var barrel=Spawn("ExplosiveBarrel",Vector2.zero).GetComponent<ExplosiveBarrel>();
            var inside=Target(Vector2.up*3.5f);var outside=Target(Vector2.down*3.52f);Physics2D.SyncTransforms();
            var pool=root.AddComponent<ProjectilePool>();pool.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Combat/Projectile.prefab").GetComponent<Projectile>(),1);
            var shot=new ProjectileSpawnRequest(origin+Vector2.left*5,Vector2.right,100,1,.12f,~0,-1,CombatFaction.Player,new DamagePacket(-1,10));
            Assert.That(pool.TrySpawn(in shot),Is.True);yield return new WaitForSeconds(.2f);
            Assert.That(barrel.ExplosionCount,Is.EqualTo(1));Assert.That(inside.GetComponent<DamageReceiver>().Health,Is.EqualTo(950));Assert.That(outside.GetComponent<DamageReceiver>().Health,Is.EqualTo(1000));Assert.That(pool.ActiveCount,Is.Zero);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {Time.timeScale=1;foreach(var minion in Object.FindObjectsByType<ShieldMinion>(FindObjectsSortMode.None))Object.Destroy(minion.gameObject);Object.Destroy(root);yield return null;}
    }
}
#endif
