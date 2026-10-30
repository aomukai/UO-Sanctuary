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

public static class NpcEquipmentMitigationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcEquipmentMitigationChecks));
    private sealed record Stored(uint Actor,uint Item,string Gear,int Physical,int Magic,int Fire,int Cold,int Lightning,int Level);
    private sealed record Saved(uint Owner,Stored[] Actors,int Generation);
    private sealed record Legacy(uint Owner,uint Actor,uint Item,string Gear,int Cold,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-equipment-mitigation-fixtures.json");
    private static string LegacyManifest=>Path.Join(Core.BaseDirectory,"npc-equipment-mitigation-legacy-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("npc-equipment-mitigation-check",Check);Register("npc-equipment-mitigation-reload-check",Reload);
        Register("npc-equipment-mitigation-legacy-create-check",LegacyCreate);
        Register("npc-equipment-mitigation-legacy-upgrade-check",LegacyUpgrade);
        Register("npc-equipment-mitigation-legacy-reload-check",LegacyReload);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original NPC item reductions and absorption",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        var property=NamedGear.Catalog.Item("piece/81").Properties.Single(x=>x.Code=="abs-cold/lvl");
        var born=NamedPropertyRuntime.Roll("p/2",property,new NoRandom());
        Require(born.Value==2&&born.Maximum==2,"Fixed PropertyFunc17 coefficient ignored min/max or drew RNG");
        var legacy=new NamedPropertyRoll("p/2",0,0);NamedPropertyRuntime.Validate(property,born);NamedPropertyRuntime.Validate(property,legacy);
        foreach(int level in new[]{1,3,4,47,99})
        {Require(NamedPropertyRuntime.Bonus(property,born,"abs-cold",level)==2*level/8&&NamedPropertyRuntime.Bonus(property,legacy,"abs-cold",level)==2*level/8,"Legacy fixed coefficient was rerolled or differs from new birth");}
        Require(NamedGear.PropertyText(property,legacy,47)==NamedGear.PropertyText(property,born,47)
            &&NamedGear.PropertyText(property,legacy,47).Contains("11")&&NamedGear.PropertyText(property,legacy,99).Contains("24"),"Legacy description differs from recovered mechanical value");
        using var bear=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bear-smite-source-probe.json")));int count=0;
        foreach(var row in bear.RootElement.GetProperty("reduction").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();var actual=NamedElements.Reduce256(N(0),N(1),checked(N(2)*256),N(3),checked(N(4)*256));
            Require(actual.Damage256==N(5)&&actual.Absorbed256==N(6),"Reused full source reducer/percentage differs at "+count);count++;
        }
        Require(count==1248,"Missing large-value source mitigation contexts");
        string root=Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT")??throw new InvalidOperationException("Missing source root");
        using var small=JsonDocument.Parse(File.ReadAllText(Path.Join(root,"tools/reference/damage-rounding-engine-evidence.json")));count=0;int excluded=0;
        foreach(var row in small.RootElement.GetProperty("probe").GetProperty("data").GetProperty("reduction").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();bool bypass=N(6)!=0;
            if(!bypass&&N(0)>0&&N(0)<N(2)){excluded++;continue;}
            int resistance=Math.Max(-100,N(5)!=0?N(1):Math.Min(75,N(1)));
            var actual=NamedElements.Reduce256(N(0),bypass?Math.Min(resistance,0):resistance,bypass?0:N(2),bypass?0:N(3),bypass?0:N(4)*256);
            Require(actual.Damage256==N(7)&&actual.Absorbed256==N(8),"Reused source reduction/bypass differs at "+count);count++;
        }
        Require(count==1260&&excluded==180,"Reused source domain changed");
    }
    private sealed class NoRandom:Random
    {public override int Next(int minValue,int maxValue)=>throw new InvalidOperationException("Fixed coefficient drew RNG");}
    private static void PlayerCoefficient()
    {
        var player=new PlayerMobile{Name="LegacyCoefficientPlayer",Body=0x190,Player=true};player.AddItem(new Backpack());
        try
        {
            ClassCreation.Profiles.Assign(player,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(player,99);
            while(player.RawStr<110){Require(ClassCreation.Profiles.TrySpendStat(player,CharacterStat.Strength),"Player coefficient control Strength failed");}
            FixtureSpace.Place(player,new(6100,2300,0),Map.Trammel);var item=Named("piece/81");var original=ClassGear.State(item);
            ClassGear.Profiles.Store(item,original with{NamedProperties=original.NamedProperties.Select(x=>x.Key=="p/2"?new NamedPropertyRoll(x.Key,0,0):x).ToArray()});
            player.AddToBackpack(item);Require(player.EquipItem(item),"Legacy coefficient player equip failed");ClassGear.Refresh(player);
            Require(ClassGear.Bonus(player,"abs-cold")==24&&ClassGear.State(item).NamedProperties.Single(x=>x.Key=="p/2").Value==0,"Real equipped player did not recover legacy fixed coefficient without rewriting it");
        }
        finally{player.Delete();}
    }
    private static Item Named(string id){Require(NamedGear.Available(id),"Named item unavailable: "+id);return NamedGear.Create(id,99,new Random(317),true);}
    private static void Train(PlayerMobile owner,string id)
    {
        if(ClassCreation.Profiles.GetRank(owner.Serial,id)>0){return;}
        Require(TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var talent),"Missing prerequisite talent");
        foreach(string required in talent.Prerequisites){Train(owner,required);}
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Prepared history training failed: "+id);
    }
    private static SkeletonGuardian Pet(PlayerMobile owner,bool iron=false)
    {
        SkeletonGuardian actor;
        if(iron){var minion=new NecromancerMinion();minion.Prepare(owner,CombatAbility.IronGolem);actor=minion;}
        else{actor=new SkeletonGuardian();actor.Initialize(owner,7,2);}
        actor.Name="NPC mitigation holder [prepared]";actor.ControlSlots=0;actor.NoKillAwards=true;
        Require(actor.SetControlMaster(owner),"Prepared holder ownership failed");actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        PersistentMinions.Profiles.Track(actor);actor.AIObject.AITimer.Stop();return actor;
    }
    private static int ItemBonus(SkeletonGuardian actor,string code)=>NpcEquipmentCombat.Context(actor).Value.Bonus(code);
    private static ElementReduction Expected(SkeletonGuardian actor,string element,int incoming,int damageRate=0)
    {
        int resistance=element switch{"physical"=>actor.PhysicalResistance,"fire"=>actor.FireResistance,"cold"=>actor.ColdResistance,"ltng"=>actor.EnergyResistance,"pois"=>actor.PoisonResistance,_=>NamedElements.MagicResistance(actor)};
        int reduction=element=="pois"?0:Math.Max(0,ItemBonus(actor,element=="physical"?"physical-reduction":"magic-reduction"));
        int value=Math.Max(0,incoming-reduction*256*(damageRate>0?damageRate:1024)/1024);
        if(resistance!=0){value=SourcePercentage.Calculate(value,Math.Clamp(100-resistance,0,200),100);}
        int percent=element is "fire" or "cold" or "ltng"?Math.Clamp(ItemBonus(actor,"abs-"+element+"%"),0,40):0;
        int absorbed=SourcePercentage.Calculate(value,percent,100);value-=absorbed;
        int flat=element is "fire" or "cold" or "ltng"?Math.Min(value,Math.Max(0,ItemBonus(actor,"abs-"+element))*256):0;
        return new(value-flat,absorbed+flat);
    }
    private static GearDamage Channel(string element,double value)=>element switch
    {"physical"=>new(value,0,0,0,0),"fire"=>new(0,value,0,0,0),"cold"=>new(0,0,value,0,0),"ltng"=>new(0,0,0,value,0),"pois"=>new(0,0,0,0,value),_=>new(0,0,0,0,0,Magic:value)};
    private static void Verify(PlayerMobile owner,SkeletonGuardian actor)
    {
        var source=new NpcPoisonRecipient();source.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);source.AIObject.AITimer.Stop();
        try
        {
            foreach(string element in new[]{"physical","fire","cold","ltng","mag","pois"})
            foreach(bool full in new[]{false,true})
            {
                int maximum=SkillHealth.Maximum256(actor),before=full?maximum:maximum/2;SkillHealth.SetBalance256(actor,before);
                var expected=Expected(actor,element,32*256);var reduced=NamedElements.Resolve256(source,actor,32*256,element,enhance:false);
                Require(reduced==expected&&SkillHealth.Balance256(actor)==before,"Original reduction/absorb capture differs or pure reduction changed life: "+element);
                int after=Math.Min(maximum,before+expected.Absorbed256)-expected.Damage256;
                if(element=="mag")
                {SkillCombat.ResolveHitParts(source,actor,Channel(element,32),false,enhanceBase:false);}
                else
                {
                    AOS.Damage(actor,source,32,false,element=="physical"?100:0,element=="fire"?100:0,element=="cold"?100:0,element=="pois"?100:0,element=="ltng"?100:0,spellDamage:true);
                }
                Require(actor.Alive&&SkillHealth.Balance256(actor)==after,"Actual native/prepared packet lost reduction or heal-before-damage cap: "+element);
            }
            foreach(int rate in new[]{256,512,1024})
            {
                SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor)/2);int before=SkillHealth.Balance256(actor);
                var parts=SkillCombat.MitigatePrepared(source,actor,Channel("fire",32),out int healed,rate);var expected=Expected(actor,"fire",32*256,rate);
                Require(NamedElements.To256(parts.Fire)==expected.Damage256&&healed==expected.Absorbed256&&SkillHealth.Balance256(actor)==before,"Prepared field-rate MDR scaling or deferred healing changed");
            }
            SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor)/2);int start=SkillHealth.Balance256(actor),max=SkillHealth.Maximum256(actor);
            var fire=Expected(actor,"fire",8*256);var cold=Expected(actor,"cold",8*256);var lightning=Expected(actor,"ltng",8*256);
            SkillCombat.ResolveHitParts(source,actor,new(0,8,8,8,0),false,enhanceBase:false);
            Require(SkillHealth.Balance256(actor)==Math.Min(max,start+fire.Absorbed256+cold.Absorbed256+lightning.Absorbed256)-fire.Damage256-cold.Damage256-lightning.Damage256,"Multichannel absorption healing was applied twice or after damage");
        }
        finally{source.Delete();SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor));}
    }
    private static void Disabled(SkeletonGuardian actor,Item item)
    {
        void Empty()=>Require(GemCombat.Reduction(actor)==0&&GemCombat.PhysicalReduction(actor)==0&&!NamedElements.Defense(actor),"Disabled original retained protection");
        var state=ClassGear.State(item);ClassGear.Profiles.Store(item,state with{Identified=false});Empty();ClassGear.Profiles.Store(item,state);
        if(item is BaseArmor armor){int hits=armor.HitPoints;armor.HitPoints=0;Empty();armor.HitPoints=hits;}
        else if(item is BaseWeapon weapon){int hits=weapon.HitPoints;weapon.HitPoints=0;Empty();weapon.HitPoints=hits;}
    }
    private static Stored Bind(PlayerMobile owner,Item item,bool iron=false)
    {
        var actor=Pet(owner,iron);item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(IronGolemItems.TryAttach(owner,actor,item,out string why),"Original bind failed: "+why);
        var row=new Stored(actor.Serial.Value,item.Serial.Value,JsonSerializer.Serialize(ClassGear.State(item)),ItemBonus(actor,"physical-reduction"),ItemBonus(actor,"magic-reduction"),ItemBonus(actor,"abs-fire"),ItemBonus(actor,"abs-cold"),ItemBonus(actor,"abs-ltng"),CreatureProfiles.Level(actor));
        Require(GemCombat.PhysicalReduction(actor)==row.Physical&&GemCombat.Reduction(actor)==row.Magic,"Real NPC reduction reader differs");
        Disabled(actor,item);Verify(owner,actor);return row;
    }
    private static void FireBoundary(PlayerMobile owner)
    {
        var fire=new NecromancerMinion();var source=new NpcPoisonRecipient();
        try
        {
            fire.Prepare(owner,CombatAbility.FireGolem);fire.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);fire.AIObject.AITimer.Stop();
            int incoming=32*256,innate=fire.FireAbsorb;var expected=NamedElements.Reduce256(incoming,fire.FireResistance,0,innate,0,100);
            Require(innate>0&&NamedElements.Resolve256(source,fire,incoming,"fire",enhance:false)==expected,"Existing innate Fire Golem adapter changed without its separate audit");
            var item=Named("unique/297");item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);fire.ControlSlots=0;Require(fire.SetControlMaster(owner),"Prepared fire owner failed");
            Require(IronGolemItems.TryAttach(owner,fire,item,out _),"Prepared fire boundary item bind failed");int before=SkillHealth.Balance256(fire);
            try{NamedElements.Resolve256(source,fire,incoming,"fire",enhance:false);throw new InvalidOperationException("Unverified innate/item composition was invented");}
            catch(NotSupportedException){Require(SkillHealth.Balance256(fire)==before,"Unverified composition mutated life");}
            IronGolemItems.DestroyBoundItem(fire);
        }
        finally{fire.Delete();source.Delete();}
    }
    private static void Check()
    {
        SourceCases();PlayerCoefficient();var owner=new PlayerMobile{Name="NpcEquipmentMitigationOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);Train(owner,"necromancer.raiseskeleton");Train(owner,"necromancer.firegolem");
        while(owner.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Owner Strength preparation failed");}
        FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        var ownerItem=Named("unique/274");owner.AddToBackpack(ownerItem);Require(owner.EquipItem(ownerItem),"Owner reduction/absorb control equip failed");
        ClassGear.Refresh(owner);Require(GemCombat.Reduction(owner) is >=12 and <=15&&ClassGear.Bonus(owner,"abs-fire%")==15,"Real owner MDR/absorb controls missing");
        var rows=new[]{Bind(owner,Named("unique/38")),Bind(owner,Named("unique/228")),Bind(owner,Named("unique/297"),true),Bind(owner,Named("unique/343")),Bind(owner,Named("unique/207")),Bind(owner,Named("piece/81"))};
        Require(rows[0].Physical==3&&rows[0].Magic==3&&rows[1].Physical is >=11 and <=16&&rows[1].Magic is >=14 and <=18
            &&rows[2].Fire is >=5 and <=11&&rows[3].Cold is >=5 and <=9&&rows[4].Lightning==20&&rows[5].Cold==2*rows[5].Level/8&&rows[5].Level==47,"Actual original scalar/range/recipient-level values differ: "+JsonSerializer.Serialize(rows.Select(x=>new{x.Physical,x.Magic,x.Fire,x.Cold,x.Lightning,x.Level})));
        FireBoundary(owner);var retired=Pet(owner);var consumed=Named("unique/38");consumed.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(IronGolemItems.TryAttach(owner,retired,consumed,out _)&&GemCombat.Reduction(retired)==3,"Removal fixture failed");IronGolemItems.DestroyBoundItem(retired);
        Require(GemCombat.Reduction(retired)==0&&GemCombat.PhysicalReduction(retired)==0&&!NamedElements.Defense(retired),"Removed original retained mitigation");retired.Delete();
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,rows,0)));Report();
    }
    private static void Reload()
    {
        SourceCases();PlayerCoefficient();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        foreach(var row in saved.Actors)
        {
            var actor=(SkeletonGuardian)World.FindMobile((Serial)row.Actor);var item=World.FindItem((Serial)row.Item);
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner&&item.Parent==actor&&JsonSerializer.Serialize(ClassGear.State(item))==row.Gear,"Reload lost original identity/full rolls");
            Require(GemCombat.PhysicalReduction(actor)==row.Physical&&GemCombat.Reduction(actor)==row.Magic&&ItemBonus(actor,"abs-fire")==row.Fire
                &&ItemBonus(actor,"abs-cold")==row.Cold&&ItemBonus(actor,"abs-ltng")==row.Lightning&&CreatureProfiles.Level(actor)==row.Level,"Reload changed original mitigation/recipient level");
            actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();Disabled(actor,item);Verify(owner,actor);
        }
        FireBoundary(owner);PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));Report();
    }
    // Boot0 deliberately uses archived pre-fix content and this compatible
    // integration assembly. It must create and actually save the original0.
    private static void LegacyCreate()
    {
        var owner=new PlayerMobile{Name="NpcMitigationLegacyOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);Train(owner,"necromancer.raiseskeleton");FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        var actor=Pet(owner);var item=Named("piece/81");item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(IronGolemItems.TryAttach(owner,actor,item,out _),"Old producer bind failed");var gear=ClassGear.State(item);
        Require(gear.NamedProperties.Single(x=>x.Key=="p/2").Value==0&&ItemBonus(actor,"abs-cold")==0,"Old producer no longer reproduces missing coefficient");
        File.WriteAllText(LegacyManifest,JsonSerializer.Serialize(new Legacy(owner.Serial.Value,actor.Serial.Value,item.Serial.Value,JsonSerializer.Serialize(gear),0,0)));
        PersistentMinions.Suspend(owner);owner.Internalize();
    }
    private static void LegacyUpgrade()
    {
        var saved=JsonSerializer.Deserialize<Legacy>(File.ReadAllText(LegacyManifest));Require(saved.Generation==0&&saved.Cold==0,"Missing old producer checkpoint");LegacyContinue(saved);
    }
    private static void LegacyReload()
    {
        var saved=JsonSerializer.Deserialize<Legacy>(File.ReadAllText(LegacyManifest));Require(saved.Generation==1&&saved.Cold==11,"Missing upgraded coefficient checkpoint");LegacyContinue(saved);
    }
    private static void LegacyContinue(Legacy saved)
    {
        var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var actor=(SkeletonGuardian)World.FindMobile((Serial)saved.Actor);var item=World.FindItem((Serial)saved.Item);
        Require(item.Parent==actor&&actor.ControlMaster==owner&&JsonSerializer.Serialize(ClassGear.State(item))==saved.Gear,"Coefficient recovery rewrote original identity/full saved rolls");
        Require(ClassGear.State(item).NamedProperties.Single(x=>x.Key=="p/2").Value==0&&ItemBonus(actor,"abs-cold")==11,"Legacy0 did not recover the fixed source coefficient without mutation");
        FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();Verify(owner,actor);
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(LegacyManifest,JsonSerializer.Serialize(saved with{Cold=11,Generation=saved.Generation+1}));
    }
    private static void Report()=>Log.Information("NPC original mitigation:1248+1260reused source reducer contexts;actual reductions/flat elemental absorb,native AOS and multichannel heal/cap/rate order,disabled/removal/owner exclusion and original reload persistence;percentage-item suppliers and innate Fire Golem parity remain unverified;resources/events/frozen Revive/learned Iron Golem unfinished");
}
