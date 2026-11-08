// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public static partial class FireGround
{
    internal static void AddShadowPhoenix(AssassinShadowBody body,int rank,double x,double y,int z,int minimum,int maximum,PhoenixNpcSource source,DateTime at)
    {
        long id=checked(++_nextId);var patch=new FirePatch(id,body.ControlMaster.Serial.Value,CombatAbility.PhoenixStrike,body.Map.MapID,x,y,z,minimum,maximum,0,
            at.AddMilliseconds(MartialPhoenixPackets.MeteorFieldFrames(rank)*40L),at.AddMilliseconds(40),2){SkillRank=rank,PhoenixNpc=source,Seed=source.UnitSeed};
        if(!ValidPhoenixNpc(patch)){throw new InvalidDataException("Invalid Shadow Phoenix field");}_patches.Add(id,patch);
    }
    private static bool ValidPhoenixNpc(FirePatch p)
    {
        if(p.PhoenixNpc==null){return true;}MartialPhoenix.ValidateNpc(p.PhoenixNpc,p.Owner);
        return p.NpcCaster==0&&p.Ability==CombatAbility.PhoenixStrike&&p.BlazeModifiers==null&&p.FireWallModifiers==null&&p.MeteorModifiers==null
            &&p.Map>=0&&p.Map<Map.Maps.Length&&Map.Maps[p.Map]!=null&&Map.Maps[p.Map]!=Map.Internal&&p.X>=0&&p.Y>=0&&p.X<Map.Maps[p.Map].Width&&p.Y<Map.Maps[p.Map].Height
            &&p.Z is>=-128 and<=127&&p.NextUtc.Kind==DateTimeKind.Utc&&p.UntilUtc.Kind==DateTimeKind.Utc&&p.NextUtc<=p.UntilUtc;
    }
    private static void TickPhoenixNpc(FirePatch patch,bool? actions)
    {
        if(patch.NextUtc>=patch.UntilUtc){_patches.Remove(patch.Id);return;}
        var next=patch with{NextUtc=patch.NextUtc.AddMilliseconds(40)};var owner=World.FindMobile((Serial)patch.Owner)as PlayerMobile;
        var body=MartialPhoenix.NpcActor(owner,patch.PhoenixNpc,patch.Map,actions);
        if(body==null){_patches[patch.Id]=next with{PhoenixNpc=patch.PhoenixNpc with{Dormant=true}};return;}
        var point=new Point3D((int)Math.Round(patch.X),(int)Math.Round(patch.Y),patch.Z);
        if(!body.Map.CanFit(point,1,false,false,false)){_patches.Remove(patch.Id);return;}
        var recipient=ImmolationArrows.Recipient(body,owner,patch);
        if(recipient!=null&&recipient is not BaseCreature){throw new NotSupportedException("Shadow Phoenix field player recipient is unqualified");}
        if(recipient is BaseCreature target)
        {
            ulong seed=patch.Seed;int raw=WeaponMissileDamage.RollValue(patch.Min256,patch.Max256,0,n=>(int)SourceRandom.Limited(ref seed,n));next=next with{Seed=seed};
            // Phoenix's fire has no extra SOFTHIT roll or ordinary GETHIT.
            _=NpcElementSpellHit.Deliver(body,owner,target,"fire",raw,0,patch.NextUtc,"Shadow Phoenix field",pierce:e=>e=="fire"?patch.PhoenixNpc.Pierce:0,periodic:true);
        }
        _patches[patch.Id]=next;
        if((patch.NextUtc.Ticks/TimeSpan.TicksPerMillisecond/40)%5==0){Effects.SendLocationEffect(point,body.Map,0x398C,3,10,0x489,0);} // Identified field placeholder.
    }
}
