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

public static class DruidRabiesChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidRabiesChecks));
    private sealed record Saved(uint Owner,uint Target,DruidPlayerForm Form,DruidAttackCharge Charge,int Life,int Mana,int TargetLife,RabiesInfectionSave Infections,TimedEffectState Poison);
    private sealed class Rolls(bool critical=false):Random{public override int Next(int maximum)=>critical?0:maximum-1;}
    private static bool _creating;private static int _scene;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-rabies-fixtures.json");
    public static void Configure()
    {
        Rates();Register("druid-rabies-check",Check);Register("druid-rabies-reload-check",Reload);Register("druid-rabies-snapshot-check",Snapshot);
        foreach(string label in new[]{"rabies-right","rabies-quick"}){Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)),false);}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        bool Held(Mobile p)=>_creating||p.Name?.StartsWith("DruidRabiesFixture",StringComparison.Ordinal)==true;
        Mobile.HitsRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action,bool controlled=true)=>ConsoleInputHandler.RegisterCommand(name,"Learned Rabies start gate, distinct ordinary/poison packets and persistence",_=>Core.LoopContext.Post(()=>
    {try{if(controlled){Controlled(action);}else{action();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent),"Talent");
        foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,SkillMechanics.All.Single(x=>x.TalentId==prior).Ability);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train "+row.TalentId);}
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="DruidRabiesFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);Train(p,CombatAbility.Rabies);Train(p,CombatAbility.Lycanthropy,20);
        FixtureSpace.Corridor(p,6000,12,1000+32*_scene++);var weapon=new Dagger();ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(p.EquipItem(weapon),"Dagger");ClassGear.Refresh(p);
        Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werewolf,TimedEffects.Now),"Werewolf");Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);p.Frozen=p.Paralyzed=false;ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;SkillHealth.SetBalance256(p,p.HitsMax*128-17);NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static BladeMissileTarget Target(PlayerMobile p)
    {
        var t=new BladeMissileTarget{Name="DruidRabiesFixtureTarget",NoKillAwards=true,Frozen=true,CantWalk=true};t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}return t;
    }
    private static TimedEffectState Poison(Mobile target)=>TimedEffects.Snapshot().Single(x=>x.Owner==target.Serial.Value);
    private static void Drop(PlayerMobile p){DruidPlayerForms.Clear(p);p.Corpse?.Delete();p.Delete();}
    private static void Controlled(Action action)
    {
        var hit=DruidRabies.HitRoll;var element=DruidRabies.ElementRoll;var physical=DruidRabies.PhysicalRoll;var poison=DruidRabies.PoisonRoll;var effects=DruidRabies.Effects;var basic=DruidFormAttacks.HitRoll;
        try{DruidRabies.HitRoll=()=>0;DruidRabies.ElementRoll=()=>0;DruidRabies.PhysicalRoll=_=>0;DruidRabies.PoisonRoll=_=>0;DruidRabies.Effects=new Rolls();DruidFormAttacks.HitRoll=()=>0;action();}
        finally{DruidRabies.HitRoll=hit;DruidRabies.ElementRoll=element;DruidRabies.PhysicalRoll=physical;DruidRabies.PoisonRoll=poison;DruidRabies.Effects=effects;DruidFormAttacks.HitRoll=basic;}
    }
    private static DruidRabies.Pending Begin(PlayerMobile p,BaseCreature target)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);
        Require(ClassCombat.Perform(p,CombatAbility.Rabies,target,target.Location,Core.TickCount),"Rabies start");var session=ClassCombat.For(p);var pending=session.RabiesAttack;
        Require(pending!=null&&session.Timing==DruidFormAttacks.S3Timing(p)&&session.Animation==(byte)WeaponAnimation.Wrestle,"Rabies S3pose/clock");
        Require(NecromancerEffects.ManaBalance256(p)==mana-2560&&SkillHealth.Balance256(target)==life,"Rabies start payment or early damage");
        Require(!ClassCombat.ResolveAction(p,pending.EffectAt-1),"Early Rabies contact");return pending;
    }
    private static void Cases()
    {
        var original=DruidRabiesInfections.Snapshot();var p=Fresh();var target=Target(p);
        try
        {
            Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Rabies)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Rabies)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Rabies),"Learned Rabies bindings");
            Require(DruidAttackCharges.Gain(p,SkillMechanics.Values(p,CombatAbility.FeralRage)),"Existing Feral charge");var charge=DruidAttackCharges.State(p,CombatAbility.FeralRage);
            var pending=Begin(p,target);int before=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p);
            var raw=GearEffects.Damage(p,0,target,physicalRoll:_=>0);var packet=GearEffects.PrepareWeaponImpact(p,target,0,new Rolls(),raw);int expected=packet.LifeDamage256??NamedElements.To256(packet.Parts.Total);
            DruidRabies.PoisonRoll=_=>{Require(DruidRabiesInfections.Infection(target)!=null&&before-SkillHealth.Balance256(target)==expected,"Infection/poison roll preceded ordinary weapon commit");return 0;};
            Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&before-SkillHealth.Balance256(target)==expected,"Ordinary bite packet");DruidRabies.PoisonRoll=_=>0;
            Require(Poison(target).PoisonClock is{Rate256:48}&&Poison(target).PoisonMs==4000&&DruidRabiesInfections.Infection(target).Rank==1,"Separate rank1poison packet");
            Require(NecromancerEffects.ManaBalance256(p)==mana&&DruidAttackCharges.State(p,CombatAbility.FeralRage)==charge&&!ClassCombat.ResolveAction(p,pending.EffectAt),"Release repaid mana,changed charge or replayed");
            var marker=DruidRabiesInfections.Infection(target);Train(p,CombatAbility.Rabies,3);pending=Begin(p,target);DruidRabies.HitRoll=()=>1;before=SkillHealth.Balance256(target);
            Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&SkillHealth.Balance256(target)==before&&Poison(target).PoisonClock is{Rate256:112}
                &&Poison(target).PoisonMs==4800&&DruidRabiesInfections.Infection(target)==marker,"Ordinary miss suppressed poison or repeat bite refreshed marker");DruidRabies.HitRoll=()=>0;
            TimedEffects.CleansePoison(target,0,TimedEffects.Now);Require(DruidRabiesInfections.Infection(target)==marker,"Cure removed marker");
            target.SetResistance(ResistanceType.Poison,100);pending=Begin(p,target);Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&Poison(target).PoisonMs==0&&DruidRabiesInfections.Infection(target)==marker,"Poison immunity removed marker or took poison");target.SetResistance(ResistanceType.Poison,0);
            Ready(p);DruidRabies.HitRoll=()=>1;mana=NecromancerEffects.ManaBalance256(p);before=SkillHealth.Balance256(target);
            Require(!ClassCombat.Perform(p,CombatAbility.Rabies,target,target.Location,Core.TickCount)&&ClassCombat.For(p).RabiesAttack==null
                &&NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(target)==before,"Failed initial source gate paid or attacked");DruidRabies.HitRoll=()=>0;
            var blocker=new GraveyardSkeleton{Name="DruidRabiesFixtureBlocker",NoKillAwards=true,Frozen=true,CantWalk=true};blocker.MoveToWorld(target.Location,p.Map);blocker.AIObject.AITimer.Stop();blocker.SetHits(50000);blocker.Hits=blocker.HitsMax;
            var choice=SourceBlocking.ChoiceRoll;var block=SourceBlocking.BlockRoll;
            try
            {
                SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>99;pending=Begin(p,blocker);SourceBlocking.BlockRoll=_=>0;before=SkillHealth.Balance256(blocker);
                Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&SkillHealth.Balance256(blocker)==before&&Poison(blocker).PoisonMs>0,"Ordinary shield block suppressed forced-success poison");
                Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.Rabies,blocker,blocker.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana,"Initial shield block paid");
            }
            finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=block;blocker.Delete();}
            pending=Begin(p,target);target.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);mana=NecromancerEffects.ManaBalance256(p);
            Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana&&ClassCombat.For(p).RabiesFault?.Contains("Lost-contact")==true,"Unverified remote attachment was not flagged");target.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
            pending=Begin(p,target);ClassCombat.Stop(p);mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&ClassCombat.For(p).RabiesAttack==null&&NecromancerEffects.ManaBalance256(p)==mana,"Stop replay/refund");
            pending=Begin(p,target);var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);ClassMovement.Moved(p,Direction.South);p.MoveToWorld(origin,p.Map);Require(ClassCombat.For(p).RabiesAttack==null&&!ClassCombat.ResolveAction(p,pending.EffectAt),"Movement retained Rabies");
            pending=Begin(p,target);var weapon=CombatTiming.Equipped(p);p.AddToBackpack(weapon);ClassGear.Refresh(p);Require(ClassCombat.For(p).RabiesAttack==null&&!ClassCombat.ResolveAction(p,pending.EffectAt),"Weapon change retained Rabies");Require(p.EquipItem(weapon),"Reequip");ClassGear.Refresh(p);
            pending=Begin(p,target);NecromancerEffects.SetMana256(p,0);Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&NecromancerEffects.ManaBalance256(p)==0,"Already paid start required a second mana payment");
            Ready(p);NecromancerEffects.SetMana256(p,2559);Require(ClassCombat.Perform(p,CombatAbility.Rabies,target,target.Location,Core.TickCount)&&ClassCombat.For(p).PendingAbility==CombatAbility.BasicAttack
                &&ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.ManaBalance256(p)==2559,"Approved low-mana ordinary Attack fallback");
            while(p.RawStr<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Bow strength");}
            var dagger=CombatTiming.Equipped(p);p.AddToBackpack(dagger);var bow=new Bow();ClassGear.Profiles.Convert(bow,1,GearRarity.Normal,new Random(173));Require(p.EquipItem(bow),"Form bow");ClassGear.Refresh(p);
            Require(CombatTiming.Range(p,CombatAbility.Rabies)==DruidFormAttacks.Reach(p)&&CombatTiming.Range(p,CombatAbility.Hunger)==DruidFormAttacks.Reach(p)&&CombatTiming.Range(p,CombatAbility.Rabies)==1,"S3bow inherited projectile reach");
            pending=Begin(p,target);Require(ClassCombat.ResolveAction(p,pending.EffectAt),"Transformed bow Rabies melee");
            pending=Begin(p,target);DruidPlayerForms.Clear(p);Require(ClassCombat.For(p).RabiesAttack==null&&!ClassCombat.ResolveAction(p,pending.EffectAt)&&!DruidCombat.Available(p,CombatAbility.Rabies),"Human Rabies allowed");
            Train(p,CombatAbility.Werebear);Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werebear,TimedEffects.Now)&&!DruidCombat.Available(p,CombatAbility.Rabies),"Bear Rabies allowed");
            Log.Information("Rabies learned cases: two hit stages,two packets,10mana,start miss/block,ordinary miss/block,poison immunity/cure,no-refresh,charges,S3,cancellation,low-mana and explicit lost-contact gap pass");
        }
        finally{DruidRabiesInfections.Restore(original);target.Delete();Drop(p);}
    }
    private static void Check()
    {
        Cases();var p=Fresh();var target=Target(p);Require(DruidAttackCharges.Gain(p,SkillMechanics.Values(p,CombatAbility.FeralRage)),"Saved Feral charge");
        var pending=Begin(p,target);Require(ClassCombat.ResolveAction(p,pending.EffectAt),"Saved learned bite");var shift=FixtureSpace.Retention;
        var pool=DruidRabiesInfections.Snapshot();DruidRabiesInfections.Restore(pool with
        {
            Infections=pool.Infections.Select(x=>x.Caster==p.Serial.Value?x with{StartedUtc=x.StartedUtc.Add(shift),UntilUtc=x.UntilUtc.Add(shift),NextUtc=x.NextUtc.Add(shift)}:x).ToArray(),
            Markers=pool.Markers.Select(x=>x.Caster==p.Serial.Value?x with{StartedUtc=x.StartedUtc.Add(shift),UntilUtc=x.UntilUtc.Add(shift)}:x).ToArray()
        });
        var poison=Poison(target);poison=poison with{UpdatedUtc=TimedEffects.Now.Add(shift)};TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=target.Serial.Value).Append(poison).ToArray());
        DruidAttackCharges.Restore(DruidAttackCharges.Snapshot().Select(x=>x.Owner==p.Serial.Value?x with{Charge=x.Charge with{UntilUtc=TimedEffects.Now.Add(shift)}}:x).ToArray());
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Rabies)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Rabies),"Saved bindings");
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,target.Serial.Value,DruidPlayerForms.State(p),DruidAttackCharges.State(p,CombatAbility.FeralRage),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(target),DruidRabiesInfections.Snapshot(),poison)));Snapshot();
    }
    private static void Snapshot()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=World.FindMobile((Serial)saved.Target);
        Require(DruidPlayerForms.State(p)==saved.Form&&DruidAttackCharges.State(p,CombatAbility.FeralRage)==saved.Charge&&SkillHealth.Balance256(p)==saved.Life
            &&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(target)==saved.TargetLife&&Json(Poison(target))==Json(saved.Poison)
            &&Json(DruidRabiesInfections.Snapshot())==Json(saved.Infections),"Saved learned Rabies state changed");
    }
    private static void Reload()
    {
        Snapshot();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(ClassCombat.For(p).RabiesAttack==null&&SkillBindings.Instance.Get(p).Right==CombatAbility.Rabies&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.Rabies,"Saved bindings or replayed bite");
    }
    private static void Input(NetState network,string label)
    {
        var original=DruidRabiesInfections.Snapshot();var p=Fresh();var target=Target(p);Ready(p);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Rabies)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Rabies),"Input bindings");
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(field!=null,"Protocol");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        var hit=DruidRabies.HitRoll;DruidRabies.HitRoll=()=>0;bool began=false;long deadline=Core.TickCount+5000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Rabies input deadline");var session=ClassCombat.For(p);
                if(session.RabiesAttack!=null&&!began){began=true;Require(NecromancerEffects.ManaBalance256(p)==mana-2560,"Input start payment");Log.Information("{Label}: windup",label);}
                if(!began||DruidRabiesInfections.Infection(target)==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(SkillHealth.Balance256(target)<life&&Poison(target).PoisonMs>0&&NecromancerEffects.ManaBalance256(p)==mana-2560&&session.RabiesAttack==null,"Actual Rabies input packets/payment");
                DruidRabies.HitRoll=hit;DruidRabiesInfections.Restore(original);target.Delete();Drop(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){DruidRabies.HitRoll=hit;DruidRabiesInfections.Restore(original);target.Delete();Drop(p);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Rabies,(int)DiabloClass.Druid,target.Serial.Value,target.X,target.Y,target.Z);Inspect();});
    }
}
