// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class DragonTalonClockChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DragonTalonClockChecks));
    private sealed record Saved(uint Owner,uint Primary,uint Secondary,uint Helm,int Mana256,int Life256,string SecondaryState,DragonTalonTimingPlan Plan,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dragon-talon-clock-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {
        var mana=Mobile.ManaRegenRateHandler;var hits=Mobile.HitsRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>Creating||p.Name=="TalonClockFixture"?FixtureSpace.Retention:mana?.Invoke(p)??Mobile.DefaultManaRate;
        Mobile.HitsRegenRateHandler=p=>Creating||p.Name=="TalonClockFixture"?FixtureSpace.Retention:hits?.Invoke(p)??Mobile.DefaultHitsRate;
    }
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"dragon-talon-clock-cases.json")));
        int rates=0,plans=0;
        foreach(var r in doc.RootElement.GetProperty("rates").EnumerateArray())
        {
            int ias=r[0].GetInt32(),wsm=r[1].GetInt32(),sias=r[2].GetInt32();
            Require(DragonTalonTiming.Advance(wsm,ias,Math.Max(0,-sias),Math.Max(0,sias))==r[6].GetInt32(),"Original Talon primary rate: "+rates);rates++;
        }
        foreach(var r in doc.RootElement.GetProperty("plans").EnumerateArray())
        {
            var plan=DragonTalonTiming.Plan(r[0].GetInt32(),r[1].GetInt32());
            Require(plan.Profile==DragonTalonTiming.Profile&&plan.Contacts.SequenceEqual(r[2].EnumerateArray().Select(x=>x.GetInt32()))
                &&plan.EndFrame==r[3].GetInt32()&&plan.EndFrame>plan.Contacts.Last(),"Prepared Talon rank plan: "+plans);plans++;
        }
        Require(rates==490&&plans==3315,"Incomplete Talon calculator fixtures");
        var ordinary=DragonTalonTiming.Plan(20,256);
        Require(ordinary.Contacts.SequenceEqual(new[]{4,8,12,16})&&ordinary.EndFrame==24,"Rank20 baseline Talon clock");
        ordinary.Contacts[0]=999;
        Require(DragonTalonTiming.Plan(20,256).Contacts[0]==4,"Prepared Talon plan array alias");
        foreach(int rank in new[]{0,256})
        {bool refused=false;try{DragonTalonTiming.Plan(rank,256);}catch(ArgumentOutOfRangeException){refused=true;}Require(refused,"Invalid Talon rank admitted");}
        foreach(int advance in new[]{0,37,449})
        {bool refused=false;try{DragonTalonTiming.Plan(1,advance);}catch(NotSupportedException){refused=true;}Require(refused,"Unknown Talon rate admitted");}
        Require(!AssassinCombat.Implemented(CombatAbility.DragonTalon),"Prepared timing exposed learned Talon");
        Log.Information("Talon prepared clock:490original-author primary-rate controls/3315rank extensions/10owned KK profiles;rank20at256 contacts4,8,12,16/end24;no input/charge/payment/engine-parity claim");
    }
    private static void Check()
    {
        Differential();PlayerMobile p;Creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Assassin);}finally{Creating=false;}
        p.Name="TalonClockFixture";p.AddItem(new Backpack());ClassProfessions.Profiles.Select(p,SkillName.Alchemy);
        while(p.RawStr<170){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Talon clock strength");}
        while(p.RawDex<130){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Talon clock dexterity");}
        ClassSkillChecks.Train(p,"assassin.dragontalon",5);
        var one=DragonTalonTiming.Plan(p);Require(one.Rank==5&&one.Contacts.Length==1,"Talon hard rank5 count");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);
        Require(p.EquipItem(helm),"Actual Talon soft-rank helmet");var two=DragonTalonTiming.Plan(p);
        Require(two.Rank==6&&two.Contacts.Length==2&&one.Contacts.Length==1,"Soft rank6 failed kick-count threshold or rewrote plan");p.AddToBackpack(helm);
        var primary=new Tekagi();ClassGear.Profiles.Convert(primary,99,GearRarity.Normal,new Random(173));p.AddToBackpack(primary);Require(p.EquipItem(primary),"Talon primary claw");
        int before=DragonTalonTiming.Advance(p);
        // A complete existing socket/rune route supplies an actual IAS claw.
        // Do not trim Firelizard's currently unsupported skill properties.
        var secondary=(BaseWeapon)RuneSocketChecks.Host(p,typeof(Tekagi),1);var rune=RuneSocketChecks.Rune(p,"attack-speed");int ias=rune.Percentage;
        var quote=RuneSockets.Quote(p,rune,secondary);Require(quote!=null&&RuneSockets.Commit(quote)&&rune.Deleted,"Actual offhand IAS rune insertion");
        Require(ClassGear.Bonus(secondary,"swing1")==ias&&ias>0&&p.EquipItem(secondary),"Equip complete IAS offhand");
        Require(WeaponHands.Pair(p) is{} pair&&pair.Primary==primary&&pair.Secondary==secondary&&DragonTalonTiming.Advance(p)==before,"Offhand IAS altered primary Talon projection");
        using(WeaponHands.Use(p,secondary))
        {Require(CombatTiming.Ias(p)==ias&&DragonTalonTiming.Advance(p)==before&&CombatTiming.Equipped(p)==secondary,"Prepared Talon failed primary selection/restoration inside secondary scope");}
        Require(CombatTiming.Equipped(p)==primary,"Talon clock leaked selected hand");
        NecromancerEffects.SetMana256(p,20*256+31);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-257);
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(p),durability=primary.HitPoints,otherDurability=secondary.HitPoints;
        var plan=DragonTalonTiming.Plan(p);
        Require(!ClassCombat.Available(p,CombatAbility.DragonTalon)&&!SkillBindings.Instance.Assign(p,false,CombatAbility.DragonTalon)
            &&!ClassCombat.Perform(p,CombatAbility.DragonTalon,null,p.Location,Core.TickCount),"Prepared Talon timing enabled active input");
        Require(NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(p)==life&&primary.HitPoints==durability&&secondary.HitPoints==otherDurability,"Prepared clock paid/healed/wore actual items");
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,primary.Serial.Value,secondary.Serial.Value,helm.Serial.Value,mana,life,Json(ClassGear.State(secondary)),plan)));
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(WeaponHands.Pair(p) is{} pair&&pair.Primary.Serial.Value==saved.Primary&&pair.Secondary.Serial.Value==saved.Secondary
            &&World.FindItem((Serial)saved.Helm)?.Parent==p.Backpack,"Saved original Talon clock equipment identity");
        Require(Json(ClassGear.State(World.FindItem((Serial)saved.Secondary)))==saved.SecondaryState,"Saved original IAS socket/roll/content changed");
        Require(Json(DragonTalonTiming.Plan(p))==Json(saved.Plan)&&NecromancerEffects.ManaBalance256(p)==saved.Mana256&&SkillHealth.Balance256(p)==saved.Life256,"Saved clock context/resources changed");
        File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
    }
    public static void Configure()
    {
        Rates();foreach(string name in new[]{"dragon-talon-clock-check","dragon-talon-clock-reload-check"})
        ConsoleInputHandler.RegisterCommand(name,"Prepared Talon calculator clock/actual gear; no learned handler",_=>Core.LoopContext.Post(()=>
        {try{if(name.EndsWith("-reload-check",StringComparison.Ordinal)){Reload();}else{Check();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
}
