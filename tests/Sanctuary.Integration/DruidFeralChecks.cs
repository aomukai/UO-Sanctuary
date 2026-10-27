// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DruidFeralChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidFeralChecks));
    private sealed class Rolls:Random{public override int Next(int maximum)=>Math.Max(0,maximum-1);}
    private sealed record Saved(uint Owner,uint Target,DruidAttackCharge Charge,DruidPlayerForm Form,int Life,int Mana,int TargetLife);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-feral-fixtures.json");
    private static bool _creating;private static int _scene;
    public static void Configure()
    {
        Rates();Register("druid-feral-check",Check);Register("druid-feral-reload-check",Reload);Register("druid-feral-snapshot-check",Snapshot);Register("druid-feral-live-check",Live);
        foreach(string label in new[]{"feral-right","feral-quick"}){Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)),controlled:false);}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        bool Held(Mobile p)=>_creating||p.Name?.StartsWith("DruidFeralFixture",StringComparison.Ordinal)==true;
        Mobile.HitsRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action,bool controlled=true)=>ConsoleInputHandler.RegisterCommand(name,"Feral actual charge velocity/leech consumers",_=>Core.LoopContext.Post(()=>
    {try{if(controlled){Controlled(action);}else{action();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent);
        foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,SkillMechanics.All.Single(x=>x.TalentId==prior).Ability);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train "+row.TalentId);}
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="DruidFeralFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        Train(p,CombatAbility.FeralRage);Train(p,CombatAbility.Lycanthropy,20);
        FixtureSpace.Corridor(p,6060,12,1000+32*_scene++);
        var dagger=new Dagger();ClassGear.Profiles.Convert(dagger,1,GearRarity.Normal,new Random(173));Require(p.EquipItem(dagger),"Feral dagger");ClassGear.Refresh(p);
        Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werewolf,TimedEffects.Now),"Feral wolf");Resources(p);return p;
    }
    private static void Resources(PlayerMobile p)
    {ClassCombat.Stop(p);p.Frozen=p.Paralyzed=false;ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;SkillHealth.SetBalance256(p,p.HitsMax*128-17);NecromancerEffects.SetMana256(p,p.ManaMax*128-17);}
    private static BaseCreature Target(PlayerMobile p,MonsterDifficulty difficulty=MonsterDifficulty.Normal)
    {
        BaseCreature t=difficulty==MonsterDifficulty.Normal?new BladeMissileTarget():new GraveyardZombie();
        // The production variant registry intentionally accepts mapped wild
        // concrete types, not our no-regeneration fixture subclass.
        if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(t,difficulty);}
        t.Name="DruidFeralFixtureTarget";t.NoKillAwards=true;t.Frozen=true;t.CantWalk=true;
        t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}
        return t;
    }
    private static DruidAttackCharge State(PlayerMobile p)=>DruidAttackCharges.State(p,CombatAbility.FeralRage);
    private static void Drop(PlayerMobile p){DruidPlayerForms.Clear(p);p.Corpse?.Delete();p.Delete();}
    private static void Hold(PlayerMobile p,DruidAttackCharge charge)=>DruidAttackCharges.Restore(DruidAttackCharges.Snapshot()
        .Where(x=>x.Owner!=p.Serial.Value||x.Charge.Ability!=CombatAbility.FeralRage).Append(new OwnedDruidAttackCharge(p.Serial.Value,charge)).ToArray());
    private static void Controlled(Action action)
    {
        var hit=DruidChargedAttacks.HitRoll;var roll=DruidChargedAttacks.ElementRoll;var physical=DruidChargedAttacks.PhysicalRoll;var effects=DruidChargedAttacks.Effects;
        try{DruidChargedAttacks.HitRoll=()=>0;DruidChargedAttacks.ElementRoll=()=>0;DruidChargedAttacks.PhysicalRoll=_=>0;DruidChargedAttacks.Effects=new Rolls();action();}
        finally{DruidChargedAttacks.HitRoll=hit;DruidChargedAttacks.ElementRoll=roll;DruidChargedAttacks.PhysicalRoll=physical;DruidChargedAttacks.Effects=effects;}
    }
    private static DruidChargedAttacks.Pending Begin(PlayerMobile p,BaseCreature target,bool expectedHit=true)
    {
        ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;
        var prior=State(p);int life=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p);
        Require(ClassCombat.Perform(p,CombatAbility.FeralRage,target,target.Location,Core.TickCount),"Feral start");
        var pending=ClassCombat.For(p).DruidChargeAttack;
        Require(pending!=null&&pending.Hit==expectedHit&&State(p)==prior&&SkillHealth.Balance256(target)==life&&NecromancerEffects.ManaBalance256(p)==mana-768,"Feral start hit/payment versus gain");
        Require(!ClassCombat.ResolveAction(p,pending.EffectAt-1),"Early Feral release");return pending;
    }
    private static void Contact(PlayerMobile p,BaseCreature target)
    {
        var pending=Begin(p,target);int life=SkillHealth.Balance256(p),mana=NecromancerEffects.ManaBalance256(p);
        int divisor=MonsterVariants.Difficulty(target) switch{MonsterDifficulty.Nightmare=>2,MonsterDifficulty.Hell=>3,_=>1};
        var expected=WeaponLeech.Calculate(NamedElements.To256(pending.Impact.Parts.Physical),SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(target),
            pending.Drain.LifePercent,pending.Drain.ManaPercent,GemCombat.Drain(target),divisor,divisor);
        Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&SkillHealth.Balance256(p)==life+expected.Life256
            &&NecromancerEffects.ManaBalance256(p)==mana+expected.Mana256,"Captured pre-gain Feral leech/difficulty delivery");
        var state=State(p);Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&State(p)==state,"Duplicate Feral event");
    }
    private static void Movement(PlayerMobile p)
    {
        int speed=DruidAttackCharges.Velocity(p);
        Require(ClassCombatHooks.RunningDelay(p,400)==EquipmentBalance.RunDelay(400,EquipmentBalance.RunPenalty(p),speed)
            &&ClassCombatHooks.WalkingDelay(p,400)==EquipmentBalance.RunDelay(400,0,speed),"Feral once-only shared run/walk speed");
    }
    private static void Check()
    {
        var p=Fresh();var target=Target(p);
        try
        {
            int plain=ClassStats.DamageBonus(p,CombatTiming.Equipped(p));Movement(p);
            Require(WeaponLeech.Capture(p)==default&&State(p)==null,"Fresh Feral leech");
            Contact(p,target);Require(State(p) is{Count:1,StoredRank:1,DamagePercent:0,LifeDrainMinimum:4,LifeDrainMaximum:4}
                &&State(p).VelocityPercent>0&&WeaponLeech.Capture(p).LifePercent==4,"First Feral charge channels");Movement(p);
            Require(ClassStats.DamageBonus(p,CombatTiming.Equipped(p))==plain&&State(p).UntilUtc>TimedEffects.Now.AddSeconds(19),"Feral attack ED leaked into persistent weapon damage or lifetime");
            Contact(p,target);Require(State(p).Count==2&&WeaponLeech.Capture(p).LifePercent==8,"Second Feral charge");Contact(p,target);Contact(p,target);
            Require(State(p).Count==3&&WeaponLeech.Capture(p).LifePercent==12,"Rank1Feral cap");Movement(p);
            var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Manald Heal").Id,99,new Random(321),true);
            Require(p.EquipItem(item),"Real mana-leech ring");ClassGear.Refresh(p);
            Require(WeaponLeech.Capture(p).ManaPercent==ClassGear.Bonus(p,"mana-steal%")&&WeaponLeech.Capture(p).LifePercent==12,"Feral replaced or doubled item drain channels");
            foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
            {var t=Target(p,difficulty);try{Resources(p);Contact(p,t);}finally{t.Delete();}}
            var saved=State(p);Resources(p);var raw=GearEffects.Damage(p,0,target);int before=SkillHealth.Balance256(p);
            var drain=WeaponLeech.Capture(p);var expected=WeaponLeech.Calculate(NamedElements.To256(raw.Physical),SkillHealth.Balance256(target),0,drain.LifePercent,drain.ManaPercent,100,1,1);
            GearEffects.Strike(p,target,0,new Rolls());Require(State(p)==saved&&SkillHealth.Balance256(p)==before+expected.Life256,"Ordinary weapon attack omitted or consumed Feral leech");
            var pending=Begin(p,target);ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&State(p)==saved,"Cancelled Feral gained/refreshed/discarded existing state");
            DruidPlayerForms.Clear(p);Require(State(p)==null&&DruidAttackCharges.Velocity(p)==0&&WeaponLeech.Capture(p).LifePercent==0&&!DruidCombat.Available(p,CombatAbility.FeralRage),"Unshift retained Feral charge consumers");
            Log.Information("Feral functional draft:pre-gain captured leech,rank1cap,attack-only ED,shared run/walk speed,real item additive mana drain,three difficulties,ordinary weapon hit and form cleanup pass");
        }
        finally{target.Delete();Drop(p);}
        Edges();var savedOwner=Fresh();var savedTarget=Target(savedOwner);Contact(savedOwner,savedTarget);Contact(savedOwner,savedTarget);
        Require(SkillBindings.Instance.Assign(savedOwner,false,CombatAbility.FeralRage)&&SkillBindings.Instance.AssignQuick(savedOwner,7,CombatAbility.FeralRage),"Saved Feral bindings");
        Hold(savedOwner,State(savedOwner) with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)});
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(savedOwner.Serial.Value,savedTarget.Serial.Value,State(savedOwner),DruidPlayerForms.State(savedOwner),
            SkillHealth.Balance256(savedOwner),NecromancerEffects.ManaBalance256(savedOwner),SkillHealth.Balance256(savedTarget))));Snapshot();
    }
    private static void Snapshot()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=World.FindMobile((Serial)saved.Target);
        Require(State(p)==saved.Charge&&DruidPlayerForms.State(p)==saved.Form&&SkillHealth.Balance256(p)==saved.Life
            &&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(target)==saved.TargetLife,"Feral saved charge/form/fractional pools");
    }
    private static void Reload()
    {
        Snapshot();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.FeralRage&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.FeralRage,"Restored Feral bindings");
        Require(DruidAttackCharges.Velocity(p)==saved.Charge.VelocityPercent&&WeaponLeech.Capture(p).LifePercent==saved.Charge.LifeDrainMinimum,"Restored Feral consumers");
    }
    private static void Edges()
    {
        var p=Fresh();var target=Target(p);var choice=SourceBlocking.ChoiceRoll;var block=SourceBlocking.BlockRoll;var basic=DruidFormAttacks.HitRoll;
        try
        {
            Contact(p,target);var before=State(p);int life=SkillHealth.Balance256(target),ownerLife=SkillHealth.Balance256(p);
            DruidChargedAttacks.HitRoll=()=>1;var pending=Begin(p,target,false);
            Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&State(p)==before&&SkillHealth.Balance256(target)==life&&SkillHealth.Balance256(p)==ownerLife,"Miss damaged/healed/gained/refreshed Feral");DruidChargedAttacks.HitRoll=()=>0;
            var skeleton=new GraveyardSkeleton{Name="DruidFeralFixtureTarget",Frozen=true,CantWalk=true,NoKillAwards=true};
            skeleton.MoveToWorld(target.Location,p.Map);skeleton.AIObject.AITimer.Stop();skeleton.SetHits(50000);skeleton.Hits=skeleton.HitsMax;
            try
            {
                SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>0;life=SkillHealth.Balance256(skeleton);pending=Begin(p,skeleton,false);
                Require(pending.Blocked&&ClassCombat.ResolveAction(p,pending.EffectAt)&&State(p)==before&&SkillHealth.Balance256(skeleton)==life&&SkillHealth.Balance256(p)==ownerLife,"Blocked Feral damage/heal/gain");
                SourceBlocking.BlockRoll=_=>99;Require(GemCombat.Drain(skeleton)==0,"Zero-drain species control");Contact(p,skeleton);Require(SkillHealth.Balance256(p)==ownerLife,"Zero-drain target healed Feral owner");
            }
            finally{skeleton.Delete();}
            target.SetResistance(ResistanceType.Physical,100);life=SkillHealth.Balance256(target);before=State(p);Contact(p,target);
            Require(State(p).Count==before.Count+1&&SkillHealth.Balance256(target)==life&&SkillHealth.Balance256(p)==ownerLife,"Immune contact gain/no-heal");target.SetResistance(ResistanceType.Physical,0);
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(322),true);
            Require(p.EquipItem(helm),"Feral soft-rank helm");ClassGear.Refresh(p);Contact(p,target);
            Require(State(p) is{Count:4,StoredRank:2,LifeDrainMinimum:16},"Feral soft-rank cap4");pending=Begin(p,target);p.AddToBackpack(helm);ClassGear.Refresh(p);
            Require(pending.Drain.LifePercent==16&&ClassCombat.ResolveAction(p,pending.EffectAt)&&State(p) is{Count:3,StoredRank:1,LifeDrainMinimum:12},"Feral pre-gain drain/current-rank downcap");helm.Delete();
            before=State(p);Resources(p);NecromancerEffects.SetMana256(p,767);DruidFormAttacks.HitRoll=()=>0;
            Require(ClassCombat.Perform(p,CombatAbility.FeralRage,target,target.Location,Core.TickCount)&&ClassCombat.For(p).PendingAbility==CombatAbility.BasicAttack,"Feral AttackNoMana fallback");
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&State(p)==before&&NecromancerEffects.ManaBalance256(p)==767,"Feral fallback gained charge or debited mana");
            Log.Information("Feral edge draft:miss/block/physical immunity,zero-drain species,real skill-gear cap/downcap and low-mana ordinary Attack pass");
        }
        finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=block;DruidFormAttacks.HitRoll=basic;DruidChargedAttacks.HitRoll=()=>0;target.Delete();Drop(p);}
    }
    private static void Live()
    {
        var p=Fresh();var target=Target(p);Contact(p,target);Hold(p,State(p) with{UntilUtc=TimedEffects.Now.AddMilliseconds(200)});target.Delete();
        Timer.StartTimer(TimeSpan.FromMilliseconds(500),()=>
        {
            try{Require(State(p)==null&&!DruidAttackCharges.Snapshot().Any(x=>x.Owner==p.Serial.Value)&&DruidAttackCharges.Velocity(p)==0&&WeaponLeech.Capture(p).LifePercent==0&&DruidPlayerForms.State(p)!=null,"Natural Feral expiry retained state/consumer or erased form");Log.Information("druid-feral-live-result: passed");}
            catch(Exception error){Log.Error(error,"druid-feral-live-result: failed");}finally{Drop(p);}
        });
    }
    private static void Input(NetState network,string label)
    {
        var p=Fresh();var target=Target(p);Resources(p);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target),ownerLife=SkillHealth.Balance256(p);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.FeralRage)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.FeralRage),"Input Feral bindings");
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(field!=null,"Feral input protocol");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        var hit=DruidChargedAttacks.HitRoll;DruidChargedAttacks.HitRoll=()=>0;long deadline=Core.TickCount+5000;bool began=false;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Feral input deadline");var session=ClassCombat.For(p);
                if(session.DruidChargeAttack!=null&&!began){began=true;Log.Information("{Label}: windup",label);}
                if(State(p)==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&State(p) is{Count:1,LifeDrainMinimum:4}&&NecromancerEffects.ManaBalance256(p)==mana-768
                    &&SkillHealth.Balance256(target)<life&&SkillHealth.Balance256(p)==ownerLife,"Actual Feral input charge/payment/first-no-leech");
                DruidChargedAttacks.HitRoll=hit;target.Delete();Drop(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception error){DruidChargedAttacks.HitRoll=hit;target.Delete();Drop(p);Log.Error(error,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.FeralRage,(int)DiabloClass.Druid,target.Serial.Value,target.X,target.Y,target.Z);Inspect();});
    }

}
