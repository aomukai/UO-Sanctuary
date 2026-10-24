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

public static class FadeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FadeChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Shield,AssassinBuffState State,int Mana,int Physical,int Fire,DateTime CurseUntil,uint Pending,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fade-fixtures.json");
    private static bool _creating;
    private static void Rates()
    {
        var mp=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=x=>_creating || x.Name?.StartsWith("AssassinFadeFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(x)??Mobile.DefaultManaRate;
    }
    public static void Configure(){Rates();Register("fade-check",Check);Register("fade-reload-check",Reload);Register("fade-input-check",Input);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Fade cast, skill IAS/velocity, lifecycle, input and saved state",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();}catch(Exception e){Log.Error(e,"{Name}: failed",name);}});
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x191,Name="AssassinFadeFixture",Player=true};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Assassin);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength allocation");}
        while(p.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity allocation");}
        FixtureSpace.Place(p,new(6060,1850,0),Map.Trammel);p.Hits=p.HitsMax;p.Stam=p.StamMax;p.Mana=p.ManaMax;
        Train(p,CombatAbility.ClawMastery,1);Train(p,CombatAbility.BurstOfSpeed,1);return p;
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
        Ready(owner);int before=NecromancerEffects.ManaBalance256(owner);bool started=held?ClassCombat.ContinueCast(owner,CombatAbility.Fade,hover,new Point3D(65530,65530,0),Core.TickCount):ClassCombat.Perform(owner,CombatAbility.Fade,hover,new Point3D(65530,65530,0),Core.TickCount);
        Require(started,"Burst cast begin");ClassCombat.ReleaseCast(owner);var s=ClassCombat.For(owner);
        Require(s.PendingTarget==Serial.Zero && s.PendingPoint==owner.Location && s.Animation==16 && NecromancerEffects.ManaBalance256(owner)==before,"Self-target SC windup or premature payment");
        Require(ClassCombat.ResolveAction(owner,s.EffectAt) && NecromancerEffects.ManaBalance256(owner)==before-2560,"Single release ten-mana payment");
        Require(!ClassCombat.ResolveAction(owner,s.EffectAt) && NecromancerEffects.ManaBalance256(owner)==before-2560,"Repeated release repaid mana");
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.Fade);Require(row.SourceId==267 && row.Text("aurastate")=="fade" && row.Number("InTown")==1 && ClassAbilities.SelfTarget(row.Ability),"Pinned self/town identity");
        foreach(var (rank,element,curse,frames) in new[]{(1,19,47,3000),(20,64,82,8700),(21,65,82,9000),(255,75,90,79200)})
        {var values=new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>0,99);var at=DateTime.UnixEpoch;var state=AssassinBuffs.Capture(1,values,at);Require(values.Mana256==2560 && state.ElementResistance==element && state.CurseResistance==curse && state.PhysicalResistance==rank && state.Fade==2 && state.Velocity==0 && state.AttackRate==0 && state.UntilUtc==at.AddMilliseconds(frames*40),"Pinned Fade golden "+rank);}
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"fade-curse-source-probe.json")));
        int count=0;foreach(var rowCase in source.RootElement.EnumerateArray())
        {
            var x=rowCase.EnumerateArray().Select(v=>v.GetInt32()).ToArray();bool accepted=SourceCurseDuration.Reduce(x[2],x[0]!=0,x[1],out int adjusted);
            Require(adjusted==x[7] && (accepted || x[6]==0) && accepted==!(x[0]!=0 && x[1]>=100),"Complete source curse-duration differential "+count);count++;
        }
        Require(count==2268 && SourceCurseDuration.Reduce(1,true,82,out int tiny) && tiny==1,"Curse source fixture coverage/one-frame preservation");
    }
    private static void Consumers(PlayerMobile owner)
    {
        var state=AssassinBuffs.State(owner);Require(state is{Ability:CombatAbility.Fade,Fade:2,AttackRate:0,Velocity:0},"Missing Fade payload");
        Require(NamedSetVisuals.Faded(owner) && NamedSetVisuals.Opacity(owner)==NamedVisualProtocol.Faded && !owner.Hidden && !owner.Blessed,"Fade opacity changed visibility/targetability");
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison})
        {int expected=Math.Clamp(ClassGear.RawResistance(owner,type),-100,NamedCombat.ResistanceCap(owner,type));Require(owner.GetResistance(type)==expected,"Native capped resistance "+type);}
        var enemy=new Ogre();enemy.AIObject.AITimer.Stop();enemy.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);enemy.Frozen=enemy.CantWalk=true;
        try
        {
            foreach(var (type,phys,fire,cold,poison,lightning) in new[]{(ResistanceType.Physical,100,0,0,0,0),(ResistanceType.Fire,0,100,0,0,0),(ResistanceType.Cold,0,0,100,0,0),(ResistanceType.Poison,0,0,0,100,0),(ResistanceType.Energy,0,0,0,0,100)})
            {owner.Hits=owner.HitsMax;SkillHealth.Clear(owner);int hp=owner.Hits;int resistance=owner.GetResistance(type);AOS.Damage(owner,enemy,100,false,phys,fire,cold,poison,lightning);Require(owner.Hits==hp-(100-resistance),"Actual typed incoming damage "+type+":"+(hp-owner.Hits)+"/"+(100-resistance));}
            Require(NamedElements.MagicResistance(owner)==0,"Fade granted magic resistance");
            if(ClassGear.RawResistance(owner,ResistanceType.Cold)>75){Require(NamedElements.Resistance(enemy,owner,"cold",owner.ColdResistance,50)==Math.Clamp(ClassGear.RawResistance(owner,ResistanceType.Cold)-50,-100,75),"Elemental overflow was capped before pierce");}
        }finally{enemy.Delete();owner.Hits=owner.HitsMax;SkillHealth.Clear(owner);}
        int timing=CombatTiming.For(owner,CombatAbility.BasicAttack).Frames,walk=owner.ComputeMovementSpeed(Direction.East,false);AssassinBuffs.Clear(owner);
        Require(CombatTiming.For(owner,CombatAbility.BasicAttack).Frames==timing && owner.ComputeMovementSpeed(Direction.East,false)==walk && !NamedSetVisuals.Faded(owner),"Fade leaked IAS/movement or opacity");Replace(state);ClassGear.Refresh(owner);
    }
    private static void Curses(PlayerMobile owner)
    {
        Require(SourceCurseDuration.Incoming(owner,"amplifydamage",125,out int reduced) && reduced==67,"Subtract truncated percentage, rank1");
        foreach(string state in new[]{"quickness","innersight","poison"}){Require(SourceCurseDuration.Incoming(owner,state,125,out int unchanged) && unchanged==125,"Noncurse state shortened "+state);}
        var before=TimedEffects.Now;EliteAuras.Amplify(owner,TimeSpan.FromSeconds(5));var expiry=EliteAuras.Curses.Expires(owner.Serial);
        Require(Math.Abs((expiry-before).TotalMilliseconds-2680)<40,"Actual elite curse producer omitted duration receiver");
        Cast(owner);Require(EliteAuras.Curses.Expires(owner.Serial)==expiry,"Fade recast retroactively rescaled an existing curse");EliteAuras.Curses.Clear(owner.Serial);EliteAuras.Refresh(owner);
        // V0 BoS saves predate the optional Fade fields. Their absent fields
        // deserialize as zero and still pass the source payload validator.
        var old=AssassinBuffs.Capture(owner.Serial.Value,SkillMechanics.Values(owner,CombatAbility.BurstOfSpeed),TimedEffects.Now);
        string json=JsonSerializer.Serialize(new{old.Owner,old.Ability,old.Rank,old.Velocity,old.AttackRate,old.UntilUtc});var legacy=JsonSerializer.Deserialize<AssassinBuffState>(json);Require(legacy==old,"V0 legacy JSON defaults");
        var fade=AssassinBuffs.State(owner);Replace(legacy);ClassGear.Refresh(owner);Require(AssassinBuffs.State(owner).Ability==CombatAbility.BurstOfSpeed && AssassinBuffs.Resistance(owner,ResistanceType.Fire)==0 && !NamedSetVisuals.Faded(owner),"BoS did not replace Fade");
        Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner);Require(ClassCombat.Perform(owner,CombatAbility.BurstOfSpeed,null,owner.Location,Core.TickCount),"Actual BoS replacement begin");ClassCombat.ReleaseCast(owner);Require(ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt) && NecromancerEffects.ManaBalance256(owner)==mana-2560 && AssassinBuffs.State(owner).Ability==CombatAbility.BurstOfSpeed && !NamedSetVisuals.Faded(owner),"Actual BoS replacement release");
        Cast(owner);Require(AssassinBuffs.State(owner).Ability==CombatAbility.Fade && AssassinBuffs.AttackRate(owner)==0 && AssassinBuffs.Velocity(owner)==0,"Fade did not replace BoS");
    }
    private static void Lifecycle()
    {
        var owner=Fresh();Train(owner,CombatAbility.Fade,1);AssassinBuffs.Cast(owner,SkillMechanics.Values(owner,CombatAbility.Fade));owner.Kill();owner.Resurrect();Require(AssassinBuffs.State(owner)==null,"Death/resurrection retained speed buff");
        owner.Corpse?.Delete();owner.Delete();owner=Fresh();Train(owner,CombatAbility.Fade,1);
        AssassinBuffs.Cast(owner,SkillMechanics.Values(owner,CombatAbility.Fade));Require(ClassCreation.Profiles.TryActivateHistory(owner,DiabloClass.Paladin),"History exit");Require(AssassinBuffs.State(owner)==null,"History retained Assassin buff");Require(ClassCreation.Profiles.TryActivateHistory(owner,DiabloClass.Assassin),"History return");
        AssassinBuffs.Cast(owner,SkillMechanics.Values(owner,CombatAbility.Fade));ClassCreation.Profiles.TryGet(owner.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(owner,profile);Require(AssassinBuffs.State(owner)==null,"Respec retained buff");owner.Delete();
    }
    private static void Check()
    {
        Goldens();Lifecycle();var owner=Fresh();Require(!ClassCombat.Available(owner,CombatAbility.Fade),"Unlearned buff available");Train(owner,CombatAbility.Fade,1);
        Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.Fade) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Fade) && SkillBindings.Instance.AssignQuick(owner,6,CombatAbility.Fade),"Right/quick only bindings");
        var weapon=Weapon(owner,new Longsword());Ready(owner);NecromancerEffects.SetMana256(owner,2559);Require(!ClassCombat.Perform(owner,CombatAbility.Fade,null,owner.Location,Core.TickCount) && NecromancerEffects.ManaBalance256(owner)==2559,"Insufficient mana cast");Mana(owner);
        int mana=NecromancerEffects.ManaBalance256(owner);Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.Fade,null,owner.Location,Core.TickCount),"Cancelled begin");long at=ClassCombat.For(owner).EffectAt;ClassCombat.Stop(owner);Require(!ClassCombat.ResolveAction(owner,at) && NecromancerEffects.ManaBalance256(owner)==mana,"Cancelled cast paid/applied");
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.Fade,null,owner.Location,Core.TickCount),"Moving begin");at=ClassCombat.For(owner).EffectAt;var origin=owner.Location;owner.Direction=Direction.East;Require(owner.Move(Direction.East),"Actual outward step");owner.Direction=Direction.West;Require(owner.Move(Direction.West) && owner.Location==origin,"Actual return step");Require(!ClassCombat.ResolveAction(owner,at) && NecromancerEffects.ManaBalance256(owner)==mana && AssassinBuffs.State(owner)==null,"Out-and-back revived windup");
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.Fade,null,owner.Location,Core.TickCount),"Lost mana begin");at=ClassCombat.For(owner).EffectAt;NecromancerEffects.SetMana256(owner,0);Require(!ClassCombat.ResolveAction(owner,at) && AssassinBuffs.State(owner)==null,"Lost-mana release created buff");Mana(owner);
        Ready(owner);mana=NecromancerEffects.ManaBalance256(owner);Require(ClassCombat.Perform(owner,CombatAbility.Fade,null,owner.Location,Core.TickCount),"Recovery begin");at=ClassCombat.For(owner).EffectAt;Require(HitRecovery.Apply(owner,owner.HitsMax,Core.TickCount) && !ClassCombat.ResolveAction(owner,at) && AssassinBuffs.State(owner)==null && NecromancerEffects.ManaBalance256(owner)==mana,"Hit recovery did not interrupt unpaid buff");Ready(owner);
        var friend=Fresh();Cast(owner,true,friend);friend.Delete();var first=AssassinBuffs.State(owner);Require(first is{Rank:1,ElementResistance:19,PhysicalResistance:1,CurseResistance:47} && Math.Abs((first.UntilUtc-TimedEffects.Now).TotalSeconds-120)<1,"Rank1 captured payload");Consumers(owner);Curses(owner);
        var helm=Named(owner,"Tarnhelm");Require(SkillMechanics.Rank(owner,CombatAbility.Fade)==2 && AssassinBuffs.State(owner).Rank==1,"Soft gear retroactively changed active buff");Cast(owner);Require(AssassinBuffs.State(owner).Rank==2,"Soft rank recast");owner.AddToBackpack(helm);ClassGear.Refresh(owner);Cast(owner);Require(AssassinBuffs.State(owner).Rank==1,"Lower-rank recast was rejected");
        Train(owner,CombatAbility.Fade,20);Cast(owner);var high=AssassinBuffs.State(owner);Require(high is{Rank:20,ElementResistance:64,PhysicalResistance:20,CurseResistance:82} && Math.Abs((high.UntilUtc-TimedEffects.Now).TotalSeconds-348)<1,"Rank20 captured payload");
        var shield=Named(owner,"Stormshield");Require(ClassGear.RawResistance(owner,ResistanceType.Physical)==55 && owner.PhysicalResistance==50,"Physical overcap/real Stormshield fixture");Consumers(owner);
        EliteAuras.Amplify(owner,TimeSpan.FromSeconds(5));Require(ClassGear.RawResistance(owner,ResistanceType.Physical)==-45 && owner.PhysicalResistance==-45,"Curse must consume raw physical overflow before cap");Consumers(owner);EliteAuras.Curses.Clear(owner.Serial);EliteAuras.Refresh(owner);
        FixtureSpace.Place(owner,new(1438,1696,0),Map.Trammel);Require(Server.Spells.SpellHelper.IsTown(owner.Location,owner),"Town fixture");Cast(owner);FixtureSpace.Place(owner,new(6060,1850,0),Map.Trammel);
        Require(owner.EquipItem(helm),"Saved soft-rank helm");ClassGear.Refresh(owner);Cast(owner);var active=AssassinBuffs.State(owner);Require(active.Rank==21 && active.ElementResistance==65,"Saved effective rank21");Replace(active with{UntilUtc=TimedEffects.Now.AddMilliseconds(80)});
        Later(180,"fade-result",()=>
        {
            Require(AssassinBuffs.State(owner)==null && AssassinBuffs.CurseResistance(owner)==0 && !NamedSetVisuals.Faded(owner) && !AssassinBuffs.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Natural expiry retained state/consumers");
            Cast(owner);active=AssassinBuffs.State(owner) with{UntilUtc=TimedEffects.Now.AddHours(1)};Replace(active); // Inspection hold after real duration and expiry assertions.
            EliteAuras.Amplify(owner,TimeSpan.FromHours(1));
            var pending=Fresh();Train(pending,CombatAbility.Fade,1);Ready(pending);int before=NecromancerEffects.ManaBalance256(pending);Require(ClassCombat.Perform(pending,CombatAbility.Fade,null,pending.Location,Core.TickCount),"Saved unpaid windup");ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,weapon.Serial.Value,helm.Serial.Value,shield.Serial.Value,active,NecromancerEffects.ManaBalance256(owner),owner.PhysicalResistance,owner.FireResistance,EliteAuras.Curses.Expires(owner.Serial),pending.Serial.Value,before)));Log.Information("fade-result: passed");
        });
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var pending=(PlayerMobile)World.FindMobile((Serial)saved.Pending);
        Require(AssassinBuffs.State(owner)==saved.State && ClassCreation.Profiles.GetRank(owner.Serial,"assassin.fade")==20 && SkillMechanics.Rank(owner,CombatAbility.Fade)==21,"Saved captured/hard/effective ranks");
        foreach(uint serial in new[]{saved.Weapon,saved.Helm,saved.Shield}){Require(World.FindItem((Serial)serial)?.Parent==owner,"Saved real gear");}
        Require(NecromancerEffects.ManaBalance256(owner)==saved.Mana && owner.PhysicalResistance==saved.Physical && owner.FireResistance==saved.Fire && EliteAuras.Curses.Expires(owner.Serial)==saved.CurseUntil,"Saved resources/resistance/curse expiry");Consumers(owner);
        Require(ClassCombat.For(pending).PendingAbility==CombatAbility.None && AssassinBuffs.State(pending)==null && NecromancerEffects.ManaBalance256(pending)==saved.PendingMana,"Saved unpaid windup replayed/paid");
        Require(SkillBindings.Instance.Get(owner).Right==CombatAbility.Fade && SkillBindings.Instance.GetQuick(owner.Serial,DiabloClass.Assassin,6)==CombatAbility.Fade,"Saved bindings");
        var helm=World.FindItem((Serial)saved.Helm);owner.AddToBackpack(helm);ClassGear.Refresh(owner);Require(AssassinBuffs.State(owner)==saved.State,"Removed soft gear changed captured state");Require(owner.EquipItem(helm),"Saved helm re-equip");ClassGear.Refresh(owner);
        Replace(saved.State with{UntilUtc=TimedEffects.Now.AddMilliseconds(80)});Later(180,"fade-reload-result",()=>{Require(AssassinBuffs.State(owner)==null && !NamedSetVisuals.Faded(owner),"Reloaded expiry retained state");Replace(saved.State);ClassGear.Refresh(owner);Log.Information("fade-reload-result: passed");});
    }
    private static void Input()
    {
        var owner=Fresh();Train(owner,CombatAbility.Fade,1);Ready(owner);Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.Fade) && SkillBindings.Instance.AssignQuick(owner,6,CombatAbility.Fade),"Connected bindings");
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=owner;owner.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(owner);
        int before=NecromancerEffects.ManaBalance256(owner);long deadline=Core.TickCount+5000;bool started=false;
        void Clean(){owner.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Connected cast did not finish");var state=AssassinBuffs.State(owner);
                if(!started && ClassCombat.For(owner).PendingAbility==CombatAbility.Fade){started=true;Require(NecromancerEffects.ManaBalance256(owner)==before && state==null,"Natural unpaid SC windup");}
                if(state==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(started && state is{Rank:1,ElementResistance:19,PhysicalResistance:1,CurseResistance:47} && NecromancerEffects.ManaBalance256(owner)==before-2560,"Natural self buff/payment");Consumers(owner);Clean();Log.Information("fade-input-result: passed");
            }catch(Exception e){Clean();Log.Error(e,"fade-input-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("fade-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.Fade,65530,65530,0);Wait();});
    }
}
