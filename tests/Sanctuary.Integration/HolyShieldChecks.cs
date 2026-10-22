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

public static class HolyShieldChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(HolyShieldChecks));
    private sealed record Saved(uint Owner,uint Shield,HolyShieldState State,Point3D Origin,int Map);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"holy-shield-checks.json");
    public static void Configure(){Register("holy-shield-check",Check);Register("holy-shield-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Holy Shield cast, block/defense state, source block rate and reload checks",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>
    {try{action();}catch(Exception e){_log.Error(e,"{Name}: failed",name);}});
    private static Item Shield(PlayerMobile owner)
    {
        var shield=new WoodenKiteShield();ClassGear.Profiles.Convert(shield,99,GearRarity.Normal,new Random(173));
        Require(owner.EquipItem(shield),"Holy Shield real shield equipment");return shield;
    }
    private static PlayerMobile Owner()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.AddItem(new Backpack());owner.RawStr=1000;owner.RawDex=75;
        owner.MoveToWorld(ClassSkillChecks.Corridor(owner,5720,1800),owner.Map);TimedEffects.Poison(owner,owner,.000001,60000,TimedEffects.Now);return owner;
    }
    private static void Replace(HolyShieldState state)=>HolyShields.Restore(HolyShields.Snapshot().Where(x=>x.Owner!=state.Owner).Append(state).ToArray());
    private static void Cast(PlayerMobile owner)
    {
        var session=ClassCombat.For(owner);session.NextAction=session.RecoveryUntil=Core.TickCount;
        Require(ClassCombat.ContinueCast(owner,CombatAbility.HolyShield,null,owner.Location,Core.TickCount),"Right-slot held Holy Shield cast");ClassCombat.ReleaseCast(owner);
        Require(ClassCombat.ResolveAction(owner,session.EffectAt),"Holy Shield cast event");
    }
    private static void Lifecycle()
    {
        var actor=Owner();var shield=Shield(actor);HolyShields.Cast(actor,SkillMechanics.Values(actor,CombatAbility.HolyShield));actor.Kill();actor.Resurrect();
        Require(HolyShields.State(actor)==null,"Death retained Holy Shield");actor.Corpse?.Delete();actor.Delete();shield.Delete();
        actor=Owner();shield=Shield(actor);HolyShields.Cast(actor,SkillMechanics.Values(actor,CombatAbility.HolyShield));
        ClassCreation.Profiles.TryGet(actor.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(actor,profile);
        Require(HolyShields.State(actor)==null && shield.Parent==actor.Backpack,"Respec retained Holy Shield or lost real gear");actor.Delete();
    }
    private static void Consumers(PlayerMobile owner,Item shield,int rank,int block,int defense)
    {
        int raw=(ClassGear.Base(shield)?.Block ?? 0)+30+ClassGear.Bonus(owner,"block");
        int expected=Math.Clamp((raw+block)*Math.Max(0,owner.Dex-15)/(2*ClassStats.Level(owner)),0,75);
        Require(HolyShields.State(owner)?.Rank==rank && HolyShields.BlockBonus(owner)==block && ClassGear.BlockChance(owner)==expected
            && HolyShields.DefensePercent(owner)==defense,"Holy Shield effective block/rank/defense");
        var saved=HolyShields.State(owner);HolyShields.Clear(owner);int baseline=ClassStats.Defense(owner);Replace(saved);
        Require(ClassStats.Defense(owner)==ClassStats.ScaleDefense(baseline,defense),"Holy Shield defense did not affect whole defense pool");
        owner.Backpack.DropItem(shield);
        Require(HolyShields.State(owner)==saved && HolyShields.DefensePercent(owner)==0 && HolyShields.BlockBonus(owner)==0
            && ClassGear.BlockChance(owner)==0 && HolyShields.SmiteBonus(owner)==(0,0),"Unequipped shield retained effects or destroyed the timed buff");
        Require(owner.EquipItem(shield) && HolyShields.State(owner)==saved && HolyShields.DefensePercent(owner)==defense,"Re-equipped shield did not resume saved buff");
    }
    private static void Blocking(PlayerMobile owner)
    {
        Require(HitRecovery.BlockFrames(DiabloClass.Paladin,0,holyShield:true)==2
            && HitRecovery.BlockFrames(DiabloClass.Paladin,85,holyShield:true)==2 && HitRecovery.BlockFrames(DiabloClass.Paladin,86,holyShield:true)==1
            && HitRecovery.BlockFrames(DiabloClass.Paladin,0)==5,"Holy Shield source block animation rate/breakpoint");
        var session=ClassCombat.For(owner);session.RecoveryUntil=Core.TickCount;session.PendingAbility=CombatAbility.HolyShield;session.NextAction=Core.TickCount+1000;
        long until=session.RecoveryUntil;HitRecovery.Block(owner,Core.TickCount);
        Require(session.PendingAbility==CombatAbility.HolyShield && session.RecoveryUntil==until,"Released2.4 active skill was interrupted by block");
        session.PendingAbility=CombatAbility.None;session.NextAction=Core.TickCount;session.Animation=0;
        int dex=owner.RawDex;owner.RawDex=10000;Require(ClassGear.BlockChance(owner)==75,"Holy Shield exceeded/lost 75 block cap");
        var enemy=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);enemy.AIObject.AITimer.Stop();
        for(int i=0;i<4096 && session.Animation!=20;i++){ClassStats.CheckHit(enemy,owner);}
        Require(session.Animation==20 && session.Timing.Frames==2 && session.RecoveryUntil-Core.TickCount<=80,"Native class hit check did not enter source two-frame block");
        enemy.Delete();owner.RawDex=dex;session.RecoveryUntil=session.NextAction=Core.TickCount;
    }
    private static void Check()
    {
        Lifecycle();var owner=Owner();var values=SkillMechanics.Values(owner,CombatAbility.HolyShield);
        var max=new SkillValues(SkillMechanics.For(CombatAbility.HolyShield),_=>20,_=>20,99);
        Require(values.Mana256==8960 && values.Eval("auralencalc")==750 && values.Eval("aurastatcalc1")==14 && values.Eval("calc1")==40
            && max.Eval("auralencalc")==12625 && max.Eval("aurastatcalc1")==35 && max.Eval("calc1")==610,"Pinned Holy Shield values");
        Require(ClassAbilities.SelfTarget(CombatAbility.HolyShield) && !PaladinCombat.IsAura(CombatAbility.HolyShield)
            && !SkillBindings.Instance.Assign(owner,true,CombatAbility.HolyShield) && SkillBindings.Instance.Assign(owner,false,CombatAbility.HolyShield),"Holy Shield right-only/self/non-aura binding");
        var session=ClassCombat.For(owner);session.NextAction=Core.TickCount;
        Require(!ClassCombat.Perform(owner,CombatAbility.HolyShield,null,owner.Location,Core.TickCount),"Holy Shield cast without shield");var shield=Shield(owner);
        owner.Mana=0;NecromancerEffects.Profiles.Store(owner,new());Require(!ClassCombat.Perform(owner,CombatAbility.HolyShield,null,owner.Location,Core.TickCount),"Unpaid Holy Shield cast");
        owner.Mana=owner.ManaMax;session.NextAction=Core.TickCount;int mana=NecromancerEffects.ManaBalance256(owner);
        Require(ClassCombat.ContinueCast(owner,CombatAbility.HolyShield,null,owner.Location,Core.TickCount),"Holy Shield begin");ClassCombat.ReleaseCast(owner);
        Require(NecromancerEffects.ManaBalance256(owner)==mana,"Holy Shield paid before event");owner.Backpack.DropItem(shield);
        Require(!ClassCombat.ResolveAction(owner,session.EffectAt) && NecromancerEffects.ManaBalance256(owner)==mana && HolyShields.State(owner)==null,"Shield removal before event still paid/applied");
        Require(owner.EquipItem(shield),"Re-equip cast shield");Cast(owner);
        Require(NecromancerEffects.ManaBalance256(owner)==mana-8960 && HolyShields.State(owner) is{Rank:1,BlockBonus:14}
            && Math.Abs((HolyShields.State(owner).UntilUtc-TimedEffects.Now).TotalSeconds-30)<1,"Holy Shield cast-time cost/duration/rank snapshot");
        Consumers(owner,shield,1,14,40);Require(HolyShields.SmiteBonus(owner)==(768,1536),"Rank1 physical Smite bonus primitive");
        var helmet=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
        Require(owner.EquipItem(helmet) && SkillMechanics.Rank(owner,CombatAbility.HolyShield)==2 && HolyShields.State(owner).Rank==1
            && HolyShields.BlockBonus(owner)==14,"Soft rank changed an existing Holy Shield snapshot");helmet.Delete();
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.defiance",out _) && HolyShields.DefensePercent(owner)==55,"Stored cast rank failed current hard Defiance synergy");
        HolyShields.Cast(owner,SkillMechanics.Values(owner,CombatAbility.HolyShield,20));
        Require(HolyShields.SmiteBonus(owner)==(14592,15360) && HolyShields.BlockBonus(owner)==35 && HolyShields.DefensePercent(owner)==340
            && Math.Abs((HolyShields.State(owner).UntilUtc-TimedEffects.Now).TotalSeconds-505)<1,"Rank20 Holy Shield physical/block/defense/duration");
        Cast(owner);Require(HolyShields.State(owner).Rank==1 && HolyShields.DefensePercent(owner)==55,"Lower recast did not replace old defensive state");Blocking(owner);
        var state=HolyShields.State(owner);Replace(state with{UntilUtc=TimedEffects.Now.AddMilliseconds(80)});
        Later(160,"holy-shield-result",()=>
        {
            Require(HolyShields.State(owner)==null && HolyShields.DefensePercent(owner)==0 && HolyShields.BlockBonus(owner)==0,"Expired Holy Shield retained effects");
            Cast(owner);state=HolyShields.State(owner) with{UntilUtc=TimedEffects.Now.AddMinutes(5)};Replace(state);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,shield.Serial.Value,state,owner.Location,owner.Map.MapID)));_log.Information("holy-shield-result: passed");
        });
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);owner.RawStr=1000;owner.RawDex=75;var shield=World.FindItem((Serial)saved.Shield);
        Require(HolyShields.State(owner)==saved.State && shield.Parent==owner && HolyShields.Shield(owner)==shield,"Holy Shield state/real shield identity lost on reload");
        Consumers(owner,shield,1,14,55);Blocking(owner);
        var at=TimedEffects.Now;Replace(saved.State with{UntilUtc=at.AddMilliseconds(80)});
        Later(160,"holy-shield-reload-result",()=>
        {
            Require(HolyShields.State(owner)==null && HolyShields.BlockBonus(owner)==0 && HolyShields.DefensePercent(owner)==0,"Reloaded absolute expiry retained effect");
            Replace(saved.State);_log.Information("holy-shield-reload-result: passed");
        });
    }
}
