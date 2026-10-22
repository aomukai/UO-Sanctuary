// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record CorpseSourceRule(string Source,bool Selectable,bool NoAura);
public sealed record CorpseSkillRecord(uint Corpse,string Source,bool Selectable,bool Owned,bool Shattered=false);
public sealed class CorpseSkillProfiles : GenericPersistence
{
    private readonly Dictionary<uint,CorpseSkillRecord> _rows=new();
    public CorpseSkillProfiles():base("SanctuaryCorpseSkillRules",122){}
    public CorpseSkillRecord Find(Corpse corpse)=>corpse!=null && _rows.TryGetValue(corpse.Serial.Value,out var row)?row:null;
    public void Store(CorpseSkillRecord row)=>_rows[row.Corpse]=row;
    public void Remove(Corpse corpse)=>_rows.Remove(corpse.Serial.Value);
    public override void Serialize(IGenericWriter writer)
    {
        var rows=new List<CorpseSkillRecord>();
        foreach(var row in _rows.Values){if(World.FindItem((Serial)row.Corpse) is Corpse {Deleted:false}){rows.Add(row);}}
        writer.Write(0);writer.Write(JsonSerializer.Serialize(rows));
    }
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown corpse skill metadata version");}
        _rows.Clear();foreach(var row in JsonSerializer.Deserialize<CorpseSkillRecord[]>(reader.ReadString()) ?? throw new InvalidDataException("Missing corpse skill metadata"))
        {if(!_rows.TryAdd(row.Corpse,row)){throw new InvalidDataException("Duplicate corpse skill metadata");}}
    }
}
public static class CorpseSkillRules
{
    private static readonly Dictionary<string,bool> _selectable=new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> _noAura=new(StringComparer.OrdinalIgnoreCase);
    public static CorpseSkillProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()
    {
        var previous=CorpseRecoveryHooks.Deleted;
        CorpseRecoveryHooks.Deleted=corpse=>{Profiles.Remove(corpse);previous?.Invoke(corpse);};
    }
    public static void Load()
    { _selectable.Clear();_noAura.Clear();foreach(var row in BalanceCatalog.Read<CorpseSourceRule>("corpse-source-rules")){_selectable.Add(row.Source,row.Selectable);if(row.NoAura){_noAura.Add(row.Source);}} }
    public static bool AllowsAura(Mobile target)
    {
        string key=target switch
        {
            BoneBarrierSegment barrier=>barrier.Ability==CombatAbility.BonePrison?"boneprison1":"bonewall",
            NecromancerMinion minion when minion.Kind!=CombatAbility.None=>SkillMechanics.For(minion.Kind).Text("summon"),
            SkeletonGuardian=>"necroskeleton",
            BaseCreature creature=>CreatureProfiles.Definition(creature).Reference,
            _=>null
        };
        return key==null || !_noAura.Contains(key);
    }
    public static void Capture(BaseCreature creature,Container container)
    {
        if(container is not Corpse corpse){return;}
        HeavenFists.CaptureDeath(creature,corpse);
        string key=CreatureProfiles.Definition(creature).Reference;
        bool known=key!=null && _selectable.TryGetValue(key,out bool selectable);
        Profiles.Store(new(corpse.Serial.Value,known?key:null,known && _selectable[key],creature.Controlled || creature.Summoned || creature is BaseVendor,PaladinHolyFreeze.Shattered(creature)));
    }
    public static bool Shattered(Corpse corpse)=>Profiles.Find(corpse)?.Shattered ?? false;
    public static bool Usable(PlayerMobile owner,Corpse corpse,double radius)
    {
        if(owner is not {Alive:true,Deleted:false} || corpse is not {Deleted:false,Parent:null,OwnerWasBaseCreature:true,OwnerWasSummoned:false,
            Carved:false,Animated:false,SanctuaryHarvested:false,SanctuaryConsumed:false,SanctuaryRestInPeace:false,IsBones:false}
            || corpse.Map!=owner.Map || Math.Abs(corpse.Z-owner.Z)>=16){return false;}
        var row=Profiles.Find(corpse);
        if(row==null && corpse.Owner is BaseCreature {Alive:false} creature){Capture(creature,corpse);row=Profiles.Find(corpse);}
        double dx=owner.X-corpse.X,dy=owner.Y-corpse.Y;
        return row is{Selectable:true,Owned:false,Shattered:false} && dx*dx+dy*dy<=radius*radius
            && owner.CanSee(corpse) && owner.InLOS(corpse) && corpse.CanLoot(owner,null) && !corpse.IsCriminalAction(owner);
    }
}
