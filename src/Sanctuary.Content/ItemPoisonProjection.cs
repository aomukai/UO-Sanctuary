// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
namespace Sanctuary;

// Replay the already saved poison property values, never an acquisition roll.
// The shared source writer owns poison-count and stat-shift semantics.
internal static class ItemPoisonProjection
{
    private sealed class Values:EquipmentPropertyServices
    {
        private readonly Dictionary<(int,int),int> Stats=new();
        internal override EquipmentPropertyBase Basis=>throw new NotSupportedException("Unexpected poison physical basis");
        internal override bool Weapon=>throw new NotSupportedException("Unexpected poison weapon classification");
        internal override bool Armor=>throw new NotSupportedException("Unexpected poison armor classification");
        internal override bool Throwable=>throw new NotSupportedException("Unexpected poison throwing classification");
        internal override int BaseValue(int stat)=>throw new NotSupportedException("Unexpected poison base read");
        internal override void SetBase(int stat,int value)=>throw new NotSupportedException("Unexpected poison base write");
        internal override bool GetOrCreateList(int state,int flags)=>state==0&&flags==0x40;
        internal override int ListValue(int stat,int layer)=>Stats.GetValueOrDefault((stat,layer));
        internal override void SetList(int stat,int value,int layer)=>Stats[(stat,layer)]=value;
        internal override void AddList(int stat,int value,int layer)=>Stats[(stat,layer)]=checked(ListValue(stat,layer)+value);
        internal override int Roll(int bound)=>throw new NotSupportedException("Original poison duration requires a saved source roll");
        internal override void SetEthereal()=>throw new NotSupportedException("Unexpected poison ethereality operation");
        internal void Apply(string code,int minimum,int maximum,int parameter=0)
            =>EquipmentPropertyWriter.Apply(EquipmentPropertyWriter.Plan(code),new(minimum,maximum,parameter),0,this);
    }
    private static bool Scalar(string code)=>code is "pois-min" or "pois-max" or "pois-len";
    internal static MissileSourceStats Read(Item item,IEnumerable<(NamedProperty Property,NamedPropertyRoll Roll)> properties)
    {
        if(ClassGear.Slot(item)==GearSlot.Cosmetic||ClassGear.Broken(item)||ClassGear.State(item) is not{Identified:true} state){return new();}
        if(ClassGear.Material(item,"damage-poison")!=0||ClassGear.Bonus(item,"damage-poison")!=0)
        {throw new NotSupportedException("NPC material poison percentage needs a verified projection");}
        var values=new Values();
        foreach(var p in properties)
        {
            if(p.Property.Code=="dmg-pois"){values.Apply(p.Property.Code,p.Roll.Value,p.Roll.Maximum,NamedPropertyRuntime.Parameter(p.Property));}
            else if(Scalar(p.Property.Code)){values.Apply(p.Property.Code,p.Roll.Value,p.Roll.Value);}
        }
        foreach(var rune in state.Runes)
        {if(Scalar(rune.Pattern.EffectCode)){values.Apply(rune.Pattern.EffectCode,rune.Percentage,rune.Percentage);}}
        foreach(var gem in state.Gems)foreach(var effect in GemSockets.Effects(item,gem))
        {if(Scalar(effect.Code)){values.Apply(effect.Code,effect.Minimum,effect.Minimum);}}
        int minimum=values.ListValue(57,0),maximum=values.ListValue(58,0),frames=values.ListValue(59,0),count=values.ListValue(326,0);
        foreach(var gem in GemCombat.PoisonSources(item))
        {
            if(gem.DurationMs%40!=0){throw new NotSupportedException("NPC socket poison duration has no exact source-frame projection");}
            // Preserve the existing Sanctuary socket policy: the gem's fixed
            // Minimum total supplies its source per-frame rate.
            int rate=checked((int)((long)gem.Minimum*40*256/gem.DurationMs));
            minimum=checked(minimum+rate);maximum=checked(maximum+rate);frames=checked(frames+gem.DurationMs/40);count=checked(count+1);
        }
        if(minimum<0||maximum<minimum||frames<0||count<0){throw new NotSupportedException("Invalid original NPC poison stat projection");}
        return new(PoisonMin:minimum,PoisonMax:maximum,PoisonFrames:frames,PoisonCount:count);
    }
}
