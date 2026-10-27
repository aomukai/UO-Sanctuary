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

public static class DruidMaulChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidMaulChecks));
    private sealed class Rolls:Random{public override int Next(int maximum)=>Math.Max(0,maximum-1);}
    private sealed record Saved(uint Owner,uint Target,uint Weapon,DruidPlayerForm Form,DruidAttackCharge Charge,
        DateTime NaturalDeadline,int Mana,int Life,uint PendingOwner,int PendingMana,uint ExpiredOwner,DateTime ExpiredDeadline);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-maul-fixtures.json");
    private static bool _creating;private static int _scene;
    public static void Configure()
    {
        Rates();Register("druid-maul-check",Check);Register("druid-maul-reload-check",Reload);Register("druid-maul-snapshot-check",Snapshot);Register("druid-maul-expiry-stage-check",StageExpiry);Register("druid-maul-live-check",Live);
        foreach(string label in new[]{"maul-right","maul-quick"})
        {Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        bool Held(Mobile p)=>_creating||p.Name?.StartsWith("DruidMaulFixture",StringComparison.Ordinal)==true;
        Mobile.HitsRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned Maul start/contact/charge consumers and saved lifecycle",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent);
        foreach(string prior in talent.Prerequisites)
        {if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,SkillMechanics.All.Single(x=>x.TalentId==prior).Ability);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train "+row.TalentId);}
    }
    private static PlayerMobile Fresh(bool maul=true)
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="DruidMaulFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        foreach(var ability in new[]{CombatAbility.Werewolf,CombatAbility.Werebear,CombatAbility.Lycanthropy}){Train(p,ability);}
        Train(p,CombatAbility.Lycanthropy,20);
        if(maul){Train(p,CombatAbility.Maul);}
        FixtureSpace.Corridor(p,6080,12,1000+32*_scene++);Ready(p);
        var dagger=new Dagger();ClassGear.Profiles.Convert(dagger,1,GearRarity.Normal,new Random(173));Require(p.EquipItem(dagger),"Maul dagger");ClassGear.Refresh(p);
        Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werebear,TimedEffects.Now),"Maul bear entry");return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);p.Frozen=p.Paralyzed=false;ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static BladeMissileTarget Target(PlayerMobile p)
    {
        var t=new BladeMissileTarget{Name="DruidMaulFixtureTarget",NoKillAwards=true,Frozen=true,CantWalk=true};
        t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}
        return t;
    }
    private static void Drop(PlayerMobile p)
    {if(p==null){return;}DruidPlayerForms.Clear(p);DruidAttackCharges.Clear(p);p.Corpse?.Delete();p.Delete();}
    private static DruidAttackCharge State(PlayerMobile p)=>DruidAttackCharges.State(p,CombatAbility.Maul);
    private static void Hold(PlayerMobile p,DruidAttackCharge state)
    {DruidAttackCharges.Restore(DruidAttackCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value||x.Charge.Ability!=CombatAbility.Maul).Append(new OwnedDruidAttackCharge(p.Serial.Value,state)).ToArray());}
    private static DruidChargedAttacks.Pending Begin(PlayerMobile p,BaseCreature target)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);var charge=State(p);
        Require(ClassCombat.Perform(p,CombatAbility.Maul,target,target.Location,Core.TickCount),"Maul start");
        var session=ClassCombat.For(p);var pending=session.DruidChargeAttack;
        Require(pending!=null&&session.PendingAbility==CombatAbility.Maul&&session.Timing==DruidFormAttacks.Timing(p)
            &&session.Animation==(byte)WeaponAnimation.Wrestle,"Maul A1/form context");
        Require(State(p)==charge&&SkillHealth.Balance256(target)==life&&NecromancerEffects.ManaBalance256(p)==mana-768,"Start payment versus impact/gain order");
        Require(!ClassCombat.ResolveAction(p,pending.EffectAt-1)&&SkillHealth.Balance256(target)==life&&State(p)==charge,"Early Maul impact");return pending;
    }
    private static int Swing(PlayerMobile p,BaseCreature target)
    {
        var pending=Begin(p,target);int life=SkillHealth.Balance256(target);
        Require(ClassCombat.ResolveAction(p,pending.EffectAt),"Maul action event");int after=SkillHealth.Balance256(target);
        Require(life-after==pending.Impact.LifeDamage256.GetValueOrDefault(NamedElements.To256(pending.Impact.Parts.Total)),"Maul committed a different captured damage packet");
        var state=State(p);Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&State(p)==state&&SkillHealth.Balance256(target)==after,"Duplicate Maul event");return life-after;
    }
    private static void Combat()
    {
        var p=Fresh();var target=Target(p);var oldWear=ClassGearHooks.WearAllowed;
        try
        {
            Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Maul)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Maul)
                &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Maul),"Maul left/right/quick binding");
            int plain=ClassStats.DamageBonus(p,CombatTiming.Equipped(p)),wears=0,oldCount=0;
            ClassGearHooks.WearAllowed=item=>
            {if(item==CombatTiming.Equipped(p)){Require((State(p)?.Count??0)==oldCount,"Charge gained before actual weapon wear");wears++;}return oldWear?.Invoke(item)??true;};
            var pending=Begin(p,target);Require(pending.Hit&&pending.Impact.SourceStunFrames==0,"First Maul borrowed its future charge stun");
            DruidChargedAttacks.HitRoll=()=>1;int firstLife=SkillHealth.Balance256(target);
            Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&SkillHealth.Balance256(target)<firstLife&&wears==1,"Latched start hit rerolled at release");
            DruidChargedAttacks.HitRoll=()=>0;
            var first=State(p);Require(first is{Count:1,StoredRank:1,DamagePercent:30,AttackRate:3,OtherAnimationRate:3}
                &&first.StunFrames>0&&first.UntilUtc>TimedEffects.Now.AddSeconds(19)&&first.UntilUtc<=TimedEffects.Now.AddSeconds(20),"First charge payload/lifetime");
            Require(CombatStuns.Remaining(target)==0&&ClassStats.DamageBonus(p,CombatTiming.Equipped(p))==plain+30,"First hit stun or damage consumer");
            oldCount=1;pending=Begin(p,target);Require(pending.Impact.SourceStunFrames==first.StunFrames,"Existing charge stun was not captured");
            Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&State(p).Count==2&&CombatStuns.Remaining(target)>0,"Second Maul damage/stun/charge");
            oldCount=2;Swing(p,target);Require(State(p).Count==3,"Rank1cap3");oldCount=3;
            Hold(p,State(p) with{UntilUtc=TimedEffects.Now.AddSeconds(1)});Swing(p,target);
            Require(State(p).Count==3&&State(p).UntilUtc>TimedEffects.Now.AddSeconds(19)&&ClassStats.DamageBonus(p,CombatTiming.Equipped(p))==plain+90,"Cap refresh or stacked duplicate damage");
            var before=State(p);DruidChargedAttacks.HitRoll=()=>1;
            Require(Swing(p,target)==0&&State(p)==before&&wears==4,"Miss damaged, wore weapon or refreshed charge");DruidChargedAttacks.HitRoll=()=>0;
            var chargedClock=DruidFormAttacks.Timing(p);var packet=GearEffects.PrepareWeaponImpact(p,target,0,new Rolls(),GearEffects.Damage(p,0,target));
            Require(packet.SourceStunFrames==before.StunFrames,"Common weapon packet omitted charge stun");
            int life=SkillHealth.Balance256(target);GearEffects.Strike(p,target,0,new Rolls(),preparedImpact:packet);
            Require(State(p)==before&&SkillHealth.Balance256(target)<life&&CombatStuns.Remaining(target)>0,"Common weapon attack consumed Maul or missed its stun");
            DruidAttackCharges.Clear(p);Require(ClassStats.DamageBonus(p,CombatTiming.Equipped(p))==plain,"Cleared charge damage survived");
            Require(DruidFormAttacks.Timing(p).Frames>=chargedClock.Frames,"Maul IAS slowed A1");
            oldCount=0;DruidAttackCharges.Allocate=()=>false;life=SkillHealth.Balance256(target);Swing(p,target);
            Require(State(p)==null&&SkillHealth.Balance256(target)<life,"Charge allocation failure rolled back committed damage");DruidAttackCharges.Allocate=()=>true;
            target.SetResistance(ResistanceType.Physical,100);life=SkillHealth.Balance256(target);Swing(p,target);
            Require(State(p)?.Count==1&&SkillHealth.Balance256(target)==life,"Immune contact failed to gain or did physical damage");target.SetResistance(ResistanceType.Physical,0);
            oldCount=1;
            // Genuine source shield blocking is latched before the action event.
            var blocker=new GraveyardSkeleton{Name="DruidMaulFixtureBlocker",NoKillAwards=true,Frozen=true};
            blocker.MoveToWorld(target.Location,p.Map);blocker.AIObject.AITimer.Stop();var choice=SourceBlocking.ChoiceRoll;var block=SourceBlocking.BlockRoll;
            try
            {
                SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>0;
                var prior=State(p);life=SkillHealth.Balance256(blocker);pending=Begin(p,blocker);
                Require(pending.Blocked&&!pending.Hit,"Source shield block control");
                Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&State(p)==prior&&SkillHealth.Balance256(blocker)==life,"Blocked Maul gained/refreshed/damaged");
            }
            finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=block;blocker.Delete();}
            Ready(p);NecromancerEffects.SetMana256(p,767);var fallbackState=State(p);life=SkillHealth.Balance256(target);
            Require(ClassCombat.Perform(p,CombatAbility.Maul,target,target.Location,Core.TickCount)&&ClassCombat.For(p).PendingAbility==CombatAbility.BasicAttack,"Low mana did not become ordinary Attack");
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&State(p)==fallbackState&&NecromancerEffects.ManaBalance256(p)==767&&SkillHealth.Balance256(target)<life,"Fallback charged mana/gained charge/missed ordinary damage");
            Log.Information("Maul combat:start-latched hit,once-only3mana,existing-state damage/stun/IAS,cap/refresh,miss/block/immunity,weapon-wear-before-gain,allocation failure,ordinary attack and approved low-mana fallback pass");
        }
        finally{ClassGearHooks.WearAllowed=oldWear;target.Delete();Drop(p);DruidAttackCharges.Allocate=()=>true;}
    }
    private static void Lifecycle()
    {
        var p=Fresh();var target=Target(p);
        try
        {
            Swing(p,target);var before=State(p);int life=SkillHealth.Balance256(target);
            var pending=Begin(p,target);int paid=NecromancerEffects.ManaBalance256(p);ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&State(p)==before&&NecromancerEffects.ManaBalance256(p)==paid&&SkillHealth.Balance256(target)==life,"Stop replay/refund/charge loss");
            pending=Begin(p,target);var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);ClassMovement.Moved(p,Direction.South);p.MoveToWorld(origin,p.Map);
            Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&State(p)==before,"Out/back movement revived Maul");
            pending=Begin(p,target);target.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);
            Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&State(p)==before,"Lost melee contact gained charge (current engine uncertainty)");target.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
            pending=Begin(p,target);var weapon=CombatTiming.Equipped(p);p.AddToBackpack(weapon);ClassGear.Refresh(p);
            Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&State(p)==before,"Weapon change replayed Maul");Require(p.EquipItem(weapon),"Reequip");ClassGear.Refresh(p);
            pending=Begin(p,target);DruidPlayerForms.Clear(p);Require(State(p)==null&&ClassCombat.For(p).DruidChargeAttack==null,"Unshift retained matching-group charge/action");
            Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werebear,TimedEffects.Now)&&!ClassCombat.ResolveAction(p,pending.EffectAt),"New form replayed prior Maul");
            Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount)&&State(p)==null,"Unshift/reentry retained old concentration action guard");
            Ready(p);Require(!DruidCombat.Available(p,CombatAbility.FeralRage),"Unimplemented Feral handler enabled");
            // Actual soft-rank gear changes cap and stored level at do120, while
            // the previously allocated damage packet remains unchanged.
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(321),true);
            Require(p.EquipItem(helm),"Real +skill helm");ClassGear.Refresh(p);
            for(int i=0;i<4;i++){Swing(p,target);}Require(State(p) is{StoredRank:2,Count:4,DamagePercent:120},"Soft rank cap4");
            pending=Begin(p,target);p.AddToBackpack(helm);ClassGear.Refresh(p);
            Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&State(p) is{StoredRank:1,Count:3,DamagePercent:90},"Release current-rank downcap/metadata");helm.Delete();
            var rows=DruidAttackCharges.Snapshot();string json=JsonSerializer.Serialize(rows);
            foreach(var malformed in new[]{rows.Append(rows.Single(x=>x.Owner==p.Serial.Value)).ToArray(),
                rows.Select(x=>x.Owner==p.Serial.Value?x with{Charge=x.Charge with{DamagePercent=999}}:x).ToArray()})
            {
                bool refused=false;try{DruidAttackCharges.Restore(malformed);}catch(InvalidDataException){refused=true;}
                Require(refused&&JsonSerializer.Serialize(DruidAttackCharges.Snapshot())==json,"Malformed charge load was not atomic");
            }
            DruidAttackCharges.Prune(State(p).UntilUtc);Require(State(p)==null&&DruidPlayerForms.State(p)!=null,"Charge expiry removed form or kept charges");
            Swing(p,target);p.Kill();p.Resurrect();Require(State(p)==null,"Death/resurrection retained charge");
            var untrained=Fresh(false);
            try
            {
                Require(!DruidCombat.Available(untrained,CombatAbility.Maul),"Untrained Maul route");
                var gear=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(322),true);
                Require(untrained.EquipItem(gear),"Untrained +skill gear");ClassGear.Refresh(untrained);
                Require(!DruidCombat.Available(untrained,CombatAbility.Maul),"Item-only Maul route silently enabled");
            }
            finally{Drop(untrained);}
            DruidPlayerForms.Clear(p);Require(!DruidCombat.Available(p,CombatAbility.Maul),"Human Maul");
            Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werewolf,TimedEffects.Now)&&!DruidCombat.Available(p,CombatAbility.Maul),"Wolf Maul");
            DruidPlayerForms.Clear(p);Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werebear,TimedEffects.Now),"History bear");
            var history=Fresh();
            try
            {
                // The restricted history fixture requires no equipment or recent
                // combat. Build its state through the already-tested gain service.
                history.AddToBackpack(CombatTiming.Equipped(history));ClassGear.Refresh(history);Ready(history);history.Hits=history.HitsMax;
                Require(DruidAttackCharges.Gain(history,SkillMechanics.Values(history,CombatAbility.Maul)),"History charge fixture");
                Require(ClassCreation.Profiles.TryActivateHistory(history,DiabloClass.Paladin)&&State(history)==null
                    &&ClassCreation.Profiles.TryActivateHistory(history,DiabloClass.Druid)&&State(history)==null,"History resurrected Maul charges");
                Require(DruidAttackCharges.Gain(history,SkillMechanics.Values(history,CombatAbility.Maul)),"Respec charge fixture");
                ClassCreation.Profiles.TryGet(history.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(history,profile);
                Require(State(history)==null&&ClassCombat.For(history).DruidChargeAttack==null&&!DruidCombat.Available(history,CombatAbility.Maul),"Respec retained Maul charge/action");
            }
            finally{Drop(history);}
            Log.Information("Maul lifecycle:Stop,out/back,range loss,weapon change,unshift/new form,real gear downcap,atomic malformed restore,charge-only expiry,death,history,respec,human/wolf/untrained/item-only exclusions pass");
        }
        finally{target.Delete();Drop(p);}
    }
    private static void Controlled(Action action)
    {
        var hit=DruidChargedAttacks.HitRoll;var roll=DruidChargedAttacks.ElementRoll;var physical=DruidChargedAttacks.PhysicalRoll;var effects=DruidChargedAttacks.Effects;var basic=DruidFormAttacks.HitRoll;
        try{DruidChargedAttacks.HitRoll=()=>0;DruidChargedAttacks.ElementRoll=()=>0;DruidChargedAttacks.PhysicalRoll=_=>0;DruidChargedAttacks.Effects=new Rolls();DruidFormAttacks.HitRoll=()=>0;action();}
        finally{DruidChargedAttacks.HitRoll=hit;DruidChargedAttacks.ElementRoll=roll;DruidChargedAttacks.PhysicalRoll=physical;DruidChargedAttacks.Effects=effects;DruidFormAttacks.HitRoll=basic;}
    }
    private static void Check()=>Controlled(()=>
    {
        Combat();Lifecycle();var p=Fresh();var t=Target(p);Swing(p,t);Swing(p,t);var charge=State(p);var natural=charge.UntilUtc;
        Hold(p,charge with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)});charge=State(p);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Maul)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Maul),"Save bindings");
        var pending=Fresh();var pt=Target(pending);Begin(pending,pt);int paid=NecromancerEffects.ManaBalance256(pending);
        // Save an already-paid interrupted action; the transient packet is never serialized.
        ClassCombat.Stop(pending);pt.Delete();var expired=Fresh();var et=Target(expired);Swing(expired,et);et.Delete();
        var deadline=TimedEffects.Now.AddSeconds(2);Hold(expired,State(expired) with{UntilUtc=deadline});
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,CombatTiming.Equipped(p).Serial.Value,
            DruidPlayerForms.State(p),charge,natural,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),pending.Serial.Value,paid,expired.Serial.Value,deadline)));
        Snapshot();
    });
    private static void Snapshot()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=World.FindMobile((Serial)saved.Target);
        Require(State(p)==saved.Charge&&DruidPlayerForms.State(p)==saved.Form&&CombatTiming.Equipped(p)?.Serial.Value==saved.Weapon
            &&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(target)==saved.Life,"Saved charge/form/weapon/mana/life changed");
    }
    private static void StageExpiry()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.ExpiredOwner);
        var deadline=TimedEffects.Now.AddSeconds(2);Hold(p,DruidAttackChargeModel.Capture(CombatAbility.Maul,1,1,deadline));
        Require(State(p)?.UntilUtc==deadline&&deadline>TimedEffects.Now,"Pre-save expiry fixture must still be active");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{ExpiredDeadline=deadline}));
    }
    private static void Reload()
    {
        Snapshot();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var pending=(PlayerMobile)World.FindMobile((Serial)saved.PendingOwner);var expired=(PlayerMobile)World.FindMobile((Serial)saved.ExpiredOwner);
        Require(ClassCombat.For(pending).PendingAbility==CombatAbility.None&&ClassCombat.For(pending).DruidChargeAttack==null
            &&NecromancerEffects.ManaBalance256(pending)==saved.PendingMana&&State(pending)==null,"Paid windup replay/refund after reload");
        Require(saved.ExpiredDeadline<TimedEffects.Now&&State(expired)==null&&DruidPlayerForms.State(expired)!=null,"Offline expiry lost form or retained charge");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.Maul&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.Maul,"Saved Maul bindings");
        Require(ClassStats.DamageBonus(p,CombatTiming.Equipped(p))>=saved.Charge.DamagePercent&&DruidAttackCharges.AttackRate(p)==saved.Charge.AttackRate,"Restored charge consumers");
    }
    private static void Live()
    {
        Controlled(()=>
        {
            var p=Fresh();var t=Target(p);Swing(p,t);var charge=State(p);Hold(p,charge with{UntilUtc=TimedEffects.Now.AddMilliseconds(200)});t.Delete();
            Timer.StartTimer(TimeSpan.FromMilliseconds(500),()=>
            {try{Require(State(p)==null&&!DruidAttackCharges.Snapshot().Any(x=>x.Owner==p.Serial.Value)&&DruidPlayerForms.State(p)!=null,"Natural charge expiry did not clear only charge");Log.Information("druid-maul-live-result: passed");}
                catch(Exception error){Log.Error(error,"druid-maul-live-result: failed");}finally{Drop(p);}});
        });
    }
    private static void Input(NetState network,string label)
    {
        var p=Fresh();var target=Target(p);Ready(p);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Maul)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Maul),"Input binding");
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(field!=null,"Input protocol");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        var hit=DruidChargedAttacks.HitRoll;DruidChargedAttacks.HitRoll=()=>0;long deadline=Core.TickCount+5000;bool began=false;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Maul input deadline");var session=ClassCombat.For(p);
                if(session.DruidChargeAttack!=null&&!began){began=true;Log.Information("{Label}: windup",label);}
                if(State(p)==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&State(p).Count==1&&NecromancerEffects.ManaBalance256(p)==mana-768&&SkillHealth.Balance256(target)<life,"Actual Maul input charge/payment/hit");
                DruidChargedAttacks.HitRoll=hit;target.Delete();Drop(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception error){DruidChargedAttacks.HitRoll=hit;target.Delete();Drop(p);Log.Error(error,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Maul,(int)DiabloClass.Druid,target.Serial.Value,target.X,target.Y,target.Z);Inspect();});
    }
}
