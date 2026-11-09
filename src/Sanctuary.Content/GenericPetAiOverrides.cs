// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace Sanctuary;

internal sealed record GenericPetAiOverrideHeader(int State,int Mode10);
internal sealed record GenericPetAiOverrideUnit(GenericPetDecisionUnit Position,int Mode,uint Flags124,uint Flags128,
    bool HasData,bool HasAi,int OwnerGame,int OwnerKind,int OwnerId)
{
    internal bool Active=>(Flags124&0x10000)==0&&(Position.Kind switch{0=>Mode is not(0 or 17),1=>Mode is not(0 or 12),_=>(Flags128&0x4000000)==0});
}
internal sealed record GenericPetAiOverrideFrame(GenericPetDecisionFrame Decision,GenericPetOwnerSurveyState Survey);
internal sealed record GenericPetAiOverrideSearch(int High48,bool ExternalStorage);
internal sealed record GenericPetAiOverrideServices(Func<GenericPetAiControl,GenericPetAiOverrideHeader> Header,
    Func<int,GenericPetAiOverrideUnit> Unit,Func<int,int,int,int> Resolve,
    Func<int,int,GenericPetAiOverrideFrame> Initialize,Func<int,int,GenericPetAiOverrideFrame,bool> Follow,
    Action<int,int,GenericPetAiTick> Default,Func<int,bool> Moving,
    Func<int,int,GenericPetAiOverrideFrame,int,int,GenericPetAiOverrideSearch> Search,
    Func<int,int[]> Events,Func<int,byte> EventType,Action<int> RemoveEvent,Action<int,int> Idle,
    Action<GenericPetAiOverrideSearch> ReleaseSearch,Action<GenericPetAiOverrideFrame> ReleaseFrame,Action<int,int,int> MissingOwner);
internal static class GenericPetAiOverrides
{
    // Complete360-byte4b10b0:empty-default copy,three explicit prefix writes,
    // then the existing player-owner survey. No command capture or control
    // publication occurs here;do not substitute the different4b0b90 initializer.
    internal static GenericPetAiOverrideFrame Initialize(int actor,int owner,byte selector,GenericPetInitializationServices services)
    {
        var defaults=GenericPetParameters.Decode(null) with{LeashSquared=784,WarpSquared=2500,CloseRaw=60,WanderRaw=0};
        var frame=new GenericPetDecisionFrame(defaults,new(){Target=0,Value0=-1,Value1=0,Value2=0});
        var survey=new GenericPetOwnerSurveyState(actor,defaults.SwarmRaw);
        if(owner!=0&&services.Kind(owner)==0)
        {GenericPetOwnerSurvey.Enumerate(selector,survey,visit=>services.OwnerUnits(owner,visit),services.Survey);}
        return new(frame,survey);
    }
    // Complete current782/933-byte override orchestration. Normal managed
    // container cleanup is represented by logical release callbacks. Native
    // raw allocator ABI,exceptions and unwritten frame tails are not inferred.
    internal static void Run(int index,int actor,GenericPetAiTick tick,int fallback,GenericPetAiOverrideServices services)
    {
        if((uint)index>1){throw new NotSupportedException("Unknown current AI override");}
        var header=services.Header(tick.Control)??throw new NotSupportedException("Override dereferences AI control");
        if(header.State!=0||header.Mode10!=1||index==1&&actor==0){return;}
        var unit=actor==0?null:services.Unit(actor);int owner=0;
        if(unit is{Position.Kind:1,HasData:true,HasAi:true,OwnerGame:not 0})
        {owner=services.Resolve(unit.OwnerGame,unit.OwnerKind,unit.OwnerId);}
        if(index==1&&(owner==0||!services.Unit(owner).Active))
        {if(services.Unit(actor).Active){services.MissingOwner(actor,0,1);}return;}
        var frame=services.Initialize(actor,owner);bool done=false;
        if(owner!=0)
        {
            var(x,y)=services.Unit(owner).Position.Coordinates();
            if(GenericPetSearch.Squared(services.Unit(actor).Position,x,y)>frame.Decision.Parameters.LeashSquared)
            {done=services.Follow(actor,owner,frame);}
        }
        if(!done)
        {
            if(fallback!=0){services.Default(fallback,actor,tick);}
            if(owner!=0&&(actor!=0&&services.Unit(actor).Mode==1||services.Moving(actor)))
            {
                var selection=services.Search(actor,owner,frame,0,0);
                if(selection.High48==0)
                {
                    // Source caches each next link before removal. Supported
                    // store removals preserve the other nodes' relative order.
                    if(actor!=0){foreach(int e in services.Events(actor)){if(services.EventType(e)==2){services.RemoveEvent(e);}}}
                    if(!services.Follow(actor,owner,frame)){services.Idle(actor,15);}
                }
                if(selection.ExternalStorage){services.ReleaseSearch(selection);}
            }
        }
        services.ReleaseFrame(frame);
    }
}
public static class GenericPetAiOverrideTables
{
    private static GenericPetAiOverride[] _rows;
    public static void Configure()
    {
        var rows=BalanceCatalog.Read<GenericPetAiOverride>("generic-pet-ai-overrides");
        if(rows.Length!=2||rows[0]!=new GenericPetAiOverride(0,0x4b63d0)||rows[1]!=new GenericPetAiOverride(0,0x4b66e0))
        {throw new InvalidDataException("Invalid pinned AI override table");}
        _rows=rows;
    }
    internal static GenericPetAiOverride At(int index)=>(uint)index<2?_rows[index]:throw new NotSupportedException("AI override index outside current table");
}
