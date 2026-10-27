// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record OwnedDruidAttackCharge(uint Owner,DruidAttackCharge Charge);
public sealed class DruidAttackChargeProfiles:GenericPersistence
{
    public DruidAttackChargeProfiles():base("SanctuaryDruidAttackCharges",177){}
    public override void Serialize(IGenericWriter writer)
    {writer.Write(0);writer.Write(JsonSerializer.Serialize(DruidAttackCharges.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Druid attack charge version");}DruidAttackCharges.Restore(JsonSerializer.Deserialize<OwnedDruidAttackCharge[]>(reader.ReadString()));}
    // Owner/form eligibility is checked after world loading, never while another
    // persistence object may still be waiting to restore its form ledger.
}

internal static class DruidAttackCharges
{
    private static ImmutableDictionary<(uint,CombatAbility),OwnedDruidAttackCharge> _states=ImmutableDictionary<(uint,CombatAbility),OwnedDruidAttackCharge>.Empty;
    internal static Func<bool> Allocate=()=>true;
    public static DruidAttackChargeProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()
    {
        var created=CorpseRecoveryHooks.Created;
        CorpseRecoveryHooks.Created=corpse=>{if(corpse.Owner is PlayerMobile owner){Clear(owner);}created?.Invoke(corpse);};
        Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),()=>Prune(TimedEffects.Now));
    }
    private static bool Owner(PlayerMobile owner)=>owner is{Alive:true,Deleted:false}
        &&ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)==true&&profile.Class==DiabloClass.Druid;
    internal static DruidAttackCharge State(PlayerMobile owner,CombatAbility ability)
        =>Owner(owner)&&DruidPlayerForms.State(owner)!=null&&_states.TryGetValue((owner.Serial.Value,ability),out var state)
            &&ShapeRestrictions.Allowed(owner,ability)&&state.Charge.UntilUtc>TimedEffects.Now?state.Charge:null;
    internal static int DamagePercent(PlayerMobile owner)=>State(owner,CombatAbility.Maul)?.DamagePercent??0;
    internal static int AttackRate(PlayerMobile owner)=>State(owner,CombatAbility.Maul)?.AttackRate??0;
    internal static int StunFrames(PlayerMobile owner)=>State(owner,CombatAbility.Maul)?.StunFrames??0;
    internal static bool Gain(PlayerMobile owner,SkillValues values)
    {
        if(!Owner(owner)||DruidPlayerForms.State(owner)==null||!ShapeRestrictions.Allowed(owner,values.Ability)){return false;}
        var next=DruidAttackChargeModel.Gain(State(owner,values.Ability),values,TimedEffects.Now,Allocate);
        if(next==null){return false;}
        var owned=new OwnedDruidAttackCharge(owner.Serial.Value,next);_states=_states.SetItem((owned.Owner,next.Ability),owned);
        owner.FixedEffect(0x375A,5,5,0x481,0); // Identified charge flash; source maul orb front/back art remains pending.
        StatNetwork.Send(owner);return true;
    }
    internal static void Clear(PlayerMobile owner)
    {
        if(owner==null){return;}
        foreach(var key in _states.Keys.Where(key=>key.Item1==owner.Serial.Value).ToArray()){_states=_states.Remove(key);}
    }
    internal static void Prune(DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Druid charge clock must be UTC");}
        foreach(var pair in _states)
        {
            var owner=World.FindMobile((Serial)pair.Value.Owner) as PlayerMobile;
            if(!Owner(owner)||DruidPlayerForms.State(owner)==null||!ShapeRestrictions.Allowed(owner,pair.Value.Charge.Ability)||pair.Value.Charge.UntilUtc<=at)
            {_states=_states.Remove(pair.Key);if(owner is{Deleted:false}){StatNetwork.Send(owner);}}
        }
    }
    internal static OwnedDruidAttackCharge[] Snapshot()=>_states.Values.OrderBy(state=>state.Owner).ThenBy(state=>state.Charge.Ability).ToArray();
    internal static void Restore(OwnedDruidAttackCharge[] saved)
    {
        if(saved==null||saved.Length>1000000){throw new InvalidDataException("Missing Druid attack charges");}
        var next=ImmutableDictionary<(uint,CombatAbility),OwnedDruidAttackCharge>.Empty;
        foreach(var state in saved)
        {
            if(state==null||state.Owner==0||!((Serial)state.Owner).IsMobile||state.Charge==null)
            {throw new InvalidDataException("Invalid Druid charge owner");}
            try{DruidAttackChargeModel.Validate(state.Charge);}
            catch(ArgumentException error){throw new InvalidDataException("Invalid Druid attack charge payload",error);}
            var key=(state.Owner,state.Charge.Ability);
            if(next.ContainsKey(key)){throw new InvalidDataException("Duplicate Druid charge slot");}next=next.Add(key,state);
        }
        _states=next;
    }
}
