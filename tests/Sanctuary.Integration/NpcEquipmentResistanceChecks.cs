// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcEquipmentResistanceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcEquipmentResistanceChecks));
    private static readonly ResistanceType[] Types=[ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy];
    private static readonly int[] Bases=[13,17,19,23,29];
    private sealed record Stored(uint Actor,uint Item,string Gear,int[] Resistance,int[] Appearance,int Magic);
    private sealed record Saved(uint Owner,Stored[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-equipment-resistances-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-equipment-resistances-check",Check);Register("npc-equipment-resistances-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original NPC item resistance and immunity consumers",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-equipment-resistances-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int[] i=row[0].EnumerateArray().Select(x=>x.GetInt32()).ToArray(),o=row[1].EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            Require(NpcEquipmentResistances.Pool(0,i[3],i[4],i[1]>=2)==o[0]&&NpcEquipmentResistances.Pool(i[2],i[3],i[4],i[1]>=2)==o[1],"Complete source item-stat resistance pool differs at "+count);count++;
        }
        Require(count==1296,"Missing resistance stat graph contexts");
        string root=Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT")??throw new InvalidOperationException("Missing source root");
        using var reduction=JsonDocument.Parse(File.ReadAllText(Path.Join(root,"tools/reference/damage-rounding-engine-evidence.json")));count=0;int excluded=0;
        foreach(var row in reduction.RootElement.GetProperty("probe").GetProperty("data").GetProperty("reduction").EnumerateArray())
        {
            int N(int n)=>row[n].GetInt32();bool bypass=N(6)!=0;
            if(!bypass&&N(0)>0&&N(0)<N(2)){excluded++;continue;}
            int resistance=Math.Max(-100,N(5)!=0?N(1):Math.Min(75,N(1)));
            var actual=NamedElements.Reduce256(N(0),bypass?Math.Min(resistance,0):resistance,bypass?0:N(2),bypass?0:N(3),bypass?0:N(4)*256);
            Require(actual.Damage256==N(7)&&actual.Absorbed256==N(8),"Reused full source reduction differs at "+count);count++;
        }
        Require(count==1260&&excluded==180,"Reused mitigation domain changed");
    }
    private static Item Named(string id){Require(NamedGear.Available(id),"Named item unavailable: "+id);return NamedGear.Create(id,99,new Random(317),true);}
    private static SkeletonGuardian Pet(PlayerMobile owner)
    {
        var actor=new SkeletonGuardian{Name="NPC resistance holder [prepared]"};actor.Initialize(owner,7,2);actor.ControlSlots=0;actor.NoKillAwards=true;
        Require(actor.SetControlMaster(owner),"Prepared holder ownership failed");actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        for(int i=0;i<Types.Length;i++){actor.SetResistance(Types[i],Bases[i]);}
        PersistentMinions.Profiles.Track(actor);actor.AIObject.AITimer.Stop();return actor;
    }
    private static int[] Resistance(SkeletonGuardian actor)=>Types.Select(actor.GetResistance).ToArray();
    private static int[] Appearance(Item item)=>[item.PhysicalResistance,item.FireResistance,item.ColdResistance,item.PoisonResistance,item.EnergyResistance];
    private static void Verify(SkeletonGuardian actor,Item item,int[] expected)
    {
        Require(Resistance(actor).SequenceEqual(expected)&&actor.PhysicalResistance==expected[0]&&actor.FireResistance==expected[1]
            &&actor.ColdResistance==expected[2]&&actor.PoisonResistance==expected[3]&&actor.EnergyResistance==expected[4],"Native getters omitted/doubled original D2 bonuses or retained UO appearance resistances");
        var original=ClassGear.State(item);ClassGear.Profiles.Store(item,original with{Identified=false});
        Require(Resistance(actor).SequenceEqual(Bases),"Unidentified original left cached D2 or UO protection");ClassGear.Profiles.Store(item,original);
        if(item is BaseArmor armor)
        {int hits=armor.HitPoints;armor.HitPoints=0;Require(Resistance(actor).SequenceEqual(Bases),"Broken armor left cached protection");armor.HitPoints=hits;}
        else if(item is BaseWeapon weapon)
        {int hits=weapon.HitPoints;weapon.HitPoints=0;Require(Resistance(actor).SequenceEqual(Bases),"Broken weapon left cached protection");weapon.HitPoints=hits;}
        Require(Resistance(actor).SequenceEqual(expected),"Restored original did not restore live protection");
    }
    private static void Damage(PlayerMobile owner,SkeletonGuardian actor)
    {
        var source=new NpcPoisonRecipient();source.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);source.AIObject.AITimer.Stop();
        try
        {
            // A non-class native attacker exercises the AOS dispatch guard.
            foreach(var type in Types)
            {
                SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor));int before=SkillHealth.Balance256(actor);
                int resistance=actor.GetResistance(type),expected=SourcePercentage.Calculate(16*256,Math.Clamp(100-resistance,0,200),100);
                int[] split=Types.Select(t=>t==type?100:0).ToArray();
                AOS.Damage(actor,source,16,false,split[0],split[1],split[2],split[3],split[4],spellDamage:true);
                Require(before-SkillHealth.Balance256(actor)==expected,"Native AOS damage did not consume equipped NPC resistance exactly once: "+type);
            }
            SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor));int magicBefore=SkillHealth.Balance256(actor);
            SkillCombat.ResolveHitParts(source,actor,new(0,0,0,0,0,Magic:16),false,enhanceBase:false);
            Require(magicBefore-SkillHealth.Balance256(actor)==SourcePercentage.Calculate(16*256,Math.Clamp(100-NamedElements.MagicResistance(actor),0,200),100),"Magic baseline/zero item pool changed");
            SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor));int poisonBefore=SkillHealth.Balance256(actor);
            var poison=PoisonPackets.Mitigate(257,13,false,NamedElements.Resistance(source,actor,"pois",actor.PoisonResistance),0,0,0,0);DateTime at=TimedEffects.Now;
            bool applied=poison.Commit(source,actor,at);Require(applied==(poison.Rate256>0),"NPC poison immunity gate changed");
            if(applied)
            {
                TimedEffects.Settle(actor,at.AddMilliseconds(40));Require(poisonBefore-SkillHealth.Balance256(actor)==poison.Rate256,"Actual poison frame omitted item resistance");
                Require(TimedEffects.TryActor(actor,out var state)&&state.PoisonMs==12*40,"Rate resistance incorrectly shortened poison");TimedEffects.CleansePoison(actor,0,at.AddMilliseconds(40));
            }
        }
        finally{source.Delete();SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor));}
    }
    private static void Immunity(PlayerMobile owner,SkeletonGuardian actor,Item item)
    {
        var context=NpcEquipmentCombat.Context(actor).Value;int bonus=NpcEquipmentResistances.Bonus(context,ResistanceType.Fire);var previous=PaladinConviction.Snapshot();
        try
        {
            foreach(int raw in new[]{99,100,150,-100})
            {
                actor.SetResistance(ResistanceType.Fire,raw-bonus);
                Require(actor.FireResistance==raw,"NPC received player75/95 cap or lost negative raw resistance");
                Require(NamedElements.Resistance(null,actor,"fire",actor.FireResistance,pierceOverride:50)==(raw>=100?raw:Math.Max(-100,raw-50)),"Item immunity/pierce ordering changed");
            }
            actor.SetResistance(ResistanceType.Fire,100-bonus);
            var values=SkillMechanics.Values(owner,CombatAbility.Conviction,1);
            Require(PaladinConviction.Apply(owner,actor,values,TimedEffects.Now.AddMinutes(1)),"Prepared Conviction rejected");
            Require(PaladinConviction.Effect(actor).Fire==(int)values.Eval("aurastatcalc2")/5,"Conviction did not observe item-created immunity");
            Require(NamedElements.Resistance(null,actor,"fire",actor.FireResistance,pierceOverride:0)==100+(int)values.Eval("aurastatcalc2")/5,"Conviction immunity reduction applied twice");
        }
        finally{PaladinConviction.Restore(previous);actor.SetResistance(ResistanceType.Fire,Bases[1]);}
        Require(Resistance(actor).SequenceEqual(Types.Select((t,i)=>Bases[i]+NpcEquipmentResistances.Bonus(context,t))),"Immunity controls changed base pools");
    }
    private static Stored Bind(PlayerMobile owner,Item item)
    {
        if(item is BaseArmor armor){armor.PhysicalBonus=17;armor.FireBonus=43;armor.ColdBonus=31;armor.PoisonBonus=29;armor.EnergyBonus=37;}
        var actor=Pet(owner);item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(IronGolemItems.TryAttach(owner,actor,item,out string why),"Original bind failed: "+why);
        var context=NpcEquipmentCombat.Context(actor).Value;int[] expected=Types.Select((t,i)=>Bases[i]+NpcEquipmentResistances.Bonus(context,t)).ToArray();
        Verify(actor,item,expected);Damage(owner,actor);Immunity(owner,actor,item);
        return new(actor.Serial.Value,item.Serial.Value,JsonSerializer.Serialize(ClassGear.State(item)),expected,Appearance(item),NamedElements.MagicResistance(actor));
    }
    private static void Check()
    {
        SourceCases();var owner=new PlayerMobile{Name="NpcEquipmentResistanceOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Holder history prerequisite failed");
        while(owner.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Owner Strength preparation failed");}
        FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);var ownerItem=Named("unique/91");owner.AddToBackpack(ownerItem);
        Require(owner.EquipItem(ownerItem),"Owner resistance control equip failed");ClassGear.Refresh(owner);Require(ClassGear.Bonus(owner,"res-all")==35,"Real owner all-resistance control missing");
        var socketed=RuneSocketChecks.Host(owner,typeof(HeaterShield),1);GemActivationChecks.Install(owner,GemActivationChecks.Gem(owner,"Diamond"),socketed);
        Require(ClassGear.Bonus(socketed,"res-all")>0,"Actual Diamond shield supplied no all-resistance");
        var rows=new[]{Bind(owner,Named("unique/262")),Bind(owner,Named("unique/91")),Bind(owner,Named("unique/83")),Bind(owner,Named("unique/253")),Bind(owner,Named("unique/216")),Bind(owner,socketed)};
        // Read actual item pools independently of the native consumer checks.
        Require(rows[0].Resistance.SequenceEqual(Bases.Select((n,i)=>n+(i==0?0:75)))&&rows[1].Resistance.SequenceEqual(Bases.Select((n,i)=>n+(i==0?0:35)))
            &&rows[2].Resistance.SequenceEqual(Bases.Select((n,i)=>n+(i==0?0:10)))&&rows[3].Resistance.SequenceEqual(new[]{48,17,79,23,54}),"Actual named resistance values or physical/all-resistance channels differ");
        var retired=Pet(owner);var consumed=Named("unique/91");consumed.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(IronGolemItems.TryAttach(owner,retired,consumed,out _)&&retired.FireResistance==52,"Removal control bind failed");
        IronGolemItems.DestroyBoundItem(retired);Require(Resistance(retired).SequenceEqual(Bases),"Removed original left protection behind");retired.Delete();
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,rows,0)));Report();
    }
    private static void Reload()
    {
        SourceCases();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        foreach(var row in saved.Actors)
        {
            var actor=(SkeletonGuardian)World.FindMobile((Serial)row.Actor);var item=World.FindItem((Serial)row.Item);
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner&&item.Parent==actor&&JsonSerializer.Serialize(ClassGear.State(item))==row.Gear&&Appearance(item).SequenceEqual(row.Appearance),"Reload changed original identity,full rolls or native appearance controls");
            Require(NamedElements.MagicResistance(actor)==row.Magic,"Reload changed magic baseline/item pool");actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();Verify(actor,item,row.Resistance);Damage(owner,actor);Immunity(owner,actor,item);
        }
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));Report();
    }
    private static void Report()=>Log.Information("NPC original resistances:1296source stat pools/1260reused reducer contexts;actual item/native AOS/poison,immunity/Conviction,owner exclusion,live broken/unidentified updates and original reload persistence pass;nonzero item magic supplier,absorb/reduction,resources/events and learned Iron Golem remain unfinished");
}
