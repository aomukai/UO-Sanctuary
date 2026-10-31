// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record MonsterReturnIdentity(string Source,int Class,int Base,int HitClass,int ModeFlags,int Velocity,bool Killable,bool Town);
internal sealed record MonsterReturnMetadata(int Class,int Base,int Modes,int Velocity,bool Killable,string Mapping);

// The source's return policy is shared; native mode/room/display/AI services
// are explicit adapters. No source animation identity is invented for native
// dungeon archetypes, whose -1 class/base means no D2 class-specific exemption.
internal sealed class NativeMonsterReturn:MonsterReturnServices
{
    private static readonly Lazy<Dictionary<string,MonsterReturnIdentity>> Identities=new(()=>
    {
        var rows=BalanceCatalog.Read<MonsterReturnIdentity>("monster-return-identities");
        if(rows.Length!=751||rows.Select(r=>r.Class).Distinct().Count()!=751||rows.Any(r=>r.Class<0||r.Base<0||r.Velocity<0||r.HitClass<0))
        {throw new InvalidDataException("Invalid pinned monster return identities");}
        return rows.ToDictionary(r=>r.Source,StringComparer.Ordinal);
    });
    internal static MonsterReturnIdentity Identity(string source)=>Identities.Value.TryGetValue(source,out var row)?row:throw new NotSupportedException("Missing pinned return identity: "+source);
    internal sealed class State
    {
        internal int LastHitClass,LastLifePercent,LastMode,Ai,Refreshes,RecoveryNotifications,Packets;
    }
    private static readonly ConditionalWeakTable<BaseCreature,State> States=new();
    internal static State Session(BaseCreature target)=>States.GetValue(target,t=>new(){LastLifePercent=Percent(t)});
    private static int Percent(BaseCreature target)
    {int hp=SkillHealth.Balance256(target)>>8,max=SkillHealth.Maximum256(target)>>8;return max>0&&hp<max?(byte)((hp<<7)/max):128;}
    internal static MonsterReturnMetadata Metadata(BaseCreature target)
    {
        var profile=CreatureProfiles.For(target);
        if(target is EncounterCreature)
        {
            if(profile.HitRecovery==null||profile.Traits.Size==null||profile.Traits.BlockChance==null)
            {throw new NotSupportedException("Unconfigured native encounter return traits");}
            return new(-1,-1,(profile.HitRecovery.Enabled?1<<3:0)|(profile.Traits.BlockChance>0?1<<6:0),
                profile.Movement.WalkMs is >0 and var ms?Math.Max(1,1000/ms):0,true,"configured native dungeon archetype");
        }
        string source=CreatureProfiles.Definition(target).Reference;
        if(source==null||!profile.SourceStats.TryGetValue("hcIdx",out string index)||!int.TryParse(index,out int id))
        {throw new NotSupportedException("No reviewed source/native recipient return mapping");}
        var identity=Identity(source);
        int modes=0;
        foreach(var pair in new[]{(3,"mGH"),(6,"mBL"),(13,"mKB")})
        {if(profile.SourceModes.TryGetValue(pair.Item2,out string value)&&value=="1"){modes|=1<<pair.Item1;}}
        if(identity.Class!=id||identity.ModeFlags!=modes)
        {throw new NotSupportedException("Recipient source/current return metadata conflict: "+source);}
        return new(id,identity.Base,modes,identity.Velocity,identity.Killable,"reviewed source row "+source);
    }
    private readonly Mobile Source;private readonly PlayerMobile Owner;private readonly BaseCreature Target;
    private readonly MonsterReturnMetadata MetadataValue;private readonly State Current;private readonly Action Death;
    private readonly Action Recovery;
    internal NativeMonsterReturn(Mobile source,PlayerMobile owner,BaseCreature target,MonsterReturnMetadata metadata,Action death,Action recovery=null)
    {Source=source;Owner=owner;Target=target;MetadataValue=metadata;Current=Session(target);Death=death;Recovery=recovery;}
    internal override int? AttackerKind=>Source is PlayerMobile?0:1;
    internal override int TargetMode=>Target is{Alive:true,Deleted:false}?1:12; // Only death/dead are distinguished by this return branch.
    internal override int TargetClass=>MetadataValue.Class;
    internal override bool HasRoom()=>Target.Map is{} map&&map!=Map.Internal;
    internal override bool RoomInTown()=>SpellHelper.IsTown(Target.Location,Owner);
    internal override bool? AttackerTownRule()=>Source is SkeletonGuardian actor?Identity(actor.SourceMonsterId).Town
        :throw new NotSupportedException("This native return source has no NPC town rule");
    internal override bool Uninterruptible()=>MonsterUninterruptibles.Active(Target);
    internal override bool Frozen()=>Target.Frozen||NamedControl.FrozenRemaining(Target)>0;
    internal override bool Stunned()=>CombatStuns.Remaining(Target)>0;
    internal override int BaseId()=>MetadataValue.Base;
    internal override int? ModeFlags()=>MetadataValue.Modes;
    internal override int CurrentLife256=>SkillHealth.Balance256(Target);
    internal override int MaximumLife256=>SkillHealth.Maximum256(Target);
    internal override int LastSentLifePercent=>Current.LastLifePercent;
    // The native actor has no recovered D2 seed. Preserve the existing native
    // random service explicitly; source RNG chronology remains unverified.
    internal override uint RollDefender()=>unchecked((uint)Random.Shared.NextInt64(1L<<32));
    internal override void LastHitClass(int value)=>Current.LastHitClass=value;
    internal override void DeathDelay()=>throw new NotSupportedException("Live recipient mode completion/death delay needs its actor adapter");
    internal override void Kill()=>Death();
    internal override void Mode(int mode,bool targetAttacker)
    {
        Current.LastMode=mode;
        switch(mode)
        {
            case PreparedMonsterReturn.GetHit:
                if(!CreatureHitRecovery.BeginPreparedGetHit(Target,checked(CreatureHitRecovery.Frames(CreatureProfiles.For(Target).HitRecovery)*40)))
                {throw new NotSupportedException("Native recipient rejected source get-hit mode");}break;
            case PreparedMonsterReturn.Block:SourceBlocking.Present(Target,true);break;
            case PreparedMonsterReturn.Knockback:
                if(!targetAttacker||!SourceKnockbacks.Begin(Source,Target)){throw new NotSupportedException("Native recipient rejected source knockback mode");}break;
            default:throw new NotSupportedException("Unsupported native return mode");
        }
    }
    internal override void Refresh(){Current.Refreshes++;Target.Delta(MobileDelta.Hits);Target.InvalidateProperties();}
    internal override void MarkLifeAndHitClass(){Current.LastLifePercent=Percent(Target);Target.Delta(MobileDelta.Hits);}
    internal override void AiState(int state)
    {
        if(state!=19){throw new NotSupportedException("Unmapped native return AI state");}
        Current.Ai=state;Target.AIObject?.ResetApproachState();Target.AIObject?.AITimer.Prod();
    }
    internal override void RecoveryNotification()
    {
        // Actors supply their reviewed recovery callback. Iron refuses an
        // unadapted Lightning Enchanted producer; player contacts retain the
        // existing native OnStruck adapter, whose source RNG remains unverified.
        if(Recovery!=null){Recovery();}else{IronGolemContact.RequireRecoveryEvents(Target);}
        Current.RecoveryNotifications++;
    }
}
