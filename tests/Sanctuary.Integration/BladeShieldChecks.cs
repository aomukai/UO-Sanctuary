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

public static class BladeShieldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeShieldChecks));
    private sealed record Saved(uint Owner,uint Target,Point3D Origin,int Map,BladeShieldState State,int Life256,int Mana256);
    private sealed class FixedRandom(int value):Random{public override int Next(int max)=>Math.Clamp(value,0,Math.Max(0,max-1));}
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-shield-fixtures.json");
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=x=>_creating || x.Name?.StartsWith("BladeShieldFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):hp?.Invoke(x)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=x=>_creating || x.Name?.StartsWith("BladeShieldFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(x)??Mobile.DefaultManaRate;
    }
    public static void Configure(){Rates();Register("blade-shield-check",Check);Register("blade-shield-reload-check",Reload);Register("blade-shield-input-check",Input);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Blade Shield start payment, source weapon fill, pulses, input and saved state",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()
    {
        // A separate corridor pool keeps these saved fixtures from shifting the
        // earlier Blade Sentinel footprint and movement fixtures onto new terrain.
        PlayerMobile p;_creating=true;try{p=BladeBodyChecks.Fresh(6040);}finally{_creating=false;}
        p.Name="BladeShieldFixture";
        while(p.RawStr<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}
        while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}
        return p;
    }
    private static BladeMissileTarget Target(PlayerMobile p,int dx=1)
    {
        var t=new BladeMissileTarget{Name="BladeShieldFixtureTarget",NoKillAwards=true,Frozen=true,CantWalk=true};
        t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}
        return t;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static void Hold(BladeShieldState state)
    {var saved=BladeShields.Snapshot();BladeShields.Restore(saved with{States=saved.States.Where(x=>x.Owner!=state.Owner).Append(state).ToArray()});}
    private static void Differential()
    {
        var row=SkillMechanics.For(CombatAbility.BladeShield);
        Require(row.SourceId==277 && row.Number("SrcDam")==96 && row.Number("ResultFlags")==32 && ClassAbilities.SelfTarget(row.Ability),"Pinned Shield identity/source fraction/event flags/self targeting");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-shield-damage-source-probe.json")));int count=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();int bonus=V(2)+(V(0)!=0?15+22+20:20)+(V(9)==1?10:V(9)==2?20+(V(0)!=0?50:0):0);
            var range=ClassStats.PhysicalEndpoints256(V(1),V(1)+7,bonus);int Draw(int span)=>V(8)!=0?span-1:0;
            bool critical=NamedCombat.MeleeCritical(V(0)!=0 && V(6)==1?35:0,V(6)==2?35:0,V(6)==3?35:0,new FixedRandom(V(7)));
            Require(BladeShieldDamage.Physical(V(3),BladeShieldDamage.Roll(range.Min,range.Max,Draw),critical)==V(10) && critical==(V(18)!=0),"Complete source weapon physical/additive skill/fraction/critical differential "+count);
            int Typed()=>V(4)==0?0:BladeShieldDamage.Scale(BladeShieldDamage.Roll(AmazonMelee.EnhanceEndpoint(256,V(5)),AmazonMelee.EnhanceEndpoint(1280,V(5)),Draw));
            Require(Enumerable.Range(11,4).All(i=>Typed()==V(i)),"Complete typed mastery/roll/source-fraction differential "+count);
            var poison=WeaponPoisonPackets.Melee(new(PoisonMin:V(4)*20,PoisonMax:V(4)*40,PoisonFrames:101,PoisonCount:2),V(5),96,Draw);
            Require(poison.Rate256==V(15) && poison.Frames==V(16) && V(17)==(V(4)!=0?75:0),"Complete source poison rate/count/length and cold-length differential "+count);count++;
        }
        Require(count==4608,"Incomplete source melee fill inventory");
        Require(BladeShields.Timing(0)==new ActionTiming(16,9) && BladeShields.Timing(65)==new ActionTiming(11,7),"SC supplementary metadata and FCR timing goldens");
        Log.Information("Blade Shield:4608 unchanged full player-fill rows match native physical, critical, typed mastery and poison/length helpers; current exact reach and modern event acceptance remain scoped");
    }
    private static void Cast(PlayerMobile p)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.BladeShield).Mana256;
        Require(ClassCombat.Perform(p,CombatAbility.BladeShield,null,new(65530,65530,0),Core.TickCount),"Shield self cast begin");var action=ClassCombat.For(p);var state=BladeShields.State(p);
        Require(state!=null && state.NextPulseUtc==null && NecromancerEffects.ManaBalance256(p)==mana-cost
            && action.PendingPoint==p.Location && action.PendingTarget==Serial.Zero && action.Animation==16,"Shield did not create/charge at start or canonicalize self target");
        Require(ClassCombat.ResolveAction(p,action.EffectAt) && BladeShields.State(p).NextPulseUtc!=null
            && NecromancerEffects.ManaBalance256(p)==mana-cost && !ClassCombat.ResolveAction(p,action.EffectAt),"First pulse paid twice or failed to schedule");
    }
    private static void Check()
    {
        Differential();var p=Fresh();var t=Target(p);var far=Target(p,4);var edge=Target(p,3);
        var oldHit=BladeShields.HitRoll;var oldSkill=BladeShields.SkillRoll;var oldWeapon=BladeShields.WeaponRoll;var oldEffects=BladeShields.Effects;var oldWear=BladeShields.WearRoll;
        try
        {
            BladeShields.HitRoll=()=>0;BladeShields.SkillRoll=_=>0;BladeShields.WeaponRoll=_=>0;BladeShields.Effects=BladeShields.WearRoll=new FixedRandom(99);
            Require(AssassinCombat.Implemented(CombatAbility.BladeShield) && ClassCombat.Available(p,CombatAbility.BladeShield)
                && !SkillBindings.Instance.Assign(p,true,CombatAbility.BladeShield) && SkillBindings.Instance.Assign(p,false,CombatAbility.BladeShield)
                && SkillBindings.Instance.AssignQuick(p,9,CombatAbility.BladeShield),"Learned availability/right/quick/left policy");
            Ready(p);NecromancerEffects.SetMana256(p,6911);Require(!ClassCombat.Perform(p,CombatAbility.BladeShield,null,p.Location,Core.TickCount) && BladeShields.State(p)==null,"Unaffordable Shield created/paid state");
            NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.BladeShield,null,p.Location,Core.TickCount),"Cancelled start");var started=BladeShields.State(p);long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,at) && BladeShields.State(p)==started && started.NextPulseUtc==null
                && NecromancerEffects.ManaBalance256(p)==mana-6912,"Cancelled source-start buff refunded/released or disappeared");
            NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);
            Require(ClassCombat.Perform(p,CombatAbility.BladeShield,null,p.Location,Core.TickCount),"Moving start");at=ClassCombat.For(p).EffectAt;var origin=p.Location;
            p.MoveToWorld(new(origin.X-1,origin.Y,origin.Z),p.Map);Require(!ClassCombat.ResolveAction(p,at) && BladeShields.State(p).NextPulseUtc==null,"Moving cast emitted first pulse");p.MoveToWorld(origin,p.Map);
            NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.BladeShield,null,p.Location,Core.TickCount),"Gear-cancel start");at=ClassCombat.For(p).EffectAt;
            var cancelledClaw=new Tekagi();ClassGear.Profiles.Convert(cancelledClaw,99,GearRarity.Normal,new Random(173));p.AddToBackpack(cancelledClaw);Require(p.EquipItem(cancelledClaw),"Equip cancellation claw");
            Require(!ClassCombat.ResolveAction(p,at) && BladeShields.State(p).NextPulseUtc==null,"Weapon swap emitted first pulse");p.AddToBackpack(cancelledClaw);cancelledClaw.Delete();
            NecromancerEffects.SetMana256(p,p.ManaMax*256);
            int first=SkillHealth.Balance256(t),last=SkillHealth.Balance256(far),outer=SkillHealth.Balance256(edge);Cast(p);
            Require(SkillHealth.Balance256(t)<first && SkillHealth.Balance256(edge)<outer && SkillHealth.Balance256(far)==last,"Current table radius did not damage beyond contact or leaked beyond radius");
            var active=BladeShields.State(p);Require(active.Rank==1 && active.UntilUtc-active.StartedUtc==TimeSpan.FromSeconds(120),"Rank1 source duration");
            var claw=new Tekagi();ClassGear.Profiles.Convert(claw,99,GearRarity.Normal,new Random(173));p.AddToBackpack(claw);Require(p.EquipItem(claw),"Equip live claw");ClassGear.Refresh(p);
            int armedBefore=SkillHealth.Balance256(t);BladeShields.Pulse(p,active.Rank);int armed=armedBefore-SkillHealth.Balance256(t);
            p.AddToBackpack(claw);ClassGear.Refresh(p);int nakedBefore=SkillHealth.Balance256(t);BladeShields.Pulse(p,active.Rank);
            Require(armed>nakedBefore-SkillHealth.Balance256(t) && BladeShields.State(p)==active,"Pulse snapshotted weapon or reset its captured buff");
            int beforeMiss=SkillHealth.Balance256(t);BladeShields.HitRoll=()=>1;BladeShields.Pulse(p,active.Rank);Require(SkillHealth.Balance256(t)==beforeMiss,"Shield bypassed attack rating");BladeShields.HitRoll=()=>0;
            int beforeOffline=SkillHealth.Balance256(t),beforeMana=NecromancerEffects.ManaBalance256(p);BladeShields.Tick(active.NextPulseUtc.Value.AddSeconds(1));
            Require(SkillHealth.Balance256(t)==beforeOffline && NecromancerEffects.ManaBalance256(p)==beforeMana,"Offline state attacked or charged pulse mana");
            using(CombatItemEvents.Suppress(p,t))
            {Require(!CombatItemEvents.Allowed(p,t) && CombatItemEvents.Allowed(p,far),"Event suppression escaped pair");using(CombatItemEvents.Suppress(p,far)){Require(!CombatItemEvents.Allowed(p,far),"Nested suppression");}}
            Require(CombatItemEvents.Allowed(p,t),"Event scope leaked");
            // A soft-rank change after Start affects the first used-skill do,
            // while subsequent callbacks retain modifier-list cast rank.
            NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.BladeShield,null,p.Location,Core.TickCount),"Soft-rank start");at=ClassCombat.For(p).EffectAt;
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Soft-rank helm");ClassGear.Refresh(p);
            int softBefore=SkillHealth.Balance256(t);Require(ClassCombat.ResolveAction(p,at),"Soft-rank first pulse");int initialDamage=softBefore-SkillHealth.Balance256(t);
            Require(BladeShields.State(p).Rank==1 && SkillMechanics.Rank(p,CombatAbility.BladeShield)==2,"First dispatch rewrote captured buff rank");
            p.AddToBackpack(helm);ClassGear.Refresh(p);int recurringBefore=SkillHealth.Balance256(t);BladeShields.Pulse(p,1);
            Require(initialDamage>recurringBefore-SkillHealth.Balance256(t),"First/recurring rank stages were collapsed");
            EventPolicy(p,t);
            t.SetResistance(ResistanceType.Physical,100);int immune=SkillHealth.Balance256(t);BladeShields.Pulse(p,1);
            Require(SkillHealth.Balance256(t)==immune,"Physical immunity did not stop physical-only shield damage");t.SetResistance(ResistanceType.Physical,0);
            var home=p.Location;var map=p.Map;p.MoveToWorld(new(1496,1628,10),Map.Trammel);
            Require(Server.Spells.SpellHelper.IsTown(p.Location,p),"Town fixture");
            var skillRoll=BladeShields.SkillRoll;BladeShields.SkillRoll=_=>throw new InvalidOperationException("Town pulse rolled damage");
            NecromancerEffects.SetMana256(p,p.ManaMax*256);Cast(p);BladeShields.SkillRoll=skillRoll;p.MoveToWorld(home,map);
            NecromancerEffects.SetMana256(p,p.ManaMax*256);
            // Separate self-state slot, source replacement and lower-rank recast.
            AssassinBuffs.Cast(p,SkillMechanics.Values(p,CombatAbility.Fade));Venoms.Release(p);
            Require(AssassinBuffs.Faded(p) && Venoms.State(p)!=null && BladeShields.State(p)!=null,"Shield displaced other Assassin buffs");
            BladeShields.Pulse(p,1);var venom=Venoms.State(p);
            var poison=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value && x.PoisonMs>0);
            Require(poison.PoisonClock.Rate256==BladeShieldDamage.Scale(venom.Minimum256) && poison.PoisonMs==400,
                "Shield did not deliver source-scaled live Venom with its independent0.4-second length");TimedEffects.ClearGear(t);
            NecromancerEffects.SetMana256(p,p.ManaMax*256);var high=BladeShields.Start(p,SkillMechanics.Values(p,CombatAbility.BladeShield,21),TimedEffects.Now);
            Require(high.Rank==21 && high.UntilUtc-high.StartedUtc==TimeSpan.FromSeconds(360),"High-rank duration");
            NecromancerEffects.SetMana256(p,p.ManaMax*256);var low=BladeShields.Start(p,SkillMechanics.Values(p,CombatAbility.BladeShield,1),TimedEffects.Now);
            Require(low.Id>high.Id && low.Rank==1 && BladeShields.Activate(p,low.Id,TimedEffects.Now),"Lower-rank recast did not replace/reset old state");TimedEffects.ClearGear(t);
            var mortal=Fresh();BladeShields.Start(mortal,SkillMechanics.Values(mortal,CombatAbility.BladeShield),TimedEffects.Now);mortal.Kill();
            Require(BladeShields.State(mortal)==null && !BladeShields.Snapshot().States.Any(x=>x.Owner==mortal.Serial.Value),"Death retained shield state");mortal.Delete();
            var held=BladeShields.State(p);held=held with{StartedUtc=held.StartedUtc.AddDays(1),UntilUtc=held.UntilUtc.AddDays(1),NextPulseUtc=held.NextPulseUtc.Value.AddDays(1)};Hold(held);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,p.Location,p.Map.MapID,held,SkillHealth.Balance256(t),NecromancerEffects.ManaBalance256(p))));
            NaturalExpiry();Log.Information("Blade Shield cast/start payment, cancellation, independent buffs, per-target hit/live weapon, current table radius, offline suppression and saved context pass");
            p=null;t=null;
        }
        finally
        {BladeShields.HitRoll=oldHit;BladeShields.SkillRoll=oldSkill;BladeShields.WeaponRoll=oldWeapon;BladeShields.Effects=oldEffects;BladeShields.WearRoll=oldWear;far.Delete();edge.Delete();t?.Delete();p?.Delete();}
    }
    private static void EventPolicy(PlayerMobile p,BladeMissileTarget target)
    {
        var face=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Guillaume's Face").Id,99,new Random(173),true);
        p.AddToBackpack(face);Require(p.EquipItem(face),"Actual Crushing Blow/Deadly Strike helm");ClassGear.Refresh(p);
        Require(ClassGear.Bonus(p,"crush")>0,"Actual Crushing Blow supplier");
        var old=BladeShields.Effects;BladeShields.Effects=new FixedRandom(0);
        try
        {
            int skill=SkillMechanics.Values(p,CombatAbility.BladeShield,1).PhysicalDamage256(false);
            var raw=BladeShieldDamage.Capture(p,target,skill,_=>0,new FixedRandom(0));int before=SkillHealth.Balance256(target);
            BladeShields.Pulse(p,1);Require(before-SkillHealth.Balance256(target)==NamedElements.To256(raw.Total),"Result32 incorrectly fired Crushing Blow/weapon damage events");
            Require(NecromancerEffects.Apply(p,target,CombatAbility.LifeTap,1),"Life Tap event fixture");SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2);int hp=SkillHealth.Balance256(p);
            BladeShields.Pulse(p,1);Require(SkillHealth.Balance256(p)==hp,"Result32 incorrectly fired Life Tap");
        }
        finally{BladeShields.Effects=old;p.AddToBackpack(face);ClassGear.Refresh(p);}
        var wall=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Lidless Wall").Id,99,new Random(173),true);
        p.AddToBackpack(wall);Require(p.EquipItem(wall),"Actual mana-after-kill shield");ClassGear.Refresh(p);
        Require(ClassGear.Bonus(p,"mana-kill")>0,"Actual kill-mana supplier");
        try
        {
            var kill=Target(p);kill.NoKillAwards=false;kill.SetHits(1);kill.Hits=1;NecromancerEffects.SetMana256(p,1000);
            BladeShields.Pulse(p,1);Require(!kill.Alive && NecromancerEffects.ManaBalance256(p)==1000,"Result32 incorrectly fired item kill callbacks");kill.Delete();
            var control=Target(p);control.NoKillAwards=false;control.SetHits(1);control.Hits=1;
            GearEffects.Strike(p,control,0,new FixedRandom(99),wearWeapon:false);
            Require(!control.Alive && NecromancerEffects.ManaBalance256(p)>1000,"Event suppression leaked into ordinary weapon kills");control.Delete();
        }
        finally{p.AddToBackpack(wall);ClassGear.Refresh(p);}
    }
    private static void NaturalExpiry()
    {
        var p=Fresh();var state=BladeShields.Start(p,SkillMechanics.Values(p,CombatAbility.BladeShield),TimedEffects.Now);
        var until=TimedEffects.Now.AddMilliseconds(120);Hold(state with{StartedUtc=until.AddSeconds(-120),UntilUtc=until});
        Timer.StartTimer(TimeSpan.FromMilliseconds(250),()=>
        {try{Require(BladeShields.State(p)==null && !BladeShields.Snapshot().States.Any(x=>x.Owner==p.Serial.Value),"Natural timer did not remove expired self state");Log.Information("blade-shield-result: passed");}catch(Exception e){Log.Error(e,"blade-shield-result: failed");}finally{p.Delete();}});
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var t=(BladeMissileTarget)World.FindMobile((Serial)saved.Target);
        Require(BladeShields.State(p)==saved.State && SkillHealth.Balance256(t)==saved.Life256 && NecromancerEffects.ManaBalance256(p)==saved.Mana256,"Reload changed rank/start/expiry/pulse/health/mana");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);Require(BladeShields.State(p)==saved.State,"Facet restoration restarted buff");
        NaturalExpiry();Log.Information("Blade Shield reload: captured cast rank, original duration/pulse phase, no offline damage/payment and independent Assassin states retained");
    }
    private static void Input()
    {
        var p=Fresh();var t=Target(p);Ready(p);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.BladeShield),"Input right binding");
        var net=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();net.SendMobileUpdate(p);
        int mana=NecromancerEffects.ManaBalance256(p),health=SkillHealth.Balance256(t),pulses=0;DateTime? last=null;bool began=false;long deadline=Core.TickCount+7000;
        var oldHit=BladeShields.HitRoll;BladeShields.HitRoll=()=>0;
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Natural Shield input timeout");var state=BladeShields.State(p);
                if(state!=null)
                {
                    Require(NecromancerEffects.ManaBalance256(p)==mana-6912,"Connected Shield charged pulse/release mana or missed start payment");began=true;
                    if(state.NextPulseUtc!=null && state.NextPulseUtc!=last){last=state.NextPulseUtc;pulses++;}
                    if(pulses>=2)
                    {Require(began && SkillHealth.Balance256(t)<health && state.Rank==1,"Natural initial/recurring weapon damage");BladeShields.HitRoll=oldHit;t.Delete();p.Delete();Log.Information("blade-shield-input-result: passed");return;}
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
            }
            catch(Exception e){BladeShields.HitRoll=oldHit;t.Delete();p.Delete();Log.Error(e,"blade-shield-input-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("blade-shield-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.BladeShield,65530,65530,0);Wait();});
    }
}
