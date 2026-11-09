// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GenericPetPlayerIterationChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetPlayerIterationChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int N(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static bool B(JsonElement e,string key)=>e.GetProperty(key).GetBoolean();
    private static int Id(IEntity entity)=>checked((int)entity.Serial.Value);
    private static GenericPetSearchWorldUnit View(int kind,byte selector,GenericPetSearchStatList stats)
        =>new(new(kind,true,0,0),0,1,selector,0,0,kind==1,0,null,0,0,stats,null);
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-player-iteration-check","Current living-player iteration and registry bucket layout",_=>Core.LoopContext.Post(()=>
    {try{Source();Log.Information("pet-player-iteration-check: passed");}catch(Exception e){Log.Error(e,"pet-player-iteration-check: failed");}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-player-iteration-cases.json")));int contexts=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var expected=row.GetProperty("output");var visits=new List<int>();int? offset=null;bool unsupported=false;
            try
            {
                if(p.GetProperty("action").GetString()=="bucket"){offset=GenericPetRegistryLayout.Offset(N(p,"kind"),N(p,"id"));}
                else
                {
                    var heads=p.GetProperty("heads").EnumerateObject().ToDictionary(v=>int.Parse(v.Name),v=>v.Value.GetInt32());var units=new Dictionary<int,GenericPetSearchWorldUnit>();var next=new Dictionary<int,int>();
                    foreach(var prop in p.GetProperty("units").EnumerateObject())
                    {
                        int id=int.Parse(prop.Name);var u=prop.Value;var words=new uint[8];words[0]=u.GetProperty("word0").GetUInt32();words[5]=u.GetProperty("word5").GetUInt32();
                        units[id]=View(N(u,"kind"),(byte)N(u,"selector"),B(u,"root")?new(N(u,"root1c"),words):null);next[id]=N(u,"next");
                    }
                    var services=new GenericPetPlayerIterationServices((game,bucket)=>{Require(game==71,"Player bucket lost game");return heads.GetValueOrDefault(bucket);},id=>units[id],_=>N(p,"count"),id=>next[id]);
                    void Visit(int id)
                    {
                        visits.Add(id);if(visits.Count!=1){return;}
                        var change=p.GetProperty("nextChange");if(change.ValueKind!=JsonValueKind.Null){next[change[0].GetInt32()]=change[1].GetInt32();}
                        var late=p.GetProperty("lateBucket");if(late.ValueKind!=JsonValueKind.Null){heads[127]=late.GetInt32();}
                    }
                    GenericPetPlayerIteration.Run(B(p,"game")?71:0,B(p,"callback")?Visit:null,services);
                }
            }
            catch(NotSupportedException){unsupported=true;}
            Require(unsupported==B(expected,"unsupported")&&visits.SequenceEqual(expected.GetProperty("visits").EnumerateArray().Select(v=>v.GetInt32()))
                &&JsonSerializer.Serialize(offset)==expected.GetProperty("offset").GetRawText(),$"Current player iteration/registry result differs at{contexts}");contexts++;
        }
        Require(contexts==69,"Missing player/bucket controls");
        Log.Information("Player iteration:69current contexts/581bytes;exact typed bucket offsets,playerbody/sharedstash exclusions,selector/state bounds,live next after callback,late bucket and duplicate membership match. Protected pet-sync payload remains separate.");
    }
    internal static void Original(PlayerMobile[] owners)
    {
        Require(owners.Length==2&&owners.All(o=>o is{Alive:true,Deleted:false}),"Player iteration fixture needs original Warlock/Witch owners");
        int first=Id(owners[0]),second=Id(owners[1]);var heads=new Dictionary<int,int>{{0,first},{127,second}};var next=new Dictionary<int,int>{{first,0},{second,0}};
        var firstWords=new uint[8];var secondWords=new uint[8];secondWords[0]=128;
        var views=new Dictionary<int,GenericPetSearchWorldUnit>{{first,View(0,1,new(-1,firstWords))},{second,View(0,1,new(-1,secondWords))}};
        var world=new GenericPetWorldIdentity(71);var visits=new List<int>();var saved=owners.Select(o=>(Life:SkillHealth.Balance256(o),Mana:o.Mana)).ToArray();
        var services=new GenericPetPlayerIterationServices((game,bucket)=>{Require(game==71,"Native recipient game changed");return heads.GetValueOrDefault(bucket);},id=>views[id],_=>232,id=>next[id]);
        GenericPetPlayerIteration.Run(71,id=>
        {
            Require(world.Resolve(71,0,id)==id&&world.Resolve(71,1,id)==0,"Recipient identity crossed typed player/monster registries");visits.Add(id);
            if(visits.Count==1){secondWords[0]=0;next[first]=second;}
        },services);
        Require(visits.SequenceEqual(new[]{first,second,second}),"Actual recipient view lost live next/state/late-bucket duplicate semantics");
        Require(owners.Select(o=>(Life:SkillHealth.Balance256(o),Mana:o.Mana)).SequenceEqual(saved),"Recipient observation changed original player resources");
        Log.Information("Player iteration original:actual Warlock/Witch identities resolve through typed registry;explicit membership/state views observe callback changes and retain later duplicate recipient. No packet or protected callback is fabricated.");
    }
}
