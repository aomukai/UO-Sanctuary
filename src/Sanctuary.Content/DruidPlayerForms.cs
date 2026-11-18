// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record DruidPlayerForm(uint Owner,DruidFormState Form,DateTime StartedUtc,DateTime UntilUtc);
public sealed class DruidFormProfiles:GenericPersistence
{
    public DruidFormProfiles():base("SanctuaryDruidPlayerForms",176){}
    public override void Serialize(IGenericWriter writer)
    {writer.Write(0);writer.Write(JsonSerializer.Serialize(DruidPlayerForms.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown player-form version");}DruidPlayerForms.Restore(JsonSerializer.Deserialize<DruidPlayerForm[]>(reader.ReadString()));}
    public override void PostDeserialize()=>DruidPlayerForms.Prune(TimedEffects.Now);
}

// Prepared saved stat consumer. Shape restrictions and ordinary form A1
// consumers read this ledger. The displayed form is derived from the ledger
// so the server body and its collision footprint stay independent.
public static class DruidPlayerForms
{
    private static ImmutableDictionary<uint,DruidPlayerForm> _states=ImmutableDictionary<uint,DruidPlayerForm>.Empty;
    public static DruidFormProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()
    {
        Timer.StartTimer(TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40), () => Prune(TimedEffects.Now));
        Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), RefreshVisibleForms);
    }

    private static void RefreshVisibleForms()
    {
        foreach (var (serial, state) in _states)
        {
            if (World.FindMobile((Serial)serial) is PlayerMobile owner && Owner(owner))
            {
                PublishAppearance(owner, state.Form.Ability);
            }
        }
    }

    private static void PublishAppearance(PlayerMobile owner, CombatAbility ability)
    {
        if (owner.Deleted || owner.Map == null || owner.Map == Map.Internal)
        {
            return;
        }

        byte form = ability switch
        {
            CombatAbility.Werewolf => DruidFormVisualProtocol.Wolf,
            CombatAbility.Werebear => DruidFormVisualProtocol.Bear,
            _ => DruidFormVisualProtocol.Human
        };
        Span<byte> packet = stackalloc byte[DruidFormVisualProtocol.Length];
        var writer = new System.Buffers.SpanWriter(packet);
        writer.Write((byte)0xBF);
        writer.Write((ushort)DruidFormVisualProtocol.Length);
        writer.Write(DruidFormVisualProtocol.SubCommand);
        writer.Write(DruidFormVisualProtocol.Version);
        writer.Write(owner.Serial.Value);
        writer.Write(form);
        foreach (var state in owner.Map.GetClientsInRange(owner.Location))
        {
            if (form == DruidFormVisualProtocol.Human || state.Mobile?.CanSee(owner) == true)
            {
                state.Send(packet);
            }
        }
    }
    private static bool Owner(PlayerMobile owner)=>owner is{Alive:true,Deleted:false}
        &&ClassCreation.Profiles?.TryGet(owner.Serial,out var profile)==true&&profile.Class==DiabloClass.Druid;
    internal static DruidPlayerForm State(Mobile owner)=>owner is PlayerMobile player&&Owner(player)
        ?_states.GetValueOrDefault(player.Serial.Value):null;
    // Keep elapsed percentages until the logical removal transaction captures
    // the old maximum. A getter must not remove them and clamp life first.
    internal static int LifePercent(PlayerMobile owner)=>State(owner)?.Form.Modifiers.LifePercent??0;
    internal static int StaminaPercent(PlayerMobile owner)=>State(owner)?.Form.Modifiers.StaminaPercent??0;
    internal static int AttackPercent(PlayerMobile owner)=>State(owner)?.Form.Modifiers.AttackPercent??0;
    internal static int DamagePercent(PlayerMobile owner)=>State(owner)?.Form.Modifiers.DamagePercent??0;
    internal static int ArmorPercent(PlayerMobile owner)=>State(owner)?.Form.Modifiers.ArmorPercent??0;
    internal static DruidPlayerForm Capture(PlayerMobile owner,CombatAbility ability,DateTime at)
    {
        if(!Owner(owner)||ability is not(CombatAbility.Werewolf or CombatAbility.Werebear)
            ||ClassCreation.Profiles.GetRank(owner.Serial,SkillMechanics.For(ability).TalentId)<=0)
        {throw new InvalidOperationException("Prepared form needs a live trained Druid");}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Player-form clock must be UTC");}
        var form=DruidFormModel.Transform(null,SkillMechanics.Values(owner,ability),0).State;
        var state=new DruidPlayerForm(owner.Serial.Value,form,at,at.AddMilliseconds(checked(form.Modifiers.DurationFrames*40)));
        Validate(state);return state;
    }
    internal static bool ApplyPrepared(PlayerMobile owner,CombatAbility ability,DateTime at)
    {
        var state=Capture(owner,ability,at);
        // Current Param12's direct switching is unresolved. This factory only
        // handles entering from human; do not silently use legacy cross-unshift.
        if(_states.ContainsKey(owner.Serial.Value)){return false;}
        var before=BattleOrders.Capture(owner);
        _states=_states.Add(owner.Serial.Value,state);
        BattleOrders.Change(owner,before);
        PublishAppearance(owner, ability);
        return true;
    }
    internal static bool Clear(PlayerMobile owner) => Clear(owner, present: true);

    internal static bool Clear(PlayerMobile owner, bool present)
    {
        if(owner==null||!_states.TryGetValue(owner.Serial.Value,out var state)){return false;}
        var before=owner is{Alive:true,Deleted:false}?BattleOrders.Capture(owner):(BattleOrders.Pools?)null;
        _states=_states.Remove(owner.Serial.Value);
        PublishAppearance(owner, CombatAbility.None);
        DruidAttackCharges.Clear(owner);DruidChargedAttacks.Cancel(owner);DruidFireClaws.Cancel(owner);DruidHunger.Cancel(owner);DruidRabies.Cancel(owner);DruidShockWave.Cancel(owner);DruidFury.Cancel(owner);
        if(before is{} pools){BattleOrders.Change(owner,pools);}
        else if(!owner.Deleted){ClassStats.Refresh(owner);}
        if(present){DruidFormEffects.Transform(owner,state.Form.Ability,entering:false);}
        return true;
    }
    internal static void Prune(DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Player-form expiry clock must be UTC");}
        foreach(var pair in _states)
        {
            var owner=World.FindMobile((Serial)pair.Key) as PlayerMobile;
            if(owner==null||!Owner(owner))
            {_states=_states.Remove(pair.Key);if(owner!=null){PublishAppearance(owner,CombatAbility.None);DruidAttackCharges.Clear(owner);DruidChargedAttacks.Cancel(owner);DruidFireClaws.Cancel(owner);DruidHunger.Cancel(owner);DruidRabies.Cancel(owner);DruidShockWave.Cancel(owner);DruidFury.Cancel(owner);}continue;}
            if(pair.Value.UntilUtc<=at){Clear(owner);}
        }
    }
    internal static DruidPlayerForm[] Snapshot()=>_states.Values.OrderBy(state=>state.Owner).ToArray();
    internal static void Validate(DruidPlayerForm state)
    {
        if(state==null||state.Owner==0||!((Serial)state.Owner).IsMobile||state.Form==null
            ||state.StartedUtc.Kind!=DateTimeKind.Utc||state.UntilUtc.Kind!=DateTimeKind.Utc||state.Form.StartedFrame!=0)
        {throw new InvalidDataException("Invalid saved player-form identity/clock");}
        try{DruidFormModel.Validate(state.Form);}
        catch(ArgumentException error){throw new InvalidDataException("Invalid saved player-form modifiers",error);}
        if(state.UntilUtc-state.StartedUtc!=TimeSpan.FromMilliseconds(checked(state.Form.Modifiers.DurationFrames*40)))
        {throw new InvalidDataException("Saved player-form lifetime escaped its captured ranks");}
    }
    internal static void Restore(DruidPlayerForm[] states)
    {
        if(states==null||states.Length>1000000){throw new InvalidDataException("Missing player-form state");}
        var next=ImmutableDictionary<uint,DruidPlayerForm>.Empty;
        foreach(var state in states)
        {Validate(state);if(next.ContainsKey(state.Owner)){throw new InvalidDataException("Duplicate player-form owner");}next=next.Add(state.Owner,state);}
        // Validation is atomic; malformed input cannot partially replace live
        // forms. Serialization reads an immutable snapshot without pruning.
        _states=next;
    }
}
