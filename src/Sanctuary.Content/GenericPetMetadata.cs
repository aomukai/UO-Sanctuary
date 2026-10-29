// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace Sanctuary;

public sealed record GenericPetSkillMetadata(int Id,string Name,byte Flags36,byte Flags39,byte AiType,short AuraState,short AuraTargetState);
public sealed record GenericPetStateMetadata(int Id,string Name,short Group);
internal sealed class GenericPetMetadataTable
{
    private readonly GenericPetSkillMetadata[] _skills;
    private readonly GenericPetStateMetadata[] _states;
    internal GenericPetMetadataTable(GenericPetSkillMetadata[] skills,GenericPetStateMetadata[] states)
    {
        ArgumentNullException.ThrowIfNull(skills);ArgumentNullException.ThrowIfNull(states);
        if(skills.Length==0||states.Length==0){throw new InvalidDataException("Missing pet metadata rows");}
        for(int i=0;i<states.Length;i++)
        {if(states[i] is not{} row||row.Id!=i||string.IsNullOrWhiteSpace(row.Name)){throw new InvalidDataException("Invalid pet state metadata");}}
        for(int i=0;i<skills.Length;i++)
        {
            if(skills[i] is not{} row||row.Id!=i||string.IsNullOrWhiteSpace(row.Name)
                ||row.AuraState< -1||row.AuraState>=states.Length||row.AuraTargetState< -1||row.AuraTargetState>=states.Length)
            {throw new InvalidDataException("Invalid pet skill metadata");}
        }
        _skills=(GenericPetSkillMetadata[])skills.Clone();_states=(GenericPetStateMetadata[])states.Clone();
    }
    internal int SkillCount=>_skills.Length;
    internal int StateCount=>_states.Length;
    internal GenericPetSkillMetadata Skill(int id)=>id>=0&&id<_skills.Length?_skills[id]:null;
    internal GenericPetStateMetadata State(int id)=>id>=0&&id<_states.Length?_states[id]:null;
    // This is an explicit adapter binding, not a claim that all three source
    // selectors alias this table or that byte445 means facet difficulty.
    internal GenericPetMetadataView Bind(byte selector)=>new(this,selector);
}
internal sealed class GenericPetMetadataView
{
    private readonly GenericPetMetadataTable _table;
    private readonly byte _selector;
    internal GenericPetMetadataView(GenericPetMetadataTable table,byte selector)
    {
        ArgumentNullException.ThrowIfNull(table);
        if(selector is <1 or >3){throw new NotSupportedException("Pet metadata requires an explicit source selector1..3");}
        _table=table;_selector=selector;
    }
    private void Require(byte selector)
    {if(selector!=_selector){throw new NotSupportedException("Pet metadata selector is not bound to this view");}}
    internal GenericPetSkillAiInfo SkillInfo(byte selector,int id)
    {Require(selector);return _table.Skill(id) is{} row?new(row.Flags36,row.Flags39,row.AiType):null;}
    internal GenericPetBuffSkill BuffSkill(byte selector,int id)
    {Require(selector);return _table.Skill(id) is{} row?new(row.AuraState,row.Flags39):null;}
    internal short? SearchSkill(byte selector,int id)
    {Require(selector);return _table.Skill(id)?.AuraTargetState;}
    internal int SkillCount(byte selector){Require(selector);return _table.SkillCount;}
    internal int StateCount(byte selector,bool wide){Require(selector);return _table.StateCount;}
    internal short? StateGroup(byte selector,int id){Require(selector);return _table.State(id)?.Group;}
    internal short RawStateGroup(byte selector,int id)
    {Require(selector);return _table.State(id)?.Group??throw new NotSupportedException("No bound raw pet state row");}
    internal GenericPetSearchStateServices SearchServices()=>new(SearchSkill,StateCount,StateGroup,RawStateGroup);
}
public static class GenericPetMetadata
{
    internal static GenericPetMetadataTable Table{get;private set;}
    public static void Configure()
    {
        // Construct both immutable tables before publishing a new view.
        Table=new(BalanceCatalog.Read<GenericPetSkillMetadata>("generic-pet-skills"),BalanceCatalog.Read<GenericPetStateMetadata>("generic-pet-states"));
    }
}
