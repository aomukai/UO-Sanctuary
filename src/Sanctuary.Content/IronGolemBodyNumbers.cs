// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record IronGolemBodyRecipe(uint Owner,uint Item,string BaseCode,int OwnerLevel,int Rank,
    Dictionary<string,int> Effective,Dictionary<string,int> Hard,int SummonResistance,
    EquipmentStatValue[] ItemBase,EquipmentStatValue[] Properties);
internal sealed record IronGolemBodyNumbers(int Level,int LifeBase256,int CurrentLifeWrite256,int BirthMaximum256,
    int ArmorBase,int AttackBase,int Regeneration256,int Fire,int Cold,int Lightning,int Poison);

// Saved numeric birth recipe, not a source-executed monster/item initializer.
// Capture rejects unrepresented properties. Difficulty rebuilds the original
// birth recipe; the live item's current activation remains a separate layer.
internal static class IronGolemBodyRecipes
{
    internal static EquipmentStatValue[] Fields(params(int Stat,int Value)[] values)=>values.Where(x=>x.Value!=0)
        .Select(x=>new EquipmentStatValue(EquipmentStatKey.Pack(x.Stat),x.Value)).OrderBy(x=>x.Packed).ToArray();
    internal static IronGolemBodyRecipe Capture(PlayerMobile owner,Item item)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)
            ||profile.Class!=DiabloClass.Necromancer||SkillMechanics.Rank(owner,CombatAbility.IronGolem)<1)
        {throw new InvalidOperationException("Iron Golem needs a live learned Necromancer owner");}
        if(!IronGolemItems.Eligible(item,out var issue)||ClassGear.Broken(item)){throw new NotSupportedException(issue??"Broken original-item birth is not integrated");}
        var effective=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);var hard=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in SkillMechanics.All.Where(x=>x.CharacterClass==DiabloClass.Necromancer))
        {effective.Add(row.SourceKey,SkillMechanics.Rank(owner,row.Ability));hard.Add(row.SourceKey,ClassCreation.Profiles.GetRank(owner.Serial,row.TalentId));}
        foreach(string code in new[]{"cold","fire","light","poison","damage","magic"})
        {if(ClassGear.Bonus(owner,"pierce-immunity-"+code)!=0){throw new NotSupportedException("Iron Golem modern sunder delivery is not integrated: "+code);}}
        var original=new EquipmentStatUnit(item,4);original.Stats=new(){Flags=EquipmentStatNode.Extended,OwnerKind=4,Owner=original};
        var values=new EquipmentStatValues(_=>IronGolemItems.Metadata(ClassGear.State(item).BaseCode).Body1,_=>null,_=>null,(_,_,_,_,_)=>{});
        var b=ClassGear.Base(item);bool weapon=ClassGear.Slot(item)==GearSlot.Weapon;
        var basis=new EquipmentPropertyBase(b.MinDamage,b.MaxDamage,b.TwoHandMin,b.TwoHandMax,b.MissileMin,b.MissileMax,b.MinDefense,b.MaxDefense);
        original.Stats.BaseValues=Fields((21,basis.Minimum),(22,basis.Maximum),(23,basis.SecondaryMinimum),(24,basis.SecondaryMaximum),
            (159,basis.ThrowMinimum),(160,basis.ThrowMaximum),(31,weapon?0:ClassGear.State(item).Defense));
        var omissions=EquipmentRawProperties.Project(item,original,values,basis,weapon,!weapon,false);
        if(omissions.Length!=0){throw new NotSupportedException("Unrepresented Iron Golem item properties: "+string.Join(",",omissions.Select(x=>x.Code+": "+x.Reason)));}
        var properties=original.Stats.Last;
        if(properties is{IsExtended:true}||properties?.Previous!=null||properties?.Last!=null)
        {throw new NotSupportedException("Iron Golem needs a single numeric item property list");}
        var recipe=new IronGolemBodyRecipe(owner.Serial.Value,item.Serial.Value,ClassGear.State(item).BaseCode,ClassStats.Level(owner),effective["IronGolem"],
            effective,hard,NecromancerSummons.Resistance(owner),(EquipmentStatValue[])original.Stats.BaseValues.Clone(),
            properties==null?[]:(EquipmentStatValue[])properties.BaseValues.Clone());
        Validate(recipe);return recipe;
    }
    internal static void Validate(IronGolemBodyRecipe recipe)
    {
        if(recipe is null||!((Serial)recipe.Owner).IsMobile||!((Serial)recipe.Item).IsItem||recipe.OwnerLevel is <1 or >99||recipe.Rank is <1 or >255
            ||recipe.Effective==null||recipe.Hard==null||recipe.Effective.GetValueOrDefault("IronGolem")!=recipe.Rank
            ||recipe.Effective.Values.Any(x=>x is <0 or >255)||recipe.Hard.Values.Any(x=>x is <0 or >20)
            ||recipe.SummonResistance is <0 or >75||(IronGolemItems.Metadata(recipe.BaseCode).Flags&2)==0)
        {throw new InvalidDataException("Invalid Iron Golem numeric birth recipe");}
        var keys=SkillMechanics.All.Where(x=>x.CharacterClass==DiabloClass.Necromancer).Select(x=>x.SourceKey).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        if(!recipe.Effective.Keys.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(keys)||!recipe.Hard.Keys.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(keys))
        {throw new InvalidDataException("Incomplete Iron Golem captured rank context");}
        foreach(var list in new[]{recipe.ItemBase,recipe.Properties})
        {
            if(list==null||list.Length>368||!list.Select(x=>x.Packed).SequenceEqual(list.Select(x=>x.Packed).Distinct().OrderBy(x=>x)))
            {throw new InvalidDataException("Invalid Iron Golem item stat arrays");}
            foreach(var stat in list)
            {
                var definition=EquipmentStatValues.Definition(stat.Stat);
                if(definition==null||definition.Operation is 6 or 7)
                {throw new NotSupportedException("Iron Golem item needs an explicit world-time/stat binding");}
            }
        }
    }
    internal static IronGolemBodyNumbers Build(IronGolemBodyRecipe recipe,MonsterDifficulty difficulty)
    {
        Validate(recipe);if(difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell){throw new ArgumentOutOfRangeException(nameof(difficulty));}
        int F(string field)=>NecromancerSummons.Source("irongolem",field,difficulty);
        if(NecromancerSummons.Source("irongolem","noRatio")!=1||NecromancerSummons.Source("irongolem","DamageRegen")!=3||F("minHP")<=0||F("minHP")!=F("maxHP"))
        {throw new NotSupportedException("Unmapped Iron Golem noRatio life service");}
        int life=checked(F("minHP")*256);var actor=new EquipmentStatUnit(new object(),1);var item=new EquipmentStatUnit(new object(),4);
        actor.Stats=new(){Flags=EquipmentStatNode.Extended,OwnerKind=1,Owner=actor,
            BaseValues=Fields((7,life),(6,life),(12,1),(31,F("AC")),(74,checked(life*3)>>12))};
        actor.Stats.FullValues=(EquipmentStatValue[])actor.Stats.BaseValues.Clone();
        item.Stats=new(){Flags=EquipmentStatNode.Extended,OwnerKind=4,Owner=item,BaseValues=(EquipmentStatValue[])recipe.ItemBase.Clone()};
        EquipmentStatValues values=null;
        values=new(_=>IronGolemItems.Metadata(recipe.BaseCode).Body1,_=>null,_=>null,(node,_,key,old,next)=>
        {
            if(node==actor.Stats&&EquipmentStatKey.Stat(key)==7)
            {
                int current=EquipmentStatValues.Total(node,EquipmentStatKey.Pack(6)),regen=EquipmentStatValues.Total(node,EquipmentStatKey.Pack(74));
                values.SetBase(node,EquipmentStatKey.Pack(6),BattleOrders.Rescale256(current,old,next));
                values.SetBase(node,EquipmentStatKey.Pack(74),BattleOrders.RegenerationAfter(old,next,current,3,regen));
            }
        });
        EquipmentStatGraph.Post(item,new(){Flags=0x40,BaseValues=(EquipmentStatValue[])recipe.Properties.Clone()},true,values);
        EquipmentStatGraph.Merge(actor,item,true,values);
        var skill=new SkillValues(SkillMechanics.For(CombatAbility.IronGolem),name=>recipe.Effective.GetValueOrDefault(name),name=>recipe.Hard.GetValueOrDefault(name),recipe.OwnerLevel);
        var birth=IronGolemBirthGraph.Apply(skill,difficulty,true,actor,item,values,_=>0,recipe.SummonResistance);
        int Base(int stat)=>EquipmentStatValues.Base(actor.Stats,EquipmentStatKey.Pack(stat),false);
        int Bonus(string stat)=>birth.Writes.Resistances.SingleOrDefault(x=>x.Name==stat)?.Value??0;
        int percent=checked((int)skill.Eval("calc1"));
        if(birth.LifeBeforePassiveWrite256<=1048576&&percent<=65536
            &&(long)birth.LifeBeforePassiveWrite256*percent is >int.MaxValue or <int.MinValue)
        {throw new NotSupportedException("Iron Golem legacy life multiplication overflows; modern behavior is unverified");}
        if(Base(7)<=0||Base(7)>65000*256||birth.MaximumLife256<=0||birth.MaximumLife256>65000*256
            ||birth.CurrentLife256<=0||birth.CurrentLife256>birth.MaximumLife256)
        {throw new NotSupportedException("Iron Golem birth exceeds the verified live NPC resource domain");}
        return new(birth.Writes.Level,Base(7),birth.CurrentLife256,birth.MaximumLife256,Base(31),Base(19),
            EquipmentStatValues.Total(actor.Stats,EquipmentStatKey.Pack(74)),F("ResFi")+Bonus("fireresist"),F("ResCo")+Bonus("coldresist"),
            F("ResLi")+Bonus("lightresist"),F("ResPo")+Bonus("poisonresist"));
    }
}
