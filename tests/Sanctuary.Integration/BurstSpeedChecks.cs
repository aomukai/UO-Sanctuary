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

public static class BurstSpeedChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BurstSpeedChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Armor,uint Gloves,AssassinBuffState State,int Mana,int Frames,int Walk,int Run,uint Pending,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"burst-speed-fixtures.json");
    private static bool _creating;
    private static void Rates()
    {
        var mp=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=x=>_creating || x.Name?.StartsWith("AssassinBuffFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(x)??Mobile.DefaultManaRate;
    }
    public static void Configure(){Rates();Register("burst-speed-check",Check);Register("burst-speed-reload-check",Reload);Register("burst-speed-input-check",Input);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Burst of Speed cast, skill IAS/velocity, lifecycle, input and saved state",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();}catch(Exception e){Log.Error(e,"{Name}: failed",name);}});
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x191,Name="AssassinBuffFixture",Player=true};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Assassin);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength allocation");}
        while(p.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity allocation");}
        FixtureSpace.Place(p,new(6060,1850,0),Map.Trammel);p.Hits=p.HitsMax;p.Stam=p.StamMax;p.Mana=p.ManaMax;
        Train(p,CombatAbility.ClawMastery,1);return p;
    }
    private static void Train(PlayerMobile owner,CombatAbility ability,int rank)
    {string id=SkillMechanics.For(ability).TalentId;while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Cannot train "+id);}}
    private static void Ready(PlayerMobile owner)
    {ClassCombat.Stop(owner);var s=ClassCombat.For(owner);s.NextAction=s.RecoveryUntil=Core.TickCount;owner.Frozen=owner.Paralyzed=false;}
    private static void Mana(PlayerMobile owner)=>NecromancerEffects.SetMana256(owner,owner.ManaMax*256);
    private static void Replace(AssassinBuffState state)=>AssassinBuffs.Restore(AssassinBuffs.Snapshot().Where(x=>x.Owner!=state.Owner).Append(state).ToArray());
    private static Item Named(PlayerMobile owner,string source)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);owner.AddToBackpack(item);Require(owner.EquipItem(item),"Named "+source);ClassGear.Refresh(owner);return item;}
    private static BaseWeapon Weapon(PlayerMobile owner,BaseWeapon item)
    {if(CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}ClassGear.Profiles.Convert(item,99,GearRarity.Normal,new Random(173));owner.AddToBackpack(item);Require(owner.EquipItem(item),"Ordinary weapon");ClassGear.Refresh(owner);return item;}
    private static void Cast(PlayerMobile owner,bool held=false,Mobile hover=null)
    {
        Ready(owner);int before=NecromancerEffects.ManaBalance256(owner);bool started=held?ClassCombat.ContinueCast(owner,CombatAbility.BurstOfSpeed,hover,new Point3D(65530,65530,0),Core.TickCount):ClassCombat.Perform(owner,CombatAbility.BurstOfSpeed,hover,new Point3D(65530,65530,0),Core.TickCount);
        Require(started,"Burst cast begin");ClassCombat.ReleaseCast(owner);var s=ClassCombat.For(owner);
        Require(s.PendingTarget==Serial.Zero && s.PendingPoint==owner.Location && s.Animation==16 && NecromancerEffects.ManaBalance256(owner)==before,"Self-target SC windup or premature payment");
        Require(ClassCombat.ResolveAction(owner,s.EffectAt) && NecromancerEffects.ManaBalance256(owner)==before-2560,"Single release ten-mana payment");
        Require(!ClassCombat.ResolveAction(owner,s.EffectAt) && NecromancerEffects.ManaBalance256(owner)==before-2560,"Repeated release repaid mana");
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.BurstOfSpeed);Require(row.SourceId==258 && row.Text("aurastate")=="quickness" && row.Number("InTown")==1 && ClassAbilities.SelfTarget(row.Ability),"Pinned self/town identity");
        foreach(var (rank,velocity,attack,frames) in new[]{(1,23,21,3000),(2,29,27,3300),(3,34,31,3600),(20,61,52,8700),(21,61,53,9000),(255,70,60,79200)})
        {var values=new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>0,99);var at=new DateTime(2026,10,24,0,0,0,DateTimeKind.Utc);var state=AssassinBuffs.Capture(1,values,at);Require(values.Mana256==2560 && state.Velocity==velocity && state.AttackRate==attack && state.UntilUtc==at.AddMilliseconds(frames*40),"Pinned speed/duration golden "+rank);}
    }
    private static void Consumers(PlayerMobile owner)
    {
        var state=AssassinBuffs.State(owner);Require(state!=null,"Missing captured buff");int ias=CombatTiming.Ias(owner),fcr=CombatTiming.Fcr(owner),slow=GearEffects.AttackSlow(owner),skill=PaladinOffense.AttackRate(owner)+state.AttackRate;var weapon=CombatTiming.Weapon(CombatTiming.Equipped(owner));
        Require(CombatTiming.For(owner,CombatAbility.BasicAttack)==CombatTiming.Calculate(DiabloClass.Assassin,CombatAbility.BasicAttack,weapon,ias,fcr,slow,skill),"Skill IAS did not join direct attack-rate pool");
        Require(CombatTiming.WeaponSequenceAdvance(owner)==CombatTiming.FrameAdvance(weapon,ias,slow,skill),"Sequence pool missed captured skill IAS");
        var cast=CombatTiming.For(owner,CombatAbility.BurstOfSpeed);AssassinBuffs.Clear(owner);var normal=CombatTiming.For(owner,CombatAbility.BasicAttack);int walk=owner.ComputeMovementSpeed(Direction.East,false),run=owner.ComputeMovementSpeed(Direction.East|Direction.Running,false);
        Require(CombatTiming.For(owner,CombatAbility.BurstOfSpeed)==cast,"Burst changed FCR-based SC timing");Replace(state);ClassGear.Refresh(owner);
        Require(CombatTiming.For(owner,CombatAbility.BasicAttack).Frames<normal.Frames && owner.ComputeMovementSpeed(Direction.East,false)<walk && owner.ComputeMovementSpeed(Direction.East|Direction.Running,false)<run,"Actual attack/walk/run consumers ignored the buff");
        Require(ClassCombatHooks.RunningDelay(owner,200)==EquipmentBalance.RunDelay(200,EquipmentBalance.RunPenalty(owner),CombatPotions.RunBonus(owner)+FireGround.Speed(owner)+PaladinVigor.VelocityPercent(owner)+state.Velocity)
            && ClassCombatHooks.WalkingDelay(owner,400)==EquipmentBalance.RunDelay(400,0,FireGround.Speed(owner)+PaladinVigor.VelocityPercent(owner)+state.Velocity),"Movement velocity composition");
    }
    private static void Lifecycle()
    {
        var owner=Fresh();Train(owner,CombatAbility.BurstOfSpeed,1);AssassinBuffs.Cast(owner,SkillMechanics.Values(owner,CombatAbility.BurstOfSpeed));owner.Kill();owner.Resurrect();Require(AssassinBuffs.State(owner)==null,"Death/resurrection retained speed buff");
        owner.Corpse?.Delete();owner.Delete();owner=Fresh();Train(owner,CombatAbility.BurstOfSpeed,1);
        AssassinBuffs.Cast(owner,SkillMechanics.Values(owner,CombatAbility.BurstOfSpeed));Require(ClassCreation.Profiles.TryActivateHistory(owner,DiabloClass.Paladin),"History exit");Require(AssassinBuffs.State(owner)==null,"History retained Assassin buff");Require(ClassCreation.Profiles.TryActivateHistory(owner,DiabloClass.Assassin),"History return");
        AssassinBuffs.Cast(owner,SkillMechanics.Values(owner,CombatAbility.BurstOfSpeed));ClassCreation.Profiles.TryGet(owner.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(owner,profile);Require(AssassinBuffs.State(owner)==null,"Respec retained buff");owner.Delete();
    }
    private static void Check()
    {
        Goldens();Lifecycle();var owner=Fresh();Require(!ClassCombat.Available(owner,CombatAbility.BurstOfSpeed),"Unlearned buff available");Train(owner,CombatAbility.BurstOfSpeed,1);
        Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.BurstOfSpeed) && SkillBindings.Instance.Assign(owner,false,CombatAbility.BurstOfSpeed) && SkillBindings.Instance.AssignQuick(owner,5,CombatAbility.BurstOfSpeed),"Right/quick only bindings");
        var weapon=Weapon(owner,new Longsword());Ready(owner);NecromancerEffects.SetMana256(owner,2559);Require(!ClassCombat.Perform(owner,CombatAbility.BurstOfSpeed,null,owner.Location,Core.TickCount) && NecromancerEffects.ManaBalance256(owner)==2559,"Insufficient mana cast");Mana(owner);
        int mana=NecromancerEffects.ManaBalance256(owner);Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.BurstOfSpeed,null,owner.Location,Core.TickCount),"Cancelled begin");long at=ClassCombat.For(owner).EffectAt;ClassCombat.Stop(owner);Require(!ClassCombat.ResolveAction(owner,at) && NecromancerEffects.ManaBalance256(owner)==mana,"Cancelled cast paid/applied");
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.BurstOfSpeed,null,owner.Location,Core.TickCount),"Moving begin");at=ClassCombat.For(owner).EffectAt;var origin=owner.Location;owner.Direction=Direction.East;Require(owner.Move(Direction.East),"Actual outward step");owner.Direction=Direction.West;Require(owner.Move(Direction.West) && owner.Location==origin,"Actual return step");Require(!ClassCombat.ResolveAction(owner,at) && NecromancerEffects.ManaBalance256(owner)==mana && AssassinBuffs.State(owner)==null,"Out-and-back revived windup");
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.BurstOfSpeed,null,owner.Location,Core.TickCount),"Lost mana begin");at=ClassCombat.For(owner).EffectAt;NecromancerEffects.SetMana256(owner,0);Require(!ClassCombat.ResolveAction(owner,at) && AssassinBuffs.State(owner)==null,"Lost-mana release created buff");Mana(owner);
        Ready(owner);mana=NecromancerEffects.ManaBalance256(owner);Require(ClassCombat.Perform(owner,CombatAbility.BurstOfSpeed,null,owner.Location,Core.TickCount),"Recovery begin");at=ClassCombat.For(owner).EffectAt;Require(HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount) && !ClassCombat.ResolveAction(owner,at) && AssassinBuffs.State(owner)==null && NecromancerEffects.ManaBalance256(owner)==mana,"Hit recovery did not interrupt unpaid buff");Ready(owner);
        var friend=Fresh();Cast(owner,true,friend);friend.Delete();var first=AssassinBuffs.State(owner);Require(first is{Rank:1,Velocity:23,AttackRate:21} && Math.Abs((first.UntilUtc-TimedEffects.Now).TotalSeconds-120)<1,"Rank1 captured payload");Consumers(owner);
        var helm=Named(owner,"Tarnhelm");Require(SkillMechanics.Rank(owner,CombatAbility.BurstOfSpeed)==2 && AssassinBuffs.State(owner)==first,"Soft gear retroactively changed active buff");Cast(owner);Require(AssassinBuffs.State(owner) is{Rank:2,Velocity:29,AttackRate:27},"Soft rank recast");owner.AddToBackpack(helm);ClassGear.Refresh(owner);Cast(owner);Require(AssassinBuffs.State(owner).Rank==1,"Lower-rank recast was rejected");
        Train(owner,CombatAbility.BurstOfSpeed,20);Cast(owner);var high=AssassinBuffs.State(owner);Require(high is{Rank:20,Velocity:61,AttackRate:52} && Math.Abs((high.UntilUtc-TimedEffects.Now).TotalSeconds-348)<1,"Rank20 captured payload");
        var armor=Named(owner,"Twitchthroe");var gloves=Named(owner,"Magefist");Require(CombatTiming.Ias(owner)>=20 && CombatTiming.Fcr(owner)>=20,"Real IAS/FCR fixture");Consumers(owner);var timing=CombatTiming.For(owner,CombatAbility.BasicAttack);Require(timing.Frames!=CombatTiming.Calculate(DiabloClass.Assassin,CombatAbility.BasicAttack,CombatTiming.Weapon(weapon),CombatTiming.Ias(owner)+high.AttackRate,CombatTiming.Fcr(owner),GearEffects.AttackSlow(owner),0).Frames,"Fixture failed to distinguish skill IAS from item IAS");
        var bow=Weapon(owner,new Bow());Consumers(owner);owner.AddToBackpack(bow);Require(owner.EquipItem(weapon),"Restore sword");ClassGear.Refresh(owner);Consumers(owner);
        FixtureSpace.Place(owner,new(1438,1696,0),Map.Trammel);Require(Server.Spells.SpellHelper.IsTown(owner.Location,owner),"Town fixture");Cast(owner);FixtureSpace.Place(owner,new(6060,1850,0),Map.Trammel);
        Require(owner.EquipItem(helm),"Saved soft-rank helm");ClassGear.Refresh(owner);Cast(owner);var active=AssassinBuffs.State(owner);Require(active.Rank==21 && active.AttackRate==53,"Saved effective rank21");Replace(active with{UntilUtc=TimedEffects.Now.AddMilliseconds(80)});
        Later(180,"burst-speed-result",()=>
        {
            Require(AssassinBuffs.State(owner)==null && AssassinBuffs.AttackRate(owner)==0 && AssassinBuffs.Velocity(owner)==0 && !AssassinBuffs.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Natural expiry retained state/consumers");
            Cast(owner);active=AssassinBuffs.State(owner) with{UntilUtc=TimedEffects.Now.AddHours(1)};Replace(active); // Inspection hold after real duration and expiry assertions.
            var pending=Fresh();Train(pending,CombatAbility.BurstOfSpeed,1);Ready(pending);int before=NecromancerEffects.ManaBalance256(pending);Require(ClassCombat.Perform(pending,CombatAbility.BurstOfSpeed,null,pending.Location,Core.TickCount),"Saved unpaid windup");ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,weapon.Serial.Value,helm.Serial.Value,armor.Serial.Value,gloves.Serial.Value,active,NecromancerEffects.ManaBalance256(owner),CombatTiming.For(owner,CombatAbility.BasicAttack).Frames,owner.ComputeMovementSpeed(Direction.East,false),owner.ComputeMovementSpeed(Direction.East|Direction.Running,false),pending.Serial.Value,before)));Log.Information("burst-speed-result: passed");
        });
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var pending=(PlayerMobile)World.FindMobile((Serial)saved.Pending);
        Require(AssassinBuffs.State(owner)==saved.State && ClassCreation.Profiles.GetRank(owner.Serial,"assassin.burstofspeed")==20 && SkillMechanics.Rank(owner,CombatAbility.BurstOfSpeed)==21,"Saved captured/hard/effective ranks");
        foreach(uint serial in new[]{saved.Weapon,saved.Helm,saved.Armor,saved.Gloves}){Require(World.FindItem((Serial)serial)?.Parent==owner,"Saved real gear");}
        Require(NecromancerEffects.ManaBalance256(owner)==saved.Mana && CombatTiming.For(owner,CombatAbility.BasicAttack).Frames==saved.Frames && owner.ComputeMovementSpeed(Direction.East,false)==saved.Walk && owner.ComputeMovementSpeed(Direction.East|Direction.Running,false)==saved.Run,$"Saved mana/speed changed: mana={NecromancerEffects.ManaBalance256(owner)}/{saved.Mana}, frames={CombatTiming.For(owner,CombatAbility.BasicAttack).Frames}/{saved.Frames}, walk={owner.ComputeMovementSpeed(Direction.East,false)}/{saved.Walk}, run={owner.ComputeMovementSpeed(Direction.East|Direction.Running,false)}/{saved.Run}");Consumers(owner);
        Require(ClassCombat.For(pending).PendingAbility==CombatAbility.None && AssassinBuffs.State(pending)==null && NecromancerEffects.ManaBalance256(pending)==saved.PendingMana,"Saved unpaid windup replayed/paid");
        Require(SkillBindings.Instance.Get(owner).Right==CombatAbility.BurstOfSpeed && SkillBindings.Instance.GetQuick(owner.Serial,DiabloClass.Assassin,5)==CombatAbility.BurstOfSpeed,"Saved bindings");
        var helm=World.FindItem((Serial)saved.Helm);owner.AddToBackpack(helm);ClassGear.Refresh(owner);Require(AssassinBuffs.State(owner)==saved.State,"Removed soft gear changed captured state");Require(owner.EquipItem(helm),"Saved helm re-equip");ClassGear.Refresh(owner);
        Replace(saved.State with{UntilUtc=TimedEffects.Now.AddMilliseconds(80)});Later(180,"burst-speed-reload-result",()=>{Require(AssassinBuffs.State(owner)==null,"Reloaded expiry retained state");Replace(saved.State);Log.Information("burst-speed-reload-result: passed");});
    }
    private static void Input()
    {
        var owner=Fresh();Train(owner,CombatAbility.BurstOfSpeed,1);Ready(owner);Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.BurstOfSpeed) && SkillBindings.Instance.AssignQuick(owner,5,CombatAbility.BurstOfSpeed),"Connected bindings");
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=owner;owner.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(owner);
        int before=NecromancerEffects.ManaBalance256(owner);long deadline=Core.TickCount+5000;bool started=false;
        void Clean(){owner.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Connected cast did not finish");var state=AssassinBuffs.State(owner);
                if(!started && ClassCombat.For(owner).PendingAbility==CombatAbility.BurstOfSpeed){started=true;Require(NecromancerEffects.ManaBalance256(owner)==before && state==null,"Natural unpaid SC windup");}
                if(state==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(started && state is{Rank:1,Velocity:23,AttackRate:21} && NecromancerEffects.ManaBalance256(owner)==before-2560,"Natural self buff/payment");Consumers(owner);Clean();Log.Information("burst-speed-input-result: passed");
            }catch(Exception e){Clean();Log.Error(e,"burst-speed-input-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("burst-speed-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.BurstOfSpeed,65530,65530,0);Wait();});
    }
}
