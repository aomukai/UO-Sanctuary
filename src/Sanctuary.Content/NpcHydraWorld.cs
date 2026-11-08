// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public sealed record HydraNpcOwner(uint Caster,string Binding);

internal static class NpcHydraWorld
{
    internal static void Validate(HydraHead head)
    {
        if(head.Source?.NpcOwner is not{} owner){return;}
        if(!((Serial)owner.Caster).IsMobile||owner.Caster==head.Owner||owner.Caster==head.Source.Actor
            ||owner.Binding is not{Length:64}||owner.Binding.Any(c=>c is not(>='0'and<='9'or>='a'and<='f'))
            ||head.Rank is<1 or>63)
        {throw new InvalidDataException("Invalid original NPC Hydra owner provenance");}
    }
}

public static partial class Hydras
{
    private static PlayerMobile ResponsibleOwner(HydraHead head)
        =>head.Source?.NpcOwner==null?PlayerBlizzards.Owner(head.Owner)
            :World.FindMobile((Serial)head.Owner) is PlayerMobile{Alive:true,Deleted:false} p&&ClassCreation.Profiles.TryGet(p.Serial,out _)?p:null;
    private static bool OwnerGraph(HydraHead head,HydraBody body,PlayerMobile responsible)
    {
        if(head.Source?.NpcOwner is not{} npc){return body.SummonMaster==responsible&&responsible!=null;}
        var original=World.FindMobile((Serial)npc.Caster);
        // Missing original after removal remains an explicit saved owner ID,
        // not a player proxy or a fabricated replacement mobile.
        return original==null?body.SummonMaster==null||body.SummonMaster is{Deleted:true} old&&old.Serial.Value==npc.Caster
            :original is SkeletonGuardian caster&&caster.ControlMaster==responsible&&body.SummonMaster==caster;
    }
    internal static Func<NpcHydraRequest,bool> PreparedNpcFactory(SkeletonGuardian caster,DateTime at,
        Func<NpcHydraRequest,NpcItemSpellAllocation> allocate=null)
    {
        long group=0;NpcHydraBirth original=null;
        return request=>
        {
            var birth=request?.Capture;var owner=caster?.ControlMaster as PlayerMobile;
            if(birth==null||caster is not{Alive:true,Deleted:false}||owner is not{Alive:true,Deleted:false}
                ||birth.Caster!=caster.Serial.Value||birth.ResponsibleOwner!=owner.Serial.Value||caster.Map==null||caster.Map==Map.Internal||owner.Map!=caster.Map
                ||birth.Binding!=OriginalNpcEquipment.Binding(caster)||request.Index is<0 or>2||request.Mode!=9
                ||at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddSeconds(-10)||request.ExpiryUtc!=at.AddSeconds(10)
                ||original!=null&&birth!=original)
            {throw new NotSupportedException("NPC Hydra factory lost its original caster,release or birth context");}
            HydraCreation.Validate(birth.Birth);Route();
            if(birth.Birth.Passive("item_pierce_fire_immunity")!=0)
            {throw new NotSupportedException("NPC Hydra inherited fire sunder needs its explicit immunity adapter");}
            double x=request.X/2.0,y=request.Y/2.0;var tile=new Point3D((int)Math.Round(x),(int)Math.Round(y),request.Z);var map=caster.Map;
            if(!Position(owner.Serial.Value,map.MapID,x,y,request.Z)||!map.CanFit(tile,1,false,false,true)){return false;}
            var allocation=allocate?.Invoke(request)??new NpcItemSpellAllocation(true,NpcWorldAllocation.UnitSeed());
            if(!allocation.Allocated){return false;}if(allocation.Seed==0){throw new NotSupportedException("NPC Hydra needs its explicit nonzero private head seed");}
            original??=birth;if(group==0){group=checked(++_nextId);}long id=checked(++_nextId);var body=new HydraBody();body.Attach(id,caster);
            try{body.MoveToWorld(tile,map);}catch{body.Delete();throw;}if(body.Deleted){return false;}
            var ends=HydraCreation.Fire(birth.Birth,SlowMissilePayload.Normal);var source=new HydraHeadSource(body.Serial.Value,birth.Birth,HydraControl.Start(250,allocation.Seed),at.AddMilliseconds(40),new(0,0))
                {NpcOwner=new(birth.Caster,birth.Binding)};
            var head=new HydraHead(id,group,request.Index,owner.Serial.Value,birth.Birth.Rank,map.MapID,x,y,request.Z,ends.Min,ends.Max,
                birth.Birth.Passive("passive_fire_pierce"),allocation.Seed,request.ExpiryUtc,at.AddMilliseconds(1000),default,0){Source=source};
            ValidateSource(head);_heads.Add(id,head);return true;
        };
    }
}
