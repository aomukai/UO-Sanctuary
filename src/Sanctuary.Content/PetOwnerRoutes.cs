// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record PetOwnerRoute(uint Owner,int Map,Point2D[] Trail,int Cursor,DateTime SampleUtc,
    Point2D LastTeleport,DateTime TeleportUntilUtc);
public sealed class PetOwnerRouteProfiles:GenericPersistence
{
    private string _snapshot="[]";
    public PetOwnerRouteProfiles():base("SanctuaryPetOwnerRoutes",221){}
    public override void Serialize(){PetOwnerRoutes.Prune();_snapshot=JsonSerializer.Serialize(PetOwnerRoutes.Snapshot());base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(_snapshot);}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown pet owner route version");}PetOwnerRoutes.Restore(JsonSerializer.Deserialize<PetOwnerRoute[]>(reader.ReadString()));}
    public override void PostDeserialize()=>PetOwnerRoutes.Prune();
}

// Source player history uses20points, >25ms and squared displacement>45.
// Successful teleport appends unconditionally and marks its point for50frames.
// Native accepted tile steps are the terrain/input projection of player motion.
internal static class PetOwnerRoutes
{
    private static Dictionary<uint,PetOwnerRoute> States=new();
    public static PetOwnerRouteProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>ClassMapHooks.LocationChanged+=LocationChanged;
    private static PetOwnerRoute Copy(PetOwnerRoute s)=>s with{Trail=[..s.Trail]};
    internal static Point2D Cell(Mobile p)=>new(p.X*2,p.Y*2);
    internal static PetOwnerRoute For(PlayerMobile owner,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc || owner is not{Deleted:false} || owner.Map==null || owner.Map==Map.Internal)
        {throw new ArgumentException("Pet route needs a world owner and UTC clock");}
        if(!States.TryGetValue(owner.Serial.Value,out var state))
        {state=new(owner.Serial.Value,owner.Map.MapID,new Point2D[20],0,at,new(0,0),DateTime.SpecifyKind(DateTime.MinValue,DateTimeKind.Utc));States.Add(state.Owner,state);}
        if(state.TeleportUntilUtc!=DateTime.MinValue && at>=state.TeleportUntilUtc)
        {state=state with{LastTeleport=new(0,0),TeleportUntilUtc=DateTime.SpecifyKind(DateTime.MinValue,DateTimeKind.Utc)};States[state.Owner]=state;}
        return Copy(state);
    }
    private static PetOwnerRoute Append(PetOwnerRoute state,Point2D point,DateTime at,int map)
    {
        var trail=(Point2D[])state.Trail.Clone();trail[state.Cursor]=point;
        return state with{Trail=trail,Cursor=(state.Cursor+1)%20,SampleUtc=at,Map=map};
    }
    internal static void Moved(PlayerMobile owner,DateTime at)
    {
        var state=For(owner,at);var point=Cell(owner);var previous=state.Trail[(state.Cursor+19)%20];
        long x=point.X-previous.X,y=point.Y-previous.Y;
        if(at>state.SampleUtc.AddMilliseconds(25) && x*x+y*y>45){States[state.Owner]=Append(state,point,at,owner.Map.MapID);}
    }
    internal static void Teleported(PlayerMobile owner,DateTime at)
    {
        var state=Append(For(owner,at),Cell(owner),at,owner.Map.MapID);
        States[state.Owner]=state with{LastTeleport=Cell(owner),TeleportUntilUtc=at.AddMilliseconds(50*40)};
    }
    private static void LocationChanged(Mobile mobile)
    {
        if(!World.Loading && mobile is PlayerMobile{Alive:true,Deleted:false} owner && owner.Map!=null && owner.Map!=Map.Internal
            && States.TryGetValue(owner.Serial.Value,out var state) && state.Map!=owner.Map.MapID)
        {Teleported(owner,TimedEffects.Now);}
    }
    internal static void Prune()
    {foreach(uint id in States.Keys.ToArray()){if(World.FindMobile((Serial)id) is not PlayerMobile{Deleted:false}){States.Remove(id);}}}
    internal static PetOwnerRoute[] Snapshot()=>States.Values.OrderBy(x=>x.Owner).Select(Copy).ToArray();
    internal static void Restore(PetOwnerRoute[] rows)
    {
        if(rows==null){throw new InvalidDataException("Missing pet owner routes");}var states=new Dictionary<uint,PetOwnerRoute>();
        foreach(var s in rows)
        {
            if(s==null || !((Serial)s.Owner).IsMobile || s.Map<0 || s.Map>=Map.Maps.Length || Map.Maps[s.Map]==null || Map.Maps[s.Map]==Map.Internal
                || s.Trail?.Length!=20 || s.Cursor is <0 or >19 || s.SampleUtc.Kind!=DateTimeKind.Utc || s.TeleportUntilUtc.Kind!=DateTimeKind.Utc
                || s.LastTeleport.X is <0 or >65535 || s.LastTeleport.Y is <0 or >65535
                || s.Trail.Any(p=>p.X is <0 or >65535 || p.Y is <0 or >65535) || !states.TryAdd(s.Owner,Copy(s)))
            {throw new InvalidDataException("Invalid pet owner route");}
        }
        States=states;
    }
}
