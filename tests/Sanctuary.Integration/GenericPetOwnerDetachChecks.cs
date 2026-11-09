// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GenericPetOwnerDetachChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetOwnerDetachChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int N(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static bool B(JsonElement e,string key)=>e.GetProperty(key).GetBoolean();
    private static int? Maybe(JsonElement e,string key)=>e.GetProperty(key).ValueKind==JsonValueKind.Null?null:N(e,key);
    private static int Id(IEntity e)=>checked((int)e.Serial.Value);
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-owner-detach-check","Current player pet-store detach and packed context",_=>Core.LoopContext.Post(()=>
    {try{Source();Log.Information("pet-owner-detach-check: passed");}catch(Exception e){Log.Error(e,"pet-owner-detach-check: failed");}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-owner-detach-cases.json")));int contexts=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var wanted=row.GetProperty("output");var trace=new List<string>();
            var ids=p.GetProperty("ids").EnumerateArray().Select(v=>v.GetInt32()).ToArray();var flags=p.GetProperty("flags").EnumerateArray().Select(v=>v.GetUInt32()).ToArray();
            var nodes=ids.Select((id,i)=>new GenericPetOwnedEntry(flags[i],id,(uint)(0x12340000+i),(ushort)(N(p,"field0c")+i),500+i,i+1<ids.Length?i+2:0)).ToArray();
            var buckets=Enumerable.Range(0,32).Select(_=>new GenericPetOwnerBucket(0)).ToArray();buckets[N(p,"type")].First=1;
            int kind=N(p,"kind"),count=N(p,"count"),petId=N(p,"id");bool hasStore=B(p,"store");byte gameSelector=(byte)N(p,"gameSelector");var client=new GenericPetOwnerClient(31);uint? applyFlags=null;
            var store=new GenericPetOwnerStoreServices(owner=>new(kind,B(p,"data"),hasStore),_=>hasStore?1:0,_=>(byte)N(p,"ownerSelector"),_=>count,
                (_,type)=>buckets[type],node=>nodes[node-1]);
            GenericPetAiGame Game()=>new(0x12345678,2,gameSelector,3);
            GenericPetAiUnit Unit()=>new(N(p,"petKind"),742,B(p,"petData"),null,B(p,"snapshot")?(byte)4:(byte)0,p.GetProperty("snapshotValue").GetUInt64());
            var services=new GenericPetOwnerDetachServices(store,_=>petId,_=>Game(),_=>Unit(),(game,owner,id,zero)=>
            {
                Require(game==(B(p,"game")?71:0)&&owner==100&&zero==0,"Detach count callback context changed");trace.Add("remove:"+id);
                petId=Maybe(p,"changeId")??petId;if(B(p,"dropStore")){hasStore=false;}
                gameSelector=(byte)(Maybe(p,"changeSelector")??gameSelector);count=Maybe(p,"changeCount")??count;
            },(pet,nonOne,value)=>
            {
                Require(pet==(B(p,"pet")?101:0),"Detach status callback lost pet");trace.Add($"apply:{(nonOne?1:0)}:{value}");applyFlags=nodes[0].Flags;
                kind=Maybe(p,"afterApplyKind")??kind;if(Maybe(p,"afterApplyField")is{} field){nodes[0].Field0c=(ushort)field;}return N(p,"result");
            },_=>B(p,"client")?client:null,c=>{trace.Add("flush:"+c.Id);c.Id=N(p,"afterFlushId");},
                (id,notice)=>trace.Add($"send:{id}:{notice.Field0c}:{notice.Result}"),(game,callback,id)=>
                {Require(game==71&&callback==0x35b3a0,"Detach broadcast context changed");trace.Add("broadcast:"+id);});
            object result=null;bool unsupported=false;
            try
            {
                int owner=B(p,"owner")?100:0;switch(p.GetProperty("action").GetString())
                {
                    case "detach":GenericPetOwnerDetach.Run(B(p,"game")?71:0,owner,B(p,"pet")?101:0,(byte)N(p,"tail"),services);break;
                    case "type":result=GenericPetOwnerStore.Type(owner,petId,store);break;
                    case "data":var data=GenericPetOwnerStore.Data(owner,petId,store);result=data==null?0:Array.IndexOf(nodes,data)+1;break;
                    case "bucket":var bucket=GenericPetOwnerStore.Bucket(gameSelector,1,N(p,"bucketType"),store);result=bucket==null?null:Array.IndexOf(buckets,bucket);break;
                    case "packed":result=GenericPetPackedContext.Read(B(p,"game")?Game():null,B(p,"pet")?Unit():null,(byte)N(p,"tail"));break;
                    default:throw new InvalidOperationException("Unknown detach fixture action");
                }
            }
            catch(NotSupportedException){unsupported=true;}
            Require(unsupported==B(wanted,"unsupported")&&(unsupported||JsonSerializer.Serialize(result)==wanted.GetProperty("result").GetRawText()),$"Detach result/boundary differs at{contexts}");
            Require(trace.SequenceEqual(wanted.GetProperty("trace").EnumerateArray().Select(v=>v.GetString()))&&nodes.Select(n=>n.Flags).SequenceEqual(wanted.GetProperty("flags").EnumerateArray().Select(v=>v.GetUInt32()))
                &&petId==N(wanted,"id")&&JsonSerializer.Serialize(applyFlags)==wanted.GetProperty("applyFlags").GetRawText(),$"Detach service/state order differs at{contexts}");contexts++;
        }
        Require(contexts==84,"Missing owner-detach controls");
        CapturedStore();
        Log.Information("Owner detach:84current contexts/1371bytes;actual type/data/bucket/packed helpers,first-match inactive nodes,full flags,status result,client flush/send and broadcast order match. External callbacks/native store birth remain explicit.");
    }
    private static void CapturedStore()
    {
        int currentStore=1;var node=new GenericPetOwnedEntry(0,17,0,0,0,0);var bucket=new GenericPetOwnerBucket(1);
        var services=new GenericPetOwnerStoreServices(_=>new(0,true,true),_=>currentStore,_=>1,_=>3,
            (backing,_)=>backing==1?bucket:new GenericPetOwnerBucket(0),_=>node);
        var iterator=GenericPetOwnerStore.Iterator(services,currentStore,()=>1,(_,_,id)=>id);var visits=new List<int>();
        GenericPetOwnerIterator.Run(71,100,id=>{visits.Add(id);currentStore=2;},iterator);
        Require(visits.SequenceEqual(new[]{17,17})&&currentStore==2,"Later bucket followed changed player store instead of captured source backing");
        Log.Information("Owner iterator binding:callback changes player store;later bucket retains original captured backing,without mutable shared traversal cache.");
    }
    internal static void Original(SkeletonGuardian pet)
    {
        var owner=(PlayerMobile)pet.GetMaster();int ownerId=Id(owner),petId=Id(pet);
        var bodies=(owner.AllFollowers??[]).OfType<SkeletonGuardian>().Where(b=>b is{Alive:true,Deleted:false}&&WarlockDemonPlacement.Prepared(b)).OrderBy(b=>b.Serial.Value).ToArray();
        Require(bodies.Contains(pet),"Actual native roster omitted original demon");
        var entries=bodies.Select((b,i)=>new GenericPetOwnedEntry(0xabc00010,Id(b),0,(ushort)i,50+i,i+1<bodies.Length?i+2:0)).ToArray();
        var bucket=new GenericPetOwnerBucket(1);var world=new GenericPetWorldIdentity(71);int countCalls=0,applyCalls=0,broadcasts=0;
        var store=new GenericPetOwnerStoreServices(id=>{Require(id==ownerId,"Wrong roster owner");return new(0,true,true);},_=>1,_=>1,_=>21,
            (storage,type)=>{Require(storage==1,"Roster store changed");return type==20?bucket:new GenericPetOwnerBucket(0);},node=>entries[node-1]);
        var iterator=GenericPetOwnerStore.Iterator(store,1,()=>1,world.Resolve);var before=new List<int>();GenericPetOwnerIterator.Run(71,ownerId,before.Add,iterator);
        Require(before.SequenceEqual(bodies.Select(Id)),"Shared iterator did not read actual supplied original roster");
        var command=DeathMarkWorld.State(pet);int life=SkillHealth.Balance256(pet),mana=pet.Mana;uint serial=pet.Serial.Value;
        var detach=new GenericPetOwnerDetachServices(store,handle=>{Require(handle==petId,"Wrong detachment identity");return Id(World.FindMobile((Serial)(uint)handle));},
            _=>new(37,2,1,3),_=>new(1,WarlockDemonPlacement.ClassId(pet),true,null,0,0),
            (game,who,id,zero)=>{Require(game==71&&who==ownerId&&id==petId&&zero==0,"Original count context changed");countCalls++;},
            (handle,nonOne,value)=>{Require(handle==petId&&!nonOne&&value==entries.Single(n=>n.Id==petId).Field10,"Original status context changed");applyCalls++;return 9;},
            _=>null,_=>throw new InvalidOperationException("No native D2 client flush"),(_,_)=>throw new InvalidOperationException("No native D2 packet alias"),
            (game,callback,id)=>{Require(game==71&&callback==0x35b3a0&&id==petId,"Original detach notification context changed");broadcasts++;});
        GenericPetOwnerDetach.Run(71,ownerId,petId,0,detach);
        var after=new List<int>();GenericPetOwnerIterator.Run(71,ownerId,after.Add,iterator);
        Require(after.SequenceEqual(bodies.Where(b=>b!=pet).Select(Id))&&entries.Single(n=>n.Id==petId).Flags==0xabc00011
            &&GenericPetOwnerStore.Type(ownerId,petId,store)==20&&ReferenceEquals(GenericPetOwnerStore.Data(ownerId,petId,store),entries.Single(n=>n.Id==petId)),
            "Inactive roster view lost upper flag bits,removed another original or hid the first-match store entry");
        Require(countCalls==1&&applyCalls==1&&broadcasts==1&&!pet.Deleted&&pet.Serial.Value==serial&&pet.GetMaster()==owner
            &&SkillHealth.Balance256(pet)==life&&pet.Mana==mana&&DeathMarkWorld.State(pet)==command,"Prepared detachment altered live original ownership/resources/controller");
        Log.Information("Owner detach original:actual original GUIDs/typed registry share full-width node backing with existing iterator;inactive marker excludes only selected pet and preserves high flags/first-match lookup. Native count/status/client/death effects remain gated.");
    }
}
