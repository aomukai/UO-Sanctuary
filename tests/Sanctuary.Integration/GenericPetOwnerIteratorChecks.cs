// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

// Source store graphs are explicit test inputs. This does not manufacture the
// live world's player-pet insertion/removal ordering or admit automatic AI.
internal static class GenericPetOwnerIteratorChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetOwnerIteratorChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int N(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static bool B(JsonElement e,string key)=>e.GetProperty(key).GetBoolean();
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    internal static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-owner-iterator-cases.json")));int count=0,refused=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var expected=row.GetProperty("output");var visited=new List<int>();var trace=new List<int[]>();
            byte selector=checked((byte)N(p,"selector"));var counts=p.GetProperty("counts").EnumerateObject().ToDictionary(x=>byte.Parse(x.Name),x=>x.Value.GetInt32());
            var heads=p.GetProperty("heads").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>x.Value.GetInt32());
            var nodes=p.GetProperty("nodes").EnumerateObject().ToDictionary(x=>int.Parse(x.Name),x=>new GenericPetOwnedNode(checked((byte)x.Value[0].GetInt32()),x.Value[1].GetInt32(),x.Value[2].GetInt32()));
            var units=p.GetProperty("units").EnumerateArray().Select(Ints).ToArray();var ids=units.ToDictionary(x=>x[0],x=>x[1]);
            var next=units.Select((x,i)=>(x[0],Next:i+1<units.Length?units[i+1][0]:0)).ToDictionary(x=>x.Item1,x=>x.Next);
            var registry=new GenericPetRegistryServices((game,kind,id)=>
            {
                Require(game==1&&kind==1,"Owner iterator used wrong registry context");trace.Add([0,id]);
                var mutation=p.GetProperty("resolveMutation");if(mutation.ValueKind!=JsonValueKind.Null&&id==mutation[0].GetInt32())
                {int node=mutation[1].GetInt32();nodes[node]=nodes[node] with{Next=mutation[2].GetInt32()};}
                return units.FirstOrDefault()?[0]??0;
            },_=>throw new InvalidOperationException(),id=>ids[id],id=>next[id]);
            var services=new GenericPetOwnerIteratorServices(_=>new(N(p,"kind"),true,B(p,"store")),()=>selector,
                s=>counts.GetValueOrDefault(s,N(p,"count")),(_,type)=>heads.GetValueOrDefault(type),node=>nodes[node],
                (game,kind,id)=>GenericPetRegistry.Resolve(game,kind,id,registry));
            void Visit(int id)
            {
                visited.Add(id);trace.Add([1,id]);if(visited.Count!=1){return;}
                var changed=p.GetProperty("selectorAfter");if(changed.ValueKind!=JsonValueKind.Null){selector=checked((byte)changed.GetInt32());}
                foreach(var pair in p.GetProperty("headsAfter").EnumerateObject()){heads[int.Parse(pair.Name)]=pair.Value.GetInt32();}
                foreach(var pair in p.GetProperty("countsAfter").EnumerateObject()){counts[byte.Parse(pair.Name)]=pair.Value.GetInt32();}
            }
            bool unsupported=false;
            try
            {
                if(B(p,"bucketOnly")){GenericPetOwnerIterator.Bucket(B(p,"game")?1:0,heads.GetValueOrDefault(1),Visit,services);}
                else{GenericPetOwnerIterator.Run(B(p,"game")?1:0,B(p,"owner")?7:0,Visit,services);}
            }
            catch(NotSupportedException){unsupported=true;refused++;}
            Require(unsupported==B(expected,"unsupported")&&visited.SequenceEqual(Ints(expected.GetProperty("visited"))),"Current iterator visitation/refusal differs");
            var rows=expected.GetProperty("trace").EnumerateArray().Select(Ints).ToArray();
            Require(trace.Count==rows.Length&&!trace.Where((entry,i)=>!entry.SequenceEqual(rows[i])).Any(),"Current lookup-before-callback/snapshot/selector order differs");count++;
        }
        Require(count==33&&refused==8,"Missing owner iterator source controls");
        Log.Information("Owner iterator:33complete-current controls/832bytes;per-type snapshots,duplicate retention,cached next,inactive flags,selector/head/count mutations and65-entry growth match. Player-store producer remains explicit;automatic AI gated.");
    }
    internal static GenericPetInitializationServices Originals(GenericPetInitializationServices leaves,int owner,SkeletonGuardian[] bodies,GenericPetWorldIdentity world)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-owner-types.json")));
        var types=doc.RootElement.EnumerateObject().Select(pair=>pair.Value).ToArray();
        Require(types.Length==21&&doc.RootElement.GetProperty("demon").GetProperty("lineNumber").GetInt32()==20
            &&doc.RootElement.GetProperty("binddemon").GetProperty("lineNumber").GetInt32()==21,"Current demon pet-type bucket changed");
        int typeCount=types.Max(row=>row.GetProperty("lineNumber").GetInt32())+1;
        var own=bodies.Where(b=>checked((int)b.GetMaster().Serial.Value)==owner).ToArray();
        // Supplied fixture order only. Neither creation order nor native serial
        // order is asserted to be the current game's linked-list producer.
        var nodes=own.Select((body,i)=>(Key:i+1,Node:new GenericPetOwnedNode(0,checked((int)body.Serial.Value),i+2)))
            .ToDictionary(x=>x.Key,x=>x.Node);
        nodes[own.Length+1]=new(1,own.Length==0?0:checked((int)own[0].Serial.Value),own.Length+2);
        nodes[own.Length+2]=new(0,int.MaxValue,0);
        var iterator=new GenericPetOwnerIteratorServices(which=>
        {Require(which==owner&&World.FindMobile((Serial)(uint)which) is PlayerMobile,"Wrong player store owner");return new(0,true,true);},
            ()=>1,_=>typeCount,(which,type)=>type==20?1:0,node=>nodes[node],world.Resolve);
        return GenericPetOwnerIterator.Bind(leaves,world.Game,iterator);
    }
}
