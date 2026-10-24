// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record AssassinBuffState(uint Owner,CombatAbility Ability,int Rank,int Velocity,int AttackRate,DateTime UntilUtc,int ElementResistance=0,int PhysicalResistance=0,int CurseResistance=0,int Fade=0);
public sealed class AssassinBuffProfiles:GenericPersistence
{
    private AssassinBuffState[] _snapshot;
    public AssassinBuffProfiles():base("SanctuaryAssassinBuffs",150){}
    public override void Serialize(){_snapshot=AssassinBuffs.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Assassin buff version");}AssassinBuffs.Restore(JsonSerializer.Deserialize<AssassinBuffState[]>(reader.ReadString()));}
    public override void PostDeserialize()=>AssassinBuffs.Prune();
}
public static class AssassinBuffs
{
    private static readonly Dictionary<uint,AssassinBuffState> _states=new();
    public static AssassinBuffProfiles Profiles{get;private set;}
    public static void Configure(){Profiles=new();EventSink.WorldSave+=Prune;}
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Prune);
    internal static AssassinBuffState State(Mobile owner)=>owner is{Alive:true,Deleted:false}
        && ClassCreation.Profiles.TryGet(owner.Serial,out var profile) && profile.Class==DiabloClass.Assassin
        && _states.TryGetValue(owner.Serial.Value,out var state) && state.UntilUtc>TimedEffects.Now?state:null;
    public static int Velocity(Mobile owner)=>State(owner)?.Velocity??0;
    public static int AttackRate(Mobile owner)=>State(owner)?.AttackRate??0;
    public static int CurseResistance(Mobile owner)=>State(owner)?.CurseResistance??0;
    public static bool Faded(Mobile owner)=>State(owner)?.Fade>0;
    public static int Resistance(Mobile owner,ResistanceType type)=>State(owner) is{} state?type switch
    {ResistanceType.Physical=>state.PhysicalResistance,ResistanceType.Fire or ResistanceType.Cold or ResistanceType.Energy or ResistanceType.Poison=>state.ElementResistance,_=>0}:0;
    internal static AssassinBuffState Capture(uint owner,SkillValues values,DateTime at)
    {
        if(values.Ability is not(CombatAbility.BurstOfSpeed or CombatAbility.Fade) || values.Rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(values));}
        DateTime until=at.AddMilliseconds(checked(values.Eval("auralencalc")*40));
        if(values.Ability==CombatAbility.BurstOfSpeed){return new(owner,values.Ability,values.Rank,(int)values.Eval("aurastatcalc1"),(int)values.Eval("aurastatcalc2"),until);}
        int resistance=(int)values.Eval("aurastatcalc1");
        for(int i=2;i<=4;i++){if(values.Eval("aurastatcalc"+i)!=resistance){throw new NotSupportedException("Fade elemental channels diverged from the pinned model");}}
        return new(owner,values.Ability,values.Rank,0,0,until,resistance,(int)values.Eval("aurastatcalc6"),(int)values.Eval("aurastatcalc5"),(int)values.Eval("passivecalc1"));
    }
    internal static void Cast(PlayerMobile owner,SkillValues values)
    {
        // Source do18 clears state group2, including the same state, before
        // applying the new rank. Quickness and Fade share this slot;
        // Venom is independent and has no entry in this group.
        _states[owner.Serial.Value]=Capture(owner.Serial.Value,values,TimedEffects.Now);Refresh(owner);
        owner.FixedEffect(0x373A,10,15,values.Ability==CombatAbility.Fade?0x835:0x481,0); // Identified quickness/fade cast/overlay placeholders.
        owner.PlaySound(0x1E5); // Identified assassin_quickness/assassin_fade sound placeholder.
    }
    public static void Clear(Mobile owner)
    {if(owner!=null && _states.Remove(owner.Serial.Value)){Refresh(owner);}}
    private static void Refresh(Mobile owner)
    {if(owner is PlayerMobile player && !player.Deleted){ClassGear.Refresh(player);StatNetwork.Send(player);}}
    internal static void Prune()
    {
        foreach(var pair in _states.ToArray())
        {
            var owner=World.FindMobile((Serial)pair.Key);
            if(pair.Value.UntilUtc<=TimedEffects.Now || owner is not{Alive:true,Deleted:false}
                || !ClassCreation.Profiles.TryGet((Serial)pair.Key,out var profile) || profile.Class!=DiabloClass.Assassin)
            {_states.Remove(pair.Key);Refresh(owner);}
        }
    }
    public static AssassinBuffState[] Snapshot(){Prune();return _states.Values.OrderBy(x=>x.Owner).ToArray();}
    public static void Restore(AssassinBuffState[] states)
    {
        if(states==null || states.Length>1000000){throw new InvalidDataException("Missing Assassin buffs");}
        _states.Clear();foreach(var state in states)
        {
            if(state==null || state.Owner==0 || !((Serial)state.Owner).IsMobile || state.Ability is not(CombatAbility.BurstOfSpeed or CombatAbility.Fade) || state.Rank is <1 or >255)
            {throw new InvalidDataException("Invalid Assassin buff identity");}
            var row=SkillMechanics.For(state.Ability);var values=new SkillValues(row,key=>key==row.SourceKey?state.Rank:0,_=>0,1);
            var expected=Capture(state.Owner,values,DateTime.UnixEpoch);
            if(state with{UntilUtc=expected.UntilUtc}!=expected || !_states.TryAdd(state.Owner,state))
            {throw new InvalidDataException("Assassin buff escaped its source values");}
        }
    }
}
