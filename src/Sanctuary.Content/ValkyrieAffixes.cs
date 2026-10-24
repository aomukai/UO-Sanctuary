// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server;

namespace Sanctuary;

public sealed record SummonAffix(int Row,int Version,bool FirstPropertySockets,AffixDefinition Definition,JsonElement SourceFields);
public sealed record SummonAffixBase(string Code,string Type,int QualityLevel,int MagicLevel,string RequiredClass,JsonElement SourceFields);
public sealed record SummonEquipChoice(int Slot,string Code,int Quality);
public sealed record SummonEquipRow(int Rank,SummonEquipChoice[] Choices);
public sealed record SummonEquipmentRequest(int Slot,string Code,int Quality,int ItemLevel,
    bool NoSockets=true,bool NoEthereal=true,bool NoSell=true,bool RestoreDurability=true);
public sealed record SummonAffixCatalogue(string ReferenceVersion,SummonAffix[] Affixes,SummonAffixBase[] Bases,GearItemType[] Types,SummonEquipRow[] EquipmentRows);

// Source selection component, not a complete item generator. Source row IDs are
// references into the pinned extraction, never claims about modern binary IDs.
// No player supported-property filter or ordinary loot-quality roll is applied.
public static class ValkyrieAffixes
{
    private static SummonAffix[] _affixes;
    private static Dictionary<string,SummonAffix> _byId;
    private static Dictionary<string,SummonAffixBase> _bases;
    private static Dictionary<string,GearItemType> _types;
    private static SummonEquipRow[] _equipment;
    public static void Configure()
    {
        var options=new JsonSerializerOptions{PropertyNameCaseInsensitive=true};options.Converters.Add(new JsonStringEnumConverter());
        var catalog=JsonSerializer.Deserialize<SummonAffixCatalogue>(File.ReadAllText(Path.Join(Core.BaseDirectory,"Data","Sanctuary","Balance","valkyrie-affix-catalogue.json")),options);
        if(catalog is not{ReferenceVersion:"3.3.93847",Affixes:{Length:478},Bases:{Length:18},Types:{Length:105},EquipmentRows:{Length:19}})
        {throw new InvalidDataException("Invalid current summon affix catalogue");}
        _affixes=catalog.Affixes;_byId=_affixes.ToDictionary(x=>x.Definition.Id,StringComparer.Ordinal);
        _bases=catalog.Bases.ToDictionary(x=>x.Code,StringComparer.Ordinal);_types=catalog.Types.ToDictionary(x=>x.Code,StringComparer.Ordinal);
        _equipment=catalog.EquipmentRows;
        foreach(var row in _affixes)
        {
            var d=row.Definition;
            if(row.Row<0 || d.Frequency<=0 || d.Group<=0 || d.Include.Length==0 ||
                d.Include.Concat(d.Exclude).Any(t=>!_types.ContainsKey(t)) || row.SourceFields.ValueKind!=JsonValueKind.Object)
            {throw new InvalidDataException("Invalid summon affix source row");}
        }
        foreach(var row in _bases.Values)
        {if(!_types.ContainsKey(row.Type) || row.QualityLevel is <1 or >99 || row.MagicLevel<0){throw new InvalidDataException("Invalid summon source base");}}
        if(_equipment.Where((row,i)=>row.Rank<0 || i>0 && row.Rank>_equipment[i-1].Rank || row.Choices is not{Length:1}
            || row.Choices.Any(x=>x.Slot is <1 or >10 || !_bases.ContainsKey(x.Code) || x.Quality is not(4 or 6))).Any())
        {throw new InvalidDataException("Invalid Valkyrie equipment source rows");}
    }
    internal static IReadOnlyCollection<SummonAffix> All=>_affixes;
    internal static SummonAffix Find(string id)=>_byId.TryGetValue(id,out var row)?row:throw new InvalidDataException("Unknown current summon affix "+id);
    internal static SummonAffixBase Basis(string code)=>_bases.TryGetValue(code,out var row)?row:throw new InvalidDataException("Unknown Valkyrie source base "+code);
    internal static int Level(string code,int itemLevel)
    {
        if(itemLevel is <1 or >787){throw new ArgumentOutOfRangeException(nameof(itemLevel));}
        var basis=Basis(code);
        // The item retains its raw calc2 level. Only the affix-level context is
        // capped here; this is equivalent to the complete source level helper
        // over the supported range, including raw item levels above 99.
        return AffixBalance.Level(Math.Min(itemLevel,99),basis.QualityLevel,basis.MagicLevel);
    }
    internal static SummonAffix[] Pool(string code,int itemLevel,GearRarity rarity,AffixKind kind,IReadOnlyList<string> existing=null)
    {
        if(rarity is not(GearRarity.Magic or GearRarity.Rare) || !Enum.IsDefined(kind)){throw new ArgumentOutOfRangeException();}
        var basis=Basis(code);int level=Level(code,itemLevel);var groups=new HashSet<int>();
        foreach(var id in existing??Array.Empty<string>()){groups.Add(Find(id).Definition.Group);}
        bool IsType(string ancestor)=>EquipmentBalance.IsType(_types,basis.Type,ancestor);
        return _affixes.Where(row=>
        {
            var d=row.Definition;
            // Full legacy CanItemHaveMagicAffix rejects a socket first-property.
            // Exact modern socket-affix behavior is not certified by this old
            // helper, even though the summon requests no sockets independently.
            return d.Kind==kind && d.Level<=level && (d.MaxLevel==0 || level<=d.MaxLevel)
                && (rarity!=GearRarity.Rare || d.Rare) && !row.FirstPropertySockets && !groups.Contains(d.Group)
                && (string.IsNullOrEmpty(d.ClassSpecific) || string.IsNullOrEmpty(basis.RequiredClass) || d.ClassSpecific==basis.RequiredClass)
                && !d.Exclude.Any(IsType) && d.Include.Any(IsType);
        }).OrderBy(x=>x.Row).ToArray();
    }
    internal static int Weight(SummonAffix row,string code)=>checked(row.Definition.Frequency*(Basis(code).MagicLevel>0?row.Definition.Level:1));
    internal static uint Next(ref ulong seed,int bound)
    {
        if(bound<=0){return 0;}
        seed=0x6AC690C5UL*(uint)seed+(seed>>32);
        return (uint)seed%(uint)bound;
    }
    internal static SummonAffix Select(string code,int itemLevel,GearRarity rarity,AffixKind kind,ref ulong seed,bool force=true,IReadOnlyList<string> existing=null)
    {
        // Even a forced attempt consumes the original two-way seed draw.
        if(Next(ref seed,2)==0 && !force){return null;}
        var pool=Pool(code,itemLevel,rarity,kind,existing);
        if(pool.Length==0){return null;}
        int ticket=(int)Next(ref seed,checked(pool.Sum(x=>Weight(x,code))+1));
        foreach(var row in pool){ticket-=Weight(row,code);if(ticket<0){return row;}}
        return pool[^1]; // The legacy total+1 draw gives the last row the endpoint.
    }
    internal static void Equip(int rank,int itemLevel,ref ulong petSeed,Func<SummonEquipmentRequest,bool> create,IReadOnlyCollection<int> occupied=null)
    {
        ArgumentNullException.ThrowIfNull(create);
        if(rank is <1 or >255 || itemLevel is <1 or >787){throw new ArgumentOutOfRangeException();}
        var filled=new HashSet<int>(occupied??Array.Empty<int>());
        if(filled.Any(slot=>slot is <1 or >10)){throw new ArgumentOutOfRangeException(nameof(occupied));}
        foreach(var row in _equipment)
        {
            if(row.Rank>rank){continue;}
            // Every visited row consumes a pet-seed draw, including singleton
            // choices and occupied slots. Item creation uses a separate seed.
            var choice=row.Choices[(int)Next(ref petSeed,row.Choices.Length)];
            if(!filled.Contains(choice.Slot) && create(new(choice.Slot,choice.Code,choice.Quality,itemLevel)))
            {filled.Add(choice.Slot);}
            // Failure leaves the slot free, so later lower-rank rows can retry.
        }
    }
}
