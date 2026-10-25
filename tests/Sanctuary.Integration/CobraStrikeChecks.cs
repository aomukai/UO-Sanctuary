// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public sealed class CobraDeathTarget:GraveyardZombie
{
    public static Action Died;
    public CobraDeathTarget(){}
    public CobraDeathTarget(Serial serial):base(serial){}
    public override void OnThink(){}
    public override void OnSectorActivate()=>AIObject?.AITimer.Stop();
    public override bool CanRegenHits=>false;
    public override void OnDeath(Container c){Died?.Invoke();base.OnDeath(c);}
    public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
    public override void Deserialize(IGenericReader reader){base.Deserialize(reader);if(reader.ReadInt()!=0){throw new InvalidDataException("Cobra death fixture version");}}
}
public static class CobraStrikeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CobraStrikeChecks));
    private sealed class FixedRandom(int value):Random{public override int Next(int max)=>Math.Clamp(value,0,Math.Max(0,max-1));}
    private sealed record Saved(uint Owner,uint Target,MartialCharge[] Charges,int Mana,int Life,int TargetMana,int OwnerLife,uint Primary,uint Secondary,Point3D Origin,uint Pending,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"cobra-strike-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=x=>Creating || x.Name?.StartsWith("CobraFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):hp?.Invoke(x)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=x=>Creating || x.Name?.StartsWith("CobraFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(x)??Mobile.DefaultManaRate;
    }
    public static void Configure(){Rates();Register("cobra-strike-check",Check);Register("cobra-strike-reload-check",Reload);Register("cobra-strike-input-check",Input);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Cobra Strike shared charge action, source player drain, input and world reload",_=>Core.LoopContext.Post(()=>
    {try{if(name=="cobra-strike-input-check"){action();}else{Controlled(action);}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int rank)=>new(SkillMechanics.For(CombatAbility.CobraStrike),_=>rank,_=>0,99);
    private static void Controlled(Action action)
    {
        var hit=TigerStrikes.HitRoll;var roll=TigerStrikes.ElementRoll;var physical=TigerStrikes.PhysicalRoll;var effects=TigerStrikes.Effects;var block=SourceBlocking.BlockRoll;
        try{TigerStrikes.HitRoll=()=>0;TigerStrikes.ElementRoll=()=>0;TigerStrikes.PhysicalRoll=_=>0;TigerStrikes.Effects=new FixedRandom(99);SourceBlocking.BlockRoll=_=>99;action();}
        finally{TigerStrikes.HitRoll=hit;TigerStrikes.ElementRoll=roll;TigerStrikes.PhysicalRoll=physical;TigerStrikes.Effects=effects;SourceBlocking.BlockRoll=block;}
    }
    private static PlayerMobile Fresh(int column=5800)
    {
        Creating=true;try{var p=BladeBodyChecks.Fresh(column);p.Name="CobraFixtureOwner";while(p.RawStr<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}Resources(p);return p;}finally{Creating=false;}
    }
    private static BaseCreature Target(PlayerMobile p,MonsterDifficulty difficulty=MonsterDifficulty.Normal,bool skeleton=false,bool death=false)
    {
        Creating=true;try
        {
            BaseCreature t=skeleton?new GraveyardSkeleton():death?new CobraDeathTarget():difficulty!=MonsterDifficulty.Normal?new GraveyardZombie():new BladeMissileTarget();
            if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(t,difficulty);}t.Name="CobraFixtureTarget";t.Frozen=t.CantWalk=true;t.NoKillAwards=true;t.AIObject?.AITimer.Stop();t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.SetHits(50000);t.Hits=t.HitsMax;t.SetMana(100);NecromancerEffects.SetMana256(t,100*256+17);
            foreach(var k in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(k,0);}return t;
        }finally{Creating=false;}
    }
    private static void Delete(BaseCreature target){target.Corpse?.Delete();target.Delete();}
    private static void Resources(PlayerMobile p){SkillHealth.SetBalance256(p,20*256+17);NecromancerEffects.SetMana256(p,20*256+63);}
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static void Hold(PlayerMobile p,params MartialCharge[] states)=>AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Concat(states).ToArray());
    private static MartialCharge Cobra(PlayerMobile p)=>AssassinCharges.State(p,CombatAbility.CobraStrike);
    private static int Swing(PlayerMobile p,Mobile target,CombatAbility ability=CombatAbility.CobraStrike)
    {
        Ready(p);int hp=SkillHealth.Balance256(target);Require(ClassCombat.Perform(p,ability,target,target.Location,Core.TickCount),"Accepted "+ability);ClassCombat.ReleaseCast(p);long at=ClassCombat.For(p).EffectAt;
        Require(!ClassCombat.ResolveAction(p,at-1) && ClassCombat.ResolveAction(p,at) && !ClassCombat.ResolveAction(p,at),"Once-only action event");return hp-SkillHealth.Balance256(target);
    }
    private static Item Named(PlayerMobile p,string key)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new FixedRandom(99),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Named "+key);ClassGear.Refresh(p);return item;}
    private static void Differential()
    {
        var row=SkillMechanics.For(CombatAbility.CobraStrike);Require(row.SourceId==265 && row.Number("srvstfunc")==23 && row.Number("srvdofunc")==34 && row.Number("prgdam")==2 && row.Number("SrcDam")==128,"Pinned Cobra identity");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"cobra-strike-source-probe.json")));int drains=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();bool apply=V(3)!=0 && V(4)!=0 && V(5)!=0 && V(6)!=0 && V(7)==2;
            var state=new MartialCharge(1,CombatAbility.CobraStrike,V(0),V(1),V(0)*50,DateTime.MaxValue);var actual=apply?AssassinCharges.CobraDrain(state,V(2)):default;
            Require(V(8)==17 && V(9)==13+actual.LifePercent && V(10)==19+actual.ManaPercent && V(11)==V(3),"Complete Cobra packet consumer");drains++;
        }
        using var mix=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"cobra-strike-composition-source-probe.json")));int compositions=0;
        foreach(var r in mix.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();var tiger=new MartialCharge(1,CombatAbility.TigerStrike,V(0),V(2),V(0)*50,DateTime.MaxValue);var cobra=new MartialCharge(1,CombatAbility.CobraStrike,V(1),V(4),V(1)*50,DateTime.MaxValue);
            var actual=V(6)!=0?AssassinCharges.CobraDrain(cobra,V(5)):default;int ed=V(6)!=0?AssassinCharges.TigerPercent(tiger,V(3)):0;
            Require(V(7)==17+ed && V(8)==13+actual.LifePercent && V(9)==19+actual.ManaPercent,"Complete two-state finisher composition");compositions++;
        }
        using var build=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"cobra-strike-builder-source-probe.json")));int builders=0;
        foreach(var r in build.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();bool valid=V(5)!=0 && V(6)!=0,has=V(0)>0;
            if(valid && V(3)!=0 && (has || V(4)!=0))
            {MartialCharge old=has?new(1,CombatAbility.CobraStrike,V(0),V(1),V(0)*50,DateTime.UnixEpoch.AddMilliseconds(321*40)):null;var state=AssassinCharges.Build(old,1,Values(V(2)),DateTime.UnixEpoch.AddMilliseconds(100*40));Require(state.Count==V(9) && state.AttackPercent==V(10) && state.InitialRank==V(11) && state.UntilUtc==DateTime.UnixEpoch.AddMilliseconds(V(12)*40),"Complete Cobra-shaped builder");}
            Require(V(7)==(valid?1:0) && V(13)==(valid?1:0) && V(14)==(valid?1:0),"Source allocation/drain controls");builders++;
        }
        for(int rank=1;rank<=255;rank++){var v=Values(rank);Require(v.Mana256==512 && v.Formula("toht")==25+10*(rank-1) && v.Eval("calc1")==40+5*(rank-1) && v.Eval("auralencalc")==375,"Cobra rank golden");}
        Require(drains==6000 && compositions==4608 && builders==512,"Incomplete Cobra differential");Log.Information("Cobra Strike:6000 complete drains,4608 two-state compositions,512 builders and255 rank goldens match");
    }
    private static (PreparedWeaponImpact Packet,PlayerWeaponDrain Drain,WeaponLeechResult Recovery) Expected(PlayerMobile p,BaseCreature t)
    {
        var tiger=AssassinCharges.State(p);int? ed=tiger==null?null:AssassinCharges.TigerPercent(tiger,SkillMechanics.Rank(p,CombatAbility.TigerStrike));
        var packet=GearEffects.PrepareWeaponImpact(p,t,0,new FixedRandom(99),GearEffects.Damage(p,0,t,ed,_=>0));var extra=AssassinCharges.CobraDrain(Cobra(p),SkillMechanics.Rank(p,CombatAbility.CobraStrike));var drain=WeaponLeech.Capture(p,extra);
        int div=MonsterVariants.Difficulty(t) switch{MonsterDifficulty.Nightmare=>2,MonsterDifficulty.Hell=>3,_=>1};
        return(packet,drain,WeaponLeech.Calculate(NamedElements.To256(packet.Parts.Physical),SkillHealth.Balance256(t),NecromancerEffects.ManaBalance256(t),drain.LifePercent,drain.ManaPercent,GemCombat.Drain(t),div,div));
    }
    private static void Finish(PlayerMobile p,BaseCreature t,CombatAbility ability=CombatAbility.BasicAttack)
    {
        int hp=SkillHealth.Balance256(p),mp=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);var states=AssassinCharges.Finisher(p);var e=Expected(p,t);
        int loss=Swing(p,t,ability),damage=NamedElements.To256(e.Packet.Parts.Total);int expectedLoss=life-damage<256?life:damage;
        Require(loss==expectedLoss && SkillHealth.Balance256(p)==Math.Min(SkillHealth.Maximum256(p),hp+e.Recovery.Life256) && NecromancerEffects.ManaBalance256(p)==Math.Min(p.ManaMax*256,mp+e.Recovery.Mana256),"Physical-only merged FP256 drain/cap/damage "+MonsterVariants.Difficulty(t));
        if(t.Alive){Require(NecromancerEffects.ManaBalance256(t)==e.Recovery.VictimMana256,"Scoped legacy victim-mana tail");}
        foreach(var state in states){Require(AssassinCharges.State(p,state.Ability)==AssassinCharges.Consumed(state,1),"One-of-each charge consumed");}
    }
    private static void Cases(PlayerMobile p)
    {
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.CobraStrike) && SkillBindings.Instance.Assign(p,false,CombatAbility.CobraStrike) && SkillBindings.Instance.AssignQuick(p,0,CombatAbility.CobraStrike),"Cobra bindings");
        var dagger=new Dagger();ClassGear.Profiles.Convert(dagger,1,GearRarity.Normal,new Random(173));Require(p.EquipItem(dagger),"Dagger");ClassGear.Refresh(p);var target=Target(p);int hp=SkillHealth.Balance256(p),mana=NecromancerEffects.ManaBalance256(p),rolls=0;
        TigerStrikes.HitRoll=()=>{rolls++;return 0;};Ready(p);Require(ClassCombat.ContinueCast(p,CombatAbility.CobraStrike,target,target.Location,Core.TickCount),"Held Cobra start");ClassCombat.ReleaseCast(p);var s=ClassCombat.For(p);
        Require(rolls==0 && Cobra(p)==null && NecromancerEffects.ManaBalance256(p)==mana-512,"Start pays only, no early hit/state");Require(ClassCombat.ResolveAction(p,s.EffectAt) && Cobra(p) is{Count:1,InitialRank:1,AttackPercent:50} && SkillHealth.Balance256(p)==hp,"First charging hit healed or failed");TigerStrikes.HitRoll=()=>0;
        for(int count=1;count<=3;count++)
        {Resources(p);if(count>1){Swing(p,target);}Require(Cobra(p).Count==count && SkillHealth.Balance256(p)==hp,"Charge building must not release skill leech");var original=Cobra(p);Resources(p);Finish(p,target);Hold(p,original);}
        var capped=Cobra(p);Hold(p,capped with{UntilUtc=TimedEffects.Now.AddSeconds(1)});Swing(p,target);Require(Cobra(p) is{Count:3,AttackPercent:150} && Cobra(p).UntilUtc>TimedEffects.Now.AddSeconds(14),"Cobra cap/refresh/AR overflow");var before=Cobra(p);
        TigerStrikes.HitRoll=()=>1;hp=SkillHealth.Balance256(target);Swing(p,target);Require(Cobra(p)==before && SkillHealth.Balance256(target)==hp,"Miss gained/refreshed/damaged");Swing(p,target,CombatAbility.BasicAttack);Require(Cobra(p)==before && SkillHealth.Balance256(target)==hp,"Missed finisher consumed");TigerStrikes.HitRoll=()=>0;
        var blocked=Target(p,skeleton:true);SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=blocked.Serial.Value).Append(new(blocked.Serial.Value,CreatureProfiles.Definition(blocked).Reference,1)).ToArray());SourceBlocking.BlockRoll=_=>0;Require(Swing(p,blocked)==0 && Cobra(p)==before,"Blocked builder");Require(Swing(p,blocked,CombatAbility.BasicAttack)==0 && Cobra(p)==before,"Blocked finisher");SourceBlocking.BlockRoll=_=>99;Delete(blocked);
        Resources(p);Swing(p,target,CombatAbility.TigerStrike);Swing(p,target,CombatAbility.TigerStrike);Require(Cobra(p)==before && AssassinCharges.State(p).Count==2 && AssassinCharges.Finisher(p).Sum(x=>x.AttackPercent)==250,"Independent charge builders/AR");Resources(p);Finish(p,target);Require(Cobra(p).Count==2 && AssassinCharges.State(p).Count==1,"Mixed finisher");
        Hold(p,Cobra(p));var helm=Named(p,"Tarnhelm");Require(AssassinCharges.CobraDrain(Cobra(p),SkillMechanics.Rank(p,CombatAbility.CobraStrike))==new PlayerWeaponDrain(45,45),"Higher live rank");Swing(p,target);Require(Cobra(p).InitialRank==1,"Refresh changed initial rank");AssassinCharges.Clear(p);Swing(p,target);p.AddToBackpack(helm);ClassGear.Refresh(p);Require(Cobra(p).InitialRank==2 && AssassinCharges.CobraDrain(Cobra(p),1)==new PlayerWeaponDrain(45,0),"Stored rank lost on gear removal");
        Resources(p);Swing(p,target);Swing(p,target);NecromancerEffects.SetMana256(p,511);Finish(p,target,CombatAbility.CobraStrike);Require(Cobra(p).Count==2,"Insufficient mana did not ordinary-finish");
        var live=Cobra(p);Hold(p,live with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)});Require(!AssassinCharges.Any(p),"Expired Cobra still affects finisher");AssassinCharges.Prune();Delete(target);
        Log.Information("Cobra native: event-only charge build, cap/refresh/miss/block, independent Tiger/Cobra partial finishers, live/stored ranks and2mana fallback passed");
    }
    private static void Delivery(PlayerMobile p)
    {
        foreach(var (difficulty,drain) in new[]{(MonsterDifficulty.Normal,100),(MonsterDifficulty.Nightmare,75),(MonsterDifficulty.Hell,50)})
        {
            var t=Target(p,difficulty);Require(GemCombat.Drain(t)==drain,"Pinned drain difficulty");Hold(p,new MartialCharge(p.Serial.Value,CombatAbility.CobraStrike,3,1,150,TimedEffects.Now.AddSeconds(15)));Resources(p);Finish(p,t);Delete(t);
        }
        var noDrain=Target(p,skeleton:true);Require(GemCombat.Drain(noDrain)==0,"No-drain species");Hold(p,new MartialCharge(p.Serial.Value,CombatAbility.CobraStrike,3,1,150,TimedEffects.Now.AddSeconds(15)));Resources(p);Finish(p,noDrain);Delete(noDrain);
        var immune=Target(p);immune.SetResistance(ResistanceType.Physical,100);Hold(p,new MartialCharge(p.Serial.Value,CombatAbility.CobraStrike,3,1,150,TimedEffects.Now.AddSeconds(15)));Resources(p);Finish(p,immune);Delete(immune);
        foreach(int mana in new[]{0,575})
        {var t=Target(p);NecromancerEffects.SetMana256(t,mana);Hold(p,new MartialCharge(p.Serial.Value,CombatAbility.CobraStrike,3,1,150,TimedEffects.Now.AddSeconds(15)));Resources(p);Finish(p,t);Delete(t);}
        var cap=Target(p);Hold(p,new MartialCharge(p.Serial.Value,CombatAbility.CobraStrike,3,1,150,TimedEffects.Now.AddSeconds(15)));SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-1);NecromancerEffects.SetMana256(p,p.ManaMax*256-1);Finish(p,cap);Require(SkillHealth.Full(p) && NecromancerEffects.ManaBalance256(p)==p.ManaMax*256,"Resource caps");Delete(cap);
        var lethal=Target(p,death:true);SkillHealth.SetBalance256(lethal,257);Hold(p,new MartialCharge(p.Serial.Value,CombatAbility.CobraStrike,3,1,150,TimedEffects.Now.AddSeconds(15)));Resources(p);var expected=Expected(p,lethal);int h=SkillHealth.Balance256(p),m=NecromancerEffects.ManaBalance256(p),calls=0;
        try{CobraDeathTarget.Died=()=>{Require(SkillHealth.Balance256(p)==h+expected.Recovery.Life256 && NecromancerEffects.ManaBalance256(p)==m+expected.Recovery.Mana256 && Cobra(p).Count==2,"Leech/charge consumption must precede actual death callback");calls++;};Finish(p,lethal);Require(calls==1,"Missing lethal callback");}finally{CobraDeathTarget.Died=null;Delete(lethal);}
        Log.Information("Cobra delivery: physical-only FP256, all difficulties, zero drain/immunity/mana, resource caps and actual lethal callback ordering passed");
    }
    private static void GearAndSave(PlayerMobile p)
    {
        p.AddToBackpack(CombatTiming.Equipped(p));var a=new Tekagi();ClassGear.Profiles.Convert(a,99,GearRarity.Normal,new Random(173));p.AddToBackpack(a);Require(p.EquipItem(a),"Primary claw");var b=(BaseWeapon)Named(p,"Cutthroat1");var ring=Named(p,"Manald Heal");Require(WeaponHands.Pair(p)!=null && CombatTiming.Equipped(p)==a && ClassGear.Bonus(p,"life-steal%")==0 && ClassGear.Bonus(p,"mana-steal%")>0,"Selected claw drain filter");
        var t=Target(p);Resources(p);AssassinCharges.Clear(p);Swing(p,t);Swing(p,t);Require(Cobra(p).InitialRank==4,"Offhand soft skill capture");Resources(p);Finish(p,t);p.AddToBackpack(a);Require(CombatTiming.Equipped(p)==b && ClassGear.Bonus(p,"life-steal%")>0 && p.EquipItem(a),"Primary promotion");ClassGear.Refresh(p);
        Hold(p,new MartialCharge(p.Serial.Value,CombatAbility.CobraStrike,3,1,150,TimedEffects.Now.AddSeconds(15)),new MartialCharge(p.Serial.Value,CombatAbility.TigerStrike,2,1,100,TimedEffects.Now.AddSeconds(14)));Resources(p);var e=Expected(p,t);Require(e.Drain.LifePercent>AssassinCharges.CobraDrain(Cobra(p),SkillMechanics.Rank(p,CombatAbility.CobraStrike)).LifePercent && e.Drain.ManaPercent>0,"Combined source item/skill drain fields");Finish(p,t);
        var rounding=Target(p,MonsterDifficulty.Hell);Hold(p,new MartialCharge(p.Serial.Value,CombatAbility.CobraStrike,3,1,150,TimedEffects.Now.AddSeconds(15)),new MartialCharge(p.Serial.Value,CombatAbility.TigerStrike,2,1,100,TimedEffects.Now.AddSeconds(14)));Resources(p);var merged=Expected(p,rounding);
        int physical=NamedElements.To256(merged.Packet.Parts.Physical);var skill=AssassinCharges.CobraDrain(Cobra(p),SkillMechanics.Rank(p,CombatAbility.CobraStrike));var item=WeaponLeech.Capture(p);
        var separateSkill=WeaponLeech.Calculate(physical,SkillHealth.Balance256(rounding),0,skill.LifePercent,skill.ManaPercent,GemCombat.Drain(rounding),3,3);var separateItem=WeaponLeech.Calculate(physical,SkillHealth.Balance256(rounding),0,item.LifePercent,item.ManaPercent,GemCombat.Drain(rounding),3,3);
        Require(merged.Recovery.Mana256!=separateSkill.Mana256+separateItem.Mana256 || merged.Recovery.Life256!=separateSkill.Life256+separateItem.Life256,"Need a visible merged-before-rounding discriminator");Finish(p,rounding);Delete(rounding);
        var states=AssassinCharges.Finisher(p).Select(x=>x with{UntilUtc=TimedEffects.Now.AddHours(1)}).ToArray();Hold(p,states);
        var pending=Fresh(5840);var other=Target(pending);Ready(pending);Require(ClassCombat.Perform(pending,CombatAbility.CobraStrike,other,other.Location,Core.TickCount),"Paid saved pending");ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,states,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),NecromancerEffects.ManaBalance256(t),SkillHealth.Balance256(p),b.Serial.Value,a.Serial.Value,p.Location,pending.Serial.Value,NecromancerEffects.ManaBalance256(pending))));
    }
    private static void Lifecycle()
    {
        var p=Fresh(5880);var t=Target(p);Swing(p,t);var charge=Cobra(p);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.CobraStrike,t,t.Location,Core.TickCount),"FHR begin");long at=ClassCombat.For(p).EffectAt;Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount) && ClassCombat.For(p).TigerAttack==null && !ClassCombat.ResolveAction(p,at) && Cobra(p)==charge,"FHR cleanup");
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.CobraStrike,t,t.Location,Core.TickCount),"Range begin");at=ClassCombat.For(p).EffectAt;t.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,at) && Cobra(p)==charge,"Range loss gained/refreshed");Delete(t);p.Delete();
        p=Fresh(5880);AssassinCharges.Gain(p,Values(1));Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Paladin) && !AssassinCharges.Any(p) && ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Assassin),"History retained Cobra");AssassinCharges.Gain(p,Values(1));ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(!AssassinCharges.Any(p),"Respec retained Cobra");Require(ClassCreation.Profiles.TryTrain(p.Serial,"assassin.tigerstrike",out _) && ClassCreation.Profiles.TryTrain(p.Serial,"assassin.cobrastrike",out _),"Retrain");AssassinCharges.Gain(p,Values(1));p.Kill();p.Resurrect();Require(!AssassinCharges.Any(p),"Death retained Cobra");p.Corpse?.Delete();p.Delete();
    }
    private static void Check(){Differential();var p=Fresh();Cases(p);Delivery(p);GearAndSave(p);Lifecycle();}
    private static void Reload()
    {
        Differential();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var pending=(PlayerMobile)World.FindMobile((Serial)s.Pending);
        Require(AssassinCharges.Finisher(p).SequenceEqual(s.Charges) && NecromancerEffects.ManaBalance256(p)==s.Mana && SkillHealth.Balance256(p)==s.OwnerLife && SkillHealth.Balance256(t)==s.Life && NecromancerEffects.ManaBalance256(t)==s.TargetMana,"Saved separate states/resources/injury");Require(ClassCombat.For(pending).PendingAbility==CombatAbility.None && !AssassinCharges.Any(pending) && NecromancerEffects.ManaBalance256(pending)==s.PendingMana,"Paid pending replay/refund");
        p.MoveToWorld(s.Origin,Map.Trammel);Require(CombatTiming.Equipped(p)?.Serial.Value==s.Primary && WeaponHands.Pair(p)?.Secondary.Serial.Value==s.Secondary,"Saved selected hand");Finish(p,t);Hold(p,s.Charges);SkillHealth.SetBalance256(p,s.OwnerLife);NecromancerEffects.SetMana256(p,s.Mana);SkillHealth.SetBalance256(t,s.Life);NecromancerEffects.SetMana256(t,s.TargetMana);
    }
    private static void Input()
    {
        var p=Fresh(5920);var t=Target(p);Ready(p);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.CobraStrike),"Input binding");var net=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();net.SendMobileUpdate(p);
        var hit=TigerStrikes.HitRoll;var roll=TigerStrikes.ElementRoll;var physical=TigerStrikes.PhysicalRoll;var effects=TigerStrikes.Effects;TigerStrikes.HitRoll=()=>0;TigerStrikes.ElementRoll=()=>0;TigerStrikes.PhysicalRoll=_=>0;TigerStrikes.Effects=new FixedRandom(99);
        int mana=NecromancerEffects.ManaBalance256(p),health=SkillHealth.Balance256(t),hp=SkillHealth.Balance256(p),phase=0,expectedLife=0;long deadline=Core.TickCount+8000;
        void Cleanup(){TigerStrikes.HitRoll=hit;TigerStrikes.ElementRoll=roll;TigerStrikes.PhysicalRoll=physical;TigerStrikes.Effects=effects;Delete(t);p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Cobra natural input timeout");
                if(phase==0 && Cobra(p)!=null && Core.TickCount>=ClassCombat.For(p).NextAction)
                {Require(Cobra(p).Count==1 && NecromancerEffects.ManaBalance256(p)==mana-512 && SkillHealth.Balance256(p)==hp && SkillHealth.Balance256(t)<health,"Natural charge/payment/no early skill leech");var e=Expected(p,t);expectedLife=hp+e.Recovery.Life256;Require(expectedLife>hp,"Observable natural finisher recovery");phase=1;Log.Information("cobra-strike-input-finisher: ready ability={Ability} target={Target} x={X} y={Y} z={Z}",(int)CombatAbility.BasicAttack,t.Serial.Value,t.X,t.Y,t.Z);}
                else if(phase==1 && Cobra(p)==null)
                {Require(SkillHealth.Balance256(p)==expectedLife && NecromancerEffects.ManaBalance256(p)==mana-512,"Natural charged finisher leech/payment");Cleanup();Log.Information("cobra-strike-input-result: passed");return;}
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
            }
            catch(Exception e){Cleanup();Log.Error(e,"cobra-strike-input-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("cobra-strike-input: ready ability={Ability} target={Target} x={X} y={Y} z={Z}",(int)CombatAbility.CobraStrike,t.Serial.Value,t.X,t.Y,t.Z);Wait();});
    }
}
