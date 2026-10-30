// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

public sealed record NativeAbilityClock(string Path,string RuntimeType,int AbilityType,long RemainingMilliseconds);
public sealed record ReviveBeforeDeathState(int Version,DateTime CapturedAtUtc,ReviveCreatureSnapshot Creature,int Hits,int MaximumHits,
    int Mana,int MaximumMana,int Stamina,int MaximumStamina,int Strength,int Dexterity,int Intelligence,
    bool Frozen,bool CantWalk,bool Hidden,bool Blessed,string EquipmentJson,string AbilityClocksJson);

// Capture at accepted BaseCreature.OnBeforeDeath, before Mobile.Kill clears
// resources/statuses and moves equipped items. Damage may already have
// changed resources; source Revive HP uses its separate base-life/passive route.
internal static class ReviveBeforeDeath
{
    private static readonly Dictionary<uint,(string Json,string Issue)> Pending=new();
    internal static void Capture(BaseCreature actor)
    {
        if(actor is not{Deleted:false,Controlled:false,Summoned:false}||actor is BaseVendor or GraveyardUndead{Raised:true}
            ||!ReviveOriginals.SupportedDeathPath(actor.GetType())){return;}
        string source=CreatureProfiles.Definition(actor).Reference;
        if(GenericPetSearchTables.SourceId(source) is not{} cls||ReviveSpecies.ForClass(cls)!=true||!SourceAiEligibility.CanSwitch(cls,SourceAiEligibility.CurrentMonster)){return;}
        try{CaptureCore(actor,source);}
        catch(Exception e)when(e is InvalidDataException or NotSupportedException or JsonException or OverflowException or ArgumentException)
        {Pending[actor.Serial.Value]=(null,"Accepted-death snapshot unavailable: "+e.Message);}
    }
    private static void CaptureCore(BaseCreature actor,string source)
    {
        var equipment=actor.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank&&!i.Deleted).Select(FrozenEquipmentEffects.Capture).ToArray();
        var clocks=new List<NativeAbilityClock>();var visiting=new HashSet<MonsterAbility>();
        void Visit(MonsterAbility ability,string path)
        {
            if(ability==null||!visiting.Add(ability)){throw new NotSupportedException("Missing or cyclic native ability group");}
            clocks.Add(new(path,ability.GetType().FullName,(int)ability.AbilityType,ability.RemainingTriggerCooldown(actor)));
            if(ability is MonsterAbilityGroup group)
            {var children=group.MembersForSnapshot();for(int i=0;i<children.Length;i++){Visit(children[i],path+"/"+i);}}
            visiting.Remove(ability);
        }
        var abilities=actor.GetMonsterAbilities()??[];for(int i=0;i<abilities.Length;i++){Visit(abilities[i],i.ToString(System.Globalization.CultureInfo.InvariantCulture));}
        var state=new ReviveBeforeDeathState(0,DateTime.UtcNow,ReviveCreatureSnapshots.Capture(actor,source),actor.Hits,actor.HitsMax,
            actor.Mana,actor.ManaMax,actor.Stam,actor.StamMax,actor.Str,actor.Dex,actor.Int,actor.Frozen,actor.CantWalk,actor.Hidden,actor.Blessed,
            JsonSerializer.Serialize(equipment),JsonSerializer.Serialize(clocks));
        string json=JsonSerializer.Serialize(state);Validate(json,actor.GetType().FullName);Pending[actor.Serial.Value]=(json,null);
    }
    internal static (string Json,string Issue) Take(BaseCreature actor)=>actor!=null&&Pending.Remove(actor.Serial.Value,out var pending)?pending:(null,null);
    internal static bool PreserveLoot(BaseCreature actor,Corpse corpse,Item item)
    {
        if(actor==null||corpse?.Owner!=actor||item is not{Deleted:false,Movable:true,LootType:LootType.Regular}
            ||item.Parent!=corpse||!Pending.TryGetValue(actor.Serial.Value,out var pending)||pending.Json==null){return false;}
        return Equipment(Decode(pending.Json)).Any(e=>e.OriginalItem==item.Serial.Value);
    }
    // An independently rejected/cancelled native death must not leave a saved
    // reconstruction record. Pending data is transient and never persisted.
    internal static void PrunePending()
    {Pending.Clear();}
    internal static ReviveBeforeDeathState Decode(string json)
    {
        try{return JsonSerializer.Deserialize<ReviveBeforeDeathState>(json)??throw new InvalidDataException("Missing before-death original creature state");}
        catch(Exception e)when(e is JsonException or ArgumentException){throw new InvalidDataException("Invalid before-death original creature state",e);}
    }
    internal static FrozenEquipmentSnapshot[] Equipment(ReviveBeforeDeathState state)
        =>FrozenEquipmentEffects.Decode<FrozenEquipmentSnapshot[]>(state.EquipmentJson);
    internal static NativeAbilityClock[] Clocks(ReviveBeforeDeathState state)
        =>FrozenEquipmentEffects.Decode<NativeAbilityClock[]>(state.AbilityClocksJson);
    internal static void Validate(string json,string runtimeType)
    {
        var state=Decode(json);
        if(state.Version!=0||state.CapturedAtUtc.Kind!=DateTimeKind.Utc||state.CapturedAtUtc==default||state.Creature==null||state.Creature.RuntimeType!=runtimeType
            ||state.MaximumHits<1||state.Hits<0||state.Hits>state.MaximumHits||state.Mana<0||state.Mana>state.MaximumMana||state.Stamina<0||state.Stamina>state.MaximumStamina)
        {throw new InvalidDataException("Invalid before-death identity/resources");}
        var combat=ReviveCreatureSnapshots.Combat(state.Creature);ReviveCreatureSnapshots.Validate(state.Creature,combat.Reference);
        var equipment=Equipment(state);var clocks=Clocks(state);
        if(equipment.Any(e=>e==null)||equipment.Select(e=>e.OriginalItem).Distinct().Count()!=equipment.Length
            ||clocks.Any(c=>c==null||string.IsNullOrWhiteSpace(c.Path)||string.IsNullOrWhiteSpace(c.RuntimeType)||c.RemainingMilliseconds<0)
            ||clocks.Select(c=>c.Path).Distinct().Count()!=clocks.Length)
        {throw new InvalidDataException("Invalid before-death equipment/ability clock identity");}
        foreach(var item in equipment){FrozenEquipmentEffects.Validate(item);}
    }
}
