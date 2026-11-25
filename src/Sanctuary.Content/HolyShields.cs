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
    private static readonly Dictionary<uint,(DateTime Until,DateTime Next)> _visible=new();
    public static HolyShieldProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize() => Timer.StartTimer(TimeSpan.Zero, TimeSpan.FromMilliseconds(100),
        () => Update(TimedEffects.Now, refresh: true));
    public static HolyShieldState State(Mobile owner)=>owner is{Alive:true,Deleted:false} && _states.TryGetValue(owner.Serial.Value,out var state)
        && state.UntilUtc>TimedEffects.Now?state:null;
    public static Item Shield(PlayerMobile owner)
    {
        foreach(var item in owner.Items)
        {if(ClassGear.Slot(item)==GearSlot.Shield && ClassGear.Active(owner,item)){return item;}}
        return null;
    }
    public static int BlockBonus(PlayerMobile owner)=>Shield(owner)!=null?State(owner)?.BlockBonus ?? 0:0;
    internal static int BlockBonus(SkillValues values)=>(int)values.Eval("aurastatcalc1");
    internal static int DefensePercent(SkillValues values)=>(int)values.Eval("calc1");
    public static int DefensePercent(PlayerMobile owner)=>Shield(owner)!=null && State(owner) is{} state
        ?DefensePercent(SkillMechanics.Values(owner,CombatAbility.HolyShield,state.Rank)):0;
    public static (int Min256,int Max256) SmiteBonus(PlayerMobile owner)
    {
        if(Shield(owner)==null || State(owner) is not{} state){return(0,0);}
        return SmiteBonus(state.Rank);
    }
    internal static (int Min256,int Max256) SmiteBonus(int rank)
    {
        var definition=SkillMechanics.For(CombatAbility.HolyShield);
        int Damage(bool maximum)=>SkillValues.Tier(definition.Number(maximum?"MaxDam":"MinDam"),rank,
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
        return Apply(owner,SkillMechanics.Values(owner,CombatAbility.HolyShield),TimedEffects.Now);
    }
    internal static void EquipmentChanged(PlayerMobile owner)
    {if(ClassCombat.For(owner).HolyShieldStart!=null){ClassCombat.Stop(owner);}}
    public static void Cast(PlayerMobile owner,SkillValues values)=>Apply(owner,values,TimedEffects.Now);
    internal static bool Apply(PlayerMobile owner,SkillValues values,DateTime at)
    {
        Route(values);if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Holy Shield clock must be UTC");}
        var next=new HolyShieldState(owner.Serial.Value,values.Rank,BlockBonus(values),at.AddMilliseconds(values.Eval("auralencalc")*40));Validate(next);
        // Ungrouped same-skill state: weaker lists refuse; equal rank refreshes
        // its values/deadline; stronger rank replaces. Cold armor is separate.
        if(State(owner) is{} old&&old.Rank>values.Rank){return false;}
        _states[owner.Serial.Value]=next;
        PaladinDefenseVisuals.ShieldMoment(owner, SpellEffectGraphics.HolyShieldRaise, SpellEffectSounds.HolyShieldRaise, 12, at);
        Refresh(owner, next, at);
        return true;
    }
    internal static void Block(PlayerMobile owner)
    {
        if (State(owner) != null && Shield(owner) != null)
        {
            PaladinDefenseVisuals.ShieldMoment(owner, SpellEffectGraphics.HolyShieldBlock, SpellEffectSounds.HolyShieldBlock, 7, TimedEffects.Now);
        }
    }
    public static void Clear(Mobile owner)
    {
        _states.Remove(owner.Serial.Value);
        Hide(owner.Serial.Value, owner);
    }
    private static bool Hide(uint serial, Mobile owner)
    {
        if (!_visible.Remove(serial)) { return false; }
        PaladinAuraVisuals.Clear(owner, SpellEffectGraphics.HolyShieldShell);
        return true;
    }
    private static void Refresh(PlayerMobile owner, HolyShieldState state, DateTime now)
    {
        if (Shield(owner) == null || !PaladinAuraVisuals.InWorld(owner))
        { Hide(state.Owner, owner); return; }
        if (_visible.TryGetValue(state.Owner, out var visible) && visible.Until == state.UntilUtc && visible.Next > now)
        { return; }
        DateTime leaseEnd = now.AddMilliseconds(650);
        if (state.UntilUtc < leaseEnd) { leaseEnd = state.UntilUtc; }
        if (PaladinAuraVisuals.Attach(owner, SpellEffectGraphics.HolyShieldShell, leaseEnd, now))
        { _visible[state.Owner] = (state.UntilUtc, now.AddMilliseconds(400)); }
    }
    private static void Update(DateTime now, bool refresh)
    {
        if (World.Loading) { return; }
        _expired.Clear();
        foreach (var (serial, state) in _states)
        {
            var owner = World.FindMobile((Serial)serial);
            if (state.UntilUtc <= now || owner is not { Alive: true, Deleted: false })
            {
                _expired.Add(serial);
                if (Hide(serial, owner) && owner is PlayerMobile { Alive: true, Deleted: false } player && Shield(player) != null)
                {
                    PaladinDefenseVisuals.ShieldMoment(player, SpellEffectGraphics.HolyShieldFade, SpellEffectSounds.HolyShieldFade, 10, now);
                }
            }
            else if (refresh && owner is PlayerMobile player) { Refresh(player, state, now); }
        }
        foreach (uint owner in _expired) { _states.Remove(owner); }
    }
    public static HolyShieldState[] Snapshot()
    {
        Update(TimedEffects.Now, refresh: false);
        return new List<HolyShieldState>(_states.Values).ToArray();
    }
    public static void Restore(HolyShieldState[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing Holy Shields");}
        var candidate=new Dictionary<uint,HolyShieldState>();
        foreach(var state in saved)
        {Validate(state);if(!candidate.TryAdd(state.Owner,state)){throw new InvalidDataException("Duplicate Holy Shield");}}
        foreach (var serial in _states.Keys)
        {
            if (!candidate.ContainsKey(serial)) { Hide(serial, World.FindMobile((Serial)serial)); }
        }
        _states.Clear();foreach(var pair in candidate){_states.Add(pair.Key,pair.Value);}
    }
    private static void Validate(HolyShieldState state)
    {if(state==null||state.Owner==0||!((Serial)state.Owner).IsMobile||state.Rank is <1 or >255||state.BlockBonus is <0 or >40||state.UntilUtc.Kind!=DateTimeKind.Utc)
        {throw new InvalidDataException("Invalid Holy Shield");}}
}
