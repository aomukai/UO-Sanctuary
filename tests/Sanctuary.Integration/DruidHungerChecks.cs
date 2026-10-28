// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
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

public static class DruidHungerChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidHungerChecks));
    private sealed record Saved(uint Owner,uint Target,DruidPlayerForm Form,DruidAttackCharge Charge,int Life,int Mana,int TargetLife);
    private sealed class Rolls(bool critical):Random{public override int Next(int maximum)=>critical?0:maximum-1;}
    private static bool _creating;private static int _scene;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-hunger-fixtures.json");
    public static void Configure()
    {
        Rates();
        Register("druid-form-basic-block-check",BasicBlock);Register("druid-hunger-model-check",Model);Register("druid-hunger-check",Check);Register("druid-hunger-reload-check",Reload);Register("druid-hunger-snapshot-check",Snapshot);
        foreach(string label in new[]{"hunger-right","hunger-quick"}){Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)),false);}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        bool Held(Mobile p)=>_creating||p.Name?.StartsWith("DruidHungerFixture",StringComparison.Ordinal)==true;
        Mobile.HitsRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action,bool controlled=true)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Hunger action/leech and saved inputs",_=>Core.LoopContext.Post(()=>
    {try{if(controlled){Controlled(action);}else{action();}Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent);
        foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,SkillMechanics.All.Single(x=>x.TalentId==prior).Ability);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train "+row.TalentId);}
    }
    private static PlayerMobile Fresh(CombatAbility form=CombatAbility.Werewolf)
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="DruidHungerFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);Train(p,CombatAbility.Hunger);Train(p,CombatAbility.Lycanthropy,20);
        FixtureSpace.Corridor(p,6040,12,2000+32*_scene++);
        var weapon=new Dagger();ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(p.EquipItem(weapon),"Hunger dagger");ClassGear.Refresh(p);
        Require(DruidPlayerForms.ApplyPrepared(p,form,TimedEffects.Now),"Hunger form");Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);p.Frozen=p.Paralyzed=false;ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;SkillHealth.SetBalance256(p,p.HitsMax*128-17);NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static BladeMissileTarget Target(PlayerMobile p)
    {
        var t=new BladeMissileTarget{Name="DruidHungerFixtureTarget",NoKillAwards=true,Frozen=true,CantWalk=true};
        t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}return t;
    }
    private static void Drop(PlayerMobile p){DruidPlayerForms.Clear(p);p.Corpse?.Delete();p.Delete();}
    private static void Controlled(Action action)
    {
        var hit=DruidHunger.HitRoll;var roll=DruidHunger.ElementRoll;var physical=DruidHunger.PhysicalRoll;var effects=DruidHunger.Effects;var basic=DruidFormAttacks.HitRoll;
        try{DruidHunger.HitRoll=()=>0;DruidHunger.ElementRoll=()=>0;DruidHunger.PhysicalRoll=_=>0;DruidHunger.Effects=new Rolls(false);DruidFormAttacks.HitRoll=()=>0;action();}
        finally{DruidHunger.HitRoll=hit;DruidHunger.ElementRoll=roll;DruidHunger.PhysicalRoll=physical;DruidHunger.Effects=effects;DruidFormAttacks.HitRoll=basic;}
    }
    private static void BasicBlock()
    {
        foreach(var form in new[]{CombatAbility.Werewolf,CombatAbility.Werebear})
        {
            var p=Fresh(form);Train(p,CombatAbility.FireClaws);Train(p,form==CombatAbility.Werewolf?CombatAbility.FeralRage:CombatAbility.Maul);
            var target=new GraveyardSkeleton{Name="DruidHungerFixtureBlocker",Frozen=true,CantWalk=true,NoKillAwards=true};
            target.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();target.SetHits(50000);target.Hits=target.HitsMax;
            var choice=SourceBlocking.ChoiceRoll;var block=SourceBlocking.BlockRoll;
            try
            {
                SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>0;
                Require(SourceBlocking.TryRate(target,out int rate,out _)&&rate>0,"Positive shield supplier missing");
                foreach(var ability in new[]{CombatAbility.BasicAttack,CombatAbility.Hunger,CombatAbility.FireClaws,form==CombatAbility.Werewolf?CombatAbility.FeralRage:CombatAbility.Maul})
                {
                    Ready(p);NecromancerEffects.SetMana256(p,0);int life=SkillHealth.Balance256(target),ownerLife=SkillHealth.Balance256(p);
                    Require(ClassCombat.Perform(p,ability,target,target.Location,Core.TickCount)&&ClassCombat.For(p).PendingAbility==CombatAbility.BasicAttack,"Expected ordinary Attack fallback");
                    Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&SkillHealth.Balance256(target)==life&&SkillHealth.Balance256(p)==ownerLife
                        &&NecromancerEffects.ManaBalance256(p)==0,"Transformed ordinary Attack ignored NPC shield: "+form+"/"+ability);
                    Ready(p);NecromancerEffects.SetMana256(p,0);SourceBlocking.BlockRoll=_=>99;life=SkillHealth.Balance256(target);
                    Require(ClassCombat.Perform(p,ability,target,target.Location,Core.TickCount)&&ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)
                        &&SkillHealth.Balance256(target)<life&&NecromancerEffects.ManaBalance256(p)==0,"Failed block did not permit ordinary damage");SourceBlocking.BlockRoll=_=>0;
                }
            }
            finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=block;target.Delete();Drop(p);}
        }
        Log.Information("Transformed Basic Attack:16shield/no-block impacts across both forms and Hunger/Fire Claws/Feral/Maul low-mana fallbacks pass");
    }
    private static void Model()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-hunger-source-probe.json")));int compared=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int rank=row[0].GetInt32();var definition=SkillMechanics.For(CombatAbility.Hunger);var values=new SkillValues(definition,key=>key==definition.SourceKey?rank:0,_=>0,99);
            int drain=50+150*Math.Min(100,110*rank/(rank+6))/100;
            Require(values.Mana256==768&&values.Formula("toht")==50+10*(rank-1)&&values.Eval("calc1")==-75&&values.Eval("calc2")==drain&&values.Eval("calc3")==drain,"Hunger formulas");
            if(row[9].GetInt32()!=0){Require(DruidHunger.ReducePhysical(row[1].GetInt32()*(row[2].GetInt32()!=0?2:1))==row[14].GetInt32(),"Hunger post-critical physical source mismatch");}compared++;
        }
        Require(compared==34560,"Incomplete Hunger matrix");
        using var clock=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-s3-clock-source-probe.json")));int clocks=0;
        foreach(var row in clock.RootElement.EnumerateArray())
        {
            var data=row[8];int human=data[0].GetInt32(),progress=data[8].GetInt32();var form=row[1].GetInt32()==2?CombatAbility.Werewolf:CombatAbility.Werebear;
            var wolfValues=new SkillValues(SkillMechanics.For(CombatAbility.Werewolf),key=>key=="Wearwolf"?row[6].GetInt32():0,_=>0,99);
            int skillRate=form==CombatAbility.Werewolf?DruidFormModel.Capture(wolfValues).AttackRate:0;
            var rate=DruidFormRates.Calculate(new(form,human,256,row[3].GetInt32(),row[4].GetInt32(),row[5].GetInt32(),skillRate,row[7].GetInt32()));
            var schedule=DruidFormAttacks.ScheduleS3(rate.Progress256);
            Require(rate.Progress256==progress&&schedule.EffectFrame==row[9][0][1].GetInt32()&&schedule.Frames==row[9][1][1].GetInt32(),"S3author/source/native clock mismatch");clocks++;
        }
        Require(clocks==28800,"Incomplete S3clock matrix");Log.Information("Hunger model:34560source cases and28800S3author/source/native schedules pass");
    }
    private static DruidHunger.Pending Begin(PlayerMobile p,BaseCreature target)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);
        Require(ClassCombat.Perform(p,CombatAbility.Hunger,target,target.Location,Core.TickCount),"Hunger start");var session=ClassCombat.For(p);var pending=session.HungerAttack;
        Require(pending!=null&&session.Timing==DruidFormAttacks.S3Timing(p)&&session.Animation==(byte)WeaponAnimation.Wrestle,"S3timing/pose");
        Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(target)==life,"Hunger rolled/debited during start");
        Require(!ClassCombat.ResolveAction(p,pending.EffectAt-1)&&SkillHealth.Balance256(target)==life,"Early Hunger release");return pending;
    }
    private static void Cases(CombatAbility form)
    {
        var p=Fresh(form);var target=Target(p);
        try
        {
            var amulet=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Highlord's Wrath").Id,99,new Random(173),true);
            Require(p.EquipItem(amulet),"Real critical/soft-rank gear");ClassGear.Refresh(p);
            var ordinary=GearEffects.Damage(p,0,target,physicalRoll:_=>0);
            var packet=GearEffects.PrepareWeaponImpact(p,target,0,new Rolls(true),ordinary,physicalAfterCritical:DruidHunger.ReducePhysical);
            Require(NamedElements.To256(packet.Parts.Physical)==DruidHunger.ReducePhysical(NamedElements.To256(ordinary.Physical*2)),"Post-critical reduction became additive ED");
            var typed=new GearDamage(10+1/256.0,11,7,5,2,Magic:3);var typedPacket=GearEffects.PrepareWeaponImpact(p,target,0,new Rolls(false),typed,physicalAfterCritical:DruidHunger.ReducePhysical);
            Require(typedPacket.Parts.Fire==11&&typedPacket.Parts.Cold==7&&typedPacket.Parts.Lightning==5&&typedPacket.Parts.Magic==3&&typedPacket.Damage.Poison==2,"Hunger reduced nonphysical components");
            var pending=Begin(p,target);DruidHunger.HitRoll=()=>1;int life=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&SkillHealth.Balance256(target)==life&&NecromancerEffects.ManaBalance256(p)==mana-768,"Hunger result was captured at start instead of release");DruidHunger.HitRoll=()=>0;
            var chargeAbility=form==CombatAbility.Werewolf?CombatAbility.FeralRage:CombatAbility.Maul;Require(DruidAttackCharges.Gain(p,SkillMechanics.Values(p,chargeAbility)),"Existing charge");
            var charge=DruidAttackCharges.State(p,chargeAbility);pending=Begin(p,target);
            NecromancerEffects.SetMana256(p,p.ManaMax*128-11);int ownLife=SkillHealth.Balance256(p);mana=NecromancerEffects.ManaBalance256(p);life=SkillHealth.Balance256(target);
            var values=SkillMechanics.Values(p,CombatAbility.Hunger);var raw=GearEffects.Damage(p,0,target,physicalRoll:_=>0);packet=GearEffects.PrepareWeaponImpact(p,target,0,new Rolls(false),raw,physicalAfterCritical:DruidHunger.ReducePhysical);
            var drains=WeaponLeech.Capture(p,new((int)values.Eval("calc2"),(int)values.Eval("calc3")));
            var recovered=WeaponLeech.Calculate(NamedElements.To256(packet.Parts.Physical),life,NecromancerEffects.ManaBalance256(target),drains.LifePercent,drains.ManaPercent,GemCombat.Drain(target),1,1);
            Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&life-SkillHealth.Balance256(target)==NamedElements.To256(packet.Parts.Total),"Hunger packet");
            Require(SkillHealth.Balance256(p)==Math.Min(p.HitsMax*256,ownLife+recovered.Life256)&&NecromancerEffects.ManaBalance256(p)==Math.Min(p.ManaMax*256,mana+recovered.Mana256)-768,"Dual physical drain/post-leech mana payment");
            Require(DruidAttackCharges.State(p,chargeAbility)==charge,"Hunger gained/consumed/refreshed charges");
            pending=Begin(p,target);Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&NecromancerEffects.ManaBalance256(p)==p.ManaMax*256-768,"Full mana banked bite leech against payment");
            target.SetResistance(ResistanceType.Physical,100);pending=Begin(p,target);ownLife=SkillHealth.Balance256(p);mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&SkillHealth.Balance256(p)==ownLife&&NecromancerEffects.ManaBalance256(p)==mana-768,"Physical immunity still leeched");target.SetResistance(ResistanceType.Physical,0);
            var blocker=new GraveyardSkeleton{Name="DruidHungerFixtureBlocker",Frozen=true,CantWalk=true,NoKillAwards=true};
            blocker.MoveToWorld(target.Location,p.Map);blocker.AIObject.AITimer.Stop();var choice=SourceBlocking.ChoiceRoll;var block=SourceBlocking.BlockRoll;
            try
            {
                SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>0;int blockerLife=SkillHealth.Balance256(blocker);
                pending=Begin(p,blocker);mana=NecromancerEffects.ManaBalance256(p);ownLife=SkillHealth.Balance256(p);
                Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&SkillHealth.Balance256(blocker)==blockerLife&&SkillHealth.Balance256(p)==ownLife
                    &&NecromancerEffects.ManaBalance256(p)==mana-768,"Blocked Hunger damaged or leeched");
            }
            finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=block;blocker.Delete();}
            pending=Begin(p,target);target.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&NecromancerEffects.ManaBalance256(p)==p.ManaMax*256,"Lost target paid or hit");target.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
            pending=Begin(p,target);var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);ClassMovement.Moved(p,Direction.South);p.MoveToWorld(origin,p.Map);Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&ClassCombat.For(p).HungerAttack==null,"Moved Hunger replay");
            pending=Begin(p,target);ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&NecromancerEffects.ManaBalance256(p)==p.ManaMax*256,"Cancelled Hunger paid before do");
            pending=Begin(p,target);var weapon=CombatTiming.Equipped(p);p.AddToBackpack(weapon);ClassGear.Refresh(p);Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&ClassCombat.For(p).HungerAttack==null,"Changed weapon kept Hunger");Require(p.EquipItem(weapon),"Reequip");ClassGear.Refresh(p);
            pending=Begin(p,target);NecromancerEffects.SetMana256(p,767);Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&NecromancerEffects.ManaBalance256(p)==767,"Lost-mana unsupported branch changed resources");
            Ready(p);NecromancerEffects.SetMana256(p,767);Require(ClassCombat.Perform(p,CombatAbility.Hunger,target,target.Location,Core.TickCount)&&ClassCombat.For(p).PendingAbility==CombatAbility.BasicAttack,"Low-mana ordinary Attack");Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.ManaBalance256(p)==767,"Low-mana debit");
            pending=Begin(p,target);DruidPlayerForms.Clear(p);Require(ClassCombat.For(p).HungerAttack==null&&!ClassCombat.ResolveAction(p,pending.EffectAt)&&!DruidCombat.Available(p,CombatAbility.Hunger),"Unshift retained Hunger");
            Log.Information("Hunger functional draft:{Form},release-time hit,post-critical physical reduction,retained typed damage,dual drain,payment,charges,immunity,cancellation and low-mana adapter pass",form);
        }
        finally{target.Delete();Drop(p);}
    }
    private static void Check()
    {
        Cases(CombatAbility.Werewolf);Cases(CombatAbility.Werebear);
        var p=Fresh();var target=Target(p);Train(p,CombatAbility.FeralRage);Require(DruidAttackCharges.Gain(p,SkillMechanics.Values(p,CombatAbility.FeralRage)),"Saved charge");var pending=Begin(p,target);Require(ClassCombat.ResolveAction(p,pending.EffectAt),"Saved bite");
        DruidAttackCharges.Restore(DruidAttackCharges.Snapshot().Select(x=>x.Owner==p.Serial.Value?x with{Charge=x.Charge with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}}:x).ToArray());
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Hunger)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Hunger),"Saved bindings");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,DruidPlayerForms.State(p),DruidAttackCharges.State(p,CombatAbility.FeralRage),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(target))));Snapshot();
    }
    private static void Snapshot()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=World.FindMobile((Serial)saved.Target);
        Require(DruidPlayerForms.State(p)==saved.Form&&DruidAttackCharges.State(p,CombatAbility.FeralRage)==saved.Charge&&SkillHealth.Balance256(p)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(target)==saved.TargetLife,"Saved Hunger changed: form="+(DruidPlayerForms.State(p)==saved.Form)+", charge="+(DruidAttackCharges.State(p,CombatAbility.FeralRage)==saved.Charge)+", ownerLife="+SkillHealth.Balance256(p)+"/"+saved.Life+", mana="+NecromancerEffects.ManaBalance256(p)+"/"+saved.Mana+", targetLife="+SkillHealth.Balance256(target)+"/"+saved.TargetLife);
    }
    private static void Reload()
    {
        Snapshot();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(ClassCombat.For(p).HungerAttack==null&&SkillBindings.Instance.Get(p).Right==CombatAbility.Hunger&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.Hunger,"Saved Hunger bindings/replayed packet");
    }
    private static void Input(NetState network,string label)
    {
        var p=Fresh(label.EndsWith("right",StringComparison.Ordinal)?CombatAbility.Werewolf:CombatAbility.Werebear);var target=Target(p);Ready(p);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Hunger)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Hunger),"Input bindings");
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(field!=null,"Input protocol");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        var hit=DruidHunger.HitRoll;DruidHunger.HitRoll=()=>0;bool began=false;long deadline=Core.TickCount+5000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Hunger input deadline");var session=ClassCombat.For(p);
                if(session.HungerAttack!=null&&!began){began=true;Require(NecromancerEffects.ManaBalance256(p)==mana,"Input start paid before action");Log.Information("{Label}: windup",label);}
                if(!began||SkillHealth.Balance256(target)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(NecromancerEffects.ManaBalance256(p)==mana-768&&session.HungerAttack==null,"Actual Hunger input payment/packet");DruidHunger.HitRoll=hit;target.Delete();Drop(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception error){DruidHunger.HitRoll=hit;target.Delete();Drop(p);Log.Error(error,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Hunger,(int)DiabloClass.Druid,target.Serial.Value,target.X,target.Y,target.Z);Inspect();});
    }
}
