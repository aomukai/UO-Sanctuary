// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Sanctuary;
internal sealed record EquipmentPropertyStep(int Function,int Stat,bool Set,int Value);
internal sealed record EquipmentPropertyPlan(string Code,int Ordinal,bool Supported,EquipmentPropertyStep[] Steps);
internal readonly record struct EquipmentPropertyInput(int Minimum,int Maximum,int Parameter);
internal readonly record struct EquipmentPropertyBase(int Minimum,int Maximum,int SecondaryMinimum,int SecondaryMaximum,int ThrowMinimum,int ThrowMaximum,int MinimumArmor,int MaximumArmor);
internal abstract class EquipmentPropertyServices
{
    internal abstract EquipmentPropertyBase Basis{get;}
    internal abstract bool Weapon{get;}
    internal abstract bool Armor{get;}
    internal abstract bool Throwable{get;}
    internal abstract int BaseValue(int stat);
    internal abstract void SetBase(int stat,int value);
    internal abstract bool GetOrCreateList(int state,int flags);
    internal abstract int ListValue(int stat,int layer);
    internal abstract void SetList(int stat,int value,int layer);
    internal abstract void AddList(int stat,int value,int layer);
    internal abstract int Roll(int bound);
    internal abstract void SetEthereal();
}

// Writes a declared numeric property to an original-item context. It preserves
// source base reset/first-result sharing; it does not create or reroll an item.
internal static class EquipmentPropertyWriter
{
    private static readonly HashSet<int> Functions=[1,2,3,4,5,6,7,8,15,16,17];
    private static readonly Lazy<IReadOnlyDictionary<string,EquipmentPropertyPlan>> Plans=new(()=>
    {
        var rows=BalanceCatalog.Read<EquipmentPropertyPlan>("equipment-property-plans");
        if(rows.Length!=284||rows.Select(r=>r.Code).Distinct(StringComparer.Ordinal).Count()!=284||rows.Select(r=>r.Ordinal).Distinct().Count()!=284||rows.Count(r=>r.Supported)!=191
            ||rows.Any(r=>r.Ordinal is <0 or >284||r.Ordinal==121||r.Steps.Length>7||r.Steps.Any(s=>s.Stat is < -1 or >367)||r.Supported!=(r.Steps.Length>0&&r.Steps.All(s=>Functions.Contains(s.Function)))))
        {throw new InvalidDataException("Invalid source equipment property plans");}
        return rows.ToDictionary(r=>r.Code,StringComparer.Ordinal);
    });
    internal static EquipmentPropertyPlan Plan(string code)=>Plans.Value.GetValueOrDefault(code);
    internal static void ResetBase(EquipmentPropertyServices s,int stat)
    {
        var b=s.Basis;
        switch(stat)
        {
            case 16:case 31:
                if(s.Armor&&b.MaximumArmor!=0){s.SetBase(31,Math.Max(checked(s.BaseValue(31)+1),checked(b.MaximumArmor+1)));}break;
            case 17:case 22:
                if(s.Weapon)
                {if(b.Maximum!=0){s.SetBase(22,b.Maximum);}if(b.SecondaryMaximum!=0){s.SetBase(24,b.SecondaryMaximum);}if(s.Throwable&&b.ThrowMaximum!=0){s.SetBase(160,b.ThrowMaximum);}}break;
            case 18:case 21:
                if(s.Weapon)
                {if(b.Minimum!=0){s.SetBase(21,b.Minimum);}if(b.SecondaryMinimum!=0){s.SetBase(23,b.SecondaryMinimum);}if(s.Throwable&&b.ThrowMinimum!=0){s.SetBase(159,b.ThrowMinimum);}}break;
        }
    }
    internal static void Ethereality(EquipmentPropertyServices s)
    {
        s.SetEthereal();
        foreach(int stat in s.Weapon?new[]{21,22,23,24,159,160}:new[]{31}){s.SetBase(stat,checked(3*s.BaseValue(stat))/2);}
    }
    internal static int Rolled(EquipmentPropertyInput p,EquipmentPropertyServices s)
    {
        int min=Math.Min(p.Minimum,p.Maximum),max=Math.Max(p.Minimum,p.Maximum);
        return min==max?min:checked(min+s.Roll(checked(max-min+1)));
    }
    internal static int Write(EquipmentPropertyServices s,EquipmentPropertyInput p,bool set,int stat,int layer,int value,int state,int flags)
    {
        if(value==0||EquipmentStatValues.Definition(stat) is not{} cost){return 0;}
        if(!s.GetOrCreateList(state,flags)){return 0;}
        int shifted=checked(value*(1<<cost.Shift));
        if(set)
        {s.SetList(stat,shifted,layer);if(stat==58&&s.ListValue(326,0)==0){s.SetList(326,1,0);}}
        else
        {s.AddList(stat,shifted,layer);if(stat==58){s.AddList(326,1,0);}}
        return value;
    }
    private static int FlatDamage(EquipmentPropertyServices s,EquipmentPropertyInput p,bool set,bool maximum,int first,int state,int flags)
    {
        int value=first!=0?first:Rolled(p,s);var b=s.Basis;
        void Channel(int stat,int basis)
        {
            int n=value;if(basis!=0&&checked(basis+value)<=0){n=maximum?-basis:1-basis;}
            if(n!=0){Write(s,p,set,stat,0,n,state,flags);}
        }
        int primary=maximum?b.Maximum:b.Minimum,secondary=maximum?b.SecondaryMaximum:b.SecondaryMinimum;
        if(!s.Weapon||primary!=0||secondary==0){Channel(maximum?22:21,primary);}
        if(!s.Weapon||secondary!=0||primary==0){Channel(maximum?24:23,secondary);}
        if(!s.Weapon||s.Throwable){Channel(maximum?160:159,maximum?b.ThrowMaximum:b.ThrowMinimum);}
        return value;
    }
    private static int ApplyStep(EquipmentPropertyServices s,EquipmentPropertyInput p,int mode,EquipmentPropertyStep step,int first,int state,int flags)
    {
        int stat=step.Stat;bool set=step.Set;
        switch(step.Function)
        {
            case 1:case 2:case 3:case 4:case 8:
                if(step.Function is 2 or 4||mode==1&&step.Function is 1 or 3){ResetBase(s,stat);}
                return Write(s,p,set,stat,0,step.Function is 1 or 2?Rolled(p,s):first!=0?first:Rolled(p,s),state,flags);
            case 5:return FlatDamage(s,p,set,false,first,state,flags);
            case 6:return FlatDamage(s,p,set,true,first,state,flags);
            case 7:
                int value=first!=0?first:Rolled(p,s);
                foreach(int id in new[]{21,22,23,24,159,160}){ResetBase(s,id);}
                int maximum=Math.Max(s.Basis.Maximum,s.Basis.SecondaryMaximum);long bonus=(long)value*maximum/100;
                if(s.Weapon&&maximum+bonus<=maximum){return FlatDamage(s,p,set,true,1,state,flags);}
                Write(s,p,set,18,0,value,state,flags);Write(s,p,set,17,0,value,state,flags);return value;
            case 10:
                // Literal older multiply-before-divide expression; current
                // skill-tab reader packing differs. Fresh admission is gated.
                return Write(s,p,set,stat,checked(p.Parameter%3+8*p.Parameter/3),first!=0?first:Rolled(p,s),state,flags);
            case 15:
                if(stat==21){FlatDamage(s,p,set,false,p.Minimum,state,flags);}else{Write(s,p,set,stat,0,p.Minimum,state,flags);}return p.Minimum;
            case 16:
                if(stat==22){FlatDamage(s,p,set,true,p.Maximum,state,flags);}else{Write(s,p,set,stat,0,p.Maximum,state,flags);}return p.Maximum;
            case 17:
                int parameter=p.Parameter!=0?p.Parameter:Rolled(p,s);if(parameter==0){return 0;}
                if(stat==22){FlatDamage(s,p,set,true,parameter,state,flags);}else{Write(s,p,set,stat,0,parameter,state,flags);}return parameter;
            case 20:
                if(!s.GetOrCreateList(state,flags)){return 0;}
                s.AddList(152,1,0);return 1;
            case 21:return Write(s,p,set,stat,step.Value,Rolled(p,s),state,flags);
            default:throw new NotSupportedException("Unmapped item property function "+step.Function);
        }
    }
    internal static void Apply(EquipmentPropertyPlan plan,EquipmentPropertyInput input,int mode,EquipmentPropertyServices services,int state=0,int flags=0x40)
    {
        if(plan is not{Supported:true}){throw new NotSupportedException("No verified numeric property writer for "+plan?.Code);}
        if(mode is <0 or >7){throw new NotSupportedException("Unmapped property mode");}
        int first=0;
        for(int i=0;i<plan.Steps.Length;i++){int result=ApplyStep(services,input,mode,plan.Steps[i],first,state,flags);if(i==0){first=result;}}
    }
    // Fresh creation can roll ranges and retain layered skill stats. Original
    // saved-item projection still uses its separately admitted191-plan set.
    // Current war-tab-rand supplies three step values that this older func10
    // ignores in favor of the property parameter; its modern meaning is open.
    internal static bool CanCreate(EquipmentPropertyPlan plan)=>plan is{Steps.Length:>0}&&plan.Code!="war-tab-rand"
        &&Array.TrueForAll(plan.Steps,s=>Functions.Contains(s.Function)||s.Function is 10 or 20 or 21);
    internal static void Create(EquipmentPropertyPlan plan,EquipmentPropertyInput input,int mode,EquipmentPropertyServices services,int state=0,int flags=0x40)
    {
        if(!CanCreate(plan)){throw new NotSupportedException("No verified creation writer for "+plan?.Code);}
        if(mode is <0 or >7){throw new NotSupportedException("Unmapped property mode");}
        int first=0;
        for(int i=0;i<plan.Steps.Length;i++){int result=ApplyStep(services,input,mode,plan.Steps[i],first,state,flags);if(i==0){first=result;}}
    }
}

internal sealed record EquipmentPropertyOmission(string Code,string Slot,string Reason);
internal static class EquipmentRawProperties
{
    private sealed class Target(EquipmentStatUnit unit,EquipmentStatValues values,EquipmentPropertyBase basis,bool weapon,bool armor,bool throwable):EquipmentPropertyServices
    {
        private EquipmentStatNode List;
        internal bool Ethereal{get;private set;}
        internal bool HasDurability()
        {
            var facts=SavedItemEthereality.Facts(ClassGear.State((Server.Item)unit.Entity).BaseCode);
            ItemPropertyCreationContext guard=null;
            int Stat(int stat)=>values.Query((guard??=SavedItemEthereality.Guard((Server.Item)unit.Entity,unit,values,facts.Durability)).Unit.Stats,EquipmentStatKey.Pack(stat));
            return SavedItemEthereality.HasDurability(true,unit.Kind,true,facts.NoDurability,facts.Durability,()=>Stat(73),()=>Stat(152));
        }
        internal override EquipmentPropertyBase Basis=>basis;
        internal override bool Weapon=>weapon;
        internal override bool Armor=>armor;
        internal override bool Throwable=>throwable;
        internal override int BaseValue(int stat)=>EquipmentStatValues.Base(unit.Stats,EquipmentStatKey.Pack(stat),false);
        internal override void SetBase(int stat,int value)=>values.SetBase(unit.Stats,EquipmentStatKey.Pack(stat),value);
        internal override bool GetOrCreateList(int state,int flags)
        {
            if(state!=0||flags!=0x40){throw new NotSupportedException("No original-item state-list adapter for this property");}
            if(List==null){List=new(){Flags=0x40,OwnerKind=4};EquipmentStatGraph.Post(unit,List,true,values);}
            return true;
        }
        internal override int ListValue(int stat,int layer)=>EquipmentStatValues.Raw(List.BaseValues,EquipmentStatKey.Pack(stat,layer));
        internal override void SetList(int stat,int value,int layer)=>values.SetBase(List,EquipmentStatKey.Pack(stat,layer),value);
        internal override void AddList(int stat,int value,int layer)=>SetList(stat,checked(ListValue(stat,layer)+value),layer);
        internal override int Roll(int bound)=>throw new NotSupportedException("Projection cannot reroll a saved original item");
        internal override void SetEthereal()=>Ethereal=true;
    }
    // Base arrays/type metadata remain explicit inputs until full item birth is
    // integrated. This projects saved numeric properties, not a complete item.
    internal static EquipmentPropertyOmission[] Project(Server.Item original,EquipmentStatUnit unit,EquipmentStatValues values,
        EquipmentPropertyBase basis,bool weapon,bool armor,bool throwable)
    {
        if(unit is null||unit.Entity!=original||unit.Kind!=4||unit.Stats is not{OwnerKind:4,Parent:null}||unit.Stats.Owner!=unit
            ||ClassGear.State(original) is not{Identified:true} state||ClassGear.Broken(original))
        {throw new NotSupportedException("Numeric projection requires an intact identified original and an unattached item graph");}
        var target=new Target(unit,values,basis,weapon,armor,throwable);var omissions=new List<EquipmentPropertyOmission>();
        var properties=EquipmentIntrinsicProperties.For(original);
        int mode=state.NamedItemId==null?0:state.Roll.Rarity==GearRarity.Unique?3:4;
        foreach(var (property,roll) in properties)
        {
            var plan=EquipmentPropertyWriter.Plan(property.Code);
            // Saved class/element-skill rolls reuse the already verified func21
            // writer. The original191-plan kernel catalogue stays immutable.
            bool layered=plan is{Steps.Length:>0}&&plan.Steps.All(s=>s.Function==21);
            bool tree=CurrentTreePropertyWriter.Supports(plan);
            bool single=ItemSingleSkillProperty.Supports(plan);
            bool ethereal=SavedItemEthereality.Supports(plan);
            bool indestruct=plan is{Code:"indestruct",Steps.Length:1}&&plan.Steps[0] is{Function:20};
            bool socket=plan is{Code:"sock",Steps.Length:1}&&plan.Steps[0] is{Function:14,Stat:194,Set:false};
            if(plan is not{Supported:true}&&!layered&&!tree&&!single&&!ethereal&&!indestruct&&!socket){omissions.Add(new(property.Code,property.Slot,"Numeric writer is not integrated"));continue;}
            if(roll.Value==0&&property.SourceMin!=property.SourceMax&&plan.Steps.Skip(1).Any(s=>s.Function is 3 or 4 or 8))
            {omissions.Add(new(property.Code,property.Slot,"A zero first roll can reroll later stats; those separate rolls are not represented"));continue;}
            int parameter=single?ItemSingleSkillProperty.Parameter(property):NamedPropertyRuntime.Parameter(property);
            if(plan.Steps.Any(s=>s.Function==17)&&parameter==0&&property.Code.EndsWith("/lvl",StringComparison.Ordinal))
            {omissions.Add(new(property.Code,property.Slot,"The original zero-parameter fallback roll is not represented"));continue;}
            if(plan.Steps.Any(s=>s.Function==17)&&parameter==0&&roll.Value!=roll.Maximum)
            {omissions.Add(new(property.Code,property.Slot,"The original independent zero-parameter range fallback is not recorded"));continue;}
            var input=new EquipmentPropertyInput(roll.Value,roll.Maximum,parameter);
            if(socket)
            {
                // A saved count incompatible with the source cap is an explicit
                // legacy-state gap, not permission to change the live original.
                int count=EquipmentSocketProperty.Apply(EquipmentSocketProperty.Geometry(state.BaseCode,state.Roll.ItemLevel),input,0,target,()=>{});
                if(count!=roll.Value){omissions.Add(new(property.Code,property.Slot,"Saved socket roll differs from source dimension/type/level cap"));}
            }
            else if(tree){CurrentTreePropertyWriter.Apply(plan,input,target);}
            else if(single){ItemSingleSkillProperty.Apply(plan,input,target);}
            else if(ethereal){SavedItemEthereality.Apply(true,true,unit.Kind,target.Ethereal,target.HasDurability,target);}
            else if(layered||indestruct){EquipmentPropertyWriter.Create(plan,input,mode,target);}
            else{EquipmentPropertyWriter.Apply(plan,input,mode,target);}
        }
        if(state.Quality!=0){omissions.Add(new("quality","","Quality-base initialization is not integrated"));}
        if(state.MaterialRolls.Length>0||state.CraftedEquipment||state.DurabilityPercent!=0){omissions.Add(new("materials","","Crafted/material stat projection is not integrated"));}
        if(state.Runes.Length>0||state.Gems.Length>0){omissions.Add(new("socket-contents","","Installed rune/gem projection is not integrated"));}
        if(state.NamedItemId!=null&&NamedGear.Catalog.Item(state.NamedItemId).AdditionalProperties.Any(p=>p.SourceEnabled))
        {omissions.Add(new("set-additional","","Conditional named-piece stats are not integrated"));}
        return omissions.ToArray();
    }
}
