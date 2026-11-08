// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public sealed record ShockShardNpc(uint Actor,string Binding,ulong Seed,int LightningPierce=0,bool Dormant=false,string Fault=null);

// Own NPC lob/field producer. Intrinsic action admission remains separate
// uses the approved intrinsic no-extra NPC cooldown policy;no player cast is proxied.
internal static class ShadowShockWebs
{
    internal static ItemExecutionMetadata Row()
    {
        var r=ItemSkillExecution.Skill(256);
        if(r is not{Start:0,Do:43,InTown:false,UseManaOnDo:false,Lob:false,DecrementQuantity:false,Missile:-1}
            ||r.Text("monanim")!="S2"||r.Number("UseAttackRate")!=1||ShadowSkillBook.Metadata(256).Mode!=9||r.Text("EType")!="ltng"
            ||r.Number("localdelay")!=15||r.HasDelay("delay")||r.HasDelay("globaldelay")||r.HasDelay("perdelay")
            ||r.Number("TargetableOnly")!=0||r.Number("LineOfSight")!=0||r.Text("srvmissilea")!="shock field in air"
            ||NecromancerSummons.Source("shock field on ground","NextHit")!=1||NecromancerSummons.Source("shock field on ground","NextDelay")!=25
            ||NecromancerSummons.Source("shock field on ground","Range")!=90||NecromancerSummons.Source("shock field on ground","SoftHit")!=1
            ||NecromancerSummons.Source("shock field on ground","GetHit")!=0||NecromancerSummons.Source("shock field on ground","SrcDamage")!=0)
        {throw new NotSupportedException("Unqualified current Shadow Shock Web metadata");}
        return r;
    }
    internal static int Release(AssassinShadowBody body,BaseCreature target,Point3D cached,int rank,DateTime at)
    {
        _=Row();
        if(!ShadowNpcCooldowns.Approved(256)||!ShadowSkillCasting.Attached(body)||body.OwnedSkills.Used!=256||rank!=body.OwnedSkillRank(256)
            ||!ShadowBladeSentinels.Destination(body,target,cached,out var point)){return 0;}
        // Full do43 reports success even when close-point exclusions yield0.
        return ShockWebs.EmitShadow(body,point,rank,at)>=0?1:0;
    }
    internal static void Validate(ShockShard s)
    {
        if(s.Npc is not{} n||!((Serial)n.Actor).IsMobile||n.Actor==s.Owner||n.Binding is not{Length:64}||n.Binding.Any(c=>!Uri.IsHexDigit(c))
            ||n.Seed==0||n.Fault?.Length>512||s.Potion!=0||s.FireMastery!=0||s.ColdMastery!=0||s.PoisonMastery!=0)
        {throw new InvalidDataException("Invalid original NPC Shock Web source");}
    }
    internal static ShockShard[] Prepare(long first,long cast,AssassinShadowBody body,Point3D aim,int rank,DateTime at)
    {
        _=Row();if(!ShadowSkillCasting.Attached(body)||body.OwnedSkills.Used!=256||rank!=body.OwnedSkillRank(256)||at.Kind!=DateTimeKind.Utc)
        {throw new NotSupportedException("Shock Web needs its actual original NPC emission context");}
        if(NecromancerSummons.Source(body.SourceMonsterId,"Crit")!=0){throw new NotSupportedException("Shock Web NPC intrinsic critical needs its source roll adapter");}
        if(!LobProjectiles.Valid(body,aim)){return null;}
        var values=ShadowSkillCasting.OwnValues(body,rank,256);var result=new List<ShockShard>();string binding=OriginalNpcEquipment.Binding(body);
        foreach(var point in ShockWebs.Scatter(body.X*2,body.Y*2,aim.X*2,aim.Y*2,ShockWebs.Count(values),(int)values.Eval("aurarangecalc")))
        {
            int dx=point.X-body.X*2,dy=point.Y-body.Y*2;
            if(Math.Abs(dx)>=100||Math.Abs(dy)>=100||point.X<=0||point.Y<=0||point.X>=body.Map.Width*2||point.Y>=body.Map.Height*2){continue;}
            var v=MissileVectors.VelocityFp8(dx,dy,LobProjectiles.Velocity);long id=checked(first+result.Count);
            var shard=new ShockShard(id,cast,body.ControlMaster.Serial.Value,body.Map.MapID,rank,body.X,body.Y,body.Z,v.X,v.Y,LobProjectiles.Lifetime(dx,dy),false,at.AddMilliseconds(40))
                {Npc=new(body.Serial.Value,binding,NpcWorldAllocation.UnitSeed())};Validate(shard);result.Add(shard);
        }
        return result.ToArray();
    }
    private static ShockShard Land(AssassinShadowBody body,ShockShard s)
    {
        if(!LobProjectiles.CanLand(body,s.X,s.Y)){return null;}
        var values=ShadowSkillCasting.OwnValues(body,s.Rank,256);var graph=OriginalNpcEquipment.Graph(body);int mastery=graph.Read(330);
        int Enhance(int value)=>(int)Math.Clamp((long)value+(long)value*mastery/100,0,int.MaxValue);
        return s with{X=Math.Floor(s.X*2+.5)/2,Y=Math.Floor(s.Y*2+.5)/2,Vx=0,Vy=0,Ground=true,Remaining=90,NextUtc=s.NextUtc.AddMilliseconds(40),
            Min256=Enhance(values.Damage256(false)),Max256=Enhance(values.Damage256(true)),LightningMastery=mastery,Npc=s.Npc with{LightningPierce=graph.Read(335)}};
    }
    private static ShockShard Hit(AssassinShadowBody body,PlayerMobile owner,BaseCreature target,ShockShard s)
    {
        ShockWebs.Lease(s,target);_=NpcElementSpellHit.Require(body,owner,target,s.NextUtc,"Shadow Shock Web");
        if(!SourceBlocking.TryRate(target,out int shield,out _)||shield!=0){throw new NotSupportedException("Shadow Shock Web shield recipient needs its source defense adapter");}
        int raw=SorceressSpellMissiles.Roll(s.Min256,s.Max256,s.Npc.Seed,out ulong seed);s=s with{Npc=s.Npc with{Seed=seed}};
        if(!ClassAvoidance.Missile(target)){NpcElementSpellHit.Deliver(body,owner,target,"ltng",raw,0,s.NextUtc,"Shadow Shock Web",pierce:e=>e=="ltng"?s.Npc.LightningPierce:0,softHit:true);}
        return s;
    }
    private static ShockShard Age(ShockShard s)
    {
        if(s.Ground&&s.Remaining<=1){return null;}
        bool land=s.Remaining<=1;
        return s with{Remaining=land?90:s.Remaining-1,Ground=s.Ground||land,Vx=land?0:s.Vx,Vy=land?0:s.Vy,NextUtc=s.NextUtc.AddMilliseconds(40),Npc=s.Npc with{Dormant=true}};
    }
    internal static ShockShard Advance(ShockShard s,bool? actions,bool loaded)
    {
        Validate(s);var body=World.FindMobile((Serial)s.Npc.Actor)as AssassinShadowBody;var owner=World.FindMobile((Serial)s.Owner)as PlayerMobile;
        if(loaded||s.Npc.Dormant||!NpcItemSpellFlights.Attached(body,owner,s.Map,s.Npc.Binding)||!(actions??owner?.NetState is{Running:true})){return Age(s);}
        ShockShard next;
        if(s.Ground)
        {
            if(s.Remaining<=1){return null;}var target=ShockWebs.Recipient(owner,s);
            if(target!=null&&!ClassCombat.Friendly(body,target)&&body.CanBeHarmful(target,false)){s=Hit(body,owner,target,s);}
            else if(LobProjectiles.Barrier(body.Map,new((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z))){return null;}
            next=s with{Remaining=s.Remaining-1,NextUtc=s.NextUtc.AddMilliseconds(40)};
            if(s.Remaining%5==0){Effects.SendLocationEffect(new((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z),body.Map,0x374A,2,10,0x481,0);} // Existing field placeholder.
        }
        else
        {
            var move=LobProjectiles.Advance(body.Map,s.X,s.Y,s.Z,s.Vx,s.Vy,s.Remaining);var moved=s with{X=move.X,Y=move.Y};
            next=move.Landed?Land(body,moved):moved with{Remaining=s.Remaining-1,NextUtc=s.NextUtc.AddMilliseconds(40)};
            if(next!=null){Effects.SendLocationEffect(new((int)Math.Round(next.X),(int)Math.Round(next.Y),next.Z),body.Map,0x36E4,2,10,0x481,0);} // Existing lob placeholder.
        }
        return next;
    }
}
