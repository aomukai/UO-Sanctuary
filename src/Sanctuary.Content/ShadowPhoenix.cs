// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

public sealed record PhoenixNpcSource(uint Actor,string Binding,ulong UnitSeed,int Pierce,bool Dormant=false,string Fault=null);

// Own NPC context in the existing Phoenix pools. The shared path,curve,
// collision,lease and expiry engine is unchanged; no player cast is proxied.
public static partial class MartialPhoenix
{
    private static PlayerMobile SourceOwner(uint owner,int map,PhoenixNpcSource npc)
        =>npc==null?Owner(owner,map):World.FindMobile((Serial)owner)as PlayerMobile;
    internal static AssassinShadowBody NpcActor(PlayerMobile owner,PhoenixNpcSource npc,int map,bool? actions)
    {
        if(npc==null||npc.Dormant||npc.Fault!=null||owner is not{Alive:true,Deleted:false}||!(actions??owner.NetState is{Running:true})){return null;}
        return World.FindMobile((Serial)npc.Actor)is AssassinShadowBody body&&NpcItemSpellFlights.Attached(body,owner,map,npc.Binding)?body:null;
    }
    internal static void ValidateNpc(PhoenixNpcSource npc,uint owner)
    {
        if(npc==null){return;}
        if(!((Serial)npc.Actor).IsMobile||npc.Actor==owner||npc.Binding is not{Length:64}
            ||System.Linq.Enumerable.Any(npc.Binding,c=>c is not(>='0'and<='9'or>='a'and<='f'))
            ||npc.UnitSeed!=SourceRandom.InitLow(unchecked((int)(uint)npc.UnitSeed))||npc.Fault is{Length:>512})
        {throw new InvalidDataException("Invalid original NPC Phoenix source");}
    }
    private static PhoenixNpcSource Source(AssassinShadowBody body,int pierce)
        =>new(body.Serial.Value,OriginalNpcEquipment.Binding(body),NpcWorldAllocation.UnitSeed(),pierce);
    internal static long AfterShadow(AssassinShadowBody body,BaseCreature target,Point3D fallback,MartialCharge charge,DateTime at)
    {
        if(charge is not{Ability:CombatAbility.PhoenixStrike,Count:>=1 and <=3}||charge.Owner!=body.Serial.Value
            ||!ShadowSkillCasting.Attached(body)||at.Kind!=DateTimeKind.Utc){return 0;}
        Tables();if(NecromancerSummons.Source(body.SourceMonsterId,"Crit")!=0){throw new NotSupportedException("Shadow Phoenix intrinsic critical is not qualified");}
        var owner=(PlayerMobile)body.ControlMaster;var center=target is{Deleted:false}&&target.Map==body.Map?target.Location:fallback;
        if(center.X<=0||center.Y<=0||center.X>=body.Map.Width||center.Y>=body.Map.Height||center.Z is<-128 or>127||SpellHelper.IsTown(center,owner)){return 0;}
        int rank=Math.Max(charge.InitialRank,body.OwnedSkillRank(280));var graph=OriginalNpcEquipment.Graph(body);long cast=checked(++_nextCast);
        if(charge.Count==1)
        {
            long id=checked(++_nextId);Drops.Add(id,new(id,cast,owner.Serial.Value,body.Map.MapID,rank,center.X,center.Y,center.Z,0,at.AddMilliseconds(40)){Npc=Source(body,graph.Read(333))});
            Effects.SendLocationEffect(center,body.Map,0x3709,60,10,0x489,0);return cast; // Identified warning placeholder.
        }
        // Complete do143/do41 each draw one caster seed for their local
        // spread stream. Child allocation has the separate world unit seed.
        ulong stream=SourceRandom.InitLow(unchecked((int)NpcEquipmentRandom.Raw(body)));bool cold=charge.Count==3;
        for(int i=0;i<(cold?16:7);i++)
        {
            Point2D aim;uint targetSeed=0,childSeed=0;int packed=0;Point2D[] path=[];
            if(cold)
            {int x=(int)(SourceRandom.Roll(ref stream)%40)-20,y=(int)(SourceRandom.Roll(ref stream)%40)-20;if(x==0&&y==0){x=20;}aim=new(x,y);targetSeed=(uint)stream;packed=unchecked((ushort)x+((int)(ushort)y<<16));}
            else{aim=ChainDirections[i];childSeed=SourceRandom.Roll(ref stream);}
            var values=ShadowSkillCasting.OwnValues(body,rank,280);var damage=MartialPhoenixPackets.Capture(cold?"royalstrikechaosice":"royalstrikechainlightning",values,graph.Read(cold?331:330));
            var slow=AmazonSlowMissiles.Capture(body,true);int speed=SlowMissilePayload.VelocityFp8(cold?16:30,0,rank,3,0,true,slow.VelocityPercent);
            ulong seed=SourceRandom.InitLow(unchecked((int)targetSeed));
            if(!cold){var chain=MartialPhoenixPaths.Chain(unchecked((int)childSeed),MartialPhoenixPaths.Direction(aim.X,aim.Y),60,center.X*2,center.Y*2);path=chain.Points;seed=chain.Seed;}
            int pierce=graph.Read(cold?335:334);long id=checked(++_nextId);
            var flight=new PhoenixFlight(id,cast,owner.Serial.Value,body.Map.MapID,rank,cold,center.X,center.Y,center.Z,0,cold?40:60,speed,
                slow.Damage256(damage.Minimum256),slow.Damage256(damage.Maximum256),damage.ColdFrames,pierce,slow,seed,targetSeed,packed,
                MissileVectors.Direction(aim.X,aim.Y),path,0,new([new(center.X*2,center.Y*2)]),at.AddMilliseconds(40)){Npc=Source(body,pierce)};
            Validate(flight);Flights.Add(id,flight);
        }
        return cast;
    }
    private static PhoenixFlight NpcImpact(PlayerMobile owner,BaseCreature target,PhoenixFlight flight)
    {
        var body=NpcActor(owner,flight.Npc,flight.Map,true);if(body==null){return flight;}
        _=NpcElementSpellHit.Require(body,owner,target,flight.NextUtc,"Shadow Phoenix");
        if(flight.Cold){NpcFreezeStatus.Require(body,target);}
        if(ClassAvoidance.Missile(target)){return flight;}
        ulong seed=flight.Seed;int raw=WeaponMissileDamage.RollValue(flight.Minimum256,flight.Maximum256,0,n=>(int)SourceRandom.Limited(ref seed,n));
        if(NecromancerSummons.Source(body.SourceMonsterId,"Crit")!=0){throw new NotSupportedException("Shadow Phoenix intrinsic skill critical needs its own roll context");}
        _=NpcElementSpellHit.Deliver(body,owner,target,flight.Cold?"cold":"ltng",raw,0,flight.NextUtc,"Shadow Phoenix",
            pierce:e=>e==(flight.Cold?"cold":"ltng")?flight.Pierce:0,freezeFrames:flight.Cold?flight.ColdFrames:0);
        return flight with{Seed=seed};
    }
    private static void NpcMeteorImpact(AssassinShadowBody body,PhoenixDrop drop,DateTime at)
    {
        var owner=(PlayerMobile)body.ControlMaster;var values=ShadowSkillCasting.OwnValues(body,drop.Rank,280);var graph=OriginalNpcEquipment.Graph(body);
        var packet=MartialPhoenixPackets.Capture("royalstrikemeteor",values,graph.Read(329));ulong seed=NpcWorldAllocation.UnitSeed();
        int raw=WeaponMissileDamage.RollValue(packet.Minimum256,packet.Maximum256,0,n=>(int)SourceRandom.Limited(ref seed,n));var center=new Point3D(drop.X,drop.Y,drop.Z);
        foreach(var target in SkillCombat.NearbySnapshot<BaseCreature>(body.Map,center,4))
        {
            if(!FistsFireRelease.InRadius(center,target,6)||!ClassCombat.IsHostile(owner,target)||ClassCombat.Friendly(body,target)||SpellHelper.IsTown(target.Location,target)){continue;}
            _=NpcElementSpellHit.Deliver(body,owner,target,"fire",raw,0,at,"Shadow Phoenix meteor",pierce:e=>e=="fire"?drop.Npc.Pierce:0);
        }
        if(!ShadowSkillCasting.Attached(body)){return;}
        foreach(var offset in MartialPhoenixPackets.MeteorOffsets())
        {
            double x=drop.X+offset.X/2.0,y=drop.Y+offset.Y/2.0;var point=new Point3D((int)Math.Round(x),(int)Math.Round(y),drop.Z);
            if(x<0||y<0||x>=body.Map.Width||y>=body.Map.Height||SpellHelper.IsTown(point,owner)){continue;}
            values=ShadowSkillCasting.OwnValues(body,drop.Rank,280);var fire=MartialPhoenixPackets.Capture("royalstrikemeteorfire",values,graph.Read(329));
            FireGround.AddShadowPhoenix(body,drop.Rank,x,y,drop.Z,fire.Minimum256,fire.Maximum256,Source(body,drop.Npc.Pierce),at);
        }
        Effects.SendLocationEffect(center,body.Map,0x36BD,10,10,0x489,0); // Identified impact placeholder.
    }
}
