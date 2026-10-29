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

public static class ItemMaximumOperationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ItemMaximumOperationChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"item-maximum-operations-fixtures.json");
    private sealed record WeaponBase(string Code,int Minimum,int Maximum,int TwoMinimum,int TwoMaximum,int ThrowMinimum,int ThrowMaximum,int StrengthBonus,int DexterityBonus,int TwoHanded,int EitherHanded,int RequiredStrength,int RequiredDexterity,int RequiredLevel);
    private sealed record PlayerSaved(uint Owner,uint Item,string Gear,int Minimum,int Maximum,int SkillMinimum256,int SkillMaximum256,int Durability);
    private sealed record Held(uint Pet,uint Owner,uint Item,string Gear,int Minimum,int Maximum);
    private sealed record Saved(PlayerSaved[] Players,Held Npc);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("item-maximum-operations-check",Check);Register("item-maximum-operations-reload-check",Reload);}
    private static void Register(string name,Action a)=>ConsoleInputHandler.RegisterCommand(name,"Separate level-scaled local maximum damage",_=>Core.LoopContext.Post(()=>
    {try{a();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        Require(WeaponHands.DamageRelated("dmg-max%")&&WeaponHands.DamageRelated("dmg%/lvl"),"Maximum channel lost selected-hand classification");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-maximum-operations-source-probe.json")));int count=0,combined=0,baseDifference=0;
        foreach(var row in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var i=row[0].EnumerateArray().Select(v=>v.GetInt32()).ToArray();int minimum=row[1][0].GetInt32(),maximum=row[1][1].GetInt32();
            var property=new NamedProperty("0","dmg%/lvl","dmg%/lvl",true,JsonSerializer.SerializeToElement(i[6]),0,0);var roll=new NamedPropertyRoll("0",i[6],i[6]);
            int percent=NamedPropertyRuntime.Bonus(property,roll,"dmg-max%",i[4]);
            Require(NamedPropertyRuntime.Bonus(property,roll,"dmg%",i[4])==0&&NamedPropertyRuntime.Bonus(property,roll,"dmg-min",i[4])==0,"Maximum percent leaked into general/minimum damage");
            Require(maximum-minimum-(i[4]*i[5]>>3)==ItemDamageOperations.LevelMaximum(i[1],percent),"Separate source op5 term differs at "+count);
            if(maximum!=i[1]*(100+i[3]+percent)/100+i[2]+(i[4]*i[5]>>3)){combined++;}
            if(minimum!=i[1]*(100+i[3])/100+i[2]){baseDifference++;}count++;
        }
        Require((count,combined,baseDifference)==(35280,6480,720),"Complete maximum operation source scope missing");
        foreach(var pair in new[]{(-1,1),(1,-1)}){bool no=false;try{ItemDamageOperations.LevelMaximum(pair.Item1,pair.Item2);}catch(NotSupportedException){no=true;}Require(no,"Unmapped negative maximum scaling accepted");}
        bool overflow=false;try{ItemDamageOperations.LevelMaximum(int.MaxValue,2);}catch(OverflowException){overflow=true;}Require(overflow,"Unsupported overflowing source product wrapped");
        foreach(var r in JsonSerializer.Deserialize<WeaponBase[]>(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-maximum-bases-source-probe.json"))))
        {
            var b=EquipmentBalance.Base(r.Code);Require((b.MinDamage,b.MaxDamage,b.TwoHandMin,b.TwoHandMax,b.MissileMin,b.MissileMax,b.StrengthBonus,b.DexterityBonus)==(r.Minimum,r.Maximum,r.TwoMinimum,r.TwoMaximum,r.ThrowMinimum,r.ThrowMaximum,r.StrengthBonus,r.DexterityBonus),"Owned weapon base differs: "+r.Code);
            Require(b.TwoHanded==(r.TwoHanded!=0)&&b.OneOrTwoHanded==(r.EitherHanded!=0)
                &&(b.Strength,b.Dexterity,b.RequiredLevel)==(r.RequiredStrength,r.RequiredDexterity,r.RequiredLevel),"Owned weapon requirements/hand flags differ: "+r.Code);
        }
        Require(!NecromancerCombat.Implemented(CombatAbility.IronGolem),"Incomplete full golem actor enabled");
    }
    private static Item Named(string key,int seed)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==key).Id,99,new Random(seed),true);
    private static PlayerMobile Owner(int n)
    {
        var p=new PlayerMobile{Name="MaximumItemOwner"+n,Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Maximum fixture Strength");}
        while(p.RawDex<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Maximum fixture Dexterity");}
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.raiseskeleton",out _),"Persistent holder training");FixtureSpace.Place(p,new(6100,1670+n*4,0),Map.Trammel);return p;
    }
    private static PlayerSaved Capture(PlayerMobile owner,Item item)
    {
        var w=item as BaseWeapon;Require(w!=null&&item.Parent==owner&&ClassGear.Active(owner,item),"Saved player weapon is not active");
        var local=ClassGear.Damage(item);var skill=ClassStats.SkillPhysicalRange256(owner,0,0);
        Require((w.MinDamage,w.MaxDamage)==local,"Local maximum did not reach native weapon getters");
        Require(ClassGear.Bonus(item,"dmg%")==200,"Level maximum contaminated ordinary item ED");
        var basis=ClassGear.Base(item);int attributes=(int)((long)owner.Str*basis.StrengthBonus/100+(long)owner.Dex*basis.DexterityBonus/100);
        Require(ClassStats.PhysicalDamageBonus(owner,w)==attributes,"Local maximum entered the general player damage pool");
        int low=local.Min*256,high=local.Max*256;
        Require(skill==(low+(int)((long)low*attributes/100),high+(int)((long)high*attributes/100)),"Maximum did not enter the shared FP256 physical endpoint consumer");
        Require(GearEffects.Damage(owner,1).Physical==ClassStats.PhysicalDamageRange(owner).Max,"Ordinary weapon packet lost corrected maximum");
        return new(owner.Serial.Value,item.Serial.Value,JsonSerializer.Serialize(ClassGear.State(item)),local.Min,local.Max,skill.Min,skill.Max,w.HitPoints);
    }
    private static Held CaptureHeld(BaseCreature pet,Item item)
    {
        var c=IronGolemItems.Equipment(pet,37);var damage=c.WeaponDamage;
        Require(c.Item==item&&c.Bonus("dmg%")==200&&c.Bonus("dmg-max%")==92,"Declared recipient37 did not keep separate percentage pools");
        Require(damage==(177,368),"Owned Reaver endpoints differ at declared recipient37");
        return new(pet.Serial.Value,pet.ControlMaster.Serial.Value,item.Serial.Value,JsonSerializer.Serialize(ClassGear.State(item)),damage.Min,damage.Max);
    }
    private static void Strike(PlayerMobile owner)
    {
        var target=MonsterOrderedReleaseChecks.Body();target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);target.AIObject?.AITimer.Stop();
        foreach(var resistance in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){target.SetResistance(resistance,0);}
        var packet=GearEffects.Damage(owner,1);int before=SkillHealth.Balance256(target);var weapon=CombatTiming.Equipped(owner);int durability=weapon.HitPoints;
        GearEffects.Strike(owner,target,1,new Random(519),wearWeapon:false);
        Require(before-SkillHealth.Balance256(target)==NamedElements.To256(packet.Total),"Actual weapon strike did not deliver corrected physical/elemental packet");
        Require(weapon.HitPoints==durability&&target.Alive&&!target.Deleted,"Strike fixture consumed equipment or unexpectedly killed target");target.Delete();
    }
    private static void Check()
    {
        SourceCases();var owners=new[]{Owner(0),Owner(1)};var items=new[]{Named("Messerschmidt's Reaver",37),Named("Eaglehorn",38)};
        Require((IronGolemItems.Metadata("7ga").Flags&2)!=0&&(IronGolemItems.Metadata("6l7").Flags&2)==0,"Weapon eligibility changed to admit a nonmetal bow");
        var saved=new PlayerSaved[2];
        for(int n=0;n<2;n++)
        {
            var owner=owners[n];var item=items[n];owner.AddToBackpack(item);Require(owner.EquipItem(item),"Named maximum fixture equip failed");ClassGear.Refresh(owner);
            var expected=n==0?(177,514):(45,313);Require(ClassGear.Damage(item)==expected,"Named weapon level99 endpoints differ");
            int expectedMinimum=n==0?177:45;
            foreach(int level in new[]{1,37,99}){Require(new ItemStatContext(item,level).WeaponDamage.Min==expectedMinimum,"Level maximum increased minimum damage");}
            Strike(owner);saved[n]=Capture(owner,item);
            owner.AddToBackpack(item);ClassGear.Refresh(owner);Require(ClassGear.Bonus(owner,"dmg-max%")==0&&CombatTiming.Equipped(owner)!=item,"Unequipped maximum supplier remained active");
            Require(owner.EquipItem(item),"Named maximum fixture re-equip failed");ClassGear.Refresh(owner);Require(Capture(owner,item)==saved[n],"Equip cycle rerolled original item or damage");
        }
        var original=ClassGear.State(items[0]);ClassGear.Profiles.Store(items[0],original with{Identified=false});ClassGear.Refresh(owners[0]);
        Require(new ItemStatContext(items[0],37).Bonus("dmg-max%")==0&&ClassGear.Damage(items[0])==(59,94),"Unidentified level bonus leaked");ClassGear.Profiles.Store(items[0],original);ClassGear.Refresh(owners[0]);
        var weapon=(BaseWeapon)items[0];int durability=weapon.HitPoints;weapon.HitPoints=0;
        Require(new ItemStatContext(weapon,37).WeaponDamage==(0,0)&&new ItemStatContext(weapon,37).Bonus("dmg-max%")==0,"Broken weapon contributed maximum scaling");weapon.HitPoints=durability;ClassGear.Refresh(owners[0]);
        Require(Capture(owners[0],items[0])==saved[0],"Inactive-item controls changed original item");
        var plain=new Broadsword();ClassGear.Profiles.Ensure(plain);foreach(int level in new[]{1,37,99}){Require(new ItemStatContext(plain,level).WeaponDamage==(7,14),"Unscaled ordinary weapon damage changed");}plain.Delete();
        var pet=new SkeletonGuardian{Name="Maximum item [persistent storage fixture]",Frozen=true,CantWalk=true};pet.Initialize(owners[0],1,0);Require(pet.SetControlMaster(owners[0]),"Maximum holder ownership failed");PersistentMinions.Profiles.Track(pet);pet.MoveToWorld(owners[0].Location,owners[0].Map);pet.AIObject?.AITimer.Stop();
        var held=Named("Messerschmidt's Reaver",39);held.MoveToWorld(new(owners[0].X+1,owners[0].Y,owners[0].Z),owners[0].Map);uint serial=held.Serial.Value;string gear=JsonSerializer.Serialize(ClassGear.State(held));
        Require(IronGolemItems.TryAttach(owners[0],pet,held,out var why),"Named original maximum item attach failed: "+why);var npc=CaptureHeld(pet,held);
        Require(npc.Item==serial&&npc.Gear==gear&&!held.Visible&&!held.Movable,"Named original item identity/roll/private attachment changed");
        owners[0].AutoStablePets();Require(pet.ControlMaster==owners[0]&&!pet.IsStabled,"Maximum holder entered ordinary pet stabling");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(saved,npc)));pet.Internalize();foreach(var owner in owners){owner.Internalize();}
        Log.Information("Maximum item damage:35280complete-source delta comparisons/6480separate-rounding controls;Reaver177-514/Eaglehorn45-313at99,actual player packets/strikes,original held Reaver177-368at declared37,unchanged minimum/general ED and inactive-item controls pass;full NPC graph remains gated");
    }
    private static void Reload()
    {
        SourceCases();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        foreach(var p in s.Players)
        {
            var owner=World.FindMobile((Serial)p.Owner) as PlayerMobile;var item=World.FindItem((Serial)p.Item);
            Require(owner is{Deleted:false}&&ClassStats.Level(owner)==99&&item is{Deleted:false}&&Capture(owner,item)==p,"Saved player item/roll/damage context changed");
        }
        var pet=World.FindMobile((Serial)s.Npc.Pet) as BaseCreature;var held=World.FindItem((Serial)s.Npc.Item);
        Require(pet is{Alive:true,Deleted:false}&&pet.ControlMaster?.Serial.Value==s.Npc.Owner&&held is{Deleted:false}&&CaptureHeld(pet,held)==s.Npc&&!held.Visible&&!held.Movable,"Saved original bound item or maximum supplier changed");pet.AIObject?.AITimer.Stop();
        Log.Information("Maximum item damage:actual saved Reaver/Eaglehorn player weapons and original privately held Reaver retain complete rolls,serials,ownership,local endpoints and FP256physical ranges through reload");
    }
}
