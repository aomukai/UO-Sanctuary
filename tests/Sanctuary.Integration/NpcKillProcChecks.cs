// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class NpcKillProcChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcKillProcChecks));
    private sealed record Saved(uint Owner,uint Actor,uint[] Items,NpcProcState Graph,ulong Seed,int Life,int Mana,uint Victim,NecromancerCurse Curse,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-kill-proc-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("npc-kill-proc-check",false);Register("npc-kill-proc-reload-check",true);
        ConsoleInputHandler.RegisterCommand("npc-kill-proc-live","Natural summon kill with actual item curse",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("npc-kill-proc-live-result",Live)));
    }
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"NPC actual kill callback,area curse,ordering and original state",_=>Core.LoopContext.Post(()=>
    {try{if(reload){Reload();}else{Check();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Item Add(PlayerMobile p,NpcItemProcGraph graph,int stat,int skill,int chance)
    {
        var item=new Dagger();p.AddToBackpack(item);graph.Add(item,4,SourceRandom.InitLow(511));
        graph.Apply(item,new("actual-kill-curse",0,false,[new(11,stat,false,0)]),new(chance,5,skill),ItemSkillProperties.ForLevel(40));return item;
    }
    private sealed class Pair:IDisposable
    {
        internal GraveyardSkeleton Primary,Neighbor;
        internal IronGolemContact.Prepared Packet;
        public void Dispose(){Primary.Corpse?.Delete();Primary.Delete();Neighbor?.Delete();}
    }
    private static Pair Battle(PlayerMobile p,BloodGolemWorldBody b,bool lethal,bool miss=false)
    {
        var primary=NpcOpenWoundsChecks.Target(p,b);var near=NpcOpenWoundsChecks.Target(p,b);near.MoveToWorld(new(primary.X+1,primary.Y,primary.Z),primary.Map);
        if(lethal){SkillHealth.SetBalance256(primary,256);}
        var packet=IronGolemContact.Prepare(b,primary,UnarmedSummonContact.Require(b,p,primary),()=>miss?0:5);
        Require(miss||packet.Impact.Packet.Total256>256,"Need actual lethal-capable ordinary packet");return new(){Primary=primary,Neighbor=near,Packet=packet};
    }
    private static void Release(PlayerMobile p,BloodGolemWorldBody b,Pair pair,int draws,CombatAbility? ability,ulong seed=99)
    {
        NpcEquipmentRandom.Set(b,seed);ulong expected=seed;for(int i=0;i<draws;i++){SourceRandom.Roll(ref expected);}
        int life=SkillHealth.Balance256(pair.Neighbor);IronGolemContact.Release(b,p,pair.Primary,pair.Packet,_=>true,TimedEffects.Now);
        Require(NpcEquipmentRandom.State(b).Seed==expected,"Kill callback duplicate/missing actor RNG draw");
        var curse=NecromancerEffects.Curse(pair.Neighbor);
        Require(ability==null?curse==null:curse is{Rank:5}&&curse.Ability==ability&&curse.Source==b.Serial.Value,"Actual kill curse area/creator/rank/order");
        Require(SkillHealth.Balance256(pair.Neighbor)==life&&NecromancerEffects.ManaBalance256(p)==0,"Kill curse dealt incidental damage or paid owner mana");
    }
    private static ulong Seed(bool succeeds)
    {for(ulong s=1;s<1000;s++){ulong next=s;if((SourceRandom.Roll(ref next)%100<50)==succeeds){return s;}}throw new InvalidOperationException("Need source chance seed");}
    private static void Check()
    {
        var p=NpcOpenWoundsChecks.Owner(1,3160);var b=NpcOpenWoundsChecks.Body(p);var graph=NpcItemProcs.Create(b);var first=Add(p,graph,196,72,50);
        var blocks=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;NecromancerEffects.SetMana256(p,0);
        try
        {
            foreach(bool lethal in new[]{false,true})foreach(bool success in new[]{false,true})
            {
                using var pair=Battle(p,b,lethal);Release(p,b,pair,lethal?1:0,lethal&&success?CombatAbility.Weaken:null,Seed(success));
                Require((pair.Primary.Deleted||!pair.Primary.Alive)==lethal,"On-kill fixture death/survival");
                if(lethal){Require(pair.Primary.LastKiller==b&&ClassLoot.Killer(pair.Primary)==p,"Kill curse lost real native source/owner credit");}
            }
            using(var miss=Battle(p,b,true,true)){Release(p,b,miss,0,null);Require(miss.Primary.Alive,"Miss killed target");}
            var second=Add(p,graph,196,72,50);
            Require(graph.Capture().Events.Entries.Count(e=>e.Guid1==unchecked((int)EquipmentStatKey.Pack(196,(72<<6)|5)))==1,"Duplicate on-kill layer callbacks");
            using(var combined=Battle(p,b,true)){Release(p,b,combined,1,CombatAbility.Weaken);}
            var hit=Add(p,graph,198,66,100);var attack=Add(p,graph,195,91,100);
            using(var survivor=Battle(p,b,false)){Release(p,b,survivor,2,CombatAbility.LowerResist);}
            using(var all=Battle(p,b,true))
            {
                bool observed=false;var before=ClassLootHooks.BeforeDeath;
                ClassLootHooks.BeforeDeath=actor=>{if(actor==all.Primary){observed=true;Require(NecromancerEffects.Curse(all.Neighbor)?.Ability==CombatAbility.LowerResist,"Death committed before kill/attack item events");}before?.Invoke(actor);};
                try{Release(p,b,all,3,CombatAbility.LowerResist);}finally{ClassLootHooks.BeforeDeath=before;}
                Require(observed,"Actual native death hook not reached");
            }
            var modes=MonsterUninterruptibles.Snapshot();
            try
            {
                using var refused=Battle(p,b,true);MonsterUninterruptibles.Restore(modes.Where(x=>x.Actor!=b.Serial.Value).Append(new(b.Serial.Value,true,false,0)).ToArray());
                Release(p,b,refused,3,null);Require(refused.Primary.Deleted||!refused.Primary.Alive,"Refused nested cast cancelled actual kill");
            }
            finally{MonsterUninterruptibles.Restore(modes);}
            graph.Remove(hit.Serial.Value);hit.Delete();graph.Remove(attack.Serial.Value);attack.Delete();
            using var held=Battle(p,b,true);Release(p,b,held,1,CombatAbility.Weaken);
            var curse=NecromancerEffects.Curse(held.Neighbor) with{UntilUtc=TimedEffects.Now.AddDays(1)};
            NecromancerEffects.Profiles.Store(held.Neighbor,NecromancerEffects.Profiles.Find(held.Neighbor) with{Curse=curse});
            uint victim=held.Neighbor.Serial.Value;held.Neighbor.Internalize();held.Neighbor=null;
            SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-771);b.Combatant=null;b.Internalize();p.Internalize();
            File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,b.Serial.Value,[first.Serial.Value,second.Serial.Value],graph.Capture(),NpcEquipmentRandom.State(b).Seed,SkillHealth.Balance256(b),NecromancerEffects.ManaBalance256(p),victim,curse,0)));
            Log.Information("NPC kill procs:actual stat196/function20/source-written carriers;survival/miss no draw,50percent success/failure,duplicate-layer100single draw,hit/kill/attack stages before native death,neighbor area/rank/creator/owner credit,no mana or incidental damage,uninterruptible cast refusal and original saved graph/curse pass");
        }
        finally{SourceBlocking.BlockRoll=blocks;}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(BloodGolemWorldBody)World.FindMobile((Serial)s.Actor);
        var graph=NpcItemProcs.Get(b);Require(s.Generation<2&&Json(graph.Capture())==Json(s.Graph)&&NpcEquipmentRandom.State(b).Seed==s.Seed&&s.Items.All(i=>World.FindItem((Serial)i)is{Deleted:false}),"Original on-kill item graph/order/seed changed");
        Require(SkillHealth.Balance256(b)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&NecromancerEffects.Curse(World.FindMobile((Serial)s.Victim))==s.Curse,"Reload changed wounds,mana or original held curse");
        FixtureSpace.Corridor(p,6100,40,3160);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);var blocks=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try{using var pair=Battle(p,b,true);Release(p,b,pair,1,CombatAbility.Weaken);}finally{SourceBlocking.BlockRoll=blocks;}
        b.Combatant=null;b.Internalize();p.Internalize();File.WriteAllText(Manifest,Json(s with{Graph=graph.Capture(),Seed=NpcEquipmentRandom.State(b).Seed,Life=SkillHealth.Balance256(b),Mana=NecromancerEffects.ManaBalance256(p),Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=NpcOpenWoundsChecks.Owner(1,3660);FixtureNetwork.Authenticate(network,p,"kill-proc");var b=NpcOpenWoundsChecks.Body(p);var t=NpcOpenWoundsChecks.Target(p,b);TimedEffects.PreventHealing(t,TimedEffects.Now);
        var graph=NpcItemProcs.Create(b);var item=Add(p,graph,196,72,100);NecromancerEffects.SetMana256(p,0);GraveyardSkeleton neighbor=null;
        var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;NpcEquipmentRandom.Set(b,BloodGolemWorldChecks.Seed(b,t));
        b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});long deadline=Core.TickCount+10000;
        void End(){SourceBlocking.BlockRoll=block;t.Corpse?.Delete();t.Delete();neighbor?.Delete();b.Delete();item.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&network.Running,$"Natural on-kill timeout/disconnect:running={network.Running},targetAlive={t.Alive},targetDeleted={t.Deleted},life={SkillHealth.Balance256(t)},neighbor={neighbor?.Serial.Value},controller={Json(b.WorldState.Controller)}");
                if(neighbor==null&&b.WorldContact!=null)
                {Require(b.WorldContact.Impact.Target==t.Serial.Value&&b.WorldContact.Impact.Packet.Total256>256,"Natural lethal action target/damage");SkillHealth.SetBalance256(t,256);neighbor=NpcOpenWoundsChecks.Target(p,b);neighbor.MoveToWorld(new(t.X+1,t.Y,t.Z),t.Map);}
                if(neighbor==null||NecromancerEffects.Curse(neighbor)is not{} curse){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(curse is{Ability:CombatAbility.Weaken,Rank:5}&&curse.Source==b.Serial.Value&&b.WorldState.Controller.Attempts==1&&(t.Deleted||!t.Alive)&&NecromancerEffects.ManaBalance256(p)==0,"Natural learned on-kill event/area/creator/action/mana");
                End();Log.Information("npc-kill-proc-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"npc-kill-proc-live-result: failed");}
        }
        Log.Information("npc-kill-proc-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
