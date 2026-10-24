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

public static class VenomChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(VenomChecks));
    private sealed record Saved(uint Owner,uint Target,uint Weapon,uint Helm,VenomState Buff,int Mana,int Balance,TimedEffectState Poison,GearDamage Packet,uint Pending,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"venom-fixtures.json");
    private static bool _creating;
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=x=>_creating || x.Name?.StartsWith("VenomFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):hp?.Invoke(x)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=x=>_creating || x.Name?.StartsWith("VenomFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(x)??Mobile.DefaultManaRate;
    }
    public static void Configure(){Rates();Register("venom-check",Check);Register("venom-reload-check",Reload);Register("venom-input-check",Input);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Venom cast, poison packets, gear, lifecycle and persistence",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();}catch(Exception e){Log.Error(e,"{Name}: failed",name);}});
    private static SkillValues Values(int rank){var row=SkillMechanics.For(CombatAbility.Venom);return new(row,k=>k==row.SourceKey?rank:0,_=>0,99);}
    private static void Train(PlayerMobile p,CombatAbility ability,int rank){while(ClassCreation.Profiles.GetRank(p.Serial,SkillMechanics.For(ability).TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,SkillMechanics.For(ability).TalentId,out _),"Train "+ability);}}
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x191,Name="VenomFixture",Player=true};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Assassin);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}
        FixtureSpace.Place(p,new(5910,1850,0),Map.Trammel);Train(p,CombatAbility.ClawMastery,1);Train(p,CombatAbility.BurstOfSpeed,1);Train(p,CombatAbility.Fade,1);p.Hits=p.HitsMax;p.Mana=p.ManaMax;return p;
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static TimedEffectState Poison(Mobile target)=>TimedEffects.Snapshot().Single(x=>x.Owner==target.Serial.Value);
    private static void Freeze(Mobile target){var s=Poison(target);TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=target.Serial.Value).Append(s with{UpdatedUtc=TimedEffects.Now.AddHours(1)}).ToArray());}
    private static void Hold(VenomState state)=>Venoms.Restore(Venoms.Snapshot().Where(x=>x.Owner!=state.Owner).Append(state).ToArray());
    private static GraveyardSkeleton Target(PlayerMobile p,int dx=2,int dy=0)
    {
        GraveyardSkeleton t;_creating=true;try{t=new GraveyardSkeleton{Name="VenomFixtureTarget",NoKillAwards=true,Frozen=true,CantWalk=true};}finally{_creating=false;}
        t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Poison,0);return t;
    }
    private static Item Named(PlayerMobile p,string source)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Equip "+source);ClassGear.Refresh(p);return item;}
    private static void Cast(PlayerMobile p,bool held=false)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(held?ClassCombat.ContinueCast(p,CombatAbility.Venom,null,new(65530,65530,0),Core.TickCount):ClassCombat.Perform(p,CombatAbility.Venom,null,new(65530,65530,0),Core.TickCount),"Venom begin");ClassCombat.ReleaseCast(p);
        var s=ClassCombat.For(p);Require(s.PendingTarget==Serial.Zero && s.PendingPoint==p.Location && s.Animation==16 && NecromancerEffects.ManaBalance256(p)==mana,"Canonical unpaid self SC cast");
        Require(ClassCombat.ResolveAction(p,s.EffectAt) && NecromancerEffects.ManaBalance256(p)==mana-3072 && !ClassCombat.ResolveAction(p,s.EffectAt),"Venom release/payment exactly once");
    }
    private static void Source()
    {
        var row=SkillMechanics.For(CombatAbility.Venom);Require(row.SourceId==278 && ClassAbilities.SelfTarget(row.Ability),"Pinned Venom/self identity");
        foreach(var (rank,low,high,seconds) in new[]{(1,1536,2048,120),(20,10880,11392,348),(21,11520,12032,360)})
        {var state=Venoms.Capture(1,Values(rank),0,DateTime.UnixEpoch);Require(state.Minimum256==low && state.Maximum256==high && state.Frames==10 && state.UntilUtc==DateTime.UnixEpoch.AddSeconds(seconds) && Values(rank).Mana256==3072,"Venom independent rank golden");}
        int cases=0;using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"venom-damage-source-probe.json")));
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();var state=Venoms.Capture(1,Values(V(0)),V(1),DateTime.UnixEpoch);Require(state.Minimum256==V(11) && state.Maximum256==V(12) && state.Frames==V(13),"Complete elemental getter/cast mastery differential");
            var raw=new MissileSourceStats(PoisonMin:V(11)+V(3),PoisonMax:V(12)+V(3),PoisonFrames:V(4),PoisonCount:V(5));int bound=-1;
            var melee=WeaponPoisonPackets.Melee(raw,V(2),V(7),span=>{bound=span;return V(8)!=0?span-1:0;},V(9),V(10),V(6));
            Require(melee.Rate256==V(14) && melee.Frames==V(15) && bound==V(16),"Unchanged melee rate/length/roll differential");
            if(V(6)>0){raw=raw with{PoisonFrames=V(6),PoisonCount=0};}var missile=raw.Scale(V(7));
            Require(missile.PoisonMin==V(17) && missile.PoisonMax==V(18) && missile.PoisonFrames==V(19) && missile.PoisonCount==V(20) && V(21)==0,"Full weapon missile creator/override/no-second-mastery differential");cases++;
        }
        Require(cases==10240,"Source case inventory");
        var old=Venoms.Capture(1,Values(20),25,DateTime.UnixEpoch);Require(!Venoms.Replaces(old,19) && Venoms.Replaces(old,20) && Venoms.Replaces(old,21),"Shared non-group2 rank arbitration");
        Log.Information("Venom:10240 source/native rate/length/mastery comparisons passed; current ranged-mastery acceptance remains separate");
    }
    private static void Check()
    {
        Source();var p=Fresh();Require(!ClassCombat.Available(p,CombatAbility.Venom),"Unlearned Venom available");Train(p,CombatAbility.Venom,1);
        Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.Venom) && SkillBindings.Instance.Assign(p,false,CombatAbility.Venom) && SkillBindings.Instance.AssignQuick(p,9,CombatAbility.Venom),"Venom right/quick bindings");
        Ready(p);NecromancerEffects.SetMana256(p,3071);Require(!ClassCombat.Perform(p,CombatAbility.Venom,null,p.Location,Core.TickCount),"Insufficient mana");Mana(p);
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.Venom,null,p.Location,Core.TickCount),"Cancelled begin");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at) && Venoms.State(p)==null && NecromancerEffects.ManaBalance256(p)==mana,"Cancelled cast paid/applied");
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.Venom,null,p.Location,Core.TickCount),"Moving begin");at=ClassCombat.For(p).EffectAt;p.Direction=Direction.East;Require(p.Move(Direction.East) && !ClassCombat.ResolveAction(p,at),"Move did not cancel Venom");
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.Venom,null,p.Location,Core.TickCount),"Hit recovery begin");at=ClassCombat.For(p).EffectAt;Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount) && !ClassCombat.ResolveAction(p,at),"Recovery failed cancellation");
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.Venom,null,p.Location,Core.TickCount),"Lost mana begin");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);Require(!ClassCombat.ResolveAction(p,at) && Venoms.State(p)==null,"Lost mana applied state");Mana(p);Cast(p,true);
        var first=Venoms.State(p);Require(first is{Rank:1,Minimum256:1536,Maximum256:2048,Frames:10} && Math.Abs((first.UntilUtc-TimedEffects.Now).TotalSeconds-120)<.2,"Native captured rank1 payload");
        AssassinBuffs.Cast(p,SkillMechanics.Values(p,CombatAbility.Fade));Require(Venoms.State(p)==first && AssassinBuffs.Faded(p),"Fade displaced Venom");AssassinBuffs.Cast(p,SkillMechanics.Values(p,CombatAbility.BurstOfSpeed));Require(Venoms.State(p)==first && AssassinBuffs.AttackRate(p)>0,"Quickness displaced Venom");AssassinBuffs.Clear(p);
        var claw=new Tekagi();ClassGear.Profiles.Convert(claw,99,GearRarity.Normal,new Random(173));p.AddToBackpack(claw);Require(p.EquipItem(claw),"Ordinary claw");var skull=Named(p,"Wormskull");var raw=BowLaunch.RawPoison(p,claw);Require(raw is{PoisonMin:102,PoisonMax:102,PoisonFrames:200,PoisonCount:1},"Actual poison gear raw collection");
        var packet=GearEffects.Damage(p,0,poisonRoll:_=>0);Require(packet.SourcePoison==new WeaponPoisonRoll(1638,10) && packet.PoisonMs==400,"Weapon Venom plus actual item poison/override");
        var t=Target(p);GearEffects.Strike(p,t,0,wearWeapon:false,preparedDamage:packet);Require(Poison(t) is{PoisonMs:400,PoisonClock.Rate256:1638} && Poison(t).PoisonSource==p.Serial.Value,"Actual landed melee poison consumer");
        int before=SkillHealth.Balance256(t);var start=Poison(t).UpdatedUtc;TimedEffects.Settle(t,start.AddMilliseconds(40));Require(before-SkillHealth.Balance256(t)==1638 && Poison(t).PoisonMs==360,"Exact one-frame raw health settlement");TimedEffects.ClearGear(t);t.Delete();
        var immune=Target(p);immune.SetResistance(ResistanceType.Poison,100);GearEffects.Strike(p,immune,0,wearWeapon:false,preparedDamage:packet);Require(!TimedEffects.Snapshot().Any(x=>x.Owner==immune.Serial.Value && x.PoisonMs>0),"Poison immunity");immune.Delete();
        p.AddToBackpack(claw);var bow=new Bow();ClassGear.Profiles.Convert(bow,99,GearRarity.Normal,new Random(173));p.AddToBackpack(bow);Require(p.EquipItem(bow),"Actual bow");var ranged=GearEffects.Damage(p,0,poisonRoll:_=>0);Require(ranged.SourcePoison==packet.SourcePoison && BowLaunch.Poison(p,bow) is{PoisonMin:1638,PoisonMax:2150,PoisonFrames:10,PoisonCount:0},"Ranged owner stat/override collection");p.AddToBackpack(bow);Require(p.EquipItem(claw),"Restore claw");
        p.AddToBackpack(skull);var naked=GearEffects.Damage(p,0,poisonRoll:_=>0);Require(naked.SourcePoison==new WeaponPoisonRoll(1536,10) && packet.SourcePoison==new WeaponPoisonRoll(1638,10),"Live gear collection or captured weapon snapshot changed");
        Hold(first with{UntilUtc=TimedEffects.Now.AddMilliseconds(80)});
        Later(180,"venom-result",()=>
        {
            Require(Venoms.State(p)==null && GearEffects.Damage(p,0).Poison==0 && packet.SourcePoison==new WeaponPoisonRoll(1638,10),"Natural buff expiry or captured impact lost poison");Lifecycle();
            Train(p,CombatAbility.Venom,20);var helm=Named(p,"Tarnhelm");Mana(p);Cast(p);var high=Venoms.State(p);Require(high is{Rank:21,Minimum256:11520,Maximum256:12032} && Math.Abs((high.UntilUtc-TimedEffects.Now).TotalSeconds-360)<.2,"Actual soft-rank capture");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Ready(p);int remaining=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.Venom,null,p.Location,Core.TickCount),"Lower-rank windup");Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && NecromancerEffects.ManaBalance256(p)==remaining && Venoms.State(p)==high,"Failed lower-rank overwrite/debit");Require(p.EquipItem(helm),"Restore helm");ClassGear.Refresh(p);Hold(high with{UntilUtc=TimedEffects.Now.AddSeconds(2)});Cast(p);Require(Venoms.State(p).UntilUtc>TimedEffects.Now.AddSeconds(359),"Equal-rank duration restart");
            var place=p.Location;p.MoveToWorld(new(1438,1696,0),Map.Trammel);Mana(p);Cast(p);Require(Venoms.State(p)!=null,"Town self cast");p.MoveToWorld(place,p.Map);
            var state=Venoms.State(p) with{UntilUtc=TimedEffects.Now.AddHours(1)};Hold(state);var target=Target(p);var savedPacket=GearEffects.Damage(p,0,poisonRoll:_=>0);GearEffects.Strike(p,target,0,wearWeapon:false,preparedDamage:savedPacket);Freeze(target);
            var pending=Fresh();Train(pending,CombatAbility.Venom,1);Ready(pending);int unpaid=NecromancerEffects.ManaBalance256(pending);Require(ClassCombat.Perform(pending,CombatAbility.Venom,null,pending.Location,Core.TickCount),"Saved unpaid windup");ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,claw.Serial.Value,helm.Serial.Value,state,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(target),Poison(target),savedPacket,pending.Serial.Value,unpaid)));Log.Information("venom-result: passed");
        });
    }
    private static void Lifecycle()
    {
        var p=Fresh();Train(p,CombatAbility.Venom,1);Hold(Venoms.Capture(p.Serial.Value,Values(1),0,TimedEffects.Now));p.Kill();p.Resurrect();Require(Venoms.State(p)==null,"Death retained Venom");p.Corpse?.Delete();p.Delete();
        p=Fresh();Train(p,CombatAbility.Venom,1);Hold(Venoms.Capture(p.Serial.Value,Values(1),0,TimedEffects.Now));Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Paladin) && Venoms.State(p)==null && ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Assassin),"History retained Venom");Hold(Venoms.Capture(p.Serial.Value,Values(1),0,TimedEffects.Now));ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(Venoms.State(p)==null,"Respec retained Venom");p.Delete();
    }
    private static void Reload()
    {
        Source();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var pending=(PlayerMobile)World.FindMobile((Serial)s.Pending);
        Require(Venoms.State(p)==s.Buff && NecromancerEffects.ManaBalance256(p)==s.Mana && World.FindItem((Serial)s.Weapon)?.Parent==p && World.FindItem((Serial)s.Helm)?.Parent==p && SkillMechanics.Rank(p,CombatAbility.Venom)==21,"Saved buff/mana/real equipment/rank");
        Require(SkillHealth.Balance256(t)==s.Balance && JsonSerializer.Serialize(Poison(t))==JsonSerializer.Serialize(s.Poison) && s.Packet.SourcePoison!=null,"Saved poison/health/source packet");Require(ClassCombat.For(pending).PendingAbility==CombatAbility.None && Venoms.State(pending)==null && NecromancerEffects.ManaBalance256(pending)==s.PendingMana,"Unpaid cast replayed");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.Venom && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,9)==CombatAbility.Venom,"Saved bindings");
        var now=TimedEffects.Now;TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=t.Serial.Value).Append(s.Poison with{UpdatedUtc=now}).ToArray());TimedEffects.Settle(t,now.AddMilliseconds(40));Require(s.Balance-SkillHealth.Balance256(t)==s.Poison.PoisonClock.Rate256 && Poison(t).PoisonMs==s.Poison.PoisonMs-40 && Poison(t).PoisonSource==s.Owner,"Resumed poison frame/source or repeated payment");Freeze(t);
        var next=s with{Balance=SkillHealth.Balance256(t),Poison=Poison(t)};Hold(s.Buff with{UntilUtc=now.AddMilliseconds(80)});
        Later(180,"venom-reload-result",()=>{Require(Venoms.State(p)==null,"Reloaded natural buff expiry");Hold(s.Buff);File.WriteAllText(Manifest,JsonSerializer.Serialize(next));Log.Information("venom-reload-result: passed");});
    }
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.Venom,1);Ready(p);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Venom) && SkillBindings.Instance.AssignQuick(p,9,CombatAbility.Venom),"Connected binding");
        var net=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();net.SendMobileUpdate(p);
        int mana=NecromancerEffects.ManaBalance256(p);bool began=false;long deadline=Core.TickCount+5000;
        void Wait(){try{Require(Core.TickCount<deadline,"Connected Venom timeout");if(ClassCombat.For(p).PendingAbility==CombatAbility.Venom){began=true;Require(NecromancerEffects.ManaBalance256(p)==mana,"Premature input payment");}if(Venoms.State(p)==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}Require(began && Venoms.State(p) is{Rank:1,Minimum256:1536,Maximum256:2048,Frames:10} && NecromancerEffects.ManaBalance256(p)==mana-3072,"Natural Venom self cast/payment");p.Delete();Log.Information("venom-input-result: passed");}catch(Exception e){p.Delete();Log.Error(e,"venom-input-result: failed");}}
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("venom-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.Venom,65530,65530,0);Wait();});
    }
}
