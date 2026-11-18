using System.Collections.Generic;
using Game.Encounters;
using UnityEngine;

namespace Game.StreetMap
{
    public static class StreetMapCatalog
    {
        public static IEnumerable<Vector2> SummonCandidates(Vector2 origin, int slot)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = ((i + slot * 2) % 8) * Mathf.PI / 4f;
                yield return origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.5f;
            }
        }
        public static EncounterMap Create()
        {
            var result=new List<EncounterRoom>();
            var ordinary=new[]{EnemyTypes.Melee,EnemyTypes.Jumper,EnemyTypes.Shooter};
            var elites=new[]{EnemyTypes.SlimeGrenadier,EnemyTypes.ShieldShotgun,EnemyTypes.TeleportNurse};
            for(int i=0;i<5;i++)
            {
                var r=StreetMapLayout.Rooms[i];var points=new List<SpawnPoint>();
                Vector2 entry=i==0?Vector2.zero:i==2?new Vector2(0,-6):new Vector2(-r.Bounds.width/2f+2,0);
                points.Add(new SpawnPoint("hero_entry",entry.x,entry.y,.6f,System.Array.Empty<string>(),true));
                if(i>0 && i<4)
                {
                    var bank=new[]{new Vector2(-6,-4),new Vector2(0,-4),new Vector2(6,-4),new Vector2(-6,2),new Vector2(0,2),new Vector2(6,2),new Vector2(-3,5),new Vector2(3,5)};
                    for(int p=0;p<bank.Length;p++)points.Add(new SpawnPoint("ordinary_"+p,bank[p].x,bank[p].y,.6f,ordinary));
                }
                if(i==4)for(int p=0;p<3;p++)points.Add(new SpawnPoint("elite_"+p,-7+p*7,4,1,elites));
                result.Add(new EncounterRoom(r.Id,r.Center.x,r.Center.y,r.Bounds.width,r.Bounds.height,
                    i==0?new RoomBudget(0,0,0,0):i==4?new RoomBudget(3,3,9,1,12):new RoomBudget(6,8,8,2),points,i==4));
            }
            return new EncounterMap("fixed_street_1_1",2,result);
        }
        public static HashSet<Vector2Int> Reachable(float radius)
        {
            Physics2D.SyncTransforms();int mask=LayerMask.GetMask("World");
            var floor=new HashSet<Vector2Int>(StreetMapLayout.WalkableCells());var seen=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();
            var start=Vector2Int.FloorToInt(StreetMapLayout.Entry);seen.Add(start);queue.Enqueue(start);
            var dirs=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
            while(queue.Count>0)
            {
                var cell=queue.Dequeue();var center=(Vector2)cell+Vector2.one*.5f;
                foreach(var dir in dirs)
                {
                    var next=cell+dir;var point=(Vector2)next+Vector2.one*.5f;
                    if(!floor.Contains(next)||seen.Contains(next)||Physics2D.OverlapCircle(point,radius,mask)||Physics2D.CircleCast(center,radius,dir,1,mask))continue;
                    seen.Add(next);queue.Enqueue(next);
                }
            }
            return seen;
        }
    }
}
