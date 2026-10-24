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

public static class PsychicHammerChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PsychicHammerChecks));
    private sealed record Saved(uint Owner,uint Target,uint Helm,SourceKnockbackState Motion,int Mana,int Health,uint Pending,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"psychic-hammer-fixtures.json");
    private static bool _creating;
    private static void Rates(){var hp=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=x=>_creating || x.Name?.StartsWith("PsychicFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):hp?.Invoke(x)??Mobile.DefaultHitsRate;var mp=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=x=>_creating || x.Name?.StartsWith("PsychicFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(x)??Mobile.DefaultManaRate;}
    public static void Configure(){Rates();Register("psychic-hammer-check",Check);Register("psychic-hammer-reload-check",Reload);Register("psychic-hammer-input-check",Input);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Psychic Hammer damage, targeting, knockback and persistence",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();}catch(Exception e){Log.Error(e,"{Name}: failed",name);}});
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x191,Name="PsychicFixture",Player=true};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Assassin);ClassCreation.Profiles.AdvanceLevel(p,99);
        FixtureSpace.Place(p,new(6060,1850,0),Map.Trammel);p.Hits=p.HitsMax;p.Mana=p.ManaMax;return p;
    }
    private static void Train(PlayerMobile p,int rank){string id=SkillMechanics.For(CombatAbility.PsychicHammer).TalentId;while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train Psychic Hammer");}}
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static GraveyardSkeleton Target(PlayerMobile p,int dx=4,int dy=0)
    {
        GraveyardSkeleton t;_creating=true;try{t=new GraveyardSkeleton{Name="PsychicFixtureTarget",NoKillAwards=true};}finally{_creating=false;}t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(20000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Physical,0);return t;
    }
    private static void Hold(SourceKnockbackState state)=>SourceKnockbacks.Restore(SourceKnockbacks.Snapshot().Where(x=>x.Target!=state.Target).Append(state).ToArray());
    private static SkillValues Values(int rank){var r=SkillMechanics.For(CombatAbility.PsychicHammer);return new(r,k=>k==r.SourceKey?rank:0,_=>0,99);}
    private static void Goldens()
    {
        Require(!ClassAbilities.SelfTarget(CombatAbility.PsychicHammer) && SkillMechanics.For(CombatAbility.PsychicHammer).SourceId==253,"Pinned target identity");
        foreach(var (rank,min,max,mana,unique,boss) in new[]{(1,256,768,1024,57,36),(20,7168,10112,2240,92,87),(21,7680,10752,2304,92,87),(255,186368,219392,17280,100,99)})
        {
            var v=Values(rank);Require(v.PhysicalDamage256(false)==min && v.Damage256(false)==min && v.PhysicalDamage256(true)==max && v.Damage256(true)==max && v.Mana256==mana,"Pinned mixed damage/mana golden "+rank);
            Require(PsychicHammers.Chance(v,false,false,false)==100 && PsychicHammers.Chance(v,false,false,true)==unique && PsychicHammers.Chance(v,false,true,true)==boss && PsychicHammers.Chance(v,true,true,true)==boss,"Target-kind chance golden "+rank);
            int calls=0;var low=PsychicHammers.Damage(v,0,span=>{calls++;return 0;});Require(calls==2 && NamedElements.To256(low.Physical)==min && NamedElements.To256(low.Magic)==min,"Independent physical/magic minimum draws");
            var high=PsychicHammers.Damage(v,0,span=>span-1);Require(NamedElements.To256(high.Physical)==max-1 && NamedElements.To256(high.Magic)==max-1,"Exclusive FP256 upper endpoint");
        }
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"psychic-hammer-source-probe.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int[] x=row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();if(x[6]!=0){int kind=x[1],calc=kind is 0 or 1 or 6 or 7 or 8?3:kind is 2 or 5?2:kind==3?1:0;Require(x[7]==calc && x[8]==Math.Max(1,x[2]) && x[9]==1 && x[13]==3 && x[14]==64,"Compiled source routing/event order "+count);}count++;
        }
        Require(count==2916,"Complete source fixture coverage");
        using var rolls=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"psychic-hammer-damage-source-probe.json")));count=0;
        foreach(var item in rolls.RootElement.EnumerateArray())
        {int[] x=item.EnumerateArray().Select(v=>v.GetInt32()).ToArray();int n=0;var actual=PsychicHammers.Damage(Values(x[0]),x[1],span=>{Require(span==x[7+n],"Source limited-roll span/order");return x[2+n++];});Require(n==2 && NamedElements.To256(actual.Physical)==x[4] && NamedElements.To256(actual.Magic)==x[5],"Complete source spell-roll differential "+count);count++;}
        Require(count==128,"Spell-roll fixture coverage");
    }
    private static int Cast(PlayerMobile p,Mobile target,bool ground=false,bool held=false)
    {
        Ready(p);int before=NecromancerEffects.ManaBalance256(p);var at=target.Location;bool started=held?ClassCombat.ContinueCast(p,CombatAbility.PsychicHammer,ground?null:target,at,Core.TickCount):ClassCombat.Perform(p,CombatAbility.PsychicHammer,ground?null:target,at,Core.TickCount);
        Require(started,"Cast begin");if(held){Require(ClassCombat.ContinueCast(p,CombatAbility.PsychicHammer,ground?null:target,at,Core.TickCount),"Held refresh");}
        ClassCombat.ReleaseCast(p);var s=ClassCombat.For(p);Require(s.PendingTarget==target.Serial && s.Animation==16 && NecromancerEffects.ManaBalance256(p)==before,"Unpaid targeted SC windup/acquired target lost");
        Require(ClassCombat.ResolveAction(p,s.EffectAt) && NecromancerEffects.ManaBalance256(p)==before-SkillMechanics.Values(p,CombatAbility.PsychicHammer).Mana256,"Exactly one fractional release payment");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt),"Repeated release");return before-NecromancerEffects.ManaBalance256(p);
    }
    private static void Reset(GraveyardSkeleton t){SourceKnockbacks.Clear(t);CreatureProfiles.Session(t).RecoveryUntil=Core.TickCount;t.Hits=t.HitsMax;SkillHealth.Clear(t);}
    private static void Check()
    {
        Goldens();var p=Fresh();var t=Target(p);Require(!ClassCombat.Available(p,CombatAbility.PsychicHammer),"Unlearned available");Train(p,1);
        Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.PsychicHammer) && SkillBindings.Instance.Assign(p,false,CombatAbility.PsychicHammer) && SkillBindings.Instance.AssignQuick(p,7,CombatAbility.PsychicHammer),"Right/quick binding");
        var r=PsychicHammers.Roll;PsychicHammers.Roll=span=>0;
        try
        {
            Ready(p);NecromancerEffects.SetMana256(p,1023);Require(!ClassCombat.Perform(p,CombatAbility.PsychicHammer,t,t.Location,Core.TickCount),"Insufficient mana");Mana(p);
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.PsychicHammer,null,p.Location,Core.TickCount),"Distant target acquired from caster feet");Require(NecromancerEffects.ManaBalance256(p)==mana,"Failed search paid mana");
            Require(PsychicHammers.Acquire(p,new(t.X,t.Y+2,t.Z))==t && PsychicHammers.Acquire(p,new(t.X,t.Y+3,t.Z))==null,"Observed near-click adapter boundary");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.PsychicHammer,t,t.Location,Core.TickCount),"Cancelled begin");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana,"Cancelled cast paid");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.PsychicHammer,t,t.Location,Core.TickCount),"Lost mana begin");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);Require(!ClassCombat.ResolveAction(p,at),"Lost mana emitted");Mana(p);
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.PsychicHammer,t,t.Location,Core.TickCount),"Recovery begin");at=ClassCombat.For(p).EffectAt;mana=NecromancerEffects.ManaBalance256(p);Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount) && !ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana,"Hit recovery did not cancel unpaid skill");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.PsychicHammer,t,t.Location,Core.TickCount),"Moving begin");at=ClassCombat.For(p).EffectAt;p.Direction=Direction.East;Require(p.Move(Direction.East),"Actual moving step");Require(!ClassCombat.ResolveAction(p,at),"Movement failed to cancel");t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.PsychicHammer,t,t.Location,Core.TickCount),"Target leaves range begin");at=ClassCombat.For(p).EffectAt;mana=NecromancerEffects.ManaBalance256(p);var old=t.Location;t.MoveToWorld(new(p.X+18,p.Y,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana,"Out-of-range release paid/hit");t.MoveToWorld(old,p.Map);
            var town=p.Location;FixtureSpace.Place(p,new(1438,1696,0),Map.Trammel);Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.PsychicHammer,t,t.Location,Core.TickCount),"Town cast allowed");p.MoveToWorld(town,p.Map);
            var other=Target(p,8);try{Require(PsychicHammers.Acquire(p,other.Location)==other,"Cursor nearest rather than caster nearest");}finally{other.Delete();}
            int hp=SkillHealth.Balance256(t);var origin=t.Location;Cast(p,t,true,true);Require(SkillHealth.Balance256(t)==hp-512 && SourceKnockbacks.Snapshot().Single(x=>x.Target==t.Serial.Value).Position==origin,"Direct physical+magic damage/mapped knockback");Reset(t);
            t.SetResistance(ResistanceType.Physical,100);hp=SkillHealth.Balance256(t);Cast(p,t);Require(SkillHealth.Balance256(t)==hp-256 && SourceKnockbacks.Remaining(t)>0,"Physical immunity removed magic/knockback");Reset(t);t.SetResistance(ResistanceType.Physical,0);
            CombatPotions.Apply(p,3);var raw=PsychicHammers.Potion(p,new(1,0,0,0,0,Magic:1));Require(raw.Fire==.2 && raw.Cold==.2 && raw.Lightning==.2 && raw.Poison==.2 && raw.Physical==1 && raw.Magic==1,"Approved T29 original spell split");TimedEffects.Clear(p);
            Train(p,20);var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Real soft-rank helm");ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.PsychicHammer)==21,"Effective rank21");
            hp=SkillHealth.Balance256(t);Require(Cast(p,t)==2304 && SkillHealth.Balance256(t)==hp-15360,"Rank21 fractional cost and real direct damage");Reset(t);
            var friend=Fresh();Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.PsychicHammer,friend,friend.Location,Core.TickCount),"Friendly target");friend.Delete();
            var pending=Fresh();Train(pending,1);var unpaid=Target(pending);Ready(pending);int pendingMana=NecromancerEffects.ManaBalance256(pending);Require(ClassCombat.Perform(pending,CombatAbility.PsychicHammer,unpaid,unpaid.Location,Core.TickCount),"Saved unpaid cast");ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
            Cast(p,t);var motion=SourceKnockbacks.Snapshot().Single(x=>x.Target==t.Serial.Value) with{NextUtc=TimedEffects.Now.AddHours(1)};Hold(motion); // Inspection hold, not production displacement timing.
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,helm.Serial.Value,motion,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),pending.Serial.Value,pendingMana)));
            var natural=Fresh();Train(natural,1);var moving=Target(natural);var start=moving.Location;Cast(natural,moving);
            Later(180,"psychic-hammer-result",()=>{try{Require(moving.X==start.X+1 && moving.Y==start.Y && SourceKnockbacks.Remaining(moving)>0,"Natural source movement and recovery");Log.Information("psychic-hammer-result: passed");}finally{moving.Delete();natural.Delete();}});
        }finally{PsychicHammers.Roll=r;}
    }
    private static void Reload()
    {
        Goldens();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(GraveyardSkeleton)World.FindMobile((Serial)s.Target);var pending=(PlayerMobile)World.FindMobile((Serial)s.Pending);
        Require(SkillMechanics.Rank(p,CombatAbility.PsychicHammer)==21 && ClassCreation.Profiles.GetRank(p.Serial,"assassin.psychichammer")==20 && World.FindItem((Serial)s.Helm)?.Parent==p,"Saved real soft/hard ranks");
        Require(NecromancerEffects.ManaBalance256(p)==s.Mana && SkillHealth.Balance256(t)==s.Health && SourceKnockbacks.Snapshot().Single(x=>x.Target==t.Serial.Value)==s.Motion,$"Saved damage/resource/motion identity: mana={NecromancerEffects.ManaBalance256(p)}/{s.Mana}, health={SkillHealth.Balance256(t)}/{s.Health}, motion={JsonSerializer.Serialize(SourceKnockbacks.Snapshot().SingleOrDefault(x=>x.Target==t.Serial.Value))}/{JsonSerializer.Serialize(s.Motion)}");
        Require(ClassCombat.For(pending).PendingAbility==CombatAbility.None && NecromancerEffects.ManaBalance256(pending)==s.PendingMana,"Unpaid windup replayed");Require(SkillBindings.Instance.Get(p).Right==CombatAbility.PsychicHammer && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,7)==CombatAbility.PsychicHammer,"Saved bindings");
        Hold(s.Motion with{NextUtc=TimedEffects.Now});Later(180,"psychic-hammer-reload-result",()=>{Require(t.X==s.Motion.Position.X+1 && SourceKnockbacks.Remaining(t)>0 && SkillHealth.Balance256(t)==s.Health && NecromancerEffects.ManaBalance256(p)==s.Mana,"Resumed knockback repeated damage/mana or failed movement");t.MoveToWorld(s.Motion.Position,t.Map);Hold(s.Motion);Log.Information("psychic-hammer-reload-result: passed");});
    }
    private static void Input()
    {
        var p=Fresh();Train(p,1);var t=Target(p);Ready(p);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.PsychicHammer) && SkillBindings.Instance.AssignQuick(p,7,CombatAbility.PsychicHammer),"Connected bindings");
        var net=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();net.SendMobileUpdate(p);
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);long deadline=Core.TickCount+5000;bool begun=false;
        void Wait(){try{Require(Core.TickCount<deadline,"Connected cast timeout");if(ClassCombat.For(p).PendingAbility==CombatAbility.PsychicHammer){begun=true;Require(NecromancerEffects.ManaBalance256(p)==mana,"Premature connected payment");}if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}Require(begun && NecromancerEffects.ManaBalance256(p)==mana-1024 && SourceKnockbacks.Remaining(t)>0,"Real ground-input skill/mana/knockback");t.Delete();p.Delete();Log.Information("psychic-hammer-input-result: passed");}catch(Exception e){t.Delete();p.Delete();Log.Error(e,"psychic-hammer-input-result: failed");}}
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("psychic-hammer-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.PsychicHammer,t.X,t.Y+1,t.Z);Wait();});
    }
}
