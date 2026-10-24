// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record CloakCasterState(uint Owner,int Rank,int Defense,DateTime UntilUtc);
public sealed class CloakProfiles:GenericPersistence
{
    private CloakCasterState[] _snapshot;
    public CloakProfiles():base("SanctuaryCloakShadows",151){}
    public override void Serialize(){_snapshot=CloakShadows.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader){if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Cloak version");}CloakShadows.Restore(JsonSerializer.Deserialize<CloakCasterState[]>(reader.ReadString()));}
    public override void PostDeserialize()=>CloakShadows.Prune();
}
public static class CloakShadows
{
    private static readonly Dictionary<uint,CloakCasterState> _owners=new();
    private static readonly HashSet<uint> _targets=new();
    public static CloakProfiles Profiles{get;private set;}
    public static void Configure(){Profiles=new();EventSink.WorldSave+=Prune;}
    public static void Initialize()=>Timer.StartTimer(TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(40),Prune);
    internal static CloakCasterState State(Mobile owner)=>owner is{Alive:true,Deleted:false}
        && ClassCreation.Profiles.TryGet(owner.Serial,out var profile) && profile.Class==DiabloClass.Assassin
        && _owners.TryGetValue(owner.Serial.Value,out var state) && state.UntilUtc>TimedEffects.Now?state:null;
    public static int Defense(Mobile owner)=>State(owner)?.Defense??0;
    internal static CloakCasterState Capture(uint owner,SkillValues values,DateTime at)
    {
        if(values.Ability!=CombatAbility.CloakOfShadows || values.Rank is <1 or >255){throw new ArgumentOutOfRangeException(nameof(values));}
        return new(owner,values.Rank,(int)values.Eval("passivecalc1"),at.AddMilliseconds(values.Eval("auralencalc")*40));
    }
    private static bool Valid(PlayerMobile owner)=>owner.Map!=null && owner.Map!=Map.Internal && !SpellHelper.IsTown(owner.Location,owner);
    internal static void Face(PlayerMobile owner,Point3D point)
    {
        if(owner.Map is{} map && map!=Map.Internal && point.X>=0 && point.Y>=0 && point.X<map.Width && point.Y<map.Height
            && (point.X!=owner.X || point.Y!=owner.Y)){owner.Direction=owner.GetDirectionTo(point);}
    }
    private static bool RecastAllowed(PlayerMobile owner)
    {
        // Corrected 3.3.93847 firsthand observation: a surviving affected mob
        // locks this caster globally. A fresh group or leaving the old group's
        // range must not bypass it. Derive this from the saved curse sources so
        // death, expiry and reload cannot leave a separate stale recast timer.
        foreach(uint id in _targets)
        {
            var target=World.FindMobile((Serial)id);
            if(target is{Alive:true,Deleted:false} && NecromancerEffects.Curse(target) is{Ability:CombatAbility.CloakOfShadows} curse
                && curse.Source==owner.Serial.Value){return false;}
        }
        return true;
    }
    public static bool Begin(PlayerMobile owner,Serial target,Point3D point,long now)
    {
        Face(owner,point);
        // Keep cursor direction for rejection feedback, but the actual effect
        // remains centered on the caster. No target identity is required.
        return SkillCombat.Begin(owner,CombatAbility.CloakOfShadows,Serial.Zero,owner.Location,now,(p,_,_,_)=>Valid(p) && RecastAllowed(p));
    }
    internal static bool Target(PlayerMobile owner,BaseCreature target,int radius)
    {
        bool boss=PaladinConversion.SourceRule(target)?.Boss??(target is not EncounterCreature && CreatureProfiles.For(target).Traits.Boss==true);
        // Source filter E003 excludes monstats Boss and town, has no LOS bit,
        // and its search separately excludes monstats NOAURA. Native dungeon
        // archetypes retain the existing explicit SourceAuraTargets adapter.
        return ClassCombat.IsHostile(owner,target) && !boss && SourceAuraTargets.Eligible(target)
            && AmazonInnerSight.InRadius(target.X-owner.X,target.Y-owner.Y,radius) && Math.Abs(target.Z-owner.Z)<16
            && !SpellHelper.IsTown(target.Location,target);
    }
    internal static bool BlindEligible(BaseCreature target)
    {
        if(target is EncounterCreature encounter){return encounter.Grade is not(EncounterGrade.Unique or EncounterGrade.SuperUnique);}
        // sub_6FD15190 also excludes attached Imps. Those actors have no native
        // attachment-state adapter; keep that source species branch unsupported.
        return PaladinConversion.SourceRule(target) is{SwitchAi:true,Npc:false,Attackable:true} row && row.Base!="imp1";
    }
    internal static NecromancerCurse Replacement(NecromancerCurse old,NecromancerCurse next)
    {
        // Target arbitration is separate from caster recast admission. Retain
        // the no-overwrite adapter; cross-caster/rank acceptance remains open.
        if(old?.Ability is CombatAbility.Attract or CombatAbility.CloakOfShadows){return null;}
        return next;
    }
    internal static bool Apply(PlayerMobile owner,BaseCreature target,SkillValues values,DateTime at)
    {
        if(target is not{Alive:true,Deleted:false} || values.Rank is <1 or >255){return false;}
        int frames=(int)values.Eval("auralencalc");if(!SourceCurseDuration.Incoming(target,"cloaked",frames,out frames)){return false;}
        bool blind=BlindEligible(target);var old=NecromancerEffects.Curse(target);
        var motion=blind?NecromancerEffects.CaptureMotion(target):null;
        var next=Replacement(old,new(CombatAbility.CloakOfShadows,owner.Serial.Value,values.Rank,at.AddMilliseconds(frames*40L),owner.X,owner.Y,motion,CloakBlind:blind));
        if(next==null){return false;}
        var state=NecromancerEffects.Profiles.Find(target)??new();NecromancerEffects.Profiles.Store(target,state with{Curse=next});
        if(blind)
        {
            if(TimedEffects.TryActor(target,out var actor) && actor.Control is{} control){actor.Control=control with{BlindUntil=default,FearUntil=default};}
            target.AIObject?.ResetApproachState();target.AIObject?.ClearMoveIntent();target.AIObject?.AITimer.Prod();
        }
        owner.DoHarmful(target,true);target.FixedEffect(0x375A,10,15,0x455,0); // Identified cloaked overlay placeholder.
        return true;
    }
    public static bool Release(PlayerMobile owner)
    {
        if(!owner.Alive || !AssassinCombat.Available(owner,CombatAbility.CloakOfShadows) || !Valid(owner) || !RecastAllowed(owner)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.CloakOfShadows);if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        var at=TimedEffects.Now;_owners[owner.Serial.Value]=Capture(owner.Serial.Value,values,at);StatNetwork.Send(owner);
        int radius=(int)values.Eval("aurarangecalc");var targets=new List<BaseCreature>();
        foreach(var target in owner.Map.GetMobilesInRange<BaseCreature>(owner.Location,(int)Math.Ceiling(SkillGeometry.Tiles(radius)))){if(Target(owner,target,radius)){targets.Add(target);}}
        targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));foreach(var target in targets){Apply(owner,target,values,at);}
        owner.FixedEffect(0x373A,10,15,0x455,0);owner.PlaySound(0x1E5); // Identified cast/darkness/audio placeholders; no invented invisibility.
        return true;
    }
    internal static void Changed(Mobile actor,NecromancerCurse before,NecromancerCurse after)
    {
        if(after?.Ability==CombatAbility.CloakOfShadows){_targets.Add(actor.Serial.Value);}else{_targets.Remove(actor.Serial.Value);}
        if(before?.Ability==CombatAbility.CloakOfShadows && NecromancerEffects.Vision(before) && !NecromancerEffects.Vision(after)
            || before?.Ability==CombatAbility.DimVision && after is{Ability:CombatAbility.CloakOfShadows,CloakBlind:false}){SourceTerror.Reset(actor);}
    }
    internal static void AfterLoad(IEnumerable<KeyValuePair<Serial,NecromancerState>> states)
    {_targets.Clear();foreach(var p in states){if(p.Value.Curse?.Ability==CombatAbility.CloakOfShadows){_targets.Add(p.Key.Value);}}}
    public static void Clear(Mobile owner){if(owner!=null && _owners.Remove(owner.Serial.Value) && owner is PlayerMobile p && !p.Deleted){StatNetwork.Send(p);}}
    internal static void Prune()
    {
        foreach(var row in _owners.ToArray())
        {
            var actor=World.FindMobile((Serial)row.Key);if(row.Value.UntilUtc<=TimedEffects.Now || actor is not{Alive:true,Deleted:false}
                || !ClassCreation.Profiles.TryGet((Serial)row.Key,out var profile) || profile.Class!=DiabloClass.Assassin){_owners.Remove(row.Key);if(actor is PlayerMobile p && !p.Deleted){StatNetwork.Send(p);}}
        }
        foreach(uint id in _targets.ToArray())
        {
            var actor=World.FindMobile((Serial)id);var state=actor==null?null:NecromancerEffects.Profiles.Find(actor);
            if(state?.Curse?.Ability!=CombatAbility.CloakOfShadows){_targets.Remove(id);}
            else if(state.Curse.UntilUtc<=TimedEffects.Now){NecromancerEffects.Profiles.Store(actor,state with{Curse=null});}
        }
    }
    public static CloakCasterState[] Snapshot(){Prune();return _owners.Values.OrderBy(x=>x.Owner).ToArray();}
    public static void Restore(CloakCasterState[] states)
    {
        if(states==null || states.Length>1000000){throw new InvalidDataException("Missing Cloak states");}_owners.Clear();
        foreach(var state in states)
        {
            if(state==null || !((Serial)state.Owner).IsMobile || state.Rank is <1 or >255){throw new InvalidDataException("Invalid Cloak identity");}
            var row=SkillMechanics.For(CombatAbility.CloakOfShadows);var values=new SkillValues(row,k=>k==row.SourceKey?state.Rank:0,_=>0,1);
            if(state.Defense!=values.Eval("passivecalc1") || !_owners.TryAdd(state.Owner,state)){throw new InvalidDataException("Invalid Cloak payload");}
        }
    }
}
