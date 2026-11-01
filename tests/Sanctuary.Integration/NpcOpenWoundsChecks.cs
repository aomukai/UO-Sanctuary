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

public static class NpcOpenWoundsChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcOpenWoundsChecks));
    private sealed record Saved(uint Owner,uint Other,uint Source,uint Target,int Life,TimedEffectState State,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-open-wounds-fixtures.json");
    public static void Configure()
    {
        Register("npc-open-wounds-check",Check);Register("npc-open-wounds-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("npc-open-wounds-live","Connected Blood hard10 Open Wounds and durable damage",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("npc-open-wounds-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"NPC registered wound event/source HP-regeneration frames",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,int rank)=>MagePetBuffChecks.Train(p,"necromancer.bloodgolem",rank);
    internal static PlayerMobile Owner(int rank=10,int row=3080)
    {var p=PoisonDaggerChecks.Owner();FixtureSpace.Corridor(p,6100,40,row);Train(p,rank);MagePetBuffChecks.Train(p,"necromancer.golemmastery",20);return p;}
    internal static BloodGolemWorldBody Body(PlayerMobile p)
    {NecromancerEffects.SetMana256(p,p.ManaMax*256);Require(NecromancerCombat.Release(p,CombatAbility.BloodGolem,Serial.Zero,new(p.X+1,p.Y,p.Z)),"Learned wound Blood");var b=NecromancerGolems.For(p).OfType<BloodGolemWorldBody>().Single();b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddHours(1)});return b;}
    internal static GraveyardSkeleton Target(PlayerMobile p,BloodGolemBody b)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(b.Location,p.Map);t.SetResistance(ResistanceType.Physical,0);return t;}
    private static TimedActor State(Mobile t){Require(TimedEffects.TryActor(t,out var a),"Missing wound actor");return a;}
    private static bool Wounded(Mobile t)=>TimedEffects.TryActor(t,out var a)&&a.BleedMs>0;
    private static IronGolemContact.Prepared Prepare(PlayerMobile p,BloodGolemBody b,BaseCreature t,int flags=5)
    {NpcEquipmentRandom.Set(b,SourceRandom.InitLow(97));return IronGolemContact.Prepare(b,t,UnarmedSummonContact.Require(b,p,t),()=>flags);}
    private static MonsterContactResult Release(PlayerMobile p,BloodGolemBody b,BaseCreature t,IronGolemContact.Prepared packet,DateTime? at=null)
        =>IronGolemContact.Release(b,p,t,packet,_=>true,at??TimedEffects.Now);
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"open-wounds-regen-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var v=row[0].EnumerateArray().Select(x=>x.GetInt32()).ToArray();int delta=NpcEquipmentRegeneration.Delta(v[1],v[2],v[3]+v[4],v[3]>0||v[4]>0,v[5]!=0);
            int actual=delta<0?PoisonStates.BalanceAfter(v[0],-(long)delta,false,v[6]!=0):Math.Min(1024,v[0]+delta);
            Require(actual==row[1].GetInt32(),"Combined wound regeneration differs from full source at "+count);count++;
        }
        Require(count==2160,"Missing combined regeneration cases");
    }
    private static void Events()
    {
        var p=Owner(9);PoisonDaggerChecks.Named(p,"Wormskull");var b=Body(p);var t=Target(p,b);var blocks=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try
        {
            Require(b.Rank==10&&NpcOpenWounds.Inherent(b)==0,"Soft point unlocked Blood wounds");var packet=Prepare(p,b,t);ulong seed=NpcEquipmentRandom.State(b).Seed;
            Release(p,b,t,packet);Require(!Wounded(t)&&NpcEquipmentRandom.State(b).Seed==seed,"Hard9 wound event or extra draw");
            packet=Prepare(p,b,t);seed=NpcEquipmentRandom.State(b).Seed;Train(p,10);ulong expected=seed;SourceRandom.Roll(ref expected);int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);DateTime at=TimedEffects.Now;
            Release(p,b,t,packet,at);var a=State(t);Require(NpcOpenWounds.Inherent(b)==100&&a.OpenWounds is{ElapsedMs:0}&&a.BleedSource==b.Serial&&a.OpenWounds.CreditOwner==p.Serial.Value&&NpcEquipmentRandom.State(b).Seed==expected&&hp-SkillHealth.Balance256(t)==packet.Impact.Packet.Total256&&NecromancerEffects.ManaBalance256(p)==mana,"Live windup unlock/one event draw/real source/no early tick or mana");
            int rate=a.OpenWounds.Rate256;hp=SkillHealth.Balance256(t);int recovery=CreatureHitRecovery.Remaining(t);TimedEffects.Settle(t,at.AddMilliseconds(39));Require(SkillHealth.Balance256(t)==hp&&State(t).OpenWounds.ElapsedMs==39,"Wound tick before40ms");TimedEffects.Settle(t,at.AddMilliseconds(40));Require(hp-SkillHealth.Balance256(t)==rate&&CreatureHitRecovery.Remaining(t)<=recovery,"FP wound tick used native damage/recovery or lost rate");
            t.Delete();t=Target(p,b);packet=Prepare(p,b,t);seed=NpcEquipmentRandom.State(b).Seed;ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Release(p,b,t,packet);Require(!Wounded(t)&&NpcEquipmentRandom.State(b).Seed==seed,"Windup respec retained wound/draw");Train(p,10);
            t.Delete();t=Target(p,b);packet=Prepare(p,b,t,0);seed=NpcEquipmentRandom.State(b).Seed;Release(p,b,t,packet);Require(!Wounded(t)&&NpcEquipmentRandom.State(b).Seed==seed,"Miss invoked wound event");
            t.Delete();t=Target(p,b);var shields=SourceBlocking.Snapshot();try
            {SourceBlocking.Restore(shields.Where(x=>x.Actor!=t.Serial.Value).Append(new(t.Serial.Value,CreatureProfiles.Definition(t).Reference,1)).ToArray());SourceBlocking.BlockRoll=_=>0;packet=null;for(ulong n=1;n<1000;n++){NpcEquipmentRandom.Set(b,n);var candidate=UnarmedSummonContact.Prepare(b,p,t,UnarmedSummonContact.Require(b,p,t));if((candidate.Impact.Packet.ResultFlags&16)!=0){packet=candidate;break;}candidate.Cancel();}Require(packet!=null,"Actual block seed");seed=NpcEquipmentRandom.State(b).Seed;Release(p,b,t,packet);Require((packet.Impact.Packet.ResultFlags&16)!=0&&!Wounded(t)&&NpcEquipmentRandom.State(b).Seed==seed,"Blocked melee invoked wounds");}finally{SourceBlocking.Restore(shields);SourceBlocking.BlockRoll=_=>99;}
            foreach(bool armor in new[]{false,true})
            {t.Delete();t=Target(p,b);if(armor){NecromancerEffects.Profiles.Store(t,new(BoneArmor:100000));}else{t.SetResistance(ResistanceType.Physical,100);}packet=Prepare(p,b,t);Require(packet.Impact.Packet.Total256==0,"Zero/absorption setup");seed=NpcEquipmentRandom.State(b).Seed;expected=seed;SourceRandom.Roll(ref expected);hp=SkillHealth.Balance256(t);Release(p,b,t,packet);Require(Wounded(t)&&State(t).OpenWounds!=null&&SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.State(b).Seed==expected,"Successful zero/absorbed hit lost wound event");}
            t.Delete();t=Target(p,b);packet=Prepare(p,b,t);seed=NpcEquipmentRandom.State(b).Seed;at=TimedEffects.Now;TimedEffects.Bleed(p,t,8000,at);SkillHealth.SetBalance256(t,256);var cancelled=Release(p,b,t,packet,at.AddMilliseconds(100));Require(cancelled.Exit==MonsterContactExit.SettledRecipientFreed&&packet.Consumed&&!t.Alive&&NpcEquipmentRandom.State(b).Seed==seed,"Overdue lethal settlement entered/replayed prepared wound event");
        }
        finally{SourceBlocking.BlockRoll=blocks;t.Corpse?.Delete();t.Delete();b.Delete();p.Delete();}
    }
    private static void Item()
    {
        var p=Owner(9);var b=Body(p);var t=Target(p,b);var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Goreshovel").Id,99,new Random(836),true);item.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
        try
        {
            Require(IronGolemItems.TryAttach(p,b,item,out var why),"Real NPC wound item: "+why);Require(NpcOpenWounds.Chance(b)==60,"Item-only wound golden");IronGolemContact.Prepared packet=null;ulong expected=0;
            for(ulong n=1;n<1000;n++){NpcEquipmentRandom.Set(b,n);packet=IronGolemContact.Prepare(b,t,UnarmedSummonContact.Require(b,p,t),()=>5);expected=NpcEquipmentRandom.State(b).Seed;if(SourceRandom.Roll(ref expected)%100<60){break;}packet.Cancel();packet=null;}
            Require(packet!=null,"Item event seed");Release(p,b,t,packet);Require(Wounded(t)&&NpcEquipmentRandom.State(b).Seed==expected,"Item-only registered callback or seed");
            Train(p,10);Require(NpcOpenWounds.Chance(b)==160,"Item60and hard100did not aggregate");t.Delete();t=Target(p,b);packet=Prepare(p,b,t);expected=NpcEquipmentRandom.State(b).Seed;SourceRandom.Roll(ref expected);Release(p,b,t,packet);Require(Wounded(t)&&NpcEquipmentRandom.State(b).Seed==expected,"Duplicate stat135subscription/draw from skill+item");
        }
        finally{t.Delete();b.Delete();p.Delete();}
    }
    private static void Combined()
    {
        var p=Owner();var other=Owner();var b=Body(p);var holder=NpcEquipmentRegenerationChecks.Pet(other,NpcEquipmentRegenerationChecks.Named("unique/134"));int item=NpcEquipmentRegeneration.ItemRate(holder);
        try
        {
            Require(item==20,"Combined actual replenish original");
            foreach(int poison in new[]{0,16})foreach(int wound in new[]{1,20,40})foreach(bool blocked in new[]{false,true})
            {
                TimedEffects.Clear(holder);int before=SkillHealth.Maximum256(holder)-1024;SkillHealth.SetBalance256(holder,before);DateTime at=TimedEffects.Now;if(blocked){TimedEffects.PreventHealing(holder,at);}Require(TimedEffects.PreparedOpenWounds(b,holder,wound,p.Serial.Value,at),"Combined wound state");if(poison>0){Require(TimedEffects.PoisonFrames(other,holder,poison,20,at),"Combined poison");}
                TimedEffects.Settle(holder,at.AddMilliseconds(39));Require(SkillHealth.Balance256(holder)==before,"Combined source frame early");TimedEffects.Settle(holder,at.AddMilliseconds(40));int delta=NpcEquipmentRegeneration.Delta(0,item,poison+wound,true,blocked);Require(SkillHealth.Balance256(holder)==before+delta,"Wounds+poison+replenish/prevent-heal not one signed frame");
            }
            TimedEffects.Clear(holder);DateTime edge=TimedEffects.Now;SkillHealth.SetBalance256(holder,10);TimedEffects.PreparedOpenWounds(b,holder,10,p.Serial.Value,edge);TimedEffects.PoisonFrames(other,holder,16,20,edge);TimedEffects.Settle(holder,edge.AddMilliseconds(40));Require(holder.Alive&&SkillHealth.Balance256(holder)==4,"Poison killed before replenish+wound signed sum");
            TimedEffects.Clear(holder);edge=TimedEffects.Now;SkillHealth.SetBalance256(holder,5);TimedEffects.PreparedOpenWounds(b,holder,20,p.Serial.Value,edge);TimedEffects.PoisonFrames(other,holder,16,20,edge);TimedEffects.Settle(holder,edge.AddMilliseconds(40));Require(!holder.Alive&&holder.LastKiller==other,"Poison state did not win combined death credit");
        }
        finally{holder.Corpse?.Delete();holder.Delete();b.Delete();other.Delete();p.Delete();}
    }
    private static void Check()
    {
        Source();Events();Item();Combined();var p=Owner();var other=Owner();var b=Body(p);var target=new NpcPoisonRecipient();target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();DateTime at=TimedEffects.Now;
        Require(TimedEffects.PreparedOpenWounds(b,target,41,p.Serial.Value,at),"Saved exact wound");TimedEffects.Settle(target,at.AddMilliseconds(39));var actor=State(target);Require(actor.OpenWounds.ElapsedMs==39,"Saved39ms phase");uint source=b.Serial.Value;b.Delete();actor.Updated=TimedEffects.Now.AddHours(1);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,other.Serial.Value,source,target.Serial.Value,SkillHealth.Balance256(target),TimedEffects.Snapshot().Single(x=>x.Owner==target.Serial.Value),0)));
        Log.Information("NPC wounds:2160complete regen source comparisons;real hard9+soft1 refusal/live windup hard10unlock/respec,one callback draw,miss/block/zero/full absorption,pre-contact overdue lethal cancellation,actual item60+skill100single subscription,39/40ms FP/no recovery,12signed poison/replenish/prevent-heal controls and poison-priority death pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var other=(PlayerMobile)World.FindMobile((Serial)s.Other);var target=(NpcPoisonRecipient)World.FindMobile((Serial)s.Target);var actor=State(target);
        Require(World.FindMobile((Serial)s.Source)==null&&SkillHealth.Balance256(target)==s.Life&&JsonSerializer.Serialize(TimedEffects.Snapshot().Single(x=>x.Owner==s.Target))==JsonSerializer.Serialize(s.State)&&actor.OpenWounds is{Rate256:41,ElapsedMs:39}&&actor.BleedSource.Value==s.Source&&actor.OpenWounds.CreditOwner==p.Serial.Value,"Saved wound phase/rate/real creator/durable owner/health changed");
        int calls=target.DamageCalls;DateTime at=actor.Updated.AddMilliseconds(1);TimedEffects.Settle(target,at);Require(s.Life-SkillHealth.Balance256(target)==41&&target.DamageCalls==calls,"Saved pending frame replayed/lost or used native Damage");TimedEffects.PreparedOpenWounds(other,target,82,other.Serial.Value,at);Require(State(target).OpenWounds.Rate256==41&&State(target).OpenWounds.CreditOwner==p.Serial.Value&&State(target).BleedSource.Value==s.Source,"Refresh replaced first exact wound creator/rate");TimedEffects.Settle(target,at.AddMilliseconds(39));
        var original=TimedEffects.Snapshot();var sample=new NpcPoisonRecipient();sample.MoveToWorld(target.Location,target.Map);sample.AIObject.AITimer.Stop();SkillHealth.SetBalance256(sample,41);
        try{var copy=original.Single(x=>x.Owner==s.Target) with{Owner=sample.Serial.Value};TimedEffects.Load(original.Append(copy).ToArray());TimedEffects.Settle(sample,copy.UpdatedUtc.AddMilliseconds(1));Require(!sample.Alive&&sample.LastKiller==p&&sample.DamageCalls==0,"Saved source-removal copy lost owner credit or used hit damage");}finally{sample.Corpse?.Delete();sample.Delete();TimedEffects.Load(original);}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Life=SkillHealth.Balance256(target),State=TimedEffects.Snapshot().Single(x=>x.Owner==s.Target),Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner(10,3600);FixtureNetwork.Authenticate(network,p,"npc-wounds");var b=Body(p);var target=Target(p,b);target.SetResistance(ResistanceType.Physical,100);var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;NpcEquipmentRandom.Set(b,BloodGolemWorldChecks.Seed(b,target));b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});long deadline=Core.TickCount+10000;bool applied=false;uint source=b.Serial.Value;
        void End(){SourceBlocking.BlockRoll=block;target.Corpse?.Delete();target.Delete();b.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&network.Running,"Natural NPC wounds timeout/disconnect");
                if(!applied)
                {
                    if(!TimedEffects.TryActor(target,out var a)||a.OpenWounds==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                    Require(a.BleedSource.Value==source&&a.OpenWounds.CreditOwner==p.Serial.Value&&b.WorldState.Controller.Attempts==1,"Natural learned wound source/event");applied=true;int rate=a.OpenWounds.Rate256;b.Delete();SkillHealth.SetBalance256(target,2*rate+1);
                }
                if(target.Alive){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(applied&&target.LastKiller==p&&ClassLoot.Killer(target)==p&&World.FindMobile((Serial)source)==null,"Natural wound death after summon removal lost owner");End();Log.Information("npc-open-wounds-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"npc-open-wounds-live-result: failed");}
        }
        Log.Information("npc-open-wounds-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
