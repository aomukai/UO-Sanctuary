// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class SpellPoisonTickChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SpellPoisonTickChecks));
    private sealed record Stored(uint Target,int Balance,TimedEffectState State);
    private sealed record Saved(uint Source,Stored[] Targets,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"spell-poison-ticks-fixtures.json");
    private static readonly FirestormModifiers Plain=new(0,0,0,0,0);
    public static void Configure(){Register("spell-poison-ticks-check",Check);Register("spell-poison-ticks-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Pure poison spell tick scheduler",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static TimedEffectState State(Mobile t)=>TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);
    private static GraveyardSkeleton Target(PlayerMobile p,int resistance=0)
    {
        var t=PoisonDaggerChecks.Target(p,resistance);
        foreach(var kind in new[]{ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy}){t.SetResistance(kind,0);}
        return t;
    }
    private static void Replace(Mobile target,TimedEffectState state)=>TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=target.Serial.Value).Append(state).ToArray());
    private static int Advance(Mobile t,int ms)
    {int before=SkillHealth.Balance256(t);TimedEffects.Settle(t,State(t).UpdatedUtc.AddMilliseconds(ms));return before-SkillHealth.Balance256(t);}
    private static void Clear(Mobile target)=>TimedEffects.Clear(target);
    private static void Basic()
    {
        var p=PoisonDaggerChecks.Owner();var t=Target(p);try
        {
            var at=TimedEffects.Now;Require(SpellPoisonTicks.Apply(p,t,2560,50,Plain,at),"Initial spell poison");
            Require(Advance(t,39)==0&&State(t).SpellPoison.ElapsedMs==39,"Early poison frame");
            Require(Advance(t,1)==2560,"Base poison tick without potion");
            Require(CombatPotions.Apply(p,3,State(t).UpdatedUtc),"Late potion");
            // Extra is256 per element. Combine poison before resistance;
            // all four additions stay independent and never compound.
            Require(Advance(t,40)==3584,"Late potion eligibility or four independent elements");
            TimedEffects.RemoveBuff(p,TimedEffectCategory.Potion);Require(Advance(t,40)==2560,"Removed potion still contributes");
            Require(TimedEffects.CleansePoison(t,0,State(t).UpdatedUtc),"Cure rejected");
            int balance=SkillHealth.Balance256(t);TimedEffects.Settle(t,State(t).UpdatedUtc.AddSeconds(10));Require(SkillHealth.Balance256(t)==balance&&!TimedEffects.Snapshot().Any(x=>x.Owner==t.Serial.Value&&x.SpellPoison!=null),"Cure left bonus ticks");
            Clear(t);Require(CombatPotions.Apply(p,3,at),"Expiry buff");
            var buff=State(p);Replace(p,buff with{Buffs=buff.Buffs.Select(x=>x with{ExpiresUtc=at.AddMilliseconds(80)}).ToArray()});
            Require(SpellPoisonTicks.Apply(p,t,2560,4,Plain,at),"Expiry spell");Require(Advance(t,160)==3584+3*2560,"Historical tick expiry or final tick");
            Require(!TimedEffects.Snapshot().Any(x=>x.Owner==t.Serial.Value&&x.SpellPoison!=null),"Expired exposure retained");
        }finally{t.Delete();p.Delete();}
    }
    private static void Immune()
    {
        var p=PoisonDaggerChecks.Owner();var t=Target(p,100);try
        {
            var at=TimedEffects.Now;Require(SpellPoisonTicks.Apply(p,t,2560,50,Plain,at),"Immune exposure rejected");
            var s=State(t);Require(s.PoisonMs==0&&s.PoisonClock==null&&s.SpellPoison.Rate256==0&&TimedEffects.CanRegenerateHits(t),"Immune exposure fabricated poison status/life-mask");
            Require(Advance(t,40)==0,"Immune original poison damaged");Require(CombatPotions.Apply(p,3,State(t).UpdatedUtc),"Immune late potion");
            t.SetResistance(ResistanceType.Fire,100);t.SetResistance(ResistanceType.Cold,50);t.SetResistance(ResistanceType.Energy,-50);
            Require(Advance(t,40)==512,"Element-specific immunity/50/-50 defenses");
            Require(TimedEffects.CleansePoison(t,0,State(t).UpdatedUtc),"Cure ignored zero-rate exposure");
            int before=SkillHealth.Balance256(t);TimedEffects.Settle(t,State(t).UpdatedUtc.AddSeconds(5));Require(SkillHealth.Balance256(t)==before,"Immune cure left bonus ticks");
            Clear(t);Require(SpellPoisonTicks.Apply(p,t,2560,50,Plain,at),"Removed-source exposure");p.Delete();Require(Advance(t,40)==0,"Removed source supplied active potion");
        }finally{t.Delete();p.Delete();}
    }
    private static void Replacement()
    {
        var p=PoisonDaggerChecks.Owner();var t=Target(p);try
        {
            var at=TimedEffects.Now;Require(SpellPoisonTicks.Apply(p,t,2560,50,Plain,at),"Strong spell");Advance(t,20);
            Require(!SpellPoisonTicks.Apply(p,t,1280,100,Plain,State(t).UpdatedUtc),"Weaker spell replaced strongest");
            Require(State(t).SpellPoison.RawRate256==2560&&State(t).SpellPoison.ElapsedMs==0&&State(t).PoisonClock.ElapsedMs==0,"Weaker application reset only one phase");
            Require(Advance(t,20)==0&&Advance(t,20)==2560,"Rescheduled strongest poison");
            Require(SpellPoisonTicks.Apply(p,t,5120,100,Plain,State(t).UpdatedUtc)&&State(t).SpellPoison.RawRate256==5120&&State(t).PoisonMs==4000,"Stronger spell did not replace exposure");
            Require(TimedEffects.CleansePoison(t,50,State(t).UpdatedUtc)&&State(t).PoisonMs==2000&&State(t).SpellPoison.RemainingMs==2000,"Cleansing split spell/base duration");
            Require(TimedEffects.PoisonFrames(p,t,7680,25,State(t).UpdatedUtc)&&State(t).SpellPoison==null,"Nonspell replacement retained spell bonuses");
            Clear(t);Require(SpellPoisonTicks.Apply(p,t,2560,50,Plain,at),"Restore candidate");var pool=TimedEffects.Snapshot();var original=State(t);
            foreach(var invalid in new[]{original.SpellPoison with{ElapsedMs=40},original.SpellPoison with{Rate256=1},original.SpellPoison with{RemainingMs=20},original.SpellPoison with{Source=0}})
            {
                try{Replace(t,original with{SpellPoison=invalid});throw new InvalidOperationException("Corrupt exposure accepted");}catch(InvalidDataException){}
                Require(JsonSerializer.Serialize(TimedEffects.Snapshot())==JsonSerializer.Serialize(pool),"Failed restore partially replaced effects");
            }
            var old=JsonSerializer.Serialize(original with{SpellPoison=null});old=old.Replace(",\"SpellPoison\":null","");
            var legacy=JsonSerializer.Deserialize<TimedEffectState>(old);TimedEffectProfiles.Validate(legacy);Require(legacy.SpellPoison==null,"Legacy JSON invented exposure");
            TimedEffects.ClearGear(t);Require(!TimedEffects.Snapshot().Any(x=>x.Owner==t.Serial.Value),"Gear/death clear retained exposure");
        }finally{t.Delete();p.Delete();}
    }
    private static void MasteryAndCredit()
    {
        var p=PoisonDaggerChecks.Owner();var t=Target(p);try
        {
            var at=TimedEffects.Now;Require(CombatPotions.Apply(p,3,at)&&SpellPoisonTicks.Apply(p,t,2560,50,new(999,100,-50,-100,25),at),"Captured mastery exposure");
            Require(Advance(t,40)==3520,"Captured independent masteries or ignored launch potion snapshot");
            Clear(t);var probe=new NpcPoisonRecipient();probe.MoveToWorld(t.Location,t.Map);probe.AIObject.AITimer.Stop();
            try
            {
                foreach(var kind in new[]{ResistanceType.Poison,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy}){probe.SetResistance(kind,0);}
                Require(SpellPoisonTicks.Apply(p,probe,2560,50,Plain,at),"Credit exposure");int calls=probe.DamageCalls;
                SkillHealth.SetBalance256(probe,3584);Advance(probe,40);
                Require((!probe.Alive||probe.Deleted)&&probe.LastKiller==p&&probe.DamageCalls==calls,"Combined tick death/credit dispatched an ordinary hit");
            }finally{probe.Corpse?.Delete();probe.Delete();}
        }finally{t.Delete();p.Delete();}
    }
    private static void CombinedPayment()
    {
        var source=PoisonDaggerChecks.Owner();var target=PoisonDaggerChecks.Owner();
        try
        {
            var at=TimedEffects.Now;Require(CombatPotions.Apply(target,5,at)&&TimedEffects.PoisonFrames(source,target,2048,50,at),"Protection payment setup");
            Require(TimedEffects.TryActor(target,out var actor),"Missing poison actor");int before=SkillHealth.Balance256(target);actor.PoisonMs-=40;
            PoisonStates.Advance(target,actor,40,1536);
            Require(before-SkillHealth.Balance256(target)==1024,"Protection applied separately to8base+6bonus instead of once to14");
            var saved=State(target);Require(!SpellPoisonTicks.Apply(source,target,2560,50,Plain,at),"Friendly player acquired spell poison");
            Require(JsonSerializer.Serialize(State(target))==JsonSerializer.Serialize(saved),"Unsupported PvP mutated poison");
        }finally{target.Delete();source.Delete();}
        source=PoisonDaggerChecks.Owner();target=PoisonDaggerChecks.Owner();
        var pet=new SkeletonGuardian{Name="Spell poison replenish fixture"};Item item=null;var friendly=ClassCombatHooks.Friendly;
        try
        {
            pet.Initialize(target,7,2);pet.ControlSlots=0;pet.NoKillAwards=true;Require(pet.SetControlMaster(target),"Replenish ownership");
            pet.MoveToWorld(new(source.X+2,source.Y,source.Z),source.Map);pet.AIObject.AITimer.Stop();
            item=NamedGear.Create("unique/134",99,new Random(317),true);item.MoveToWorld(new(source.X+1,source.Y,source.Z),source.Map);
            Require(IronGolemItems.TryAttach(target,pet,item,out _),"Replenish original bind");
            foreach(var kind in new[]{ResistanceType.Poison,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy}){pet.SetResistance(kind,-10);}
            var at=TimedEffects.Now;Require(CombatPotions.Apply(source,3,at)&&NpcEquipmentRegeneration.ItemRate(pet)==20,"Replenish bonus controls");
            Require(TimedEffects.PoisonFrames(source,pet,100,50,at),"Replenish kernel poison");
            TimedEffects.Actor(pet,at).SpellPoison=new(source.Serial.Value,100,100,0,2000,0,0,0,0,0);SkillHealth.SetBalance256(pet,125);
            TimedEffects.Settle(pet,at.AddMilliseconds(40));Require(pet.Alive&&SkillHealth.Balance256(pet)==5,$"Bonus/replenish payment:alive={pet.Alive};balance={SkillHealth.Balance256(pet)};item={NpcEquipmentRegeneration.ItemRate(pet)};fire={pet.FireResistance};cold={pet.ColdResistance};lightning={pet.EnergyResistance};poison={pet.PoisonResistance};MDR={GemCombat.Reduction(pet)}");
            // Separate original+added poison would truncate55+5 instead of61
            // for111raw/11added at50% resistance; defenses must see the combined value.
            TimedEffects.Clear(pet);pet.SetResistance(ResistanceType.Poison,40);SkillHealth.SetBalance256(pet,10000);
            Require(TimedEffects.PoisonFrames(source,pet,55,50,at),"Odd-rate kernel poison");
            TimedEffects.Actor(pet,at).SpellPoison=new(source.Serial.Value,111,55,50,2000,0,0,0,0,0);TimedEffects.Settle(pet,at.AddMilliseconds(40));
            Require(SkillHealth.Balance256(pet)==10000-(61+33-20),"Combined poison resistance or integer bonus basis");
        }
        finally{ClassCombatHooks.Friendly=friendly;IronGolemItems.DestroyBoundItem(pet);pet.Delete();item?.Delete();target.Delete();source.Delete();}
    }
    private static void Check()
    {
        Basic();Immune();Replacement();MasteryAndCredit();CombinedPayment();
        var p=PoisonDaggerChecks.Owner();var at=TimedEffects.Now;Require(CombatPotions.Apply(p,3,at),"Saved source potion");
        var frozen=TimedEffects.Now.AddHours(1);var targets=new[]{Target(p,100),Target(p)};var rows=new Stored[2];
        for(int i=0;i<targets.Length;i++)
        {
            var t=targets[i];Require(SpellPoisonTicks.Apply(p,t,2560,50,Plain,at),"Saved exposure");Advance(t,17);
            Replace(t,State(t) with{UpdatedUtc=frozen});t.Internalize();rows[i]=new(t.Serial.Value,SkillHealth.Balance256(t),State(t));
        }
        var buff=State(p);Replace(p,buff with{Buffs=buff.Buffs.Select(x=>x with{ExpiresUtc=frozen.AddMinutes(1)}).ToArray()});p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,rows,0)));
        Log.Information("Spell poison ticks:late/removed/expired potion,39+1ms phase,immune independent defenses/cure,removed caster,strongest replacement,cleansing,masteries/death/credit,one Protection/replenish payment,legacy and atomic restore controls pass");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Source);var rows=new Stored[2];
        for(int i=0;i<saved.Targets.Length;i++)
        {
            var old=saved.Targets[i];var t=World.FindMobile((Serial)old.Target);
            Require(old.Balance==SkillHealth.Balance256(t)&&JsonSerializer.Serialize(State(t))==JsonSerializer.Serialize(old.State),"Saved exposure/health changed");
            Require(State(t).SpellPoison.ElapsedMs==17,"Saved phase changed");
            Require(Advance(t,22)==0&&Advance(t,1)==(i==0?768:3584),"Reload replayed/skipped fractional frame");
            Require((State(t).PoisonMs==0)==(i==0)&&TimedEffects.CanRegenerateHits(t)==(i==0),"Reload changed poison mask");
            Advance(t,17);Require(State(t).SpellPoison.ElapsedMs==17,"Next saved phase");rows[i]=new(t.Serial.Value,SkillHealth.Balance256(t),State(t));
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Targets=rows,Generation=saved.Generation+1}));
    }
}
