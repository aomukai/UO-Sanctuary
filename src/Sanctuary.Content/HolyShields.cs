// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record HolyShieldState(uint Owner,int Rank,int BlockBonus,DateTime UntilUtc);
public sealed class HolyShieldProfiles:GenericPersistence
{
    private HolyShieldState[] _snapshot;
    public HolyShieldProfiles():base("SanctuaryHolyShields",127){}
    public override void Serialize(){_snapshot=HolyShields.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Holy Shield version");}
        HolyShields.Restore(JsonSerializer.Deserialize<HolyShieldState[]>(reader.ReadString()));
    }
}
public static class HolyShields
{
    // A paid start is transient action state, not part of the saved buff.
    internal sealed record Pending(Map Map,Point3D Origin,Serial Weapon,long EffectAt);
    private static readonly Dictionary<uint,HolyShieldState> _states=new();
    private static readonly List<uint> _expired=new();
    public static HolyShieldProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static HolyShieldState State(Mobile owner)=>owner is{Alive:true,Deleted:false} && _states.TryGetValue(owner.Serial.Value,out var state)
        && state.UntilUtc>TimedEffects.Now?state:null;
    public static Item Shield(PlayerMobile owner)
    {
        foreach(var item in owner.Items)
        {if(ClassGear.Slot(item)==GearSlot.Shield && ClassGear.Active(owner,item)){return item;}}
        return null;
    }
    public static int BlockBonus(PlayerMobile owner)=>Shield(owner)!=null?State(owner)?.BlockBonus ?? 0:0;
    public static int DefensePercent(PlayerMobile owner)=>Shield(owner)!=null && State(owner) is{} state
        ?(int)SkillMechanics.Values(owner,CombatAbility.HolyShield,state.Rank).Eval("calc1"):0;
    public static (int Min256,int Max256) SmiteBonus(PlayerMobile owner)
    {
        if(Shield(owner)==null || State(owner) is not{} state){return(0,0);}
        var definition=SkillMechanics.For(CombatAbility.HolyShield);
        int Damage(bool maximum)=>SkillValues.Tier(definition.Number(maximum?"MaxDam":"MinDam"),state.Rank,
            tier=>definition.Number((maximum?"MaxLevDam":"MinLevDam")+tier))<<definition.Number("HitShift");
        return(Damage(false),Damage(true));
    }
    internal static void Route(SkillValues values)
    {
        var r=SkillMechanics.For(CombatAbility.HolyShield);
        if(values.Ability!=CombatAbility.HolyShield||values.Rank is <1 or >255||r.SourceId!=117||r.Number("srvstfunc")!=36||r.Number("srvdofunc")!=18
            ||r.Text("aurastate")!="holyshield"||r.Text("aurastat1")!="toblock"||r.Number("usemanaondo")!=0||r.Number("leftskill")!=0||r.Number("rightskill")!=1
            ||values.Mana256!=8960||values.Eval("auralencalc")<=0||values.Eval("aurastatcalc1") is <0 or >40)
        {throw new NotSupportedException("Unsupported pinned Holy Shield route");}
    }
    internal static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)
    {
        if(!PaladinCombat.Available(owner,CombatAbility.HolyShield)){return false;}
        try{Route(SkillMechanics.Values(owner,CombatAbility.HolyShield));}
        catch(NotSupportedException e){owner.SendMessage(0x22,e.Message);return false;}
        if(!SkillCombat.Begin(owner,CombatAbility.HolyShield,selected,point,now,PaladinCombat.ValidTarget)){return false;}
        var s=ClassCombat.For(owner);s.HolyShieldStart=null;
        if(!NecromancerEffects.Pay(owner,8960)){s.PendingAbility=CombatAbility.None;return false;}
        s.HolyShieldStart=new(s.ActionMap,s.ActionOrigin,s.PendingWeapon,s.EffectAt);return true;
    }
    internal static bool Release(PlayerMobile owner,long now)
    {
        var s=ClassCombat.For(owner);var paid=s.HolyShieldStart;s.HolyShieldStart=null;
        if(paid==null||now<paid.EffectAt||!owner.Alive||!PaladinCombat.Available(owner,CombatAbility.HolyShield)
            ||owner.Map!=paid.Map||owner.Location!=paid.Origin||(CombatTiming.Equipped(owner)?.Serial??Serial.Zero)!=paid.Weapon){return false;}
        // The generic action/equipment adapter cancels changed equipment.
        // Bare do18 itself has no second shield gate and no second payment.
        bool applied=Apply(owner,SkillMechanics.Values(owner,CombatAbility.HolyShield),TimedEffects.Now);
        if(applied){owner.PlaySound(0x1E5);}return applied; // Identified cast placeholder.
    }
    internal static void EquipmentChanged(PlayerMobile owner)
    {if(ClassCombat.For(owner).HolyShieldStart!=null){ClassCombat.Stop(owner);}}
    public static void Cast(PlayerMobile owner,SkillValues values)=>Apply(owner,values,TimedEffects.Now);
    internal static bool Apply(PlayerMobile owner,SkillValues values,DateTime at)
    {
        Route(values);if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Holy Shield clock must be UTC");}
        var next=new HolyShieldState(owner.Serial.Value,values.Rank,(int)values.Eval("aurastatcalc1"),at.AddMilliseconds(values.Eval("auralencalc")*40));Validate(next);
        // Ungrouped same-skill state: weaker lists refuse; equal rank refreshes
        // its values/deadline; stronger rank replaces. Cold armor is separate.
        if(State(owner) is{} old&&old.Rank>values.Rank){return false;}
        _states[owner.Serial.Value]=next;
        owner.FixedEffect(0x373A,10,15,0x481,0); // Placeholder enchanted shield overlay.
        return true;
    }
    public static void Clear(Mobile owner)=>_states.Remove(owner.Serial.Value);
    public static HolyShieldState[] Snapshot()
    {
        _expired.Clear();foreach(var row in _states)
        {if(row.Value.UntilUtc<=TimedEffects.Now || World.FindMobile((Serial)row.Key) is not{Alive:true,Deleted:false}){_expired.Add(row.Key);}}
        foreach(uint owner in _expired){_states.Remove(owner);}
        return new List<HolyShieldState>(_states.Values).ToArray();
    }
    public static void Restore(HolyShieldState[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing Holy Shields");}
        var candidate=new Dictionary<uint,HolyShieldState>();
        foreach(var state in saved)
        {Validate(state);if(!candidate.TryAdd(state.Owner,state)){throw new InvalidDataException("Duplicate Holy Shield");}}
        _states.Clear();foreach(var pair in candidate){_states.Add(pair.Key,pair.Value);}
    }
    private static void Validate(HolyShieldState state)
    {if(state==null||state.Owner==0||!((Serial)state.Owner).IsMobile||state.Rank is <1 or >255||state.BlockBonus is <0 or >40||state.UntilUtc.Kind!=DateTimeKind.Utc)
        {throw new InvalidDataException("Invalid Holy Shield");}}
}
