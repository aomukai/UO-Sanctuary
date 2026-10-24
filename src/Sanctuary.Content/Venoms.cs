// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record VenomState(uint Owner,int Rank,int Mastery,int Minimum256,int Maximum256,int Frames,DateTime UntilUtc);
public sealed class VenomProfiles:GenericPersistence
{
    private VenomState[] _snapshot;
    public VenomProfiles():base("SanctuaryVenom",152){}
    public override void Serialize(){_snapshot=Venoms.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader){if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Venom version");}Venoms.Restore(JsonSerializer.Deserialize<VenomState[]>(reader.ReadString()));}
    public override void PostDeserialize()=>Venoms.Prune();
}

public static class Venoms
{
    private static readonly Dictionary<uint,VenomState> _states=new();
    public static VenomProfiles Profiles{get;private set;}
    public static void Configure(){Profiles=new();EventSink.WorldSave+=Prune;}
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Prune);
    internal static VenomState State(Mobile owner)=>owner is{Alive:true,Deleted:false}
        && ClassCreation.Profiles.TryGet(owner.Serial,out var profile) && profile.Class==DiabloClass.Assassin
        && _states.TryGetValue(owner.Serial.Value,out var state) && state.UntilUtc>TimedEffects.Now?state:null;
    internal static VenomState Capture(uint owner,SkillValues values,int mastery,DateTime at)
    {
        if(values.Ability!=CombatAbility.Venom || values.Rank is <1 or >255 || mastery<0){throw new ArgumentOutOfRangeException(nameof(values));}
        int minimum=AmazonMelee.EnhanceEndpoint(values.Damage256(false),mastery),maximum=AmazonMelee.EnhanceEndpoint(values.Damage256(true),mastery);
        return new(owner,values.Rank,mastery,checked((int)values.ElementalEval("aurastatcalc1",minimum,maximum)),
            checked((int)values.ElementalEval("aurastatcalc2",minimum,maximum)),checked((int)values.Eval("aurastatcalc3")),at.AddMilliseconds(checked(values.Eval("auralencalc")*40)));
    }
    internal static bool Replaces(VenomState old,int rank)=>old==null || rank>=old.Rank;
    public static bool Begin(PlayerMobile owner,Serial target,Point3D point,long now)
        =>SkillCombat.Begin(owner,CombatAbility.Venom,Serial.Zero,owner.Location,now,(p,_,_,_)=>p.Map!=null && p.Map!=Map.Internal);
    public static bool Release(PlayerMobile owner)
    {
        if(!owner.Alive || !AssassinCombat.Available(owner,CombatAbility.Venom) || owner.Map==null || owner.Map==Map.Internal){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.Venom);
        // Group0 does not clear the old state. The shared factory rejects a
        // lower rank; null-start do18 pays only after successful dispatch.
        if(!Replaces(State(owner),values.Rank)){return false;}
        var next=Capture(owner.Serial.Value,values,NamedElements.MasteryPercent(owner,"pois"),TimedEffects.Now);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        _states[owner.Serial.Value]=next;
        owner.FixedEffect(0x373A,10,15,0x3F,0);owner.PlaySound(0x1E5); // Identified Venom cast/venomclaws/audio placeholders.

        return true;
    }
    public static void Clear(Mobile owner){if(owner!=null){_states.Remove(owner.Serial.Value);}}
    internal static void Prune()
    {
        foreach(var row in _states.ToArray())
        {
            var owner=World.FindMobile((Serial)row.Key);
            if(row.Value.UntilUtc<=TimedEffects.Now || owner is not{Alive:true,Deleted:false}
                || !ClassCreation.Profiles.TryGet((Serial)row.Key,out var profile) || profile.Class!=DiabloClass.Assassin){_states.Remove(row.Key);}
        }
    }
    public static VenomState[] Snapshot(){Prune();return _states.Values.OrderBy(x=>x.Owner).ToArray();}
    public static void Restore(VenomState[] states)
    {
        if(states==null || states.Length>1000000){throw new InvalidDataException("Missing Venom states");}_states.Clear();
        foreach(var state in states)
        {
            if(state==null || !((Serial)state.Owner).IsMobile || state.Rank is <1 or >255 || state.Mastery<0){throw new InvalidDataException("Invalid Venom identity");}
            var row=SkillMechanics.For(CombatAbility.Venom);var values=new SkillValues(row,k=>k==row.SourceKey?state.Rank:0,_=>0,1);
            if(Capture(state.Owner,values,state.Mastery,DateTime.UnixEpoch) with{UntilUtc=state.UntilUtc}!=state || !_states.TryAdd(state.Owner,state))
            {throw new InvalidDataException("Invalid Venom payload");}
        }
    }
}
