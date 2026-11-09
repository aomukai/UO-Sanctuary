// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GenericPetOwnerRemoveChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetOwnerRemoveChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int N(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static bool B(JsonElement e,string key)=>e.GetProperty(key).GetBoolean();
    private static int Id(IEntity entity)=>checked((int)entity.Serial.Value);
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-owner-remove-check","Current pet node unlink/counters and composed detach",_=>Core.LoopContext.Post(()=>
    {try{Source();Log.Information("pet-owner-remove-check: passed");}catch(Exception e){Log.Error(e,"pet-owner-remove-check: failed");}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-owner-remove-cases.json")));int contexts=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var expected=row.GetProperty("output");var trace=new List<string>();var freed=new List<int>();
            int[] ids=p.GetProperty("ids").EnumerateArray().Select(v=>v.GetInt32()).ToArray();uint[] flags=p.GetProperty("flags").EnumerateArray().Select(v=>v.GetUInt32()).ToArray();
            var nodes=ids.Select((id,i)=>new GenericPetOwnedEntry(flags[i],id,(uint)(0x12340000+i),(ushort)(N(p,"field0c")+i),500+i,i+1<ids.Length?i+2:0)).ToArray();
            var slots=Enumerable.Range(0,32).Select(_=>new GenericPetOwnerBucket(0)).ToArray();var root=slots[N(p,"type")];root.First=1;root.Count=N(p,"rootCount");
            var shared=new GenericPetOwnerBucket(0,N(p,"sharedCount"));root.Shared=B(p,"alias")?root:B(p,"shared")?shared:null;
            var store=new GenericPetOwnerStoreServices(_=>new(N(p,"kind"),B(p,"data"),B(p,"store")),_=>1,_=>(byte)N(p,"ownerSelector"),_=>N(p,"count"),(_,type)=>slots[type],node=>nodes[node-1]);
            GenericPetAiGame Game()=>new(0x12345678,2,(byte)N(p,"gameSelector"),3);uint unitFlags=p.GetProperty("unitFlags").GetUInt32();uint? applyFlags=null;
            void Broadcast(int game,int callback,int id){Require(game==71&&callback==0x35b3a0,"Removal broadcast changed");trace.Add("broadcast:"+id);}
            var removal=new GenericPetOwnerRemoveServices(store,_=>Game(),(game,kind,id)=>
                {Require(game==71&&kind==1,"Removal registry changed");trace.Add("resolve:"+id);return B(p,"lookup")&&id==N(p,"id")?101:0;},
                (actor,mask)=>{Require(actor==101&&mask==0x7fffffff,"Removal source flag mask changed");unitFlags&=mask;},
                (_,id)=>trace.Add("special:"+id),node=>{int index=Array.IndexOf(nodes,node)+1;freed.Add(index);trace.Add("free:"+index);},Broadcast);
            var client=new GenericPetOwnerClient(31);
            var outer=new GenericPetOwnerDetachServices(store,_=>N(p,"id"),_=>Game(),_=>new(N(p,"petKind"),742,B(p,"petData"),null,B(p,"snapshot")?(byte)4:(byte)0,p.GetProperty("snapshotValue").GetUInt64()),
                (_,_,_,_)=>throw new InvalidOperationException("Unbound removal"),(pet,nonOne,value)=>{trace.Add($"apply:{(nonOne?1:0)}:{value}");applyFlags=nodes[0].Flags;return N(p,"result");},
                _=>B(p,"client")?client:null,c=>{trace.Add("flush:"+c.Id);c.Id=N(p,"afterFlushId");},(id,notice)=>trace.Add($"send:{id}:{notice.Field0c}:{notice.Result}"),Broadcast);
            bool unsupported=false;try
            {
                if(p.GetProperty("action").GetString()=="remove"){GenericPetOwnerRemove.Run(B(p,"game")?71:0,B(p,"owner")?100:0,N(p,"id"),N(p,"special"),removal);}
                else{GenericPetOwnerDetach.Run(B(p,"game")?71:0,B(p,"owner")?100:0,B(p,"pet")?101:0,(byte)N(p,"tail"),GenericPetOwnerRemove.Bind(outer,removal));}
            }
            catch(NotSupportedException){unsupported=true;}
            var chain=new List<int>();for(int at=root.First;at!=0;at=nodes[at-1].Next){chain.Add(at);Require(chain.Count<32,"Cyclic removal fixture");}
            Require(unsupported==B(expected,"unsupported")&&trace.SequenceEqual(expected.GetProperty("trace").EnumerateArray().Select(v=>v.GetString())), $"Removal service/boundary order differs at{contexts}");
            Require(root.Count==N(expected,"rootCount")&&(B(p,"alias")?root.Count:shared.Count)==N(expected,"sharedCount")&&chain.SequenceEqual(expected.GetProperty("head").EnumerateArray().Select(v=>v.GetInt32()))
                &&nodes.Select(n=>n.Flags).SequenceEqual(expected.GetProperty("flags").EnumerateArray().Select(v=>v.GetUInt32()))&&nodes.Select(n=>n.Next).SequenceEqual(expected.GetProperty("next").EnumerateArray().Select(v=>v.GetInt32()))
                &&unitFlags==expected.GetProperty("unitFlags").GetUInt32()&&freed.SequenceEqual(expected.GetProperty("freed").EnumerateArray().Select(v=>v.GetInt32()))&&JsonSerializer.Serialize(applyFlags)==expected.GetProperty("applyFlags").GetRawText(),$"Removal node/count/alias/flag state differs at{contexts}");contexts++;
        }
        Require(contexts==52,"Missing current removal controls");
        Log.Information("Owner removal:52current contexts/1949composed bytes;unlink/free/counter and alias/underflow chronology,high-flag mask,special callback and repeated broadcasts match. Normal composed detach skips hireable status;duplicates retain first-match behavior.");
    }
    internal static void Original(SkeletonGuardian pet)
    {
        var owner=(PlayerMobile)pet.GetMaster();int ownerId=Id(owner),petId=Id(pet);
        var bodies=(owner.AllFollowers??[]).OfType<SkeletonGuardian>().Where(b=>b is{Alive:true,Deleted:false}&&WarlockDemonPlacement.Prepared(b)).OrderBy(b=>b.Serial.Value).ToArray();
        var nodes=bodies.Select((b,i)=>new GenericPetOwnedEntry(0xabc00010,Id(b),0,(ushort)i,50+i,i+1<bodies.Length?i+2:0)).ToArray();
        var bucket=new GenericPetOwnerBucket(1,nodes.Length);var shared=new GenericPetOwnerBucket(0,9);bucket.Shared=shared;var world=new GenericPetWorldIdentity(71);var released=new List<GenericPetOwnedEntry>();int broadcasts=0,masks=0;
        var store=new GenericPetOwnerStoreServices(_=>new(0,true,true),_=>1,_=>1,_=>21,(_,type)=>type==20?bucket:new GenericPetOwnerBucket(0),node=>nodes[node-1]);
        var rawFlags=bodies.ToDictionary(Id,_=>0xffffffffu);var before=DeathMarkWorld.State(pet);int life=SkillHealth.Balance256(pet),mana=pet.Mana;
        var removal=new GenericPetOwnerRemoveServices(store,_=>new(37,2,1,3),world.Resolve,(actor,mask)=>{rawFlags[actor]&=mask;masks++;},
            (_,_)=>throw new InvalidOperationException("Normal removal requested unresolved special branch"),entry=>released.Add(entry),(game,callback,id)=>{Require(game==71&&callback==0x35b3a0&&id==petId,"Native removal notification changed");broadcasts++;});
        var outer=new GenericPetOwnerDetachServices(store,handle=>Id(World.FindMobile((Serial)(uint)handle)),_=>new(37,2,1,3),_=>new(1,WarlockDemonPlacement.ClassId(pet),true,null,0,0),
            (_,_,_,_)=>throw new InvalidOperationException("Unbound normal removal"),(_,_,_)=>throw new InvalidOperationException("Ordinary unique pet entered hireable calculation"),_=>null,
            _=>throw new InvalidOperationException("No D2 client alias"),(_,_)=>throw new InvalidOperationException("No D2 client alias"),removal.Broadcast);
        GenericPetOwnerDetach.Run(71,ownerId,petId,0,GenericPetOwnerRemove.Bind(outer,removal));
        var visited=new List<int>();GenericPetOwnerIterator.Run(71,ownerId,visited.Add,GenericPetOwnerStore.Iterator(store,1,()=>1,world.Resolve));
        Require(visited.SequenceEqual(bodies.Where(b=>b!=pet).Select(Id))&&bucket.Count==bodies.Length-1&&shared.Count==8&&released.Count==1&&released[0].Id==petId
            &&masks==1&&rawFlags[petId]==0x7fffffff&&broadcasts==3&&GenericPetOwnerStore.Type(ownerId,petId,store)==0,
            "Composed normal detach failed to unlink selected original,update both counts,release once or retain source notifications");
        Require(!pet.Deleted&&pet.GetMaster()==owner&&SkillHealth.Balance256(pet)==life&&pet.Mana==mana&&DeathMarkWorld.State(pet)==before,"Prepared node removal changed real actor/master/resources/controller");
        Log.Information("Owner removal original:composed normal detach unlinks only actual selected GUID,releases one node,reduces local/shared counters,masks supplied source flags and skips hireable calculation;existing iterator sees remaining originals. Native flag/free/broadcast effects remain explicit.");
    }
}
