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

public static class DruidFireClawsChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidFireClawsChecks));
    private sealed record Saved(uint Owner,uint Target,DruidPlayerForm Form,DruidAttackCharge Charge,int Life,int Mana,int TargetLife);
    private sealed class Rolls(bool critical,List<string> trace=null):Random
    {public override int Next(int maximum){trace?.Add("critical");return critical?0:maximum-1;}}
    private static bool _creating;private static int _scene;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-fire-claws-fixtures.json");
    public static void Configure()
    {
        Rates();Register("druid-fire-claws-scalars-check",Scalars);Register("druid-fire-claws-check",Check);Register("druid-fire-claws-reload-check",Reload);Register("druid-fire-claws-snapshot-check",Snapshot);
        foreach(string label in new[]{"fire-claws-right","fire-claws-quick"}){Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)),controlled:false);}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        bool Held(Mobile p)=>_creating||p.Name?.StartsWith("DruidFireClawsFixture",StringComparison.Ordinal)==true;
        Mobile.HitsRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action,bool controlled=true)=>ConsoleInputHandler.RegisterCommand(name,"Fire Claws prepared weapon/fire ordering and lifecycle",_=>Core.LoopContext.Post(()=>
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
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="DruidFireClawsFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);Train(p,CombatAbility.FireClaws);Train(p,CombatAbility.Lycanthropy,20);
        FixtureSpace.Corridor(p,6040,12,1000+32*_scene++);
        var weapon=new Dagger();ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(p.EquipItem(weapon),"Fire Claws dagger");ClassGear.Refresh(p);
        Require(DruidPlayerForms.ApplyPrepared(p,form,TimedEffects.Now),"Fire Claws form");Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);p.Frozen=p.Paralyzed=false;ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;SkillHealth.SetBalance256(p,p.HitsMax*128-17);NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static BladeMissileTarget Target(PlayerMobile p)
    {
        var t=new BladeMissileTarget{Name="DruidFireClawsFixtureTarget",NoKillAwards=true,Frozen=true,CantWalk=true};
        t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}return t;
    }
    private static void Drop(PlayerMobile p){DruidPlayerForms.Clear(p);p.Corpse?.Delete();p.Delete();}
    private static void Controlled(Action action)
    {
        var hit=DruidFireClaws.HitRoll;var roll=DruidFireClaws.ElementRoll;var physical=DruidFireClaws.PhysicalRoll;var fire=DruidFireClaws.FireRoll;var effects=DruidFireClaws.Effects;var basic=DruidFormAttacks.HitRoll;
        try{DruidFireClaws.HitRoll=()=>0;DruidFireClaws.ElementRoll=()=>0;DruidFireClaws.PhysicalRoll=_=>0;DruidFireClaws.FireRoll=_=>0;DruidFireClaws.Effects=new Rolls(false);DruidFormAttacks.HitRoll=()=>0;action();}
        finally{DruidFireClaws.HitRoll=hit;DruidFireClaws.ElementRoll=roll;DruidFireClaws.PhysicalRoll=physical;DruidFireClaws.FireRoll=fire;DruidFireClaws.Effects=effects;DruidFormAttacks.HitRoll=basic;}
    }
    private static void Scalars()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-fire-claws-source-probe.json")));
        var seen=new HashSet<(int,int,int,int)>();int compared=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            if(r[7].GetInt32()==2){continue;}int rank=r[0].GetInt32(),a=r[1].GetInt32(),b=r[2].GetInt32(),mastery=r[3].GetInt32();
            if(!seen.Add((rank,a,b,mastery))){continue;}
            var definition=SkillMechanics.For(CombatAbility.FireClaws);
            var values=new SkillValues(definition,key=>key==definition.SourceKey?rank:0,key=>key=="Firestorm"?a:key=="Molten Boulder"?b:0,99);
            var range=DruidFireClaws.FireRange(values,mastery);
            Require(range.Min==r[18].GetInt32()&&range.Max==r[19].GetInt32()&&values.Mana256==1024&&values.Formula("toht")==50+(rank-1)*15,"Native mastered fire/hard synergy/mana/AR source mismatch");compared++;
        }
        Require(compared==189,"Incomplete unique Fire Claws endpoint matrix");
        Log.Information("Fire Claws scalar draft:189unique native mastered endpoint/hard-synergy/mana/AR comparisons from45360complete-source contexts pass; high-rank old overflow remains outside scope");
    }
    private static DruidFireClaws.Pending Begin(PlayerMobile p,BaseCreature target,bool hit=true)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);var charges=DruidAttackCharges.Snapshot();
        Require(ClassCombat.Perform(p,CombatAbility.FireClaws,target,target.Location,Core.TickCount),"Fire Claws start");var session=ClassCombat.For(p);var pending=session.FireClawAttack;
        Require(pending!=null&&pending.Hit==hit&&session.Timing==DruidFormAttacks.Timing(p)&&session.Animation==(byte)WeaponAnimation.Wrestle,"Fire Claws A1/pose/hit context");
        Require(NecromancerEffects.ManaBalance256(p)==mana-1024&&SkillHealth.Balance256(target)==life&&charges.SequenceEqual(DruidAttackCharges.Snapshot()),"Start paid wrong mana or damaged/changed charges");
        Require(!ClassCombat.ResolveAction(p,pending.EffectAt-1)&&SkillHealth.Balance256(target)==life,"Early Fire Claws event");return pending;
    }
    private static DruidFireClaws.Pending Swing(PlayerMobile p,BaseCreature target)
    {
        var pending=Begin(p,target);int before=SkillHealth.Balance256(target);var charges=DruidAttackCharges.Snapshot();
        Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&before-SkillHealth.Balance256(target)==NamedElements.To256(pending.Impact.Parts.Total),"Fire Claws captured packet commit");
        int after=SkillHealth.Balance256(target);Require(charges.SequenceEqual(DruidAttackCharges.Snapshot())&&!ClassCombat.ResolveAction(p,pending.EffectAt)&&SkillHealth.Balance256(target)==after,"Fire Claws gained/consumed/refreshed charges or replayed");return pending;
    }
    private static void Cases(CombatAbility form)
    {
        var p=Fresh(form);var target=Target(p);
        try
        {
            Require(SkillBindings.Instance.Assign(p,true,CombatAbility.FireClaws)&&SkillBindings.Instance.Assign(p,false,CombatAbility.FireClaws)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.FireClaws),"Fire Claws bindings");
            var chargeAbility=form==CombatAbility.Werewolf?CombatAbility.FeralRage:CombatAbility.Maul;
            Require(DruidAttackCharges.Gain(p,SkillMechanics.Values(p,chargeAbility)),"Existing charge fixture");var charge=DruidAttackCharges.State(p,chargeAbility);
            var first=Swing(p,target);Require(first.Fire256>0&&NamedElements.To256(first.Impact.Parts.Fire)==first.Fire256,"Skill fire missing or enhanced twice");
            if(form==CombatAbility.Werebear){Require(first.Impact.SourceStunFrames==charge.StunFrames&&CombatStuns.Remaining(target)>0,"Maul stun absent from Fire Claws");}
            else{Require(first.Drain.LifePercent==charge.LifeDrainMinimum,"Feral drain absent from Fire Claws");}
            var trace=new List<string>();var amulet=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Highlord's Wrath").Id,99,new Random(173),true);
            Require(p.EquipItem(amulet),"Actual Highlord");ClassGear.Refresh(p);Require(ClassGear.Bonus(p,"deadly")>0,"Actual physical critical supplier");
            DruidFireClaws.Effects=new Rolls(false,trace);DruidFireClaws.FireRoll=_=>{trace.Add("fire");return 0;};var noncritical=Begin(p,target);
            Require(trace.Count>=2&&trace[^1]=="fire"&&trace.Take(trace.Count-1).All(x=>x=="critical"),"Skill fire RNG preceded weapon critical");Require(ClassCombat.ResolveAction(p,noncritical.EffectAt),"Noncritical packet release");trace.Clear();
            DruidFireClaws.Effects=new Rolls(true,trace);var critical=Swing(p,target);
            Require(critical.Impact.Parts.Physical==noncritical.Impact.Parts.Physical*2&&critical.Fire256==noncritical.Fire256
                &&critical.Impact.Parts.Fire==noncritical.Impact.Parts.Fire,"Physical critical doubled or rerolled skill fire");
            DruidFireClaws.Effects=new Rolls(false);DruidFireClaws.FireRoll=_=>0;
            target.SetResistance(ResistanceType.Physical,100);var immunePhysical=Begin(p,target);int hp=SkillHealth.Balance256(p);
            Require(immunePhysical.Impact.Parts.Physical==0&&immunePhysical.Impact.Parts.Fire>0&&ClassCombat.ResolveAction(p,immunePhysical.EffectAt)&&SkillHealth.Balance256(p)==hp,"Fire damage incorrectly leeched through physical immunity");
            target.SetResistance(ResistanceType.Physical,0);target.SetResistance(ResistanceType.Fire,100);var immuneFire=Swing(p,target);
            Require(immuneFire.Impact.Parts.Fire==0&&immuneFire.Impact.Parts.Physical>0,"Fire immunity erased ordinary weapon damage");target.SetResistance(ResistanceType.Fire,0);
            Train(p,CombatAbility.Firestorm,20);Train(p,CombatAbility.MoltenBoulder,20);
            var values=SkillMechanics.Values(p,CombatAbility.FireClaws);Require(values.Eval("EDmgSymPerCalc")==880,"Current two-hard-skill Fire Claws synergy");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(321),true);Require(p.EquipItem(helm),"Actual Tarnhelm");ClassGear.Refresh(p);
            Require(SkillMechanics.Values(p,CombatAbility.FireClaws).Eval("EDmgSymPerCalc")==880,"Soft points leaked into Fire Claws synergy");Swing(p,target);
            DruidFireClaws.HitRoll=()=>1;var miss=Begin(p,target,false);int before=SkillHealth.Balance256(target);
            Require(miss.Fire256==0&&ClassCombat.ResolveAction(p,miss.EffectAt)&&SkillHealth.Balance256(target)==before,"Miss rolled or committed skill fire");DruidFireClaws.HitRoll=()=>0;
            var blocker=new GraveyardSkeleton{Name="DruidFireClawsFixtureBlocker",Frozen=true,CantWalk=true,NoKillAwards=true};
            blocker.MoveToWorld(target.Location,p.Map);blocker.AIObject.AITimer.Stop();var choice=SourceBlocking.ChoiceRoll;var block=SourceBlocking.BlockRoll;
            try
            {
                SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>0;var charges=DruidAttackCharges.Snapshot();int blockerLife=SkillHealth.Balance256(blocker);
                var blocked=Begin(p,blocker,false);Require(blocked.Blocked&&blocked.Fire256==0&&ClassCombat.ResolveAction(p,blocked.EffectAt)
                    &&SkillHealth.Balance256(blocker)==blockerLife&&charges.SequenceEqual(DruidAttackCharges.Snapshot()),"Blocked Fire Claws rolled fire/damaged/changed charge");
            }
            finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=block;blocker.Delete();}
            var displaced=Begin(p,target);var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);ClassMovement.Moved(p,Direction.South);p.MoveToWorld(origin,p.Map);
            Require(ClassCombat.For(p).FireClawAttack==null&&!ClassCombat.ResolveAction(p,displaced.EffectAt),"Out/back movement revived Fire Claws");
            displaced=Begin(p,target);target.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,displaced.EffectAt),"Lost melee range still delivered Fire Claws");target.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
            displaced=Begin(p,target);var weapon=CombatTiming.Equipped(p);p.AddToBackpack(weapon);ClassGear.Refresh(p);
            Require(ClassCombat.For(p).FireClawAttack==null&&!ClassCombat.ResolveAction(p,displaced.EffectAt),"Weapon change kept Fire Claws packet");Require(p.EquipItem(weapon),"Fire Claws reequip");ClassGear.Refresh(p);
            var pending=Begin(p,target);int paid=NecromancerEffects.ManaBalance256(p);ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&ClassCombat.For(p).FireClawAttack==null&&NecromancerEffects.ManaBalance256(p)==paid,"Stop replay/refund");
            Ready(p);NecromancerEffects.SetMana256(p,1023);Require(ClassCombat.Perform(p,CombatAbility.FireClaws,target,target.Location,Core.TickCount)&&ClassCombat.For(p).PendingAbility==CombatAbility.BasicAttack,"Fire Claws low-mana Attack route");
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.ManaBalance256(p)==1023,"Fire Claws fallback debited4mana");
            pending=Begin(p,target);DruidPlayerForms.Clear(p);Require(ClassCombat.For(p).FireClawAttack==null&&!ClassCombat.ResolveAction(p,pending.EffectAt)&&!DruidCombat.Available(p,CombatAbility.FireClaws),"Unshift retained Fire Claws packet");
            Log.Information("Fire Claws functional draft:{Form},physical/fire split,real critical-before-fire order,old Maul/Feral coexistence,two hard synergies,gear,immunities,miss/block,movement/range/weapon cancellation and low-mana Attack pass",form);
        }
        finally{target.Delete();Drop(p);}
    }
    private static void Check()
    {
        Cases(CombatAbility.Werewolf);Cases(CombatAbility.Werebear);
        var p=Fresh();var target=Target(p);Require(DruidAttackCharges.Gain(p,SkillMechanics.Values(p,CombatAbility.FeralRage)),"Saved charge");Swing(p,target);
        var charge=DruidAttackCharges.State(p,CombatAbility.FeralRage);
        DruidAttackCharges.Restore(DruidAttackCharges.Snapshot().Select(x=>x.Owner==p.Serial.Value?x with{Charge=x.Charge with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}}:x).ToArray());charge=DruidAttackCharges.State(p,CombatAbility.FeralRage);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.FireClaws)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.FireClaws),"Saved bindings");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,DruidPlayerForms.State(p),charge,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(target))));Snapshot();
    }
    private static void Snapshot()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=World.FindMobile((Serial)saved.Target);
        Require(DruidPlayerForms.State(p)==saved.Form&&DruidAttackCharges.State(p,CombatAbility.FeralRage)==saved.Charge
            &&SkillHealth.Balance256(p)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(target)==saved.TargetLife,"Saved Fire Claws injury/payment/form/charge changed");
    }
    private static void Reload()
    {
        Snapshot();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(ClassCombat.For(p).FireClawAttack==null&&SkillBindings.Instance.Get(p).Right==CombatAbility.FireClaws
            &&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.FireClaws,"Fire Claws saved input/action replay");
    }
    private static void Input(NetState network,string label)
    {
        var p=Fresh(label.EndsWith("right",StringComparison.Ordinal)?CombatAbility.Werewolf:CombatAbility.Werebear);var target=Target(p);Ready(p);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.FireClaws)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.FireClaws),"Fire Claws input bindings");
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(field!=null,"Input protocol");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        var hit=DruidFireClaws.HitRoll;DruidFireClaws.HitRoll=()=>0;bool began=false;long deadline=Core.TickCount+5000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Fire Claws input deadline: pending="+ClassCombat.For(p).PendingAbility+", packet="+(ClassCombat.For(p).FireClawAttack!=null)+", targetLife="+SkillHealth.Balance256(target)+", origin="+p.Location+", target="+target.Location);var session=ClassCombat.For(p);
                if(session.FireClawAttack!=null&&!began){began=true;Log.Information("{Label}: windup",label);}
                if(!began||SkillHealth.Balance256(target)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(NecromancerEffects.ManaBalance256(p)==mana-1024&&session.FireClawAttack==null,"Actual Fire Claws input payment/packet");DruidFireClaws.HitRoll=hit;target.Delete();Drop(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception error){DruidFireClaws.HitRoll=hit;target.Delete();Drop(p);Log.Error(error,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.FireClaws,(int)DiabloClass.Druid,target.Serial.Value,target.X,target.Y,target.Z);Inspect();});
    }
}
