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

public static class NpcItemSupplierChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcItemSupplierChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-item-suppliers-fixtures.json");
    private sealed record Stored(uint Pet,uint Item,string Gear,int Level,int Attack,int Defense,int Life256,int Mana256,int Minimum,int Maximum);
    private sealed record Saved(uint Owner,Stored[] Items);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-item-suppliers-check",Check);Register("npc-item-suppliers-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Recipient-level original equipment numeric suppliers",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-item-suppliers-source-probe.json")));int cases=0,profiles=0;
        foreach(var row in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int[] i=row[0].EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            int level=i[0]==2&&i[1] is 0 or 1?i[3]:i[0]==4&&i[2] is 0 or 1?i[4]:0;
            string code=i[6]==1?"att/lvl":"hp/lvl",target=i[6]==1?"att":"hp";
            var property=new NamedProperty("0",code,code,true,JsonSerializer.SerializeToElement(i[5]),0,0);
            var roll=new NamedPropertyRoll("0",i[5],i[5]);
            int bonus=level<=0?0:i[8]==8?NamedPropertyRuntime.Bonus256(property,roll,target,level):NamedPropertyRuntime.Bonus(property,roll,target,level);
            Require(i[7]+bonus==row[1].GetInt32(),"Full source op2/op4 recipient-level result differs at "+cases);cases++;
        }
        foreach(var profile in doc.RootElement.GetProperty("profiles").EnumerateArray())
        {
            string code=profile.GetProperty("Code").GetString(),target=profile.GetProperty("Target").GetString();int shift=profile.GetProperty("Shift").GetInt32();
            if(profile.GetProperty("Operation").GetInt32()==5){Require(code=="dmg%/lvl","Unknown maximum-damage projection migration");target="dmg-max%";}
            foreach(int coefficient in new[]{0,1,3,8,17,255})foreach(int level in new[]{1,37,47,99})
            {
                var property=new NamedProperty("0",code,code,true,JsonSerializer.SerializeToElement(coefficient),0,0);var roll=new NamedPropertyRoll("0",coefficient,coefficient);
                Require(NamedPropertyRuntime.Bonus(property,roll,target,level)==coefficient*level>>shift,"Owned scaled property divisor differs: "+code);
                Require(NamedPropertyRuntime.Bonus256(property,roll,target,level)==coefficient*level*256>>shift,"FP256 scaled property rounded before conversion: "+code);
                Require(NamedPropertyRuntime.Bonus(property,roll,"unrelated",level)==0,"Scaled property leaked to a different stat");
            }
            profiles++;
        }
        Require(cases==27648&&profiles==27,"Source scope missing or unfinished actor enabled");
    }
    private sealed class ExactAffixRandom:Random
    {
        private readonly int Choice,Ticket,Total;private int Phase;
        internal ExactAffixRandom(NormalBase basis,AffixDefinition selected)
        {
            Choice=selected.Kind==AffixKind.Prefix?1:2;
            var pool=AffixBalance.All.Where(a=>a.Kind==selected.Kind&&ClassGear.Implemented(a)&&AffixBalance.Eligible(a,basis.Type,AffixBalance.Level(99,basis.QualityLevel,basis.MagicLevel),GearRarity.Magic)).ToArray();
            int Weight(AffixDefinition a)=>a.Frequency*(basis.MagicLevel>0?a.Level:1);
            Require(pool.Contains(selected),"Requested live ordinary affix missing");Total=pool.Sum(Weight);Ticket=pool.TakeWhile(a=>a.Id!=selected.Id).Sum(Weight);
        }
        public override int Next(int max){if(Phase++==0){Require(max==4,"Quality draw changed");return Choice;}Require(Phase==2&&max==Total,"Weighted affix draw changed");return Ticket;}
        public override int Next(int min,int max){Require(max>min,"Invalid live affix range");return max-1;}
    }
    private static SkeletonGuardian Pet(PlayerMobile owner)
    {
        var p=new SkeletonGuardian{Name="Equipment recipient [persistent holder fixture]",Frozen=true,CantWalk=true};p.Initialize(owner,3,0);
        Require(p.SetControlMaster(owner),"Persistent equipment holder owner failed");PersistentMinions.Profiles.Track(p);p.MoveToWorld(owner.Location,owner.Map);p.AIObject?.AITimer.Stop();return p;
    }
    private static Stored Capture(BaseCreature pet,Item item)
    {
        var c=IronGolemItems.Equipment(pet,37);var range=c.WeaponDamage;
        Require(c.Item==item&&c.RecipientLevel==37&&ClassGear.State(item).Roll.ItemLevel==99,"Item, summoner and recipient levels conflated");
        return new(pet.Serial.Value,item.Serial.Value,JsonSerializer.Serialize(ClassGear.State(item)),37,c.Bonus("att"),c.Defense,c.LifeBonus256,c.ManaBonus256,range.Min,range.Max);
    }
    private static Item Named(string key,int seed)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==key).Id,99,new Random(seed),true);
    private static void Check()
    {
        SourceCases();
        var owner=new PlayerMobile{Name="NpcItemSupplierOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);
        while(owner.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Player fixture Strength failed");}
        for(int n=0;n<3;n++){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Holder training failed");}
        FixtureSpace.Place(owner,new(6100,1640,0),Map.Trammel);
        // Real player acquisition/equip/flat-AR pool, independently of the NPC reader.
        var played=new Broadsword();var playedState=ClassGear.Profiles.FromMonster(played,99,GearRarity.Magic,new ExactAffixRandom(ClassGear.Base(played),AffixBalance.Find("MagicPrefix:526")));
        ClassGear.Profiles.Store(played,playedState with{Identified=true});int oldAttack=ClassStats.PlayerAttackBase(owner);owner.AddToBackpack(played);
        Require(owner.EquipItem(played),"Real player weapon equip failed");ClassGear.Refresh(owner);
        Require(ClassGear.Bonus(owner,"att")==775&&ClassGear.Bonus(played,"att")==775&&ClassStats.PlayerAttackBase(owner)-oldAttack==775,"Corrected per-level AR did not reach the actual player attack pool");
        owner.AddToBackpack(played);played.Delete();ClassGear.Refresh(owner);Require(ClassStats.PlayerAttackBase(owner)==oldAttack,"Unequipped per-level AR remained in the player pool");
        var sword=new Broadsword();var selected=AffixBalance.Find("MagicPrefix:526");var state=ClassGear.Profiles.FromMonster(sword,99,GearRarity.Magic,new ExactAffixRandom(ClassGear.Base(sword),selected));
        Require(!state.Identified&&state.Roll.Affixes.Single().Definition.Id==selected.Id,"Actual live ordinary generation skipped");ClassGear.Profiles.Store(sword,state with{Identified=true});
        Item[] items=[sword,Named("Duriel's Shell",43),Named("Spiritforge",44)];var pets=items.Select(_=>Pet(owner)).ToArray();var saved=new Stored[3];
        for(int n=0;n<items.Length;n++)
        {
            var item=items[n];item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
            Require(IronGolemItems.TryAttach(owner,pets[n],item,out var reason),"Original equipment attach failed: "+reason);saved[n]=Capture(pets[n],item);
        }
        // Param33: source to-hit uses halves, so level37 yields610, not152.
        Require(saved[0].Attack==610&&ClassGear.Bonus(sword,"att")==16&&new ItemStatContext(sword,47).Bonus("att")==775,"NPC recipient level/halves versus implicit level1 or owner47");
        Require(saved[2].Life256%256==64&&saved[2].Life256>0,"Named life-per-level quarter hit point lost");
        Require(new ItemStatContext(items[1],1).Defense<saved[1].Defense,"Named recipient-level local armor scaling absent");
        var prior=ClassGear.State(sword);ClassGear.Profiles.Store(sword,prior with{Identified=false});Require(IronGolemItems.Equipment(pets[0],37).Bonus("att")==0,"Unidentified item bonus leaked");ClassGear.Profiles.Store(sword,prior);
        int durability=sword.HitPoints;sword.HitPoints=0;Require(IronGolemItems.Equipment(pets[0],37).WeaponDamage==(0,0)&&IronGolemItems.Equipment(pets[0],37).Bonus("att")==0,"Broken item contributed");sword.HitPoints=durability;
        foreach(int level in new[]{0,100}){bool refused=false;try{_=IronGolemItems.Equipment(pets[0],level);}catch(ArgumentOutOfRangeException){refused=true;}Require(refused,"Unsupported recipient level silently clamped");}
        var pending=Named("Messerschmidt's Reaver",45);var op5=new ItemStatContext(pending,37);Require(op5.Bonus("dmg-max%")==92&&op5.Bonus("dmg%")==200&&!pending.Deleted,"Maximum-only supplier mixed into ordinary ED or consumed original");pending.Delete();
        // Moving the same original must not make the bound reader guess a replacement.
        var first=items[0];first.MoveToWorld(owner.Location,owner.Map);bool moved=false;try{_=IronGolemItems.Equipment(pets[0],37);}catch(NotSupportedException){moved=true;}Require(moved,"Moved original equipment retained a bound supplier");pets[0].AddItem(first);
        owner.AutoStablePets();Require(pets.All(p=>p.ControlMaster==owner&&!p.IsStabled),"Persistent holder entered ordinary pet stabling");
        Require(saved.SequenceEqual(items.Select((item,n)=>Capture(pets[n],item))),"Read/eligibility controls changed original gear or suppliers");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,saved)));foreach(var pet in pets){pet.Internalize();}owner.Internalize();
        Log.Information("NPC item suppliers:27648complete-source op2/op4 contexts/27owned scaled properties;explicit recipient37 versus owner47/item99,half-scaled AR610/live-player AR775,FP256life quarter,original ordinary/named items and separate op5supplier pass;full NPC stat graph/combat remains gated");
    }
    private static void Reload()
    {
        SourceCases();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(World.FindMobile((Serial)saved.Owner) is PlayerMobile owner&&ClassStats.Level(owner)==47,"Saved equipment owner missing");
        foreach(var s in saved.Items)
        {
            var pet=World.FindMobile((Serial)s.Pet) as BaseCreature;var item=World.FindItem((Serial)s.Item);
            Require(pet is{Alive:true,Deleted:false}&&pet.ControlMaster?.Serial.Value==saved.Owner&&item is{Deleted:false}&&Capture(pet,item)==s,"Saved original item/context/numeric suppliers changed");pet.AIObject?.AITimer.Stop();
        }
        Log.Information("NPC item suppliers:original magic sword/two named armors retain complete rolls,explicit recipient37,local defense/weapon endpoints,AR610and quarter-life FP256through actual reload");
    }
}
