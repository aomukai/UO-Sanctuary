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

public static class OrdinaryPropertyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(OrdinaryPropertyChecks));
    private sealed record Saved(uint Owner,uint[] Items,string[] States,uint Repair,DateTime RepairDue,uint Replenish,NamedQuantity Quantity);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"ordinary-properties-fixtures.json");
    private static bool _creating;
    public static void Configure(){Freeze();Register("ordinary-properties-check",Check);Register("ordinary-properties-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=x=>_creating || x.Name=="OrdinaryPropertyOwner"?TimeSpan.FromHours(1):hp?.Invoke(x)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=x=>_creating || x.Name=="OrdinaryPropertyOwner"?TimeSpan.FromHours(1):mp?.Invoke(x)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Ordinary scalar/range/parameter routing, real rolls, maintenance and saves",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private sealed class ExactAffixRandom:Random
    {
        private readonly int _choice,_ticket,_total;private int _phase;
        public ExactAffixRandom(NormalBase basis,AffixDefinition selected,int level)
        {
            _choice=selected.Kind==AffixKind.Prefix?1:2;
            var options=AffixBalance.All.Where(x=>x.Kind==selected.Kind && ClassGear.Implemented(x) && AffixBalance.Eligible(x,basis.Type,AffixBalance.Level(level,basis.QualityLevel,basis.MagicLevel),GearRarity.Magic)).ToArray();
            int Weight(AffixDefinition x)=>x.Frequency*(basis.MagicLevel>0?x.Level:1);
            _total=options.Sum(Weight);_ticket=options.TakeWhile(x=>x.Id!=selected.Id).Sum(Weight);Require(options.Contains(selected),"Selected affix missing from live pool");
        }
        public override int Next(int max)
        {
            if(_phase++==0){Require(max==4,"Magic quality kind draw");return _choice;}
            Require(_phase==2 && max==_total,"Real weighted affix draw");return _ticket;
        }
        public override int Next(int min,int max){Require(max>min,"Real modifier/base roll bounds");return max-1;}
    }
    private static Item Roll(PlayerMobile owner,string code,Func<Item> factory)
    {
        var item=ItemOrigins.Create(ItemOrigin.Drop,factory);var basis=ClassGear.Base(item);
        int itemLevel=99;AffixDefinition affix=null;
        for(;itemLevel>=1;itemLevel--)
        {affix=AffixBalance.All.FirstOrDefault(x=>x.Modifiers.Any(m=>m.Code==code) && ClassGear.Implemented(x) && AffixBalance.Eligible(x,basis.Type,AffixBalance.Level(itemLevel,basis.QualityLevel,basis.MagicLevel),GearRarity.Magic));if(affix!=null){break;}}
        Require(affix!=null,"No eligible ordinary affix: "+code+" / "+basis.Code+" / "+basis.Type);
        var state=ClassGear.Profiles.FromMonster(item,itemLevel,GearRarity.Magic,new ExactAffixRandom(basis,affix,itemLevel));owner.AddToBackpack(item);
        Require(state.NamedItemId==null && state.NamedProperties.Length==0 && state.Roll.Affixes.Length==1 && state.Roll.Affixes[0].Definition.Id==affix.Id && !state.Identified,"Actual ordinary drop/quality/roll/unidentified path");
        return item;
    }
    private static void Reveal(PlayerMobile owner,Item item)
    {
        var scroll=new IdentifyScroll();owner.AddToBackpack(scroll);Require(Identification.Reveal(owner,scroll,item) && scroll.Deleted && ClassGear.State(item).Identified,"Actual Identify scroll path");
    }
    private static int Value(Item item,string code)=>OrdinaryAffixProperties.FixedValue(ClassGear.State(item),code);
    private static void Equip(PlayerMobile owner,Item item){Require(owner.EquipItem(item),"Equip "+item.GetType().Name);ClassGear.Refresh(owner);}
    private static void Unequip(PlayerMobile owner,Item item){owner.AddToBackpack(item);ClassGear.Refresh(owner);}
    private static void ProjectionCases()
    {
        int count=0;
        foreach(var affix in AffixBalance.All.Where(ClassGear.Implemented))foreach(var mod in affix.Modifiers)
        {
            var property=OrdinaryAffixProperties.Template(mod);var roll=OrdinaryAffixProperties.Projection(mod,mod.Mode==ModifierMode.Numeric?mod.Max:null);
            NamedPropertyRuntime.Validate(property,roll);count++;
        }
        Require(count>500,"Incomplete shared property projection corpus");
        var life=new AffixModifier("lifesteal","",4,5,ModifierMode.Numeric);Require(NamedPropertyRuntime.Bonus(OrdinaryAffixProperties.Template(life),OrdinaryAffixProperties.Projection(life,5),"life-steal%",99)==5,"Scalar alias");
        var poison=new AffixModifier("dmg-pois","50",31,41,ModifierMode.DamageRange);var p=OrdinaryAffixProperties.Projection(poison,null);
        Require(p.Value==31 && p.Maximum==41 && NamedPropertyRuntime.Parameter(OrdinaryAffixProperties.Template(poison))==50,"Fixed poison-rate endpoints and independent source length");
        var scaled=new AffixModifier("hp/lvl","6",0,0,ModifierMode.Parameter);var s=OrdinaryAffixProperties.Projection(scaled,null);
        Require(s.Value==6 && NamedPropertyRuntime.Bonus(OrdinaryAffixProperties.Template(scaled),s,"hp",99)==74,"Parameter/per-level projection");
        bool failed=false;try{OrdinaryAffixProperties.Projection(scaled,0);}catch(InvalidDataException){failed=true;}Require(failed,"Fixed-parameter scalar corruption accepted");
        failed=false;try{OrdinaryAffixProperties.Projection(life,null);}catch(InvalidDataException){failed=true;}Require(failed,"Missing saved scalar accepted");
        Require(AffixBalance.All.Where(a=>a.Modifiers.Any(m=>m.Mode is ModifierMode.Proc or ModifierMode.Charges || m.Code is "skilltab" or "sor" or "sock")).All(a=>!ClassGear.Implemented(a)),"Deferred skill/socket effects became enabled");
        Log.Information("Ordinary property projection: {Count} source modifiers reuse named interpretation; typed rolls and deferred gates pass",count);
    }
    private static void Check()
    {
        ProjectionCases();PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="OrdinaryPropertyOwner",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);
        var items=new List<Item>();Item Make(string code,Func<Item> factory){var item=Roll(owner,code,factory);items.Add(item);return item;}
        var life=Make("lifesteal",()=>new GoldRing());Require(ClassGear.Bonus(life,"life-steal%")==0,"Unidentified scalar bonus active");Reveal(owner,life);Equip(owner,life);
        var mana=Make("manasteal",()=>new Necklace());Reveal(owner,mana);Equip(owner,mana);
        var target=new GraveyardZombie{Name="OrdinaryPropertyTarget",Frozen=true,CantWalk=true,NoKillAwards=true};target.AIObject.AITimer.Stop();target.SetHits(5000);target.Hits=target.HitsMax;
        int drain=GemCombat.Drain(target);Require(drain>0,"Living drain fixture");owner.Hits=Math.Max(1,owner.HitsMax-10);owner.Mana=Math.Max(0,owner.ManaMax-10);int hp=owner.Hits,mp=owner.Mana;
        GemCombat.Leech(owner,target,100);Require(owner.Hits-hp==Value(life,"lifesteal")*drain/100 && owner.Mana-mp==Value(mana,"manasteal")*drain/100,"Ordinary aliases failed to reach real leech consumer");
        Unequip(owner,life);Require(ClassGear.Bonus(owner,"life-steal%")==0,"Unequipped leech active");Unequip(owner,mana);
        var fire=(BaseWeapon)Make("dmg-fire",()=>new Dagger());Reveal(owner,fire);Equip(owner,fire);var fireMod=ClassGear.State(fire).Roll.Affixes[0].Modifiers.Single(m=>m.Code=="dmg-fire");
        var damage=GearEffects.Damage(owner,.5,target);Require(Math.Abs(damage.Fire-(fireMod.Min+fireMod.Max)/2.0)<1e-9,"Ordinary elemental range absent or counted twice");long before=SkillHealth.Balance256(target);GearEffects.Strike(owner,target,.5,new Random(173),wearWeapon:false);Require(SkillHealth.Balance256(target)<before,"Ordinary range did not reach actual weapon damage");
        int maximum=fire.MaxHitPoints;fire.HitPoints=0;ClassGear.Refresh(owner);Require(GearEffects.Damage(owner,.5,target).Fire==0,"Broken ordinary range still active");fire.HitPoints=maximum;ClassGear.Refresh(owner);Unequip(owner,fire);
        var cold=Make("dmg-cold",()=>new Dagger());Reveal(owner,cold);Equip(owner,cold);var coldMod=ClassGear.State(cold).Roll.Affixes[0].Modifiers.Single(m=>m.Code=="dmg-cold");
        Require(NamedGear.ColdDuration(owner)==int.Parse(coldMod.Parameter)*40 && GearEffects.Damage(owner,0,target).Cold==coldMod.Min,"Ordinary cold range/duration consumer");Unequip(owner,cold);
        var poison=Make("dmg-pois",()=>new Dagger());Reveal(owner,poison);Equip(owner,poison);var poisonMod=ClassGear.State(poison).Roll.Affixes[0].Modifiers.Single(m=>m.Code=="dmg-pois");var sources=NamedGear.PoisonSources(owner).ToArray();
        Require(sources.Length==1 && sources[0].Minimum==poisonMod.Min && sources[0].Maximum==poisonMod.Max && sources[0].Frames==int.Parse(poisonMod.Parameter),"Ordinary poison source lost rate/length/count");Unequip(owner,poison);target.Delete();
        long baseLife=ClassResources.Parts(owner,0).Maximum256;var scaled=Make("hp/lvl",()=>new Necklace());Reveal(owner,scaled);Equip(owner,scaled);int parameter=int.Parse(ClassGear.State(scaled).Roll.Affixes[0].Modifiers.Single(m=>m.Code=="hp/lvl").Parameter);
        Require(ClassResources.Parts(owner,0).Maximum256-baseLife==(long)parameter*99*32,"Ordinary fractional per-level life lost FP256 or counted twice");Unequip(owner,scaled);Require(ClassResources.Parts(owner,0).Maximum256==baseLife,"Unequipped scaled life persisted");
        var reduction=Make("red-mag",()=>new GoldRing());Reveal(owner,reduction);Equip(owner,reduction);Require(GemCombat.Reduction(owner)==Value(reduction,"red-mag"),"Ordinary MDR alias did not reach mitigation");Unequip(owner,reduction);
        var repair=(BaseWeapon)Make("rep-dur",()=>new Dagger());Reveal(owner,repair);int rate=NamedMaintenance.Rate(ClassGear.State(repair));Require(rate==int.Parse(ClassGear.State(repair).Roll.Affixes[0].Modifiers.Single().Parameter),"Ordinary repair parameter lost");
        repair.HitPoints=repair.MaxHitPoints-2;var at=TimedEffects.Now;NamedMaintenance.Track(repair,at);NamedMaintenance.Settle(repair,at.AddMilliseconds(2*NamedMaintenance.PeriodMs(rate)));Require(repair.HitPoints==repair.MaxHitPoints,"Ordinary self-repair consumer");
        var durable=(BaseWeapon)Make("indestruct",()=>new Dagger());Require(NamedGear.Indestructible(durable) && !ClassGearHooks.WearAllowed(durable),"Ordinary intrinsic indestructibility/wear hook");Reveal(owner,durable);
        var easy=(BaseWeapon)Make("ease",()=>new Longsword());Reveal(owner,easy);Require(easy.StrRequirement==ClassGear.Base(easy).Strength*(100+Value(easy,"ease"))/100,"Ordinary requirements reduction ignored");
        var replenish=Make("rep-quant",()=>new SoulGlaive());Reveal(owner,replenish);int capacity=NamedAmmunition.Maximum(replenish),qrate=NamedAmmunition.Rate(replenish);Require(qrate>0,"Ordinary replenishing rate");
        at=TimedEffects.Now;Require(NamedAmmunition.Consume(replenish,at),"Ordinary quantity consumption");NamedAmmunition.Settle(replenish,at.AddMilliseconds(NamedMaintenance.PeriodMs(qrate)));Require(NamedAmmunition.Remaining(replenish)==capacity,"Ordinary replenishment consumer");
        var stack=Make("stack",()=>new SoulGlaive());Reveal(owner,stack);Require(NamedAmmunition.Maximum(stack)==NamedAmmunition.Source(ClassGear.State(stack).BaseCode).Maximum+Value(stack,"stack"),"Ordinary source stack bonus");
        Item rare=null;
        for(int seed=0;seed<2048;seed++)
        {
            var candidate=ItemOrigins.Create(ItemOrigin.Drop,()=>new Dagger());var state=ClassGear.Profiles.FromMonster(candidate,99,GearRarity.Rare,new Random(seed));
            if(state.Roll.Affixes.SelectMany(x=>x.Modifiers).Any(m=>m.Code=="dmg-pois")){rare=candidate;owner.AddToBackpack(rare);items.Add(rare);break;}
            candidate.Delete();
        }
        Require(rare!=null,"No real rare poison affix roll in bounded corpus");Reveal(owner,rare);Equip(owner,rare);
        Require(NamedGear.PoisonSources(owner).Count()==ClassGear.State(rare).Roll.Affixes.SelectMany(x=>x.Modifiers).Count(m=>m.Code=="dmg-pois"),"Rare compound properties failed to reach poison consumer exactly once");Unequip(owner,rare);
        foreach(var item in items){GearProfiles.Validate(item,ClassGear.State(item));using var list=new ObjectPropertyList(item);item.GetProperties(list);list.Terminate();}
        repair.HitPoints=repair.MaxHitPoints-1;NamedMaintenance.Clocks[repair.Serial]=TimedEffects.Now.AddHours(1);
        Require(NamedAmmunition.Consume(replenish,TimedEffects.Now),"Saved depleted quantity");var quantity=NamedAmmunition.Quantities[replenish.Serial] with{DueUtc=TimedEffects.Now.AddHours(1)};NamedAmmunition.Quantities[replenish.Serial]=quantity;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,items.Select(x=>x.Serial.Value).ToArray(),items.Select(x=>JsonSerializer.Serialize(ClassGear.State(x))).ToArray(),repair.Serial.Value,NamedMaintenance.Clocks[repair.Serial],replenish.Serial.Value,quantity)));
    }
    private static void Reload()
    {
        ProjectionCases();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;Require(owner!=null,"Saved ordinary owner");
        for(int i=0;i<saved.Items.Length;i++)
        {var item=World.FindItem((Serial)saved.Items[i]);Require(item!=null && item.IsChildOf(owner.Backpack) && JsonSerializer.Serialize(ClassGear.State(item))==saved.States[i],"Ordinary scalar/range/null-parameter exact saved state "+i);GearProfiles.Validate(item,ClassGear.State(item));}
        Require(NamedMaintenance.Clocks[(Serial)saved.Repair]==saved.RepairDue && NamedAmmunition.Quantities[(Serial)saved.Replenish]==saved.Quantity,"Ordinary saved maintenance/quantity clocks");
        var poison=World.FindItem((Serial)saved.Items[4]);Equip(owner,poison);Require(NamedGear.PoisonSources(owner).Any(),"Reloaded ordinary poison route");Unequip(owner,poison);
        var scaled=World.FindItem((Serial)saved.Items[5]);long before=ClassResources.Parts(owner,0).Maximum256;Equip(owner,scaled);Require(ClassResources.Parts(owner,0).Maximum256>before,"Reloaded ordinary scaled resource");Unequip(owner,scaled);
    }
}
