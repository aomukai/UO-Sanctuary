// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

public static partial class MartialCold
{
    internal static IcePacket CaptureShadow(AssassinShadowBody body,MartialCharge charge)
    {
        if(charge==null){return null;}
        if(charge.Owner!=body.Serial.Value||charge.Ability!=CombatAbility.BladesOfIce||!ShadowSkillCasting.Attached(body)
            ||NecromancerSummons.Source(body.SourceMonsterId,"Crit")!=0)
        {throw new NotSupportedException("Unqualified own Shadow Ice source");}
        int rank=Math.Max(charge.InitialRank,body.OwnedSkillRank(274));
        return Capture(ShadowSkillCasting.OwnValues(body,rank,274),charge.Count,OriginalNpcEquipment.Graph(body).Read(331),
            n=>NpcEquipmentRandom.Limited(body,n),charge.InitialRank);
    }
    internal static NpcFillPacket InlineShadow(NpcFillPacket fill,IcePacket packet)
    {
        if(packet==null){return fill;}
        int divisor=SkillMechanics.For(CombatAbility.BladesOfIce).Parameter(5);
        if(divisor!=1){throw new NotSupportedException("Unmapped current Ice freeze divisor");}
        // Complete elemental writer replaces weapon cold length; it does not
        // add it. prgdam4 then adds freeze from that final length at count3.
        return fill with{Cold=checked(fill.Cold+packet.Inline256),ColdFrames=packet.Frames,
            FreezeFrames=checked(fill.FreezeFrames+(packet.Count==3?packet.Frames/divisor:0)),ResultFlags=fill.ResultFlags|16384};
    }
    internal static void AfterShadow(AssassinShadowBody body,BaseCreature target,Point3D fallback,Map map,IcePacket packet,DateTime at)
    {
        if(packet is not{Count:>=2}||!ShadowSkillCasting.Attached(body)||body.Map!=map||body.ControlMaster is not PlayerMobile owner){return;}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Shadow Ice release needs UTC");}
        int rank=Math.Max(packet.InitialRank,body.OwnedSkillRank(274));var graph=OriginalNpcEquipment.Graph(body);
        var values=ShadowSkillCasting.OwnValues(body,rank,274);var area=Capture(values,packet.Count,graph.Read(331),n=>NpcEquipmentRandom.Limited(body,n),packet.InitialRank);
        var center=Center(target,fallback,map);int pierce=graph.Read(335);
        Effects.SendLocationEffect(center,map,0x374A,3,10,0x480,0); // Identified cold-area placeholder.
        foreach(var recipient in SkillCombat.NearbySnapshot<BaseCreature>(map,center,(area.Radius+1)/2))
        {
            if(recipient is not{Alive:true,Deleted:false}||!AreaAllowed(body,owner,recipient,center,map,area.Radius)||ClassCombat.Friendly(body,recipient)){continue;}
            // Callback38's copied packet chills but contains no freeze lane.
            if(ClassAvoidance.Missile(recipient)){continue;}
            _=NpcElementSpellHit.Deliver(body,owner,recipient,"cold",area.Inline256,area.Frames,at,"Shadow Ice area",hitFlags:1,
                pierce:e=>e=="cold"?pierce:0,periodic:true);
        }
        if(packet.Count<3||!ShadowSkillCasting.Attached(body)){return;}
        Tables();int low=unchecked((int)NpcEquipmentRandom.Raw(body));rank=Math.Max(packet.InitialRank,body.OwnedSkillRank(274));
        values=ShadowSkillCasting.OwnValues(body,rank,274);graph=OriginalNpcEquipment.Graph(body);center=Center(target,fallback,map);
        int Enhance(int v)=>checked((int)((long)v*Math.Max(0,100+graph.Read(331))/100));
        int minimum=Enhance(FireGround.MissileDamage256("bladesoficecubes",rank,0,false));
        int maximum=Enhance(FireGround.MissileDamage256("bladesoficecubes",rank,0,true));
        foreach(var offset in FistsFireRelease.Scatter((int)values.Eval("prgcalc3"),low))
        {
            double x=center.X+offset.X/2.0,y=center.Y+offset.Y/2.0;
            if(x<0||y<0||x>=map.Width||y>=map.Height||SpellHelper.IsTown(new Point3D((int)Math.Round(x),(int)Math.Round(y),center.Z),owner)){continue;}
            var source=new PhoenixNpcSource(body.Serial.Value,OriginalNpcEquipment.Binding(body),NpcWorldAllocation.UnitSeed(),graph.Read(335));
            AddShadowCube(body,rank,x,y,center.Z,minimum,maximum,source,at);
        }
    }
    internal static IceCube AddShadowCube(AssassinShadowBody body,int rank,double x,double y,int z,int minimum,int maximum,PhoenixNpcSource source,DateTime at)
    {Tables();return AddCube((PlayerMobile)body.ControlMaster,body,body.Map,rank,x,y,z,minimum,maximum,source.Pierce,source,at);}
    private static ulong NpcImpact(PlayerMobile owner,BaseCreature target,IceCube cube)
    {
        var body=MartialPhoenix.NpcActor(owner,cube.Npc,cube.Map,true);if(body==null){return cube.Seed;}
        _=NpcElementSpellHit.Require(body,owner,target,cube.NextUtc,"Shadow Ice cube");NpcFreezeStatus.Require(body,target);
        if(ClassAvoidance.Missile(target)){return cube.Seed;}
        ulong seed=cube.Seed;int raw=WeaponMissileDamage.RollValue(cube.Minimum256,cube.Maximum256,0,n=>(int)SourceRandom.Limited(ref seed,n));
        _=NpcElementSpellHit.Deliver(body,owner,target,"cold",raw,cube.ColdFrames,cube.NextUtc,"Shadow Ice cube",
            pierce:e=>e=="cold"?cube.Pierce:0,freezeFrames:cube.ColdFrames,periodic:true);
        return seed;
    }
}
