// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
namespace Sanctuary;

internal sealed record FrozenResourceInput(int Flat256,int LevelCoefficientRaw);
internal sealed record FrozenCombatBase(GearSlot Slot,string Type,bool Weapon,bool TwoHanded,bool OneOrTwoHanded,
    int StrengthCoefficient,int DexterityCoefficient);
internal sealed record FrozenCombatLevel(int Level,MissileSourceStats Elements,MissileSourceStats Poison,
    FrozenResourceInput Life,FrozenResourceInput Mana,FrozenResourceInput Stamina,
    int OneMinimum,int OneMaximum,int TwoMinimum,int TwoMaximum);
internal sealed record FrozenCombatSnapshot(int Version,FrozenCombatBase Basis,FrozenCombatLevel[] Levels,string Issue=null,
    EquipmentStatValue[] Numeric=null,string NumericIssue=null);

// Capture source suppliers while the actual item still exists. Nothing here
// creates an item, moves loot, rolls acquisition or dispatches a combat event.
// Unsupported projections retain the existing raw gear/property snapshot and
// an explicit issue;they do not fabricate an empty working combat context.
internal static class FrozenEquipmentCombat
{
    internal static FrozenCombatSnapshot Capture(Item item)
    {
        try
        {
            var first=new ItemStatContext(item,1);first.RequireOrdinaryNpcDrainInputs();
            var definition=ClassGear.Base(item);var slot=ClassGear.Slot(item);
            var basis=new FrozenCombatBase(slot,definition.Type,slot==GearSlot.Weapon&&!ClassGear.Broken(item),
                definition.TwoHanded,definition.OneOrTwoHanded,definition.StrengthBonus,definition.DexterityBonus);
            EquipmentStatValue[] numeric=[];string numericIssue=null;
            try{numeric=first.ActiveNumericProperties();}
            catch(Exception e)when(e is NotSupportedException or InvalidDataException or OverflowException)
            {numericIssue="Frozen NPC numeric projection unavailable: "+e.Message;}
            var levels=Enumerable.Range(1,99).Select(level=>
            {
                var context=new ItemStatContext(item,level);
                FrozenResourceInput Resource(string code,int stat)
                {var input=context.ResourceInput(code,stat);return new(input.Flat256,input.LevelCoefficientRaw);}
                var one=ClassGear.Damage(item,level,false,true);var two=ClassGear.Damage(item,level,true,true);
                return new FrozenCombatLevel(level,context.Elements(),context.Poison(),Resource("hp",216),Resource("mana",217),Resource("stam",242),
                    one.Min,one.Max,two.Min,two.Max);
            }).ToArray();
            var result=new FrozenCombatSnapshot(0,basis,levels,Numeric:numeric,NumericIssue:numericIssue);Validate(result);return result;
        }
        catch(Exception e)when(e is NotSupportedException or InvalidDataException or OverflowException)
        {return new(0,null,[],"Frozen NPC combat suppliers unavailable: "+e.Message);}
    }
    internal static void Validate(FrozenCombatSnapshot saved)
    {
        if(saved==null||saved.Version!=0||saved.Levels==null){throw new InvalidDataException("Invalid frozen combat supplier version/payload");}
        if(saved.Issue!=null)
        {
            if(string.IsNullOrWhiteSpace(saved.Issue)||saved.Basis!=null||saved.Levels.Length!=0||saved.Numeric!=null||saved.NumericIssue!=null)
            {throw new InvalidDataException("Unsupported frozen combat contains invented suppliers");}
            return;
        }
        var basis=saved.Basis;
        if(basis==null||!Enum.IsDefined(basis.Slot)||string.IsNullOrWhiteSpace(basis.Type)||basis.Weapon&&basis.Slot!=GearSlot.Weapon
            ||saved.Numeric==null||saved.Numeric.Select(s=>s.Packed).Distinct().Count()!=saved.Numeric.Length||saved.Levels.Length!=99
            ||saved.NumericIssue!=null&&(string.IsNullOrWhiteSpace(saved.NumericIssue)||saved.Numeric.Length!=0))
        {throw new InvalidDataException("Invalid frozen combat base or levels");}
        try{foreach(var stat in saved.Numeric){_=EquipmentStatValues.Definition(stat.Stat);}}
        catch(NotSupportedException e){throw new InvalidDataException("Unknown frozen numeric stat",e);}
        for(int i=0;i<saved.Levels.Length;i++)
        {
            var row=saved.Levels[i];
            if(row==null||row.Level!=i+1||row.Elements==null||row.Poison==null||row.Life==null||row.Mana==null||row.Stamina==null
                ||row.OneMinimum<0||row.OneMaximum<row.OneMinimum||row.TwoMinimum<0||row.TwoMaximum<row.TwoMinimum
                ||!Elements(row.Elements)||!Poison(row.Poison))
            {throw new InvalidDataException("Incomplete frozen combat level");}
        }
    }
    private static bool Elements(MissileSourceStats s)=>s.FireMin>=0&&s.FireMax>=s.FireMin&&s.LightningMin>=0&&s.LightningMax>=s.LightningMin
        &&s.MagicMin>=0&&s.MagicMax>=s.MagicMin&&s.ColdMin>=0&&s.ColdMax>=s.ColdMin&&s.ColdFrames>=0
        &&s==new MissileSourceStats(FireMin:s.FireMin,FireMax:s.FireMax,LightningMin:s.LightningMin,LightningMax:s.LightningMax,
            MagicMin:s.MagicMin,MagicMax:s.MagicMax,ColdMin:s.ColdMin,ColdMax:s.ColdMax,ColdFrames:s.ColdFrames);
    private static bool Poison(MissileSourceStats s)=>s.PoisonMin>=0&&s.PoisonMax>=s.PoisonMin&&s.PoisonFrames>=0&&s.PoisonCount>=0
        &&s==new MissileSourceStats(PoisonMin:s.PoisonMin,PoisonMax:s.PoisonMax,PoisonFrames:s.PoisonFrames,PoisonCount:s.PoisonCount);
}
