// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
namespace Sanctuary;

internal sealed class GenericPetAiPrimary(short ai){internal short Ai=ai;}
internal sealed record GenericPetAiSelectorUnit(int Kind,bool HasData,GenericPetAiPrimary Primary);
internal sealed record GenericPetAiSelection(bool Special,int Index);
internal sealed record GenericPetAiSelectionServices(Func<int,GenericPetAiSelectorUnit> Unit,Func<int,bool> CanSwitch);
internal static class GenericPetAiSelectionPolicy
{
    // Current439fe0. Cache the primary pointer before checking states10..12,
    // but read its signed AI index afterwards. No special-state clamp exists.
    internal static GenericPetAiSelection Select(int actor,int state,GenericPetAiSelectionServices services)
    {
        var unit=actor==0?null:services.Unit(actor);
        var primary=unit is{Kind:1,HasData:true}?unit.Primary:null;
        int accepted=state;
        if(unchecked((uint)(state-10))<=2&&!services.CanSwitch(actor)){accepted=0;}
        if(accepted!=0){return new(true,accepted);}
        if(primary==null){throw new NotSupportedException("Current normal AI selector requires monster primary data");}
        int index=primary.Ai;return new(false,index is>=0 and<155?index:0);
    }
}

internal sealed record GenericPetAiSwitchUnit(int Kind,int ClassId,byte Selector,int Mode,uint Flags124,uint Flags128,
    bool HasData,byte Flags1a,int StateCount,bool HasRoot,int Root1c,uint Rootb34,uint Rootb40);
internal sealed record GenericPetAiSwitchRow(byte Flags60,byte Flags62);
internal sealed record GenericPetAiSwitchServices(Func<int,GenericPetAiSwitchUnit> Unit,
    Func<byte,int,GenericPetAiSwitchRow> Primary,Func<byte,int,bool?> Walk);
internal static class GenericPetAiSwitch
{
    // Complete current27d980 over explicit unit/status and existing metadata
    // services. Its four options stay distinct;the selector passes0/0/1/1.
    internal static bool Check(int actor,bool attached,bool category,bool boss,bool switchAi,GenericPetAiSwitchServices services)
    {
        if(actor==0){return false;}var unit=services.Unit(actor);if(unit?.Kind!=1){return false;}
        if(unit.Selector is <1 or >3){throw new NotSupportedException("AI switch predicate requires selector1..3");}
        bool active=unit.HasRoot&&unit.Root1c<0;
        if(attached&&unit.StateCount>143&&active&&(unit.Rootb40&0x8000)!=0){return false;}
        if(unit.StateCount>54&&active&&(unit.Rootb34&0x400000)!=0){return false;}
        var primary=services.Primary(unit.Selector,unit.ClassId);if(primary==null){return false;}
        if(services.Walk(unit.Selector,unit.ClassId)!=true){return false;}
        if(boss&&(primary.Flags60&64)!=0||switchAi&&(primary.Flags62&1)==0){return false;}
        // Metadata calls can affect the unit. The source rereads flags/mode
        // here while using its earlier cached metadata selector/class.
        var after=services.Unit(actor);
        if((after.Flags124&0x10004)==0)
        {
            if(after.Kind==0){if(after.Mode is not(0 or 17)){return false;}}
            else if(after.Kind==1){if(after.Mode is not(0 or 12)){return false;}}
            else if((after.Flags128&0x4000000)==0){return false;}
        }
        return !category||after.Kind!=1||!after.HasData||(after.Flags1a&10)==0;
    }
}

public sealed record GenericPetAiMonster(int Id,string Name,short Ai);
public sealed record GenericPetAiDescriptor(int Id,string Name,int Type,uint Word4,int Function8,int Think,int Function24);
public static class GenericPetAiTables
{
    private static GenericPetAiMonster[] _monsters;
    private static GenericPetAiDescriptor[] _normal;
    public static void Configure()
    {
        var monsters=BalanceCatalog.Read<GenericPetAiMonster>("generic-pet-ai-monsters");
        var normal=BalanceCatalog.Read<GenericPetAiDescriptor>("generic-pet-ai-descriptors");
        if(monsters.Length!=751||normal.Length!=155){throw new InvalidDataException("Missing pinned AI tables");}
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<monsters.Length;i++){if(monsters[i] is not{} row||row.Id!=i||string.IsNullOrWhiteSpace(row.Name)||!names.Add(row.Name)){throw new InvalidDataException("Invalid AI monster index");}}
        names.Clear();for(int i=0;i<normal.Length;i++)
        {if(normal[i] is not{} row||row.Id!=i||string.IsNullOrWhiteSpace(row.Name)||!names.Add(row.Name)||row.Type is<0 or>6||row.Function8<0||row.Think<0||row.Function24<0){throw new InvalidDataException("Invalid AI descriptor");}}
        _monsters=monsters;_normal=normal;
    }
    internal static GenericPetAiMonster Monster(int id)=>id>=0&&id<_monsters.Length?_monsters[id]:null;
    internal static GenericPetAiDescriptor Descriptor(GenericPetAiSelection selection)
    {
        if(selection.Special){throw new NotSupportedException("Current special AI descriptor is not yet published");}
        return selection.Index>=0&&selection.Index<_normal.Length?_normal[selection.Index]:throw new NotSupportedException("Unknown normal AI descriptor");
    }
    internal static GenericPetAiDescriptor Original(SkeletonGuardian body,int state,Func<int,bool> canSwitch)
    {
        if(body is not{Deleted:false,Alive:true}||!WarlockDemonPlacement.Prepared(body))
        {throw new NotSupportedException("AI metadata requires an original prepared demon");}
        if(DeathMarkWorld.State(body) is not{Selector:1}){throw new NotSupportedException("Original AI metadata requires the qualified selector1 view");}
        int actor=checked((int)body.Serial.Value),classId=WarlockDemonPlacement.ClassId(body);
        var row=Monster(classId)??throw new NotSupportedException("No current source monster AI row");
        if(row.Name!=GenericPetSearchTables.SourceName(classId)){throw new InvalidDataException("Original source AI identity disagrees");}
        var services=new GenericPetAiSelectionServices(id=>id==actor?new(1,true,new(row.Ai)):throw new NotSupportedException("Wrong native AI identity"),canSwitch);
        return Descriptor(GenericPetAiSelectionPolicy.Select(actor,state,services));
    }
}
