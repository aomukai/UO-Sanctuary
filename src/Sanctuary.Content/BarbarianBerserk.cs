// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record BerserkState(uint Owner,int Rank,int DefenseOverride,DateTime UntilUtc);
public sealed class BerserkProfiles:GenericPersistence
{
    public BerserkProfiles():base("SanctuaryBarbarianBerserk",139){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(BarbarianBerserk.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Berserk version");}
        BarbarianBerserk.Restore(JsonSerializer.Deserialize<BerserkState[]>(reader.ReadString()));
    }
}
public static class BarbarianBerserk
{
    internal sealed record Temporary(Serial Weapon,Point3D Origin,Map Map,long Until,int DefenseOverride);
    private static readonly Dictionary<uint,BerserkState> _states=new();
    private static readonly List<uint> _expired=new();
    public static BerserkProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(100),TimeSpan.FromMilliseconds(100),Prune);
    private static bool Eligible(Mobile owner)=>owner is PlayerMobile{Alive:true,Deleted:false}
        && ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)==true && profile.Class==DiabloClass.Barbarian;
    public static BerserkState State(Mobile owner)=>StateAt(owner,TimedEffects.Now);
    private static BerserkState StateAt(Mobile owner,DateTime at)=>Eligible(owner) && _states.TryGetValue(owner.Serial.Value,out var state) && at<state.UntilUtc?state:null;
    private static bool Valid(PlayerMobile owner,Temporary mode,long now)=>mode!=null && now<mode.Until && Eligible(owner)
        && !owner.Frozen && !owner.Paralyzed && owner.Spell==null && AlliedOverlap.Allowed(owner)
        && owner.Map==mode.Map && owner.Location==mode.Origin && (CombatTiming.Equipped(owner)?.Serial??Serial.Zero)==mode.Weapon;
    public static int DefenseOverride(PlayerMobile owner)=>State(owner)?.DefenseOverride
        ??(Valid(owner,ClassCombat.Existing(owner)?.BerserkMode,Core.TickCount)?ClassCombat.Existing(owner).BerserkMode.DefenseOverride:0);
    internal static int ApplyOverride(int defense,int percent)=>(int)Math.Clamp((long)defense+(long)defense*percent/100,0,int.MaxValue);
    private static void Refresh(PlayerMobile owner){ClassStats.Refresh(owner);StatNetwork.Send(owner);}
    public static void Begin(PlayerMobile owner,SkillValues values,long now)
    {
        int frames=(int)values.Eval("calc2");if(frames<=0){frames=10;}
        CancelMode(owner);_states[owner.Serial.Value]=new(owner.Serial.Value,values.Rank,(int)values.Eval("aurastatcalc2"),TimedEffects.TimeAt(now).AddMilliseconds(frames*40));Refresh(owner);
    }
    internal static void Impact(PlayerMobile owner,long now)
    {
        if(StateAt(owner,TimedEffects.TimeAt(now))!=null){return;} // Do2 refreshes the same constant stats without extending its timed expiry.
        // If a very slow attack outlives start39's timed state, shared Do2 creates
        // flag4/TEMPONLY instead. It ends with this animation, not another timed buff.
        _states.Remove(owner.Serial.Value);var action=ClassCombat.For(owner);
        action.BerserkMode=new(action.PendingWeapon,action.ActionOrigin,action.ActionMap,action.NextAction,(int)SkillMechanics.Values(owner,CombatAbility.Berserk).Eval("aurastatcalc2"));Refresh(owner);
    }
    internal static void CancelMode(PlayerMobile owner)
    {
        if(ClassCombat.Existing(owner) is not{} action || action.BerserkMode==null){return;}
        action.BerserkMode=null;Refresh(owner);
    }
    internal static void Tick(PlayerMobile owner,long now)
    {if(ClassCombat.Existing(owner)?.BerserkMode is{} mode && !Valid(owner,mode,now)){CancelMode(owner);}}
    public static void Clear(Mobile owner)
    {
        if(owner==null){return;}bool had=_states.Remove(owner.Serial.Value);
        if(owner is PlayerMobile player){CancelMode(player);if(had){Refresh(player);}}
    }
    private static void Prune()
    {
        _expired.Clear();foreach(var pair in _states)
        {if(pair.Value.UntilUtc<=TimedEffects.Now || !Eligible(World.FindMobile((Serial)pair.Key))){_expired.Add(pair.Key);}}
        foreach(uint serial in _expired){Clear(World.FindMobile((Serial)serial));_states.Remove(serial);}
    }
    public static BerserkState[] Snapshot(){Prune();return new List<BerserkState>(_states.Values).ToArray();}
    public static void Restore(BerserkState[] states)
    {
        if(states==null){throw new InvalidDataException("Missing Berserk states");}_states.Clear();
        foreach(var state in states)
        {if(state.Owner==0 || state.Rank<1 || state.DefenseOverride is <-100 or >0 || !_states.TryAdd(state.Owner,state)){throw new InvalidDataException("Invalid Berserk state");}}
    }
}
