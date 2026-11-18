using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
namespace Game.Encounters
{
    public sealed class EliteStageSelection
    {
        public string StageId { get; }
        public string FirstType { get; }
        public string SecondType { get; }
        public string DuplicateType { get; }
        internal EliteStageSelection(string stage, string first, string second, string duplicate)
        { StageId=stage; FirstType=first; SecondType=second; DuplicateType=duplicate; }
    }
    public sealed class EncounterPlanner : IEncounterPlanner
    {
        public const int GeneratorVersion=1, ContentVersion=1, MaximumAttempts=20, MaximumSearchNodes=1024;
        private static readonly string[] Rooms={"entry","combat_a","combat_b","combat_c","elite"};
        private static readonly string[] Stages={"fixed_street_1_1","fixed_street_1_2","fixed_street_1_4"};
        private static readonly string[] Ordinary={EnemyTypes.Melee,EnemyTypes.Jumper,EnemyTypes.Shooter};
        private struct RandomStream
        {
            private uint state;
            public RandomStream(uint seed) { state=seed; }
            public uint Next()
            {
                unchecked { uint z=(state+=0x9e3779b9u); z=(z^(z>>16))*0x85ebca6bu; z=(z^(z>>13))*0xc2b2ae35u; return z^(z>>16); }
            }
            public int Below(int bound) => (int)(Next()%(uint)bound);
            public void Shuffle<T>(T[] array)
            { for(int i=array.Length-1;i>0;i--) { int j=Below(i+1); T t=array[i];array[i]=array[j];array[j]=t; } }
        }
        // Explicit UTF-16 code-unit bytes; never runtime-dependent string.GetHashCode.
        private static uint Hash(string value)
        { unchecked { uint h=2166136261u;foreach(char c in value){h=(h^(byte)c)*16777619u;h=(h^(byte)(c>>8))*16777619u;}return h; } }
        private static RandomStream Stream(int seed,int generator,int content,string key,int attempt=0)
        { unchecked { return new RandomStream((uint)seed ^ Hash(key) ^ (uint)generator*0x632be5abu ^ (uint)content*0x85157af5u ^ (uint)attempt*0x9e3779b9u); } }
        public static ReadOnlyCollection<EliteStageSelection> CreateEliteSchedule(int seed,int generatorVersion=1,int contentVersion=1)
        {
            if(generatorVersion!=GeneratorVersion || contentVersion!=ContentVersion) throw new ArgumentException("Unsupported encounter versions.");
            var random=Stream(seed,generatorVersion,contentVersion,"elite_schedule");
            var types=new[]{EnemyTypes.SlimeGrenadier,EnemyTypes.ShieldShotgun,EnemyTypes.TeleportNurse}; random.Shuffle(types);
            var result=new List<EliteStageSelection>();
            for(int i=0;i<3;i++)result.Add(new EliteStageSelection(Stages[i],types[i],types[(i+1)%3],types[random.Below(2)==0?i:(i+1)%3]));
            return result.AsReadOnly();
        }
        public EncounterResult Generate(EncounterRequest request) => Generate(request,null);
        public EncounterResult Generate(EncounterRequest request,EncounterPlan fallback)
        {
            if(request==null)return EncounterResult.Failure("NULL_REQUEST: request required");
            string context="seed="+request.Seed.ToString(CultureInfo.InvariantCulture)+"; G="+request.GeneratorVersion+"; C="+request.ContentVersion+"; map="+request.Map.MapId+"/"+request.Map.MapVersion+"; ";
            if(request.GeneratorVersion!=GeneratorVersion || request.ContentVersion!=ContentVersion)return EncounterResult.Failure(context+"UNSUPPORTED_VERSION");
            var map=request.Map;
            if(!Stages.Contains(map.MapId) || map.Rooms.Count!=5 || Rooms.Any(id=>!map.Rooms.Any(r=>r.RoomId==id)))return EncounterResult.Failure(context+"MAP_SHAPE: expected fixed five rooms");
            if(map.Rooms.Any(r=>r.IsElite!=(r.RoomId=="elite")))return EncounterResult.Failure(context+"ROOM_KIND");
            if(fallback!=null)
            {
                if(fallback.Seed!=request.Seed || fallback.GeneratorVersion!=request.GeneratorVersion || fallback.ContentVersion!=request.ContentVersion)
                    return EncounterResult.Failure(context+"FALLBACK_METADATA");
                var supplied=EncounterPlanValidator.ValidateStage(map,fallback);
                if(!supplied.IsSuccess)return EncounterResult.Failure(context+"FALLBACK_INVALID: "+supplied.Diagnostic);
            }
            var selection=CreateEliteSchedule(request.Seed,request.GeneratorVersion,request.ContentVersion).Single(s=>s.StageId==map.MapId);
            string reason="No candidates";
            for(int attempt=0;attempt<MaximumAttempts;attempt++)
            {
                var plans=new List<RoomPlan>();bool valid=true;
                foreach(string id in Rooms)
                {
                    var room=map.Rooms.Single(r=>r.RoomId==id);
                    var random=Stream(request.Seed,request.GeneratorVersion,request.ContentVersion,map.MapId+"/"+map.MapVersion.ToString(CultureInfo.InvariantCulture)+"/"+id,attempt);
                    var plan=BuildRoom(room,selection,ref random,out reason);
                    if(plan==null){valid=false;break;}plans.Add(plan);
                }
                if(!valid)continue;
                var output=new EncounterPlan(request.Seed,request.GeneratorVersion,request.ContentVersion,map.MapId,map.MapVersion,plans);
                var check=EncounterPlanValidator.ValidateStage(map,output);
                if(check.IsSuccess)return EncounterResult.Success(output,false,context+"attempts="+(attempt+1)+"; VALID");
                reason=check.Diagnostic;
            }
            string rejection=reason;
            if(fallback==null)
            {
                var rooms=new List<RoomPlan>();
                foreach(string id in Rooms)
                {
                    var random=Stream(request.Seed,1,1,"fallback/"+id);
                    var room=BuildRoom(map.Rooms.Single(r=>r.RoomId==id),selection,ref random,out reason,true);
                    if(room==null)return EncounterResult.Failure(context+"attempts=20; EXHAUSTED: "+rejection+"; FALLBACK_FAILED: "+reason);
                    rooms.Add(room);
                }
                fallback=new EncounterPlan(request.Seed,request.GeneratorVersion,request.ContentVersion,map.MapId,map.MapVersion,rooms);
            }
            if(fallback.Seed!=request.Seed || fallback.GeneratorVersion!=request.GeneratorVersion || fallback.ContentVersion!=request.ContentVersion)
                return EncounterResult.Failure(context+"attempts=20; FALLBACK_METADATA");
            var validation=EncounterPlanValidator.ValidateStage(map,fallback);
            if(!validation.IsSuccess)return EncounterResult.Failure(context+"attempts=20; EXHAUSTED: "+rejection+"; FALLBACK_INVALID: "+validation.Diagnostic);
            return EncounterResult.Success(fallback,true,context+"attempts=20; last rejection="+rejection+"; validated fallback");
        }
        private static RoomPlan BuildRoom(EncounterRoom room,EliteStageSelection elite,ref RandomStream random,out string reason,bool fallback=false)
        {
            reason="ROOM_BUDGET: "+room.RoomId;
            if(room.RoomId=="entry")return new RoomPlan(room.RoomId,Array.Empty<WavePlan>());
            int count,waves;
            string[] types;
            if(room.IsElite)
            {
                count=3;waves=1;types=new[]{elite.FirstType,elite.SecondType,elite.DuplicateType};if(!fallback)random.Shuffle(types);
                if(room.Budget.MinEnemies>3 || room.Budget.MaxEnemies<3 || room.Budget.MaxCost<9 || room.Budget.MaxWaves<1)return null;
            }
            else
            {
                int min=Math.Max(1,room.Budget.MinEnemies),max=Math.Min(room.Budget.MaxEnemies,room.Budget.MaxCost);
                if(max<min || room.Budget.MaxWaves<1)return null;
                count=fallback?min:min+random.Below(max-min+1);
                if(room.RoomId=="combat_b" && (count<2 || room.Budget.MaxWaves<2))return null;
                waves=count>=2 && room.Budget.MaxWaves>=2 && (room.RoomId=="combat_b" || (!fallback && random.Below(2)==0))?2:1;
                var bank=(string[])Ordinary.Clone();if(!fallback)random.Shuffle(bank);types=new string[count];for(int i=0;i<count;i++)types[i]=bank[i%3];
            }
            int capacity=room.Points.Count(p=>!p.IsHeroEntry);
            if(!room.IsElite && count>capacity && room.Budget.MaxWaves>=2)waves=2;
            int lower=Math.Max(1,count-capacity),upper=Math.Min(count-1,capacity);
            if(count>capacity*waves || (waves==2 && lower>upper)){reason="POINT_CAPACITY: "+room.RoomId;return null;}
            int first=waves==2?(fallback?lower:lower+random.Below(upper-lower+1)):count;
            int grenadiers=types.Count(t=>t==EnemyTypes.SlimeGrenadier);
            int summons=grenadiers==0?0:Math.Min(4,room.Budget.MaxSummons/grenadiers);
            var points=room.Points.Where(p=>!p.IsHeroEntry).OrderBy(p=>p.PointId,StringComparer.Ordinal).ToArray();if(!fallback)random.Shuffle(points);
            var result=new List<WavePlan>();int offset=0,nodes=0;
            for(int w=0;w<waves;w++)
            {
                int length=w==0?first:count-first;
                var chosen=new List<SpawnPoint>();
                if(!Place(room,types,offset,length,points,chosen,ref nodes)){reason="POINT_CAPACITY: "+room.RoomId+"; searchNodes="+nodes;return null;}
                var spawns=new List<SpawnPlan>();
                for(int i=0;i<length;i++)spawns.Add(new SpawnPlan(room.RoomId+"/wave/"+w+"/spawn/"+i,types[offset+i],chosen[i].PointId,types[offset+i]==EnemyTypes.SlimeGrenadier?summons:0));
                result.Add(new WavePlan("wave_"+w,w==0?0:2,spawns));offset+=length;
            }
            return new RoomPlan(room.RoomId,result);
        }
        private static bool Place(EncounterRoom room,string[] types,int offset,int count,SpawnPoint[] points,List<SpawnPoint> chosen,ref int nodes)
        {
            if(chosen.Count==count)return true;
            string type=types[offset+chosen.Count];float radius=EnemyTypes.Radius(type);
            foreach(var p in points)
            {
                if(nodes>=MaximumSearchNodes)return false;
                nodes++;
                if(p.ClearanceRadius<radius || !p.AllowedEnemyTypeIds.Contains(type))continue;
                bool blocked=room.Points.Any(h=>h.IsHeroEntry && Overlap(p,radius,h,.45f));
                for(int i=0;i<chosen.Count && !blocked;i++)blocked=Overlap(p,radius,chosen[i],EnemyTypes.Radius(types[offset+i]));
                if(blocked)continue;
                chosen.Add(p);if(Place(room,types,offset,count,points,chosen,ref nodes))return true;chosen.RemoveAt(chosen.Count-1);
            }
            return false;
        }
        private static bool Overlap(SpawnPoint a,float ar,SpawnPoint b,float br)
        { double dx=(double)a.X-b.X,dy=(double)a.Y-b.Y,r=(double)ar+br;return dx*dx+dy*dy<r*r; }
    }
}
