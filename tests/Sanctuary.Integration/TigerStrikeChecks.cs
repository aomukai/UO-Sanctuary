// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using Server.Network;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class TigerStrikeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(TigerStrikeChecks));
    private sealed record Saved(uint Owner,uint Target,MartialCharge Charge,int Mana256,int Life256,uint PendingOwner,int PendingMana256,Point3D Origin);
    private sealed class FixedRandom(int value):Random{public override int Next(int max)=>Math.Clamp(value,0,Math.Max(0,max-1));}
    private static string Manifest=>Path.Join(Core.BaseDirectory,"tiger-strike-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=x=>Creating || x.Name?.StartsWith("TigerFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):hp?.Invoke(x)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=x=>Creating || x.Name?.StartsWith("TigerFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(x)??Mobile.DefaultManaRate;
    }
    public static void Configure(){Rates();Register("tiger-strike-check",Check);Register("tiger-strike-reload-check",Reload);Register("tiger-strike-input-check",Input);}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {Rates();foreach(var t in World.Mobiles.Values.OfType<BaseCreature>().Where(x=>x.Name=="TigerFixtureTarget")){t.Frozen=t.CantWalk=true;t.AIObject?.AITimer.Stop();}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Tiger Strike source differential, charge melee consumers and saved state",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh(int column=6920)
    {Creating=true;try{var p=BladeBodyChecks.Fresh(column);p.Name="TigerFixtureOwner";return p;}finally{Creating=false;}}
    private static BaseCreature Target(PlayerMobile p,int dx=1)
    {
        Creating=true;try
        {var t=new BladeMissileTarget{Name="TigerFixtureTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.AIObject.AITimer.Stop();t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var kind in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(kind,0);}return t;}
        finally{Creating=false;}
    }
    private static SkillValues Values(int rank)=>new(SkillMechanics.For(CombatAbility.TigerStrike),_=>rank,_=>0,99);
    private static void Hold(MartialCharge state)=>AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=state.Owner || x.Ability!=state.Ability).Append(state).ToArray());
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static int Swing(PlayerMobile p,Mobile target,CombatAbility ability=CombatAbility.TigerStrike)
    {
        Ready(p);int hp=SkillHealth.Balance256(target);Require(ClassCombat.Perform(p,ability,target,target.Location,Core.TickCount),"Accepted melee input "+ability+" at "+p.Location+" target "+target.Location+" overlap="+AlliedOverlap.Allowed(p)+" frozen="+p.Frozen+" recovery="+HitRecovery.Active(p,Core.TickCount)+" hostile="+ClassCombat.IsHostile(p,target));
        ClassCombat.ReleaseCast(p);var s=ClassCombat.For(p);Require(!ClassCombat.ResolveAction(p,s.EffectAt-1),"Early action");Require(ClassCombat.ResolveAction(p,s.EffectAt),"Action release");Require(!ClassCombat.ResolveAction(p,s.EffectAt),"Duplicate release");return hp-SkillHealth.Balance256(target);
    }
    private static Item Named(PlayerMobile p,string source)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);Require(p.EquipItem(item),"Named equip "+source);ClassGear.Refresh(p);return item;}
    private static void Differential()
    {
        var definition=SkillMechanics.For(CombatAbility.TigerStrike);
        Require(definition.SourceId==254 && definition.Number("srvstfunc")==23 && definition.Number("srvdofunc")==34 && definition.Number("SrcDam")==128,"Pinned Tiger identity");
        Require(CommonMeleeSkills.Attack.Number("finishing")==1 && CommonMeleeSkills.Attack.Number("prgchargesconsumed")==1 && CommonMeleeSkills.Attack.Number("Param8")==0,"Modern common Attack policy/normal AR");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"tiger-strike-source-probe.json")));int builds=0,finish=0;
        foreach(var r in doc.RootElement.GetProperty("build").EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();bool valid=V(5)!=0 && V(6)!=0,has=V(0)>0,success=V(3)!=0;
            MartialCharge old=has?new(1,CombatAbility.TigerStrike,V(0),V(1),V(0)*50,DateTime.UnixEpoch.AddMilliseconds(321*40)):null;
            if(valid && success && (has || V(4)!=0))
            {var state=AssassinCharges.Build(old,1,Values(V(2)),DateTime.UnixEpoch.AddMilliseconds(100*40));Require(state.Count==V(9) && state.AttackPercent==V(10) && state.InitialRank==V(11) && state.UntilUtc==DateTime.UnixEpoch.AddMilliseconds(V(12)*40),"Complete source builder mismatch");}
            else{Require(V(8)==(has?1:0) && V(9)==V(0),"Source miss/allocation/target controls");}
            Require(V(7)==(valid?1:0) && V(13)==(valid?1:0) && V(14)==(valid?1:0),"Complete source allocation/drain control");builds++;
        }
        foreach(var r in doc.RootElement.GetProperty("finish").EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();bool applies=V(3)!=0 && V(4)!=0 && V(5)!=0 && V(6)!=0 && V(7)==1 && V(0)>0 && Math.Max(V(1),V(2))>0;
            var state=new MartialCharge(1,CombatAbility.TigerStrike,V(0),V(1),V(0)*50,DateTime.MaxValue);
            Require(V(8)==17+(applies?AssassinCharges.TigerPercent(state,V(2)):0) && V(9)==(applies?1:0),"Complete source physical finisher mismatch");finish++;
        }
        for(int rank=1;rank<=255;rank++)
        {var v=Values(rank);Require(v.Mana256==256 && v.Formula("toht")==25+10*(rank-1) && v.Eval("calc1")==100+20*(rank-1) && v.Eval("auralencalc")==375 && v.Eval("aurastatcalc2")==50,"Rank golden");}
        using var evidence=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"tiger-strike-source-evidence.json")));int schedules=0;
        foreach(var row in doc.RootElement.GetProperty("timing").EnumerateArray())
        {var record=evidence.RootElement.GetProperty("cachedAnimation").GetProperty("records")[row[0].GetInt32()];string code=record.GetProperty("code").GetString()[4..];var actual=TigerStrikes.Timing(code,row[1].GetInt32());Require(row[2].GetInt32()==0 && actual.EffectFrame==row[3].GetInt32() && actual.Frames==row[4].GetInt32(),"Complete Assassin A1 scheduler");schedules++;}
        Require(schedules==72,"Incomplete Assassin timing matrix");
        Require(builds==512 && finish==4800,"Incomplete Tiger differential");Log.Information("Tiger Strike:512 complete charge builders,4800 physical finishers 72 complete A1 schedules and255 pinned rank goldens passed");
    }
    private static void Cases(PlayerMobile p)
    {
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.TigerStrike) && SkillBindings.Instance.Assign(p,false,CombatAbility.TigerStrike) && SkillBindings.Instance.AssignQuick(p,0,CombatAbility.TigerStrike),"Tiger binding availability");
        var target=Target(p);var weapon=new Dagger();ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(p.EquipItem(weapon),"Dagger");ClassGear.Refresh(p);
        NecromancerEffects.SetMana256(p,20*256+17);int rolls=0;TigerStrikes.HitRoll=()=>{rolls++;return 0;};TigerStrikes.ElementRoll=()=>0;TigerStrikes.PhysicalRoll=_=>0;TigerStrikes.Effects=new FixedRandom(99);
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(target);Require(ClassCombat.ContinueAttack(p,CombatAbility.TigerStrike,target,target.Location,Core.TickCount),"Held Tiger input");ClassCombat.ReleaseCast(p);
        var session=ClassCombat.For(p);Require(rolls==0 && AssassinCharges.State(p)==null && NecromancerEffects.ManaBalance256(p)==mana-256 && SkillHealth.Balance256(target)==hp,"Start must pay but not hit/charge");
        Require(ClassCombat.ResolveAction(p,session.EffectAt) && rolls==1 && AssassinCharges.State(p) is{Count:1,InitialRank:1,AttackPercent:50} && SkillHealth.Balance256(target)<hp,"First landed charge");
        int firstLoss=hp-SkillHealth.Balance256(target);int secondLoss=Swing(p,target);Require(secondLoss==firstLoss && AssassinCharges.State(p).Count==2,"Charge amplified its own builder");
        Swing(p,target);Require(AssassinCharges.State(p).Count==3,"Third charge");var capped=AssassinCharges.State(p);Hold(capped with{UntilUtc=TimedEffects.Now.AddSeconds(1)});Swing(p,target);Require(AssassinCharges.State(p) is{Count:3,AttackPercent:150} && AssassinCharges.State(p).UntilUtc>TimedEffects.Now.AddSeconds(14),"Cap/refresh/AR overflow");
        var before=AssassinCharges.State(p);TigerStrikes.HitRoll=()=>1;hp=SkillHealth.Balance256(target);Swing(p,target);Require(AssassinCharges.State(p)==before && SkillHealth.Balance256(target)==hp,"Miss gained/refreshed/damaged");
        Swing(p,target,CombatAbility.BasicAttack);Require(AssassinCharges.State(p)==before && SkillHealth.Balance256(target)==hp,"Missed finisher consumed or always hit");TigerStrikes.HitRoll=()=>0;
        var blocker=new GraveyardSkeleton{Name="TigerFixtureTarget",Frozen=true,CantWalk=true,NoKillAwards=true};blocker.AIObject.AITimer.Stop();blocker.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);blocker.SetHits(50000);blocker.Hits=blocker.HitsMax;
        var choice=SourceBlocking.ChoiceRoll;var block=SourceBlocking.BlockRoll;
        try{SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>0;Require(SourceBlocking.TryRate(blocker,out int rate,out _) && rate>0,"Mapped shield block supplier");Require(Swing(p,blocker)==0 && AssassinCharges.State(p)==before,"Blocked builder gained/refreshed/damaged");Require(Swing(p,blocker,CombatAbility.BasicAttack)==0 && AssassinCharges.State(p)==before,"Blocked finisher consumed/damaged");}
        finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=block;blocker.Delete();}
        int expectedEd=AssassinCharges.TigerPercent(before,1);var expected=GearEffects.PrepareWeaponImpact(p,target,0,new FixedRandom(99),GearEffects.Damage(p,0,target,expectedEd,_=>0));
        int loss=Swing(p,target,CombatAbility.BasicAttack);Require(loss==NamedElements.To256(expected.Parts.Total) && loss>firstLoss && AssassinCharges.State(p) is{Count:2,AttackPercent:100},"Ordinary finisher ED/decrement");
        var after=AssassinCharges.State(p);Require(after.UntilUtc==before.UntilUtc,"Provisional finisher retained deadline");
        var helm=Named(p,"Tarnhelm");Require(SkillMechanics.Rank(p,CombatAbility.TigerStrike)==2 && AssassinCharges.TigerPercent(after,2)==240,"Live soft-rank finisher boost");
        Swing(p,target);Require(AssassinCharges.State(p) is{Count:3,InitialRank:1},"Refresh rewrote initial rank");p.AddToBackpack(helm);ClassGear.Refresh(p);
        AssassinCharges.Clear(p);helm=Named(p,"Tarnhelm");Swing(p,target);p.AddToBackpack(helm);ClassGear.Refresh(p);Require(AssassinCharges.State(p).InitialRank==2 && AssassinCharges.TigerPercent(AssassinCharges.State(p),1)==120,"Stored initial rank lost on gear removal");
        target.SetResistance(ResistanceType.Physical,100);hp=SkillHealth.Balance256(target);Swing(p,target);Require(SkillHealth.Balance256(target)==hp && AssassinCharges.State(p).Count==2,"Immune hit must build charges");target.SetResistance(ResistanceType.Physical,0);
        NecromancerEffects.SetMana256(p,255);hp=SkillHealth.Balance256(target);Swing(p,target);Require(NecromancerEffects.ManaBalance256(p)==255 && AssassinCharges.State(p) is{Count:1} && SkillHealth.Balance256(target)<hp,"Insufficient-mana ordinary finisher fallback");
        NecromancerEffects.SetMana256(p,10*256+17);Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.TigerStrike,target,target.Location,Core.TickCount),"Cancel begin");var pending=ClassCombat.For(p).TigerAttack;ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,pending.EffectAt) && NecromancerEffects.ManaBalance256(p)==mana-256 && AssassinCharges.State(p).Count==1,"Cancelled start refund or charge");
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.TigerStrike,target,target.Location,Core.TickCount),"Range change begin");pending=ClassCombat.For(p).TigerAttack;target.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,pending.EffectAt) && AssassinCharges.State(p).Count==1,"Out-of-range release gained charge");target.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
        var live=AssassinCharges.State(p);Hold(live with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)});Require(AssassinCharges.State(p)==null,"Expiry still supplies stats");AssassinCharges.Prune();Require(!AssassinCharges.Snapshot().Any(x=>x.Owner==p.Serial.Value),"Expired state retained");
        Swing(p,target);var saved=AssassinCharges.State(p);bool rejected=false;try{AssassinCharges.Restore([saved with{Count=4}]);}catch(InvalidDataException){rejected=true;}Require(rejected && AssassinCharges.State(p)==saved,"Malformed restore not atomic");
        Hold(saved with{UntilUtc=TimedEffects.Now.AddHours(1)});
        var unpaid=Fresh(6970);Ready(unpaid);NecromancerEffects.SetMana256(unpaid,10*256+17);var other=Target(unpaid);Require(ClassCombat.Perform(unpaid,CombatAbility.TigerStrike,other,other.Location,Core.TickCount),"Pending save begin");ClassCombat.For(unpaid).EffectAt=Core.TickCount+3600000;ClassCombat.For(unpaid).NextAction=Core.TickCount+3601000;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,AssassinCharges.State(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(target),unpaid.Serial.Value,NecromancerEffects.ManaBalance256(unpaid),p.Location)));
        Log.Information("Tiger Strike native: paid event-only charge build, cap/refresh, misses, ED/one-charge basic finisher, gear snapshots, immunity, fallback, range/cancel and expiry passed");
    }
    private static void Lifecycle()
    {
        var p=Fresh(7020);var target=Target(p);NecromancerEffects.SetMana256(p,20*256+17);Swing(p,target);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.TigerStrike,target,target.Location,Core.TickCount),"Recovery begin");long at=ClassCombat.For(p).EffectAt;
        Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount) && ClassCombat.For(p).TigerAttack==null && !ClassCombat.ResolveAction(p,at) && AssassinCharges.State(p).Count==1,"FHR kept pending hit or discarded existing charge");
        Ready(p);var dagger=new Dagger();ClassGear.Profiles.Convert(dagger,1,GearRarity.Normal,new Random(173));Require(p.EquipItem(dagger),"Lifecycle dagger");ClassGear.Refresh(p);Require(ClassCombat.Perform(p,CombatAbility.TigerStrike,target,target.Location,Core.TickCount),"Gear cancel begin");at=ClassCombat.For(p).EffectAt;p.AddToBackpack(dagger);ClassGear.Refresh(p);Require(!ClassCombat.ResolveAction(p,at) && AssassinCharges.State(p).Count==1,"Weapon change gained/discarded charges");
        target.Delete();p.Delete();p=Fresh(7020);AssassinCharges.Gain(p,SkillMechanics.Values(p,CombatAbility.TigerStrike));
        Ready(p);Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Paladin) && AssassinCharges.State(p)==null && ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Assassin),"Class history retained charges");
        AssassinCharges.Gain(p,SkillMechanics.Values(p,CombatAbility.TigerStrike));ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(AssassinCharges.State(p)==null && ClassCombat.For(p).TigerAttack==null,"Respec retained charges/pending");
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"assassin.tigerstrike",out _),"Retrain Tiger");AssassinCharges.Gain(p,SkillMechanics.Values(p,CombatAbility.TigerStrike));p.Kill();p.Resurrect();Require(AssassinCharges.State(p)==null,"Death retained charges");p.Corpse?.Delete();p.Delete();
    }
    private static void Check()
    {
        Differential();var hit=TigerStrikes.HitRoll;var roll=TigerStrikes.ElementRoll;var physical=TigerStrikes.PhysicalRoll;var effects=TigerStrikes.Effects;
        try{Cases(Fresh());Lifecycle();}finally{TigerStrikes.HitRoll=hit;TigerStrikes.ElementRoll=roll;TigerStrikes.PhysicalRoll=physical;TigerStrikes.Effects=effects;}
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=World.FindMobile((Serial)saved.Target);var pending=(PlayerMobile)World.FindMobile((Serial)saved.PendingOwner);
        Require(AssassinCharges.State(p)==saved.Charge && NecromancerEffects.ManaBalance256(p)==saved.Mana256 && SkillHealth.Balance256(target)==saved.Life256,"Saved charge/mana/injury");
        Require(AssassinCharges.State(pending)==null && ClassCombat.For(pending).PendingAbility==CombatAbility.None && NecromancerEffects.ManaBalance256(pending)==saved.PendingMana256,"Saved paid windup replay/refund");
        p.MoveToWorld(saved.Origin,Map.Trammel);
        var hit=TigerStrikes.HitRoll;try{TigerStrikes.HitRoll=()=>0;int old=AssassinCharges.State(p).Count;Ready(p);Swing(p,target,CombatAbility.BasicAttack);Require((AssassinCharges.State(p)?.Count??0)==old-1,"Restored finisher consumption");Hold(saved.Charge);SkillHealth.SetBalance256(target,saved.Life256);}finally{TigerStrikes.HitRoll=hit;}
    }
    private static void Input()
    {
        var p=Fresh(7060);var target=Target(p);Ready(p);NecromancerEffects.SetMana256(p,10*256+17);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.TigerStrike),"Input binding");
        var net=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();net.SendMobileUpdate(p);
        var hit=TigerStrikes.HitRoll;TigerStrikes.HitRoll=()=>0;int mana=NecromancerEffects.ManaBalance256(p),health=SkillHealth.Balance256(target);long deadline=Core.TickCount+5000;
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Tiger natural input timeout");
                if(AssassinCharges.State(p) is{} state)
                {Require(state.Count==1 && NecromancerEffects.ManaBalance256(p)==mana-256 && SkillHealth.Balance256(target)<health,"Natural paid charge hit count="+state.Count+" mana="+NecromancerEffects.ManaBalance256(p)+" expected="+(mana-256)+" life="+SkillHealth.Balance256(target)+" initial="+health+" raw="+GearEffects.Damage(p,0,target,0,_=>0).Physical+" resist="+target.PhysicalResistance+" canDamage="+target.CanBeDamaged());TigerStrikes.HitRoll=hit;target.Delete();p.Delete();Log.Information("tiger-strike-input-result: passed");return;}
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
            }
            catch(Exception e){TigerStrikes.HitRoll=hit;target.Delete();p.Delete();Log.Error(e,"tiger-strike-input-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("tiger-strike-input: ready ability={Ability} target={Target} x={X} y={Y} z={Z}",(int)CombatAbility.TigerStrike,target.Serial.Value,target.X,target.Y,target.Z);Wait();});
    }

}
