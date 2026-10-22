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

public static class VengeanceChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(VengeanceChecks));
    private sealed record Saved(uint Owner,uint Enemy,uint Weapon,Point3D Origin,int Map,int EnemyLife256,int ManaFraction,uint NormalWeapon=0,int NormalDamage=0);
    private sealed class NoProcRandom:Random{public override int Next(int maxValue)=>maxValue-1;}
    private static string Manifest=>Path.Join(Core.BaseDirectory,"vengeance-checks.json");
    public static void Configure(){Register("vengeance-check",Check);Register("vengeance-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Vengeance melee, resources, source damage and persistence",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int Life(Mobile m)=>m.Hits*256-SkillHealth.Remainder256(m);
    private static void Freeze(Mobile m)
    {
        TimedEffects.Poison(m,m,.000001,60000,TimedEffects.Now);
        // Hold only fixture inspection time; retain the legal persisted poison duration.
        TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==m.Serial.Value?x with{UpdatedUtc=TimedEffects.Now.AddMinutes(5)}:x).ToArray());
    }
    private static void Ready(PlayerMobile owner)
    {var s=ClassCombat.For(owner);ClassCombat.Stop(owner);s.NextAction=s.RecoveryUntil=Core.TickCount;}
    private static void Mana(PlayerMobile owner){owner.Mana=owner.ManaMax;NecromancerEffects.Profiles.Store(owner,new());}
    private static Item Equip(PlayerMobile owner,Item item)
    {ClassGear.Profiles.Convert(item,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(item),"Vengeance real gear equip");return item;}
    private static void Resist(BaseCreature enemy,int fire=0,int cold=100,int lightning=100,int physical=100)
    {
        enemy.SetResistance(ResistanceType.Physical,physical);enemy.SetResistance(ResistanceType.Fire,fire);
        enemy.SetResistance(ResistanceType.Cold,cold);enemy.SetResistance(ResistanceType.Energy,lightning);
    }
    private static void Cast(PlayerMobile owner,Mobile enemy,bool left=true)
    {
        Ready(owner);
        Require(left?ClassCombat.ContinueAttack(owner,CombatAbility.Vengeance,enemy,enemy.Location,Core.TickCount)
            :ClassCombat.ContinueCast(owner,CombatAbility.Vengeance,enemy,enemy.Location,Core.TickCount),"Vengeance held input begin");
        ClassCombat.ReleaseCast(owner);Require(ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt),"Vengeance melee event");
    }
    private static void Probe()
    {
        string root=Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT") ?? throw new InvalidOperationException("Missing project root for independent source probe");
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(root,"tools/reference/vengeance-engine-evidence.json")));
        foreach(var row in json.RootElement.GetProperty("probe").GetProperty("data").GetProperty("elementCases").EnumerateArray())
        {
            if(row[0].GetInt32()==0){continue;}
            int basis=(row[1].GetInt32()==1?50:10)*256+row[3].GetInt32(),mastery=row[2].GetInt32();
            Require(PaladinVengeance.Scale(basis,70,mastery)==row[7].GetInt32()
                && PaladinVengeance.Scale(basis,80,mastery)==row[8].GetInt32()
                && PaladinVengeance.Scale(basis,90,mastery)==row[9].GetInt32(),"Native elemental arithmetic differs from compiled original routine");
        }
        var one=new SkillValues(SkillMechanics.For(CombatAbility.Vengeance),_=>1,_=>0,99);
        var twenty=new SkillValues(SkillMechanics.For(CombatAbility.Vengeance),_=>20,_=>20,99);
        Require(one.Mana256==1024 && one.Eval("calc1")==70 && one.DurationFrames==30 && one.Formula("toht")==20
            && twenty.Mana256==2240 && twenty.Eval("calc1")==424 && twenty.Eval("calc2")==424
            && twenty.Eval("calc3")==424 && twenty.DurationFrames==315 && twenty.Formula("toht")==210,"Pinned Vengeance goldens");
    }
    private static void Timing(PlayerMobile owner)
    {
        var w=CombatTiming.Weapon(CombatTiming.Equipped(owner));var plain=CombatTiming.Calculate(DiabloClass.Paladin,CombatAbility.Vengeance,w);
        Require(plain==CombatTiming.Calculate(DiabloClass.Paladin,CombatAbility.BasicAttack,w)
            && plain==CombatTiming.Calculate(DiabloClass.Paladin,CombatAbility.Vengeance,w,fcr:100)
            && CombatTiming.Calculate(DiabloClass.Paladin,CombatAbility.Vengeance,w,ias:100).Frames<plain.Frames
            && CombatTiming.Calculate(DiabloClass.Paladin,CombatAbility.Vengeance,w,skillAttackRate:30).Frames<plain.Frames
            && CombatTiming.Range(owner,CombatAbility.Vengeance)==CombatTiming.Range(owner,CombatAbility.BasicAttack),"Vengeance used casting timing/range or ignored IAS/Fanaticism");
    }
    private static void RankAndBase(PlayerMobile owner,BaseCreature enemy)
    {
        var values=SkillMechanics.Values(owner,CombatAbility.Vengeance);var original=PaladinVengeance.Elements(owner,values);
        int str=owner.RawStr;owner.RawStr+=100;Require(PaladinVengeance.Elements(owner,values)==original,"Strength scaled elemental weapon base");owner.RawStr=str;
        Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.Might),"Might source for Vengeance exclusion");
        int basis=Math.Max(1,5*owner.Dex-35+ClassStats.For(DiabloClass.Paladin).AttackBonus+ClassGear.Bonus(owner,"att"));
        int ordinary=AosAttributes.GetValue(owner,AosAttribute.AttackChance)+(SorceressEffects.Enchantment(owner)?.AttackPercent ?? 0)+PaladinAuras.AttackPercent(owner);
        Require(ClassStats.AttackRating(owner,20)==Math.Max(1,basis*(100+ordinary+20)/100),"Skill attack rating multiplied existing percentages");
        Require(PaladinVengeance.Elements(owner,values)==original,"Might scaled elemental weapon base");SkillBindings.Instance.Assign(owner,false,CombatAbility.None);
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
        Require(owner.EquipItem(helm),"Vengeance soft rank gear");var soft=SkillMechanics.Values(owner,CombatAbility.Vengeance);
        Require(soft.Rank==2 && soft.Eval("calc1")==88 && soft.Eval("calc2")==88 && soft.Mana256==1088,"Soft points supplied hard synergies or wrong Vengeance mana");
        Mana(owner);int balance=NecromancerEffects.ManaBalance256(owner);Cast(owner,enemy);
        Require(balance-NecromancerEffects.ManaBalance256(owner)==1088 && NecromancerEffects.Profiles.Find(owner).ManaFraction==192,"Actual soft-rank fractional Vengeance payment");helm.Delete();
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.resistfire",out _),"Train hard fire synergy");values=SkillMechanics.Values(owner,CombatAbility.Vengeance);
        Require(values.Eval("calc1")==92 && values.Eval("calc2")==82 && values.Eval("calc3")==82,"Elemental hard synergies were combined incorrectly");
        var veil=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Nightwing's Veil").Id,99,new Random(173),true);
        Require(owner.EquipItem(veil),"Vengeance actual mastery gear");values=SkillMechanics.Values(owner,CombatAbility.Vengeance);
        var boosted=PaladinVengeance.Elements(owner,values);int mastery=NamedElements.MasteryPercent(owner,"cold");
        Require(mastery>0 && boosted.Cold256==PaladinVengeance.Scale(boosted.Base256,(int)values.Eval("calc2"),mastery),"Elemental item mastery did not modify source percentage");
        Resist(enemy,fire:100,cold:0);int hp=Life(enemy);Mana(owner);Cast(owner,enemy);
        Require(hp-Life(enemy)==boosted.Cold256,"Actual Vengeance applied item mastery twice or lost its rounded bonus");veil.Delete();
        var oldRoll=PaladinVengeance.Roll;PaladinVengeance.Roll=n=>n-1;
        var upper=PaladinVengeance.Elements(owner,SkillMechanics.Values(owner,CombatAbility.Vengeance));var range=ClassStats.WeaponBaseDamage(owner);
        Require(upper.Base256==Math.Max(Math.Max(2,range.Max)*256,Math.Max(1,range.Min)*256+256)-1,"Elemental roll included its upper endpoint");PaladinVengeance.Roll=oldRoll;
    }
    private static void Gates(PlayerMobile owner,BaseCreature enemy,Item weapon)
    {
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.Vengeance) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Vengeance)
            && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Vengeance),"Vengeance mouse/quick bindings");
        Ready(owner);owner.Mana=0;NecromancerEffects.Profiles.Store(owner,new());
        Require(!ClassCombat.Perform(owner,CombatAbility.Vengeance,enemy,enemy.Location,Core.TickCount),"Vengeance started unpaid");Mana(owner);
        var origin=enemy.Location;enemy.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);Ready(owner);
        Require(!ClassCombat.Perform(owner,CombatAbility.Vengeance,enemy,enemy.Location,Core.TickCount),"Vengeance used spell reach");enemy.MoveToWorld(origin,owner.Map);
        Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner);
        Require(ClassCombat.ContinueAttack(owner,CombatAbility.Vengeance,enemy,enemy.Location,Core.TickCount),"Vengeance attack windup");ClassCombat.ReleaseCast(owner);
        var s=ClassCombat.For(owner);Require(s.Animation==ClassCombat.AnimationFor(owner,CombatAbility.Vengeance,CombatTiming.Equipped(owner))
            && NecromancerEffects.ManaBalance256(owner)==mana,"Vengeance cast animation or early debit");
        long cooldown=s.NextAction;owner.Backpack.DropItem(weapon);
        Require(!ClassCombat.ResolveAction(owner,s.EffectAt) && NecromancerEffects.ManaBalance256(owner)==mana && s.NextAction==cooldown,"Weapon change consumed mana or refunded action");
        Require(owner.EquipItem(weapon),"Vengeance re-equip weapon");
        var bow=new Bow();owner.Backpack.DropItem(weapon);Equip(owner,bow);Require(!PaladinVengeance.WeaponAllowed(owner),"Vengeance accepted a bow");bow.Delete();Require(owner.EquipItem(weapon),"Vengeance restore sword");
    }
    private static void Damage(PlayerMobile owner,BaseCreature enemy)
    {
        Resist(enemy);var values=SkillMechanics.Values(owner,CombatAbility.Vengeance);var extra=PaladinVengeance.Elements(owner,values);
        int hp=Life(enemy),mana=NecromancerEffects.ManaBalance256(owner);Cast(owner,enemy);
        Require(hp-Life(enemy)==extra.Fire256 && mana-NecromancerEffects.ManaBalance256(owner)==values.Mana256,"Actual fire-only Vengeance damage/payment differs from FP256 bonus");
        hp=Life(enemy);mana=NecromancerEffects.ManaBalance256(owner);PaladinVengeance.HitRoll=()=>1;
        int rolls=0;var roll=PaladinVengeance.Roll;PaladinVengeance.Roll=n=>{rolls++;return 0;};
        try{Cast(owner,enemy,false);Require(Life(enemy)==hp && rolls==0 && mana-NecromancerEffects.ManaBalance256(owner)==values.Mana256,"Miss damaged/rolled elements or avoided payment");}
        finally{PaladinVengeance.HitRoll=()=>0;PaladinVengeance.Roll=roll;}
        Resist(enemy,fire:50,cold:0,lightning:100);hp=Life(enemy);Cast(owner,enemy);
        Require(hp-Life(enemy)==(int)Math.Floor(extra.Fire256*.5+extra.Cold256) && GearEffects.ChillPercent(enemy)>0,"Vengeance channel resistance or cold duration missing");
        Resist(enemy);hp=Life(enemy);Cast(owner,enemy);Require(hp-Life(enemy)==extra.Fire256,"Repeated Vengeance fractional damage mismatch");
    }
    private static void Shield(PlayerMobile owner)
    {
        int str=owner.RawStr;owner.RawStr=25;
        var mage=ClassSkillChecks.Player(DiabloClass.Sorceress);mage.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);Freeze(mage);Mana(mage);
        var values=SkillMechanics.Values(mage,CombatAbility.EnergyShield);SorceressEffects.CastShield(mage,values);
        int mana=NecromancerEffects.ManaBalance256(mage),hp=Life(mage);var extra=PaladinVengeance.Elements(owner,SkillMechanics.Values(owner,CombatAbility.Vengeance));
        GearEffects.Strike(owner,mage,0,skillElements:extra.Damage);int shielded=hp-Life(mage);
        Require(NecromancerEffects.ManaBalance256(mage)<mana && shielded>0,"Vengeance bypassed Energy Shield");
        SorceressEffects.Profiles.Store(mage,new());mage.Hits=mage.HitsMax;SkillHealth.Clear(mage);hp=Life(mage);GearEffects.Strike(owner,mage,0,skillElements:extra.Damage);
        Require(hp-Life(mage)>shielded,"Energy Shield did not reduce actual Vengeance life loss");
        SorceressEffects.CastShield(mage,values);Mana(mage);mage.Hits=mage.HitsMax;mana=NecromancerEffects.ManaBalance256(mage);
        GearEffects.Strike(owner,mage,0);Require(NecromancerEffects.ManaBalance256(mage)<mana,"Ordinary shared weapon packet bypassed Energy Shield");
        mage.Delete();owner.RawStr=str;
    }
    private static void Block(PlayerMobile owner)
    {
        var defender=ClassSkillChecks.Player(DiabloClass.Paladin);defender.AddItem(new Backpack());defender.RawStr=1000;defender.RawDex=10000;
        defender.MoveToWorld(new(owner.X+1,owner.Y+1,owner.Z),owner.Map);Equip(defender,new WoodenKiteShield());Ready(defender);
        Require(ClassGear.BlockChance(defender)==75,"Vengeance block fixture chance");int hp=Life(defender),rolls=0;var roll=PaladinVengeance.Roll;
        try
        {
            PaladinVengeance.Roll=n=>{rolls++;return 0;};PaladinVengeance.Strike(owner,defender,SkillMechanics.Values(owner,CombatAbility.Vengeance));
            Require(Life(defender)==hp && rolls==0 && ClassCombat.For(defender).Animation==20,"Blocked Vengeance dealt/rolled elemental damage or skipped normal block");
        }
        finally{PaladinVengeance.Roll=roll;defender.Delete();}
    }
    private static void TwoHanded(PlayerMobile owner,Item sword)
    {
        owner.Backpack.DropItem(sword);var polearm=Equip(owner,new Halberd());var raw=ClassStats.WeaponBaseDamage(owner);
        var elements=PaladinVengeance.Elements(owner,SkillMechanics.Values(owner,CombatAbility.Vengeance));
        Require(PaladinVengeance.WeaponAllowed(owner) && elements.Base256==Math.Max(1,raw.Min)*256 && CombatTiming.Range(owner,CombatAbility.Vengeance)>=2,
            "Two-handed Vengeance lost its equipped base or weapon reach");polearm.Delete();Require(owner.EquipItem(sword),"Restore Vengeance sword after polearm");
    }
    private static void Controlled(Action action)
    {
        var roll=PaladinVengeance.Roll;var hit=PaladinVengeance.HitRoll;var physical=PaladinVengeance.PhysicalRoll;
        try{PaladinVengeance.Roll=_=>0;PaladinVengeance.HitRoll=()=>0;PaladinVengeance.PhysicalRoll=()=>0;action();}
        finally{PaladinVengeance.Roll=roll;PaladinVengeance.HitRoll=hit;PaladinVengeance.PhysicalRoll=physical;}
    }
    private static BaseWeapon NormalDamage(PlayerMobile owner,BaseWeapon stone=null)
    {
        stone??=(BaseWeapon)NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Stone Crusher").Id,99,new Random(173),true,"HammerPick");
        var previous=CombatTiming.Equipped(owner);owner.AddToBackpack(previous);Require(owner.EquipItem(stone),"Vengeance actual Stone Crusher equip");
        var enemy=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.SetHits(30000);enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);enemy.AIObject.AITimer.Stop();Freeze(enemy);
        var source=ClassGear.State(stone);var definition=NamedGear.Catalog.Item(source.NamedItemId);
        var property=definition.Properties.Single(x=>x.Code=="dmg");string key="p/"+property.Slot;
        VengeanceElements? first=null;double firstPhysical=0;
        try
        {
            foreach(int normal in new[]{10,30})
            {
                // Vary only the legal Damage+N roll, retaining this real item's ED/Strength/base.
                ClassGear.Profiles.Store(stone,source with{NamedProperties=source.NamedProperties.Select(x=>x.Key==key?new NamedPropertyRoll(key,normal,normal):x).ToArray()});
                Require(normal>=property.SourceMin && normal<=property.SourceMax && ClassGear.Bonus(owner,"normal-damage")==normal,"Vengeance fixture escaped source normal-damage range");
                var basis=ClassGear.Base(stone);int ed=ClassGear.Bonus(stone,"dmg%");
                int minimum=basis.MinDamage*(100+ed)/100,maximum=basis.MaxDamage*(100+ed)/100;
                var values=SkillMechanics.Values(owner,CombatAbility.Vengeance);var elements=PaladinVengeance.Elements(owner,values);
                Require(elements.Base256==minimum*256 && (first==null || elements==first),"Damage+N leaked into Vengeance elemental base/channels");
                var roll=PaladinVengeance.Roll;try{PaladinVengeance.Roll=n=>n-1;Require(PaladinVengeance.Elements(owner,values).Base256==maximum*256-1,"Damage+N leaked into upper elemental endpoint");}finally{PaladinVengeance.Roll=roll;}
                int hp=Life(enemy);Resist(enemy);Mana(owner);Cast(owner,enemy);
                Require(hp-Life(enemy)==elements.Fire256,"Actual elemental-only Vengeance included Damage+N");
                Resist(enemy,fire:100,physical:0);hp=Life(enemy);double physical=GearEffects.Damage(owner,0).Physical;
                GearEffects.Strike(owner,enemy,0,new NoProcRandom(),skillElements:elements.Damage);
                Require(hp-Life(enemy)==NamedElements.To256(physical) && (first==null || physical>firstPhysical),"Vengeance shared physical packet lost Damage+N or duplicated it");
                _log.Information("Vengeance normal-damage differential: normal={Normal}, base256={Base}, fire256={Fire}, physical={Physical}",normal,elements.Base256,elements.Fire256,physical);
                first??=elements;firstPhysical=physical;
            }
        }
        finally{enemy.Delete();owner.AddToBackpack(stone);Require(owner.EquipItem(previous),"Restore Vengeance ordinary weapon");}
        return stone;
    }
    private static void Check()=>Controlled(()=>
    {
        Probe();var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.AddItem(new Backpack());owner.RawStr=1000;owner.RawDex=150;owner.RawInt=1000;
        owner.MoveToWorld(ClassSkillChecks.Corridor(owner,5800,1800),owner.Map);Freeze(owner);var weapon=Equip(owner,new Longsword());
        var enemy=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.SetHits(30000);enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);enemy.AIObject.AITimer.Stop();enemy.Hits-=100;Freeze(enemy);
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.Vengeance),"Initial Vengeance attack binding");
        Timing(owner);RankAndBase(owner,enemy);Gates(owner,enemy,weapon);Damage(owner,enemy);Shield(owner);Block(owner);TwoHanded(owner,weapon);
        var normalWeapon=NormalDamage(owner);
        SkillBindings.Instance.Assign(owner,true,CombatAbility.Vengeance);SkillBindings.Instance.Assign(owner,false,CombatAbility.Vengeance);
        NecromancerEffects.Profiles.Store(owner,(NecromancerEffects.Profiles.Find(owner) ?? new()) with{ManaFraction=192});owner.Mana=100;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,enemy.Serial.Value,weapon.Serial.Value,owner.Location,owner.Map.MapID,Life(enemy),192,normalWeapon.Serial.Value,ClassGear.Bonus(normalWeapon,"normal-damage"))));
    });
    private static void Reload()=>Controlled(()=>
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var enemy=(BaseCreature)World.FindMobile((Serial)saved.Enemy);var weapon=World.FindItem((Serial)saved.Weapon);
        owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);owner.RawStr=1000;owner.RawDex=150;owner.RawInt=1000;Freeze(owner);
        enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);enemy.Frozen=enemy.CantWalk=true;enemy.AIObject.AITimer.Stop();Freeze(enemy);
        Require(weapon.Parent==owner && CombatTiming.Equipped(owner)?.Serial==weapon.Serial && SkillMechanics.Rank(owner,CombatAbility.Vengeance)==1
            && ClassCreation.Profiles.GetRank(owner.Serial,"paladin.resistfire")==2,"Vengeance real weapon/ranks lost on reload");
        Require(SkillBindings.Instance.Get(owner)==(CombatAbility.Vengeance,CombatAbility.Vengeance) && SkillBindings.Instance.GetQuick(owner.Serial,DiabloClass.Paladin,0)==CombatAbility.Vengeance,"Vengeance bindings lost on reload");
        Require(Life(enemy)==saved.EnemyLife256 && NecromancerEffects.Profiles.Find(owner).ManaFraction==saved.ManaFraction,"Vengeance fractional target health/mana lost on reload");
        Timing(owner);Resist(enemy);Mana(owner);Damage(owner,enemy);
        var normalWeapon=(BaseWeapon)World.FindItem((Serial)saved.NormalWeapon);
        Require(normalWeapon?.RootParent==owner && ClassGear.Bonus(normalWeapon,"normal-damage")==saved.NormalDamage && saved.NormalDamage==30,"Saved Vengeance normal-damage weapon/roll lost");
        NormalDamage(owner,normalWeapon);
        owner.Mana=100;NecromancerEffects.Profiles.Store(owner,(NecromancerEffects.Profiles.Find(owner) ?? new()) with{ManaFraction=saved.ManaFraction});
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{EnemyLife256=Life(enemy)}));
    });
}
