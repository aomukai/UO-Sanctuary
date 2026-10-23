// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public sealed record SourceBlockRule(string Source,int Normal,int Nightmare,int Hell,bool Innate,bool BlockMode,string[] ShieldCodes,bool[] ShieldItems);
public sealed record SourceShield(uint Actor,string Source,int Choice);
public sealed class SourceBlockProfiles:GenericPersistence
{
    public SourceBlockProfiles():base("SanctuarySourceBlocking",143){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(SourceBlocking.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown source blocking version");}
        SourceBlocking.Restore(JsonSerializer.Deserialize<SourceShield[]>(reader.ReadString()));
    }
    public override void PostDeserialize()=>SourceBlocking.Snapshot();
}
public static class SourceBlocking
{
    private static readonly Dictionary<string,SourceBlockRule> _rules=new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<uint,SourceShield> _shields=new();
    internal static Func<int,int> ChoiceRoll=Utility.Random;
    internal static Func<int,int> BlockRoll=Utility.Random;
    public static SourceBlockProfiles Profiles{get;private set;}
    public static void Configure()
    {
        Profiles=new();foreach(var row in BalanceCatalog.Read<SourceBlockRule>("source-block-rules"))
        {
            if(row.ShieldCodes==null || row.ShieldItems==null || row.ShieldCodes.Length!=row.ShieldItems.Length || !_rules.TryAdd(row.Source,row))
            {throw new InvalidDataException("Invalid source block rule");}
        }
    }
    internal static SourceShield Shield(BaseCreature actor)=>actor!=null?_shields.GetValueOrDefault(actor.Serial.Value):null;
    internal static bool Special(string source)=>source is "diablo" or "doomknight1" or "diabloclone";
    internal static int Rate(int basis,bool innate,string source,bool shield)=>innate || Special(source) || source!="act3hire" && shield?Math.Clamp(basis,0,75):0;
    internal static bool TryRate(BaseCreature actor,out int rate,out bool blockMode)
    {
        rate=0;blockMode=false;if(actor is not{Alive:true,Deleted:false}){return false;}
        var profile=CreatureProfiles.For(actor);
        // Existing explicitly configured native dungeon archetypes retain their
        // effective block trait; their source-component model is not a D2 actor.
        if(actor is EncounterCreature && profile.Traits.BlockChance is{} native)
        {rate=Math.Clamp(native,0,75);blockMode=rate>0;return true;}
        string source=CreatureProfiles.Definition(actor).Reference;
        if(source==null || !_rules.TryGetValue(source,out var row)){return false;}
        blockMode=row.BlockMode;
        int basis=MonsterVariants.Difficulty(actor) switch{MonsterDifficulty.Nightmare=>row.Nightmare,MonsterDifficulty.Hell=>row.Hell,_=>row.Normal};
        bool enabled=row.Innate || Special(source);
        if(!enabled && source!="act3hire" && row.ShieldCodes.Length>0)
        {
            if(!_shields.TryGetValue(actor.Serial.Value,out var saved) || saved.Source!=source)
            {
                // Source MONSTER_SetComponents has an independent-choice branch
                // when no regional appearance pool is supplied. Persist the SH
                // component on first use for existing-save compatibility. Native
                // regional pools/RNG order/visual equipment remain acceptance.
                int choice=Math.Clamp(ChoiceRoll(row.ShieldCodes.Length),0,row.ShieldCodes.Length-1);
                _shields[actor.Serial.Value]=saved=new(actor.Serial.Value,source,choice);
            }
            enabled=row.ShieldItems[saved.Choice];
        }
        rate=Rate(basis,row.Innate,source,enabled);return true;
    }
    internal static bool TryBlock(BaseCreature actor,bool physical,out bool blocked)
    {
        blocked=false;if(!TryRate(actor,out int rate,out bool animation)){return false;}
        blocked=physical && rate>0 && BlockRoll(100)<rate;
        if(blocked)
        {
            if(animation && actor is GraveyardUndead undead){undead.InterruptForRecovery();}
            actor.FixedEffect(0x375A,5,5,0x481,0);actor.PlaySound(0x1A4); // Identified block/SH presentation placeholders.
        }
        return true;
    }
    public static SourceShield[] Snapshot()
    {
        foreach(uint serial in _shields.Keys.ToArray())
        {if(World.FindMobile((Serial)serial) is not BaseCreature{Alive:true,Deleted:false}){_shields.Remove(serial);}}
        return _shields.Values.OrderBy(x=>x.Actor).ToArray();
    }
    public static void Restore(SourceShield[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing source shields");}
        _shields.Clear();foreach(var state in saved)
        {
            if(!((Serial)state.Actor).IsMobile || !_rules.TryGetValue(state.Source,out var row) || state.Choice<0 || state.Choice>=row.ShieldCodes.Length || !_shields.TryAdd(state.Actor,state))
            {throw new InvalidDataException("Invalid source shield");}
        }
    }
}
