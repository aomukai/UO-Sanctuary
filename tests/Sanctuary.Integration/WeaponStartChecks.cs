// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class WeaponStartChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WeaponStartChecks));
    private sealed record Rule(string Source,bool Immune);
    private sealed record Saved(uint Owner,uint HealTarget,uint DefenseTarget,DateTime Until,int Loss,int Defense);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"weapon-start-fixtures.json");
    public static void Configure(){Register("weapon-start-check",Check);Register("weapon-start-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Weapon hit-roll/setup effects and real reload",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static DateTime Until(Mobile target)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==target.Serial.Value)?.HealBlockedUntilUtc??default;
    private static bool Begin(PlayerMobile p,BaseCreature t)
    {PoisonDaggerChecks.Ready(p);return ClassCombat.Perform(p,CombatAbility.PoisonDagger,t,t.Location,Core.TickCount);}
    private static void HitRoll()
    {
        foreach(bool hit in new[]{false,true})foreach(bool blocked in new[]{false,true})
        {
            var p=PoisonDaggerChecks.Owner();PoisonDaggerChecks.Named(p,"Spineripper");var t=PoisonDaggerChecks.Target(p);bool blockSeen=false;
            try
            {
                Require(ClassGear.Bonus(p,"noheal")==1,"Real Prevent Monster Heal dagger");
                string source=CreatureProfiles.Definition(t).Reference;
                SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=t.Serial.Value).Append(new(t.Serial.Value,source,1)).ToArray());
                PoisonDaggerPackets.HitRoll=()=>hit?0:1;SourceBlocking.BlockRoll=_=>
                {blockSeen=true;Require(Until(t)>TimedEffects.Now,"Prevent Heal was not applied before shield roll");return blocked?0:99;};
                int hp=SkillHealth.Balance256(t);Require(Begin(p,t),"Actual setup start");var prepared=ClassCombat.For(p).PoisonDaggerAttack;
                Require(blockSeen==hit&&(Until(t)>TimedEffects.Now)==hit&&SkillHealth.Balance256(t)==hp,"Miss/hit/block start side effects or premature damage");
                if(hit&&!blocked)
                {
                    // Preserve an independently earlier active deadline across
                    // the actual release; a late legacy AfterStrike refresh
                    // would overwrite it even inside the same game-loop tick.
                    var state=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);var deadline=state.HealBlockedUntilUtc.AddMinutes(-1);
                    TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=t.Serial.Value).Append(state with{HealBlockedUntilUtc=deadline}).ToArray());
                    Require(ClassCombat.ResolveAction(p,prepared.EffectAt)&&Until(t)==deadline,"Release refreshed start-only Prevent Heal");
                }
                else
                {
                    ClassCombat.Stop(p);Require((Until(t)>TimedEffects.Now)==hit&&SkillHealth.Balance256(t)==hp,"Stop reverted a completed hit-roll effect");
                }
            }
            finally{PoisonDaggerChecks.Drop(p,t);}
        }
    }
    private static void EquippedDefense()
    {
        var p=PoisonDaggerChecks.Owner();var actor=new SkeletonGuardian();actor.Initialize(p,7,2);actor.ControlSlots=0;
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Shaftstop").Id,99,new Random(173),true);
        try
        {
            Require(actor.SetControlMaster(p),"Equipped defense holder ownership");actor.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);actor.AIObject.AITimer.Stop();
            item.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);Require(IronGolemItems.TryAttach(p,actor,item,out string why),"Actual armor attachment: "+why);
            int basis=CreatureProfiles.Defense(actor),armor=NpcEquipmentCombat.Defense(actor);Require(armor>0,"Actual original armor defense");
            TimedEffects.ReduceDefense(actor,int.MaxValue,TimedEffects.Now);
            Require(TimedEffects.DefenseLoss(actor)==basis+armor&&ClassStats.Defense(actor)==0,"Armor loss capped before attached NPC equipment was included");
        }
        finally{IronGolemItems.DestroyBoundItem(actor);actor.Delete();item.Delete();p.Delete();}
    }
    private static void Defense()
    {
        var p=PoisonDaggerChecks.Owner();PoisonDaggerChecks.Named(p,"Woestave");
        var t=new Ogre{CantWalk=true,Frozen=true,NoKillAwards=true,VirtualArmor=400};t.RawDex=1000;t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        try
        {
            int basis=CreatureProfiles.Defense(t)+NpcEquipmentCombat.Defense(t);Require(basis>100&&ClassGear.Bonus(p,"dmg-ac")==-50,$"Actual defense-loss weapon/basis:armor{basis},delta{ClassGear.Bonus(p,"dmg-ac")},dex{t.Dex}");
            PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Defiance,1,100,TimedEffects.Now.AddDays(1)));
            Require(ClassStats.Defense(t)==basis*2,"Real Defiance fixture");
            WeaponStartEffects.PrepareDamage(p,t);
            Require(TimedEffects.DefenseLoss(t)==50&&ClassStats.Defense(t)==(basis-50)*2,"Target armor loss did not precede percent defense");
            WeaponStartEffects.PrepareDamage(p,t);
            Require(TimedEffects.DefenseLoss(t)==100&&ClassStats.Defense(t)==(basis-100)*2,"Repeated target base armor loss");
            // An item-independent curse/Inner Sight reduction must still compose
            // with the mutated raw armor before the percent multiplier.
            AmazonInnerSight.Restore(AmazonInnerSight.Snapshot().Append(new(t.Serial.Value,p.Serial.Value,1,10,TimedEffects.Now.AddDays(1))).ToArray());
            Require(ClassStats.Defense(t)==Math.Max(0,(basis-110)*2),"Inner Sight/target armor/percent composition");
            AmazonInnerSight.Clear(t);PaladinAuras.Clear(t);
            var heal=PoisonDaggerChecks.Target(p);PoisonDaggerChecks.Named(p,"Blackhorn's Face");WeaponStartEffects.SuccessfulHitRoll(p,heal);
            Require(Until(heal)>TimedEffects.Now,"Saved Prevent Heal setup");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,heal.Serial.Value,t.Serial.Value,Until(heal),TimedEffects.DefenseLoss(t),ClassStats.Defense(t))));
            // Retain these real actors for two native save/load cycles.
            p=null;t=null;
        }
        finally{if(t!=null){AmazonInnerSight.Clear(t);PaladinAuras.Clear(t);t.Delete();}p?.Delete();}
    }
    private static void Check()
    {
        var rules=JsonSerializer.Deserialize<Rule[]>(File.ReadAllText(Path.Join(Core.BaseDirectory,"weapon-start-source-cases.json")));
        Require(rules.Length==751&&rules.Count(x=>x.Immune)==9&&rules.All(x=>WeaponStartEffects.PreventHealAllowed(x.Source)!=x.Immune),"Current pinned monster PMH flags");
        Require(WeaponStartEffects.PreventHealAllowed(null),"Native unclassified monster policy");
        var hit=PoisonDaggerPackets.HitRoll;var poison=PoisonDaggerPackets.PoisonRoll;var block=SourceBlocking.BlockRoll;var shields=SourceBlocking.Snapshot();
        try{PoisonDaggerPackets.PoisonRoll=_=>0;HitRoll();EquippedDefense();Defense();}
        finally{PoisonDaggerPackets.HitRoll=hit;PoisonDaggerPackets.PoisonRoll=poison;SourceBlocking.BlockRoll=block;SourceBlocking.Restore(shields);}
        Log.Information("Weapon start:751current monster PMH flags,4real Spineripper hit/shield cases,start-only persistence after Stop/release,real attached armor cap,Woestave base armor before Defiance/Inner Sight and saved deadlines/loss pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var heal=World.FindMobile((Serial)s.HealTarget);var t=(BaseCreature)World.FindMobile((Serial)s.DefenseTarget);
        Require(p is{Deleted:false}&&heal is{Alive:true,Deleted:false}&&t is{Alive:true,Deleted:false}&&Until(heal)==s.Until&&TimedEffects.DefenseLoss(t)==s.Loss
            &&ClassStats.Defense(t)==s.Defense&&!TimedEffects.CanRegenerateHits(heal),"Saved start-only defense/Prevent Heal deadline changed");
    }
}
