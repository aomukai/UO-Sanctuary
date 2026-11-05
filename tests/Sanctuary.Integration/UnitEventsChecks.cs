// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class UnitEventsChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(UnitEventsChecks));
    private sealed record Saved(uint Owner,uint Actor,uint[] Items,NpcProcState Graph,ulong Seed,int Life,int Mana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"unit-events-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Refuses(Action action,string why)
    {try{action();}catch(Exception e)when(e is NotSupportedException or InvalidDataException){return;}throw new InvalidOperationException(why);}
    public static void Configure()
    {
        Register("unit-events-check",false);Register("unit-events-reload-check",true);
        ConsoleInputHandler.RegisterCommand("unit-events-live","Actual learned summon clock with original item proc graph",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("unit-events-live-result",Live)));
    }
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Source unit event sequences and persistent original NPC proc graphs",_=>Core.LoopContext.Post(()=>
    {try{Source();if(reload){Reload();}else{Check();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Source()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"unit-events-source-cases.json")));int count=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {
            var v=row[0];int V(int n)=>v[n].GetInt32();bool acted=false;var trace=new List<long[]>();SourceUnitEvents events=null;
            events=new(V(0)!=0,(fn,phase,a,b)=>
            {
                trace.Add([0,fn,phase,a,b]);
                if(a==1000&&!acted)
                {
                    acted=true;var op=v[8][0];int queue=unchecked((int)op[5].GetInt64());var current=events.Get(queue,3000,1000);
                    Require(current!=null&&(current.Value.Flags&1)!=0,"Callback did not expose its active flag");
                    if(V(7)==2){events.Register((byte)phase,9000,9001,21,2,9000);}
                    if(V(7)==3){events.Unregister(queue,3000);trace.Add([21,current.Value.Flags]);}
                    if(V(7)==4){events.Trigger((byte)(phase==5?6:5));}
                }
                return V(7)==1?7:0;
            });
            var bindings=new ItemEventBindings(events,stat=>{trace.Add([10,stat]);return V(6)!=0?[new((short)V(2),(ushort)V(4)),new((short)V(3),(ushort)V(5))]:null;},V(1)!=0);
            int step=0;
            foreach(var op in v[8].EnumerateArray())
            {
                int O(int n)=>unchecked((int)op[n].GetInt64());
                switch(O(0))
                {
                    case 0:events.Register((byte)O(1),O(2),O(3),20,unchecked((uint)O(5)),O(6));break;
                    case 1:events.RegisterFunction((byte)O(1),O(2),O(3),O(4),unchecked((uint)O(5)),O(6));break;
                    case 2:events.Unregister(O(1),O(2));break;
                    case 3:trace.Add([20,events.Get(O(1),O(2),O(3))?.Value.Id??0]);break;
                    case 4:events.Trigger((byte)O(1));break;
                    case 5:events.Clear();break;
                    case 6:bindings.Changed(unchecked((uint)O(1)),O(2));break;
                    default:throw new InvalidDataException("Unknown source operation");
                }
                var actual=events.Capture();var expected=row[1][step++];
                long[][] entries=actual.Entries.Select(e=>new long[]{e.Id,e.Event,e.Guid1,e.Guid2,e.Function,e.Queue,e.Key,e.Flags}).ToArray();
                Require(actual.NextId==expected[0].GetInt32()&&Json(entries)==expected[1].GetRawText(),$"Source event node/order/state case{count}/step{step}");
                var restored=SourceUnitEvents.Restore(actual,(_,_,_,_)=>0);Require(Json(restored.Capture())==Json(actual),"Original ordered snapshot round trip");
            }
            Require(Json(trace)==row[2].GetRawText(),"Source callback/metadata service trace case"+count);count++;
        }
        Require(count==1488,"Incomplete unit event source controls");
        Log.Information("Unit events:1488complete source sequence controls/8routines;per-operation original node order/identity/flags,callback mutations,packed-layer registration and snapshot round trips");
    }
    private static Item Carrier(PlayerMobile p){var item=new Dagger();p.AddToBackpack(item);return item;}
    private static void Add(NpcItemProcGraph graph,Item item,int skill,int chance=100,int stat=198)
    {
        graph.Add(item,4,SourceRandom.InitLow(733));
        graph.Apply(item,new("explicit-curse-carrier",0,false,[new(11,stat,false,0)]),new(chance,5,skill),ItemSkillProperties.ForLevel(40));
    }
    private static GraveyardSkeleton Hit(PlayerMobile p,BloodGolemWorldBody b,CombatAbility? expected,int draws,ulong? start=null)
    {
        var t=NpcOpenWoundsChecks.Target(p,b);t.SetResistance(ResistanceType.Physical,100);
        var packet=IronGolemContact.Prepare(b,t,UnarmedSummonContact.Require(b,p,t),()=>5);
        Require(packet.Impact.Packet.Total256==0,"Qualified zero-damage proc fixture");
        NpcEquipmentRandom.Set(b,start??SourceRandom.InitLow(99));ulong seed=NpcEquipmentRandom.State(b).Seed;
        for(int i=0;i<draws;i++){SourceRandom.Roll(ref seed);}
        IronGolemContact.Release(b,p,t,packet,_=>true,TimedEffects.Now);
        Require(expected==null?NecromancerEffects.Curse(t)==null:NecromancerEffects.Curse(t) is{Rank:5} curse&&curse.Ability==expected&&curse.Source==b.Serial.Value,"Original NPC graph proc order/effect/source");
        Require(NpcEquipmentRandom.State(b).Seed==seed,"NPC graph extra/missing proc draw");return t;
    }
    private static void Check()
    {
        var p=NpcOpenWoundsChecks.Owner(1,3160);var b=NpcOpenWoundsChecks.Body(p);NecroSnapshotChecks.MissingMetadata(b);var blocks=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        var a=Carrier(p);var z=Carrier(p);var graph=NpcItemProcs.Create(b);
        try
        {
            Add(graph,a,66);Add(graph,z,82);graph.Refresh(NpcCombatEvents.LegacyForSource(b));
            var t=Hit(p,b,CombatAbility.AmplifyDamage,2);t.Delete();
            // A nonzero value update preserves insertion order, not stat order.
            graph.Apply(a,new("increase-same-layer",0,false,[new(11,198,false,0)]),new(1,5,66),ItemSkillProperties.ForLevel(40));
            t=Hit(p,b,CombatAbility.AmplifyDamage,2);t.Delete();
            graph.Remove(a.Serial.Value);Add(graph,a,66);t=Hit(p,b,CombatAbility.LifeTap,2);t.Delete();
            // Duplicate layers aggregate into one callback pair and one draw.
            graph.Remove(z.Serial.Value);Add(graph,z,66);t=Hit(p,b,CombatAbility.AmplifyDamage,1);t.Delete();
            Require(graph.Capture().Events.Entries.Count(e=>e.Guid1==unchecked((int)EquipmentStatKey.Pack(198,(66<<6)|5)))==2,"Duplicate skill layer registrations");
            // Unsupported inputs must not alter the live graph or its item RNG.
            string before=Json(graph.Capture());
            Refuses(()=>graph.Apply(a,new("unsupported-death",0,false,[new(11,197,false,0)]),new(100,5,66),ItemSkillProperties.ForLevel(40)),"Death proc silently admitted");
            Refuses(()=>graph.Apply(a,new("unsupported-confuse",0,false,[new(11,198,false,0)]),new(100,5,81),ItemSkillProperties.ForLevel(40)),"Unsupported curse admitted");
            Require(Json(graph.Capture())==before,"Refused property changed original graph");
            string global=NpcItemProcs.SnapshotJson;var all=JsonSerializer.Deserialize<NpcProcState[]>(global);
            Refuses(()=>NpcItemProcs.Restore(all.Append(graph.Capture() with{Events=null}).ToArray()),"Malformed global state accepted");
            Require(NpcItemProcs.SnapshotJson==global&&ReferenceEquals(NpcItemProcs.Get(b),graph),"Rejected restore partially cleared live ownership");
            var corrupt=graph.Capture();corrupt=corrupt with{Events=corrupt.Events with{Entries=corrupt.Events.Entries.Select((e,i)=>i==0?e with{Function=31}:e).ToArray()}};
            Refuses(()=>NpcItemProcs.Restore(all.Where(s=>s.Actor!=b.Serial.Value).Append(corrupt).ToArray()),"Corrupt callback payload accepted");
            Require(NpcItemProcs.SnapshotJson==global&&ReferenceEquals(NpcItemProcs.Get(b),graph),"Rejected callback payload changed global ownership");
            // Native item deletion is observed before the next world dispatch.
            z.Delete();graph.Refresh(NpcCombatEvents.LegacyForSource(b));Require(graph.Capture().Items.Length==1,"Deleted carrier retained");
            graph.Remove(a.Serial.Value);Add(graph,a,82,5);
            ulong threshold=0;
            for(ulong i=1;i<1000;i++){ulong next=i;SourceRandom.Roll(ref next);uint roll=SourceRandom.Roll(ref next)%100;if(roll is >=5 and <10){threshold=i;break;}}
            Require(threshold!=0,"Need 5-versus10 percent aggregate seed");
            MagePetBuffChecks.Train(p,"necromancer.bloodgolem",19);t=Hit(p,b,null,2,threshold);t.Delete();
            MagePetBuffChecks.Train(p,"necromancer.bloodgolem",20);t=Hit(p,b,CombatAbility.LifeTap,2,threshold);t.Delete();
            graph.Refresh(NpcCombatEvents.LegacyForSource(b));
            Require(graph.Capture().Innate.Any(v=>v.Stat==198&&v.Value==5)&&graph.Capture().Events.Entries.Count(e=>e.Guid1==unchecked((int)EquipmentStatKey.Pack(198,(82<<6)|5)))==2,"Inherent plus item proc must share one10percent slot");
            ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);
            ulong noProc=1;for(;noProc<1000;noProc++){ulong next=noProc;if(SourceRandom.Roll(ref next)%100>=5){break;}}
            t=Hit(p,b,null,1,noProc);t.Delete();
            MagePetBuffChecks.Train(p,"necromancer.bloodgolem",1);MagePetBuffChecks.Train(p,"necromancer.golemmastery",20);
            graph.Remove(a.Serial.Value);Add(graph,a,72,100,195);t=Hit(p,b,CombatAbility.Weaken,1);t.Delete();
            graph.Remove(a.Serial.Value);Add(graph,a,66);z=Carrier(p);Add(graph,z,82);
            t=Hit(p,b,CombatAbility.AmplifyDamage,2);t.Delete();
            SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-771);NecromancerEffects.SetMana256(p,0);
            b.Combatant=null;b.Internalize();p.Internalize();
            File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,b.Serial.Value,[a.Serial.Value,z.Serial.Value],graph.Capture(),NpcEquipmentRandom.State(b).Seed,SkillHealth.Balance256(b),NecromancerEffects.ManaBalance256(p),0)));
            Require(JsonSerializer.Deserialize<NpcProcState[]>(NpcItemProcs.SnapshotJson).Any(s=>s.Actor==b.Serial.Value),"Worker snapshot lacks stable original graph");
            Log.Information("NPC proc graphs:real original carriers/source writer and qualified hit/attack paths;LIFO curse order,stable positive changes,detach/re-add,duplicate-layer and live hard19/20/respec aggregation,one actor RNG,zero damage,unsupported/corrupt atomic refusal,deleted-item cleanup and saved wounds/mana pass");
        }
        finally{SourceBlocking.BlockRoll=blocks;}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var b=(BloodGolemWorldBody)World.FindMobile((Serial)saved.Actor);
        Require(saved.Generation<2&&saved.Items.All(i=>World.FindItem((Serial)i)is{Deleted:false}),"Original saved carrier identities lost");
        var graph=NpcItemProcs.Get(b);Require(Json(graph.Capture())==Json(saved.Graph)&&NpcEquipmentRandom.State(b).Seed==saved.Seed,"Saved original graph/order/LCG changed");
        Require(SkillHealth.Balance256(b)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Reload healed wounds or reset mana");
        FixtureSpace.Corridor(p,6100,40,3160);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);var blocks=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try{var t=Hit(p,b,CombatAbility.AmplifyDamage,2);t.Delete();}finally{SourceBlocking.BlockRoll=blocks;}
        b.Combatant=null;b.Internalize();p.Internalize();
        File.WriteAllText(Manifest,Json(saved with{Graph=graph.Capture(),Seed=NpcEquipmentRandom.State(b).Seed,Life=SkillHealth.Balance256(b),Mana=NecromancerEffects.ManaBalance256(p),Generation=saved.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=NpcOpenWoundsChecks.Owner(1,3660);FixtureNetwork.Authenticate(network,p,"unit-event");var b=NpcOpenWoundsChecks.Body(p);var t=NpcOpenWoundsChecks.Target(p,b);t.SetResistance(ResistanceType.Physical,100);
        var item=Carrier(p);var graph=NpcItemProcs.Create(b);Add(graph,item,72);NecromancerEffects.SetMana256(p,0);
        var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;NpcEquipmentRandom.Set(b,BloodGolemWorldChecks.Seed(b,t));
        b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});long deadline=Core.TickCount+10000;
        void End(){SourceBlocking.BlockRoll=block;t.Delete();b.Delete();item.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&network.Running,"Natural graph proc timeout/disconnect");
                if(NecromancerEffects.Curse(t) is not{} curse){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(curse is{Ability:CombatAbility.Weaken,Rank:5}&&curse.Source==b.Serial.Value&&b.WorldState.Controller.Attempts==1&&NecromancerEffects.ManaBalance256(p)==0,"Natural actor event/curse/mana");
                uint actor=b.Serial.Value;End();Require(!JsonSerializer.Deserialize<NpcProcState[]>(NpcItemProcs.SnapshotJson).Any(s=>s.Actor==actor),"Deleted actor retained graph");
                Log.Information("unit-events-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"unit-events-live-result: failed");}
        }
        Log.Information("unit-events-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
