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
public static class FistsClawChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FistsClawChecks));
    private sealed class FixedRandom:Random{public override int Next(int max)=>Math.Max(0,max-1);}
    private sealed record Saved(uint Owner,uint Target,uint Primary,uint Secondary,MartialCharge Charge,uint Pending,int PaidMana,Point3D Origin);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fists-claw-fixtures.json");
    private static bool Creating;
    private static void Rates(){var old=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>Creating || p.Name=="FistsClawFixture"?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure(){Rates();Register("fists-claw-check",Check);Register("fists-claw-reload-check",Reload);Register("fists-claw-input-check",Input);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Fists learned claw sequence/payment/selected hands/cancellation/input",_=>Core.LoopContext.Post(()=>
    {try{if(name.EndsWith("input-check")){action();}else{Controlled(action);}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Controlled(Action action)
    {
        var hit=TigerStrikes.HitRoll;var element=TigerStrikes.ElementRoll;var physical=TigerStrikes.PhysicalRoll;var effects=TigerStrikes.Effects;var block=SourceBlocking.BlockRoll;
        try{TigerStrikes.HitRoll=()=>0;TigerStrikes.ElementRoll=()=>0;TigerStrikes.PhysicalRoll=_=>0;TigerStrikes.Effects=new FixedRandom();SourceBlocking.BlockRoll=_=>99;action();}
        finally{TigerStrikes.HitRoll=hit;TigerStrikes.ElementRoll=element;TigerStrikes.PhysicalRoll=physical;TigerStrikes.Effects=effects;SourceBlocking.BlockRoll=block;}
    }
    private static PlayerMobile Fresh(int column=7020)
    {Creating=true;try{var p=BladeBodyChecks.Fresh(column,10,column==6000?3528:2600);p.Name="FistsClawFixture";while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}p.Hits=p.HitsMax;p.Mana=p.ManaMax;return p;}finally{Creating=false;}}
    private static BaseWeapon Claw(PlayerMobile p,bool speed=false)
    {
        var item=new Tekagi();bool found=false;
        for(int seed=0;seed<(speed?500:1);seed++)
        {ClassGear.Profiles.Convert(item,99,speed?GearRarity.Magic:GearRarity.Normal,new Random(speed?seed:173));if(!speed || ClassGear.Bonus(item,"swing1")+ClassGear.Bonus(item,"swing2")+ClassGear.Bonus(item,"swing3")>0){found=true;break;}}
        Require(found,"Actual IAS affix claw");p.AddToBackpack(item);Require(p.EquipItem(item),"Primary claw");ClassGear.Refresh(p);return item;
    }
    private static BaseWeapon Second(PlayerMobile p)
    {var item=(BaseWeapon)NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Cutthroat1").Id,99,new FixedRandom(),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Bartuc offhand");ClassGear.Refresh(p);return item;}
    private static BaseCreature Target(PlayerMobile p,int x=1)
    {var t=new CobraPassiveTarget{Name="FistsClawTarget",NoKillAwards=true,Frozen=true,CantWalk=true};t.MoveToWorld(new(p.X+x,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}return t;}
    private static MartialCharge State(PlayerMobile p)=>AssassinCharges.State(p,CombatAbility.FistsOfFire);
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static AssassinClaws.Series Begin(PlayerMobile p,Mobile t)
    {Ready(p);Require(ClassCombat.Perform(p,CombatAbility.FistsOfFire,t,t.Location,Core.TickCount),"Fists start");ClassCombat.ReleaseCast(p);return ClassCombat.For(p).ClawAttack;}
    private static void Resolve(PlayerMobile p,AssassinClaws.Series s)
    {ClassCombat.ResolveAction(p,s.Until);Require(ClassCombat.For(p).ClawAttack==null && ClassCombat.For(p).PendingAbility==CombatAbility.None,"Series end cleanup");}
    private static void Differential()
    {
        using var rate=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"assassin-claw-rate-source-probe.json")));int rates=0;
        foreach(var row in rate.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();if(V(1)!=0){continue;}
            int actual=AssassinClaws.Advance(V(2),V(0)==1?V(3):null,V(4)+V(6),Math.Max(0,-V(7)),Math.Max(0,V(7)));
            // Existing native slow consumer caps85; the source fixture's -100
            // control is recorded separately, not silently claimed equivalent.
            if(V(7)==-100){continue;}Require(actual==V(10),"Complete entry selector/merge/rate");rates++;
        }
        using var seq=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"assassin-claw-sequence-source-probe.json")));int clocks=0;
        foreach(var row in seq.RootElement.EnumerateArray())
        {
            int speed=row[1].GetInt32();if(speed<=0){continue;}bool dual=row[0].GetInt32()!=0;var native=AssassinClaws.Schedule(dual,speed);var events=row[9].EnumerateArray().ToArray();
            Require(native.End==events[^1][1].GetInt32() && native.Events.SequenceEqual(events[..^1].Select(x=>x[1].GetInt32())),"Complete source ordinal clock");clocks++;
        }
        Require(rates==2304 && clocks==576,"Source/native matrix counts");
    }
    private static void Cases(PlayerMobile p,BaseCreature t)
    {
        Require(!ClassCombat.Perform(p,CombatAbility.FistsOfFire,t,t.Location,Core.TickCount),"Unarmed Fists accepted");
        var first=Claw(p,true);AssassinCharges.Clear(p);int mana=NecromancerEffects.ManaBalance256(p);var s=Begin(p,t);Require(!s.Basic && s.Events.Length==1 && State(p)==null && NecromancerEffects.ManaBalance256(p)==mana-512,"Single charge paid at start only");
        long at=s.Started+s.Events[0];Require(!ClassCombat.ResolveAction(p,at-1) && ClassCombat.ResolveAction(p,at) && !ClassCombat.ResolveAction(p,at) && State(p)?.Count==1,"Single exact action");Resolve(p,s);
        var second=Second(p);Require(WeaponHands.Pair(p)!=null,"Native dual claws");int primaryIas=CombatTiming.Ias(p),primaryWsm=CombatTiming.Weapon(first).Modifier,secondaryWsm=CombatTiming.Weapon(second).Modifier;
        Require(primaryIas>0,"Source IAS affix did not enter actual native attributes");int advance=AssassinClaws.Advance(p);Require(advance==AssassinClaws.Advance(primaryWsm,secondaryWsm,primaryIas,GearEffects.AttackSlow(p),PaladinOffense.AttackRate(p)+AssassinBuffs.AttackRate(p)),"Native dual mean rate");
        using(WeaponHands.Use(p,second)){Require(AssassinClaws.Advance(p)==advance,"Outer secondary damage scope changed sequence-entry IAS");}
        AssassinCharges.Clear(p);mana=NecromancerEffects.ManaBalance256(p);s=Begin(p,t);Require(s.Events.Length==2 && NecromancerEffects.ManaBalance256(p)==mana-512,"Dual pays once");int hits=0;
        TigerStrikes.HitRoll=()=>{Require(CombatTiming.Equipped(p)==(hits++==0?first:second),"Wrong contact hand");return 0;};NecromancerEffects.SetMana256(p,0);
        Require(ClassCombat.ResolveAction(p,s.Started+s.Events[0]) && State(p)?.Count==1 && NecromancerEffects.ManaBalance256(p)==0,"First contact or release debit");
        Require(ClassCombat.ResolveAction(p,s.Started+s.Events[1]) && State(p)?.Count==2 && hits==2 && CombatTiming.Equipped(p)==first,"Second contact/hand restoration");Resolve(p,s);TigerStrikes.HitRoll=()=>0;
        p.Mana=p.ManaMax;s=Begin(p,t);Resolve(p,s);Require(State(p)?.Count==3 && State(p).AttackPercent==150,"Late tick/cap/refresh");
        var held=State(p);TigerStrikes.HitRoll=()=>1;s=Begin(p,t);Resolve(p,s);Require(State(p)==held,"Miss gained/refreshed");TigerStrikes.HitRoll=()=>0;
        t.SetResistance(ResistanceType.Physical,100);AssassinCharges.Clear(p);s=Begin(p,t);Resolve(p,s);Require(State(p)?.Count==2,"Immune contacts failed to build");t.SetResistance(ResistanceType.Physical,0);
        AssassinCharges.Clear(p);NecromancerEffects.SetMana256(p,511);s=Begin(p,t);Require(s.Basic && s.Events.Length==1 && NecromancerEffects.ManaBalance256(p)==511,"Insufficient-mana ordinary fallback");Resolve(p,s);Require(State(p)==null && NecromancerEffects.ManaBalance256(p)==511,"Fallback built charges or debited mana");p.Mana=p.ManaMax;
        AssassinCharges.Clear(p);s=Begin(p,t);ClassCombat.ResolveAction(p,s.Started+s.Events[0]);var neighbor=Target(p,2);int hp=SkillHealth.Balance256(neighbor);t.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);Resolve(p,s);Require(State(p)?.Count==1 && SkillHealth.Balance256(neighbor)==hp,"Lost target redirected second claw");t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);neighbor.Delete();
        // Movement, weapon removal/return, FHR and Stop keep accepted payment.
        foreach(int cause in new[]{0,1,2,3})
        {
            AssassinCharges.Clear(p);p.Mana=p.ManaMax;mana=NecromancerEffects.ManaBalance256(p);s=Begin(p,t);
            if(cause==0){p.Direction=Direction.West;Require(p.Move(Direction.West),"Accepted move");p.Direction=Direction.East;Require(p.Move(Direction.East),"Return step");}
            if(cause==1){p.AddToBackpack(second);Require(p.EquipItem(second),"Reequip offhand");ClassGear.Refresh(p);}
            if(cause==2){Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount),"FHR");}
            if(cause==3){ClassCombat.Stop(p);}
            Require(ClassCombat.For(p).ClawAttack==null && !ClassCombat.ResolveAction(p,s.Until) && State(p)==null && NecromancerEffects.ManaBalance256(p)==mana-512,"Paid cancellation/replay/refund "+cause);Ready(p);
        }
        AssassinCharges.Clear(p);var blocked=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};blocked.MoveToWorld(t.Location,p.Map);blocked.AIObject?.AITimer.Stop();blocked.SetHits(50000);
        SourceBlocking.Restore(SourceBlocking.Snapshot().Append(new(blocked.Serial.Value,CreatureProfiles.Definition(blocked).Reference,1)).ToArray());SourceBlocking.BlockRoll=_=>0;s=Begin(p,blocked);Resolve(p,s);Require(State(p)==null,"Blocked contacts gained charges");SourceBlocking.BlockRoll=_=>99;blocked.Delete();
        AssassinCharges.Gain(p,SkillMechanics.Values(p,CombatAbility.TigerStrike));AssassinCharges.Gain(p,SkillMechanics.Values(p,CombatAbility.CobraStrike));var tiger=AssassinCharges.State(p);var cobra=AssassinCharges.State(p,CombatAbility.CobraStrike);s=Begin(p,t);Resolve(p,s);Require(AssassinCharges.State(p)==tiger && AssassinCharges.State(p,CombatAbility.CobraStrike)==cobra,"Fists builder consumed other charge families");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new FixedRandom(),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Real soft rank");ClassGear.Refresh(p);AssassinCharges.Clear(p);p.Mana=p.ManaMax;s=Begin(p,t);Resolve(p,s);Require(State(p)?.InitialRank==SkillMechanics.Rank(p,CombatAbility.FistsOfFire),"Current actual item rank not captured");
    }
    private static void Lifecycle()
    {
        var p=Fresh(7020);var weapon=Claw(p);var t=Target(p);var s=Begin(p,t);Resolve(p,s);s=Begin(p,t);p.Kill();Require(ClassCombat.For(p).ClawAttack==null && !AssassinCharges.Any(p) && !ClassCombat.ResolveAction(p,s.Until),"Death retained claw mode/charges");p.Resurrect();p.Corpse?.Delete();
        if(weapon.Deleted){weapon=Claw(p);}else if(weapon.Parent!=p){p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Death re-equip");}p.Mana=p.ManaMax;
        s=Begin(p,t);Require(!ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Paladin) && ClassCombat.For(p).ClawAttack==s,"Active paid attack should prevent history switching");
        ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(ClassCombat.For(p).ClawAttack==null && !AssassinCharges.Any(p) && !ClassCombat.ResolveAction(p,s.Until),"Respec retained action/charges");t.Delete();p.Delete();
        var history=Fresh(7020);AssassinCharges.Gain(history,SkillMechanics.Values(history,CombatAbility.FistsOfFire));Require(ClassCreation.Profiles.TryActivateHistory(history,DiabloClass.Paladin) && !AssassinCharges.Any(history) && ClassCombat.For(history).ClawAttack==null,"Idle history retained charges");history.Delete();
    }
    private static void Check()
    {
        Differential();Lifecycle();var p=Fresh();var t=Target(p);Cases(p,t);var charge=State(p) with{UntilUtc=TimedEffects.Now.AddHours(1)};AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value || x.Ability!=CombatAbility.FistsOfFire).Append(charge).ToArray());
        var pending=Fresh(7020);Claw(pending);var other=Target(pending);p.Mana=p.ManaMax;var series=Begin(pending,other);ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,WeaponHands.Pair(p).Value.Primary.Serial.Value,WeaponHands.Pair(p).Value.Secondary.Serial.Value,charge,pending.Serial.Value,NecromancerEffects.ManaBalance256(pending),p.Location)));
        Log.Information("Fists claw:2304 source entry rates,576 clocks,actual IAS affix/dual hands,single/double/cap/miss/immunity/fallback,move/equipment/FHR/Stop and actual item ranks pass");
    }
    private static void Reload()
    {
        Differential();var x=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)x.Owner);var t=(BaseCreature)World.FindMobile((Serial)x.Target);var pending=(PlayerMobile)World.FindMobile((Serial)x.Pending);
        Require(State(p)==x.Charge && ClassCombat.For(pending).ClawAttack==null && ClassCombat.For(pending).PendingAbility==CombatAbility.None && State(pending)==null && NecromancerEffects.ManaBalance256(pending)==x.PaidMana,"Saved charges or paid pending action replay/refund");p.MoveToWorld(x.Origin,Map.Trammel);
        Require(WeaponHands.Pair(p)?.Primary.Serial.Value==x.Primary && WeaponHands.Pair(p)?.Secondary.Serial.Value==x.Secondary,"Saved primary/secondary");p.Mana=p.ManaMax;var s=Begin(p,t);Resolve(p,s);AssassinCharges.Restore(AssassinCharges.Snapshot().Where(a=>a.Owner!=p.Serial.Value || a.Ability!=CombatAbility.FistsOfFire).Append(x.Charge).ToArray());
    }
    private static void Input()
    {
        var p=Fresh(7020);Claw(p,true);Second(p);var t=Target(p);Require(SkillBindings.Instance.Assign(p,true,CombatAbility.FistsOfFire) && SkillBindings.Instance.Assign(p,false,CombatAbility.FistsOfFire) && SkillBindings.Instance.AssignQuick(p,0,CombatAbility.FistsOfFire),"Input bindings");Ready(p);
        var net=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();net.SendMobileUpdate(p);
        var hit=TigerStrikes.HitRoll;var element=TigerStrikes.ElementRoll;var physical=TigerStrikes.PhysicalRoll;var effects=TigerStrikes.Effects;TigerStrikes.HitRoll=()=>0;TigerStrikes.ElementRoll=()=>0;TigerStrikes.PhysicalRoll=_=>0;TigerStrikes.Effects=new FixedRandom();
        int phase=0,mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);uint stopSequence=0;bool stopping=false;long deadline=Core.TickCount+15000;
        void Cleanup(){TigerStrikes.HitRoll=hit;TigerStrikes.ElementRoll=element;TigerStrikes.PhysicalRoll=physical;TigerStrikes.Effects=effects;t.Delete();p.Delete();}
        void Announce()=>Log.Information("fists-claw-input: ready action={Action} ability={Ability} target={Target} x={X} y={Y} z={Z}",phase is 0 or 3?3:phase==1?4:7,(int)(phase==3?CombatAbility.BasicAttack:CombatAbility.FistsOfFire),t.Serial.Value,t.X,t.Y,t.Z);
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Natural claw input timeout");
                if(phase==3 && State(p)?.Count==1 && Core.TickCount>=ClassCombat.For(p).NextAction)
                {Require(NecromancerEffects.ManaBalance256(p)==mana && SkillHealth.Balance256(t)<life,"Natural charged ordinary finisher");Cleanup();Log.Information("fists-claw-input-result: passed");return;}
                if(phase<3 && State(p)?.Count==2 && !stopping)
                {stopping=true;stopSequence=ClassCombat.For(p).Sequence;Log.Information("fists-claw-input-stop: phase={Phase}",phase);}
                if(phase<3 && stopping && ClassCombat.For(p).Sequence>stopSequence && ClassCombat.For(p).HeldAbility==CombatAbility.None && State(p)?.Count==2 && ClassCombat.For(p).ClawAttack==null && Core.TickCount>=ClassCombat.For(p).NextAction)
                {
                    Require(NecromancerEffects.ManaBalance256(p)==mana-512 && SkillHealth.Balance256(t)<life,$"Natural one payment/two damaging claws phase={phase} mana={NecromancerEffects.ManaBalance256(p)}/{mana-512} life={SkillHealth.Balance256(t)}/{life} regen={Mobile.ManaRegenRateHandler(p)}");
                    phase++;stopping=false;if(phase<3){AssassinCharges.Clear(p);}Ready(p);mana=NecromancerEffects.ManaBalance256(p);life=SkillHealth.Balance256(t);Announce();
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);
            }
            catch(Exception e){Cleanup();Log.Error(e,"fists-claw-input-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Announce();Wait();});
    }
}
