// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

public sealed record FireWallMotion(int VelocityFp8,int Frame,int Frames,bool CellChanged,SourceStepTrace Steps,uint LastCollision=0);
internal sealed record PreparedFireWall(Point3D Point,int Rank,int Frames,DruidEmitterRequest[] Requests,DateTime At);
internal static class FireWalls
{
    internal static DruidEmitterRequest[] Requests(int x,int y,int targetX,int targetY,int rank,int center=69)
    {
        int dx=x-targetX,dy=y-targetY;
        var requests=new List<DruidEmitterRequest>{new(33,68,targetX,targetY,targetX-dy,targetY+dx,false,0,0,rank),
            new(33,68,targetX,targetY,targetX+dy,targetY-dx,false,0,0,rank)};
        if(center is >=0 and <1000){requests.Add(new(1,center,targetX,targetY,0,0,false,0,0,rank));}return requests.ToArray();
    }
    internal static bool ValidAim(PlayerMobile owner,Serial selected,Point3D point,out Point3D aim)
    {
        aim=point;
        if(owner is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map==Map.Internal){return false;}
        if(selected!=Serial.Zero)
        {
            if(World.FindMobile(selected) is not{} target||!ClassCombat.IsHostile(owner,target)){return false;}aim=target.Location;
        }
        // Existing world LOS/range/safe-area policies are retained adapters.
        // The older do24's complete target mask5 helper is reference-tested;
        // exact modern grid/outer cast LOS remains explicitly unverified.
        return aim.X>0&&aim.Y>0&&aim.X<owner.Map.Width&&aim.Y<owner.Map.Height&&owner.InRange(aim,CombatProtocol.TeethRange)
            &&owner.InLOS(aim)&&!SpellHelper.IsTown(owner.Location,owner)&&!SpellHelper.IsTown(aim,owner)
            &&owner.Map.CanFit(aim,1,false,false,false);
    }
    internal static void Route()
    {
        var s=SkillMechanics.For(CombatAbility.FireWall);int M(string key,string name)=>NecromancerSummons.Source(key,name);
        if(s.SourceId!=51||s.Number("srvdofunc")!=24||s.Text("srvmissilea")!="firewallmaker"||s.Text("srvmissileb")!="firewall"||s.Left||!s.Right||s.Number("localdelay")!=35
            ||M("firewallmaker","pSrvDoFunc")!=6||M("firewallmaker","Vel")!=12||M("firewallmaker","MaxVel")!=12||M("firewallmaker","VelLev")!=0
            ||M("firewallmaker","CanSlow")!=0||M("firewallmaker","InitSteps")!=0||M("firewallmaker","LastCollide")!=1||M("firewallmaker","CollideType")!=8
            ||M("firewall","pSrvDoFunc")!=5||M("firewall","pSrvHitFunc")!=0||M("firewall","pSrvDmgFunc")!=3||M("firewall","dParam1")!=19
            ||M("firewall","Range")!=90||M("firewall","Size")!=1||M("firewall","DamageRate")!=41||M("firewall","GetHit")!=0||M("firewall","LastCollide")!=0||M("firewall","CollideKill")!=0)
        {throw new NotSupportedException("Unsupported pinned Fire Wall route");}
    }
    internal static PreparedFireWall Prepare(PlayerMobile owner,Point3D point,SkillValues values,DateTime at)
    {
        Route();if(values.Ability!=CombatAbility.FireWall||values.Rank is <1 or >255||at.Kind!=DateTimeKind.Utc||!ValidAim(owner,Serial.Zero,point,out _))
        {throw new NotSupportedException("Unsupported prepared Fire Wall");}
        int frames=NecromancerSummons.Formula("firewallmaker","Range",values);if(frames!=7+2*values.Rank){throw new NotSupportedException("Unsupported maker lifetime");}
        return new(point,values.Rank,frames,Requests(owner.X*2,owner.Y*2,point.X*2,point.Y*2,values.Rank),at);
    }
    internal static bool Release(PlayerMobile owner,Serial selected,Point3D point,SkillValues values)
    {
        if(!ValidAim(owner,selected,point,out var aim)||!FireGround.Ready(owner,CombatAbility.FireWall)){return false;}
        var prepared=Prepare(owner,aim,values,TimedEffects.Now);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        FireGround.CommitWall(owner,prepared);FireGround.Delay(owner,CombatAbility.FireWall,prepared.At);owner.PlaySound(0x1E5);return true; // Cast sound placeholder.
    }
    internal static bool Valid(FirePatch p)=>p.FireWallModifiers==null||p.BlazeModifiers==null&&p.MeteorModifiers==null&&p.Ability==CombatAbility.FireWall&&p.NpcCaster==0
        &&p.SkillRank is >=1 and <=255&&p.Size==1&&p.DamageRate==41&&p.FireWallModifiers.Potion>=0&&p.X*2==Math.Floor(p.X*2)&&p.Y*2==Math.Floor(p.Y*2)
        &&p.NextUtc.Kind==DateTimeKind.Utc&&p.UntilUtc.Kind==DateTimeKind.Utc;
    internal static bool Valid(FireMaker m)=>m.Motion==null||m.Rank is >=1 and <=255&&m.Motion.VelocityFp8==2304&&m.Motion.Frames==7+2*m.Rank
        &&m.Motion.Frame>=0&&m.Motion.Frame<m.Motion.Frames&&m.Motion.Steps?.Cells is{Length:>0 and <=10}&&Math.Abs(m.Dx)<=.28125&&Math.Abs(m.Dy)<=.28125
        &&m.CellX==(int)Math.Floor(m.X*2+.5)&&m.CellY==(int)Math.Floor(m.Y*2+.5)&&m.NextUtc.Kind==DateTimeKind.Utc&&m.UntilUtc.Kind==DateTimeKind.Utc;
}

public static partial class FireGround
{
    private static readonly HashSet<long> _loadedWallMakers=new(),_loadedWallFields=new();
    internal static void CommitWall(PlayerMobile owner,PreparedFireWall prepared)
    {
        foreach(var r in prepared.Requests)
        {
            if(r.Missile==69){AddWallField(owner,r.Rank,r.X/2.0,r.Y/2.0,prepared.Point.Z,prepared.At);continue;}
            int dx=r.AimX-r.X,dy=r.AimY-r.Y;if(dx==0&&dy==0){dx=dy=1;} // Complete factory same-point nudge; modern equivalence remains open.
            var v=MissileVectors.VelocityFp8(dx,dy,2304);long id=checked(++_nextId);
            var motion=new FireWallMotion(2304,0,prepared.Frames,false,new([new(r.X,r.Y)]));
            var maker=new FireMaker(id,owner.Serial.Value,r.Rank,owner.Map.MapID,r.X/2.0,r.Y/2.0,prepared.Point.Z,v.X,v.Y,0,0,r.X,r.Y,
                prepared.At.AddMilliseconds(prepared.Frames*40L),prepared.At.AddMilliseconds(40)){Motion=motion};
            if(!FireWalls.Valid(maker)){throw new NotSupportedException("Invalid Fire Wall maker");}_makers.Add(id,maker);
        }
    }
    private static FirePatch AddWallField(PlayerMobile owner,int rank,double x,double y,int z,DateTime at)
    {
        var values=SkillMechanics.Values(owner,CombatAbility.FireWall,rank);var mods=DruidFirestorm.Modifiers(owner);
        var ends=DruidFirestorm.Endpoints(values,mods.FireMastery,AmazonSlowMissiles.Capture(owner,false));long id=checked(++_nextId);
        var p=new FirePatch(id,owner.Serial.Value,CombatAbility.FireWall,owner.Map.MapID,x,y,z,ends.Min,ends.Max,41,at.AddMilliseconds(3600),at.AddMilliseconds(40),1)
            {SkillRank=rank,FireWallModifiers=mods,Seed=SourceRandom.InitLow(Blazes.SeedLow())};
        if(!FireWalls.Valid(p)){throw new NotSupportedException("Invalid Fire Wall field");}_patches.Add(id,p);return p;
    }
    // Dedicated model clock advances this actual controller without advancing
    // unrelated owners or NPC fields. Natural connected timers are tested separately.
    internal static void AdvanceWall(PlayerMobile owner,DateTime at,bool actions)
    {
        while(true)
        {
            var maker=_makers.Values.Where(x=>x.Owner==owner.Serial.Value&&x.Motion!=null&&x.NextUtc<=at).OrderBy(x=>x.NextUtc).ThenBy(x=>x.Id).FirstOrDefault();
            var field=_patches.Values.Where(x=>x.Owner==owner.Serial.Value&&x.FireWallModifiers!=null&&x.NextUtc<=at).OrderBy(x=>x.NextUtc).ThenBy(x=>x.Id).FirstOrDefault();
            if(maker==null&&field==null){return;}
            if(maker!=null&&(field==null||maker.NextUtc<field.NextUtc||maker.NextUtc==field.NextUtc&&maker.Id<field.Id)){TickWallMaker(maker,at,actions);}
            else{TickBlaze(field,at,actions);}
        }
    }
    private static FirePatch TickWallMaker(FireMaker m,DateTime at,bool? actions)
    {
        var owner=Owner(m.Owner);var motion=m.Motion;
        if(owner==null||owner.Map!=Map.Internal&&owner.Map?.MapID!=m.Map){_makers.Remove(m.Id);_loadedWallMakers.Remove(m.Id);return null;}
        if(_loadedWallMakers.Remove(m.Id)||owner.Map==Map.Internal||!(actions??owner.NetState is{Running:true}))
        {
            int skip=(int)Math.Min(motion.Frames-motion.Frame,Math.Max(1,(at-m.NextUtc).Ticks/TimeSpan.TicksPerMillisecond/40+1));
            if(motion.Frame+skip>=motion.Frames||m.UntilUtc<=at){_makers.Remove(m.Id);return null;}
            _makers[m.Id]=m with{NextUtc=m.NextUtc.AddMilliseconds(40L*skip),Motion=motion with{Frame=motion.Frame+skip,CellChanged=false}};return null;
        }
        var point=new Point3D((m.CellX+1)/2,(m.CellY+1)/2,m.Z);
        if(SpellHelper.IsTown(point,owner)){_makers.Remove(m.Id);return null;}
        FirePatch child=null;
        foreach(var request in MoltenBoulderPackets.Trail(true,true,true,true,69,motion.CellChanged?8:0,m.CellX,m.CellY,m.Rank,1).Requests)
        {child=AddWallField(owner,request.Rank,request.X/2.0,request.Y/2.0,m.Z,m.NextUtc);}
        double x=m.X+m.Dx,y=m.Y+m.Dy;var steps=SourceMissileSteps.Advance(m.X,m.Y,m.Dx,m.Dy,motion.Steps);
        bool blocked=x<=0||y<=0||x>=owner.Map.Width||y>=owner.Map.Height;
        foreach(var c in steps.Cells)
        {
            var dest=new Point3D((c.X+1)/2,(c.Y+1)/2,m.Z);
            if(!owner.Map.CanFit(dest,1,false,false,false)||!owner.Map.LineOfSight(new Point3D((int)Math.Round(m.X),(int)Math.Round(m.Y),m.Z+12),new Point3D(dest.X,dest.Y,m.Z+12))){blocked=true;break;}
        }
        int frame=motion.Frame+1;
        if(blocked||frame>=motion.Frames){_makers.Remove(m.Id);return child;}
        // Mode8 zero-damage contacts retain LastCollide. Full hit-by-missile
        // event subscribers remain an explicit unsupported actor/event context.
        uint last=motion.LastCollision;
        foreach(var c in steps.Cells)
        {
            var probe=new FirePatch(m.Id,m.Owner,CombatAbility.FireWall,m.Map,c.X/2.0,c.Y/2.0,m.Z,0,0,0,m.NextUtc,m.NextUtc,1);
            if(ImmolationArrows.Recipient(owner,probe) is not{} target){continue;}
            if(last!=target.Serial.Value){last=target.Serial.Value;SourceFireFields.Gap("Fire Wall maker zero-damage hit-by-missile event subscribers need their ordered actor adapter");}break;
        }
        int cx=(int)Math.Floor(x*2+.5),cy=(int)Math.Floor(y*2+.5);
        _makers[m.Id]=m with{X=x,Y=y,CellX=cx,CellY=cy,NextUtc=m.NextUtc.AddMilliseconds(40),Motion=motion with{Frame=frame,CellChanged=cx!=m.CellX||cy!=m.CellY,Steps=steps,LastCollision=last}};
        return child;
    }
}
