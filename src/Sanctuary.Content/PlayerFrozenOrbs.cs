// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

public sealed record FrozenSource(int VelocityFp8,SourceStepTrace Steps,FirestormModifiers Modifiers,ulong Seed);
internal static class PlayerFrozenOrbs
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PlayerFrozenOrbs));private static readonly HashSet<string> Warned=new();
    internal static void Gap(string reason){if(Warned.Add(reason)){Log.Warning("Frozen Orb unsupported context: {Reason}",reason);}}
    internal static bool ValidAim(PlayerMobile p,Serial selected,Point3D point,out Point3D aim)
    {
        aim=point;if(p is not{Alive:true,Deleted:false}||p.Map==null||p.Map==Map.Internal){return false;}
        if(selected!=Serial.Zero){if(World.FindMobile(selected) is not{} t||!ClassCombat.IsHostile(p,t)){return false;}aim=t.Location;}
        // Ordinary projectile creation has no do28 landing mask5. Native
        // range/LOS/safe-area rules remain explicit outer-world adapters.
        return aim.X>0&&aim.Y>0&&aim.X<p.Map.Width&&aim.Y<p.Map.Height&&p.InRange(aim,CombatProtocol.TeethRange)&&p.InLOS(aim)
            &&!SpellHelper.IsTown(p.Location,p)&&!SpellHelper.IsTown(aim,p);
    }
    internal static void Route()
    {
        var s=SkillMechanics.For(CombatAbility.FrozenOrb);int M(string key,string f)=>NecromancerSummons.Source(key,f);
        if(s.SourceId!=64||s.Number("srvdofunc")!=0||s.Text("srvmissile")!="frozenorb"||!s.Left||!s.Right||s.Number("localdelay")!=25
            ||M("frozenorb","pSrvDoFunc")!=15||M("frozenorb","pSrvHitFunc")!=29||M("frozenorb","Range")!=30||M("frozenorb","Vel")!=10||M("frozenorb","InitSteps")!=1
            ||M("frozenorb","Param1")!=1||M("frozenorb","Param2")!=19||M("frozenorb","sHitPar1")!=4||M("frozenorb","CollideKill")!=0||M("frozenorb","AlwaysExplode")!=1
            ||M("frozenorbbolt","pSrvDoFunc")!=1||M("frozenorbnova","pSrvDoFunc")!=16||M("frozenorbnova","Param1")!=6||M("frozenorbnova","Param2")!=2)
        {throw new NotSupportedException("Unsupported pinned Frozen Orb route");}
        foreach(string key in new[]{"frozenorb","frozenorbbolt","frozenorbnova"})
        {
            bool root=key=="frozenorb";
            if(M(key,"CollideType")!=3||M(key,"Size")!=1||M(key,"LastCollide")!=1||M(key,"NextHit")!=0||M(key,"ToHit")!=0||M(key,"GetHit")!=1||M(key,"ReturnFire")!=1
                ||M(key,"CanSlow")!=0||M(key,"HitFlags")!=0||M(key,"ResultFlags")!=4||M(key,"pSrvDmgFunc")!=0
                ||M(key,"Vel")!=(root?10:key=="frozenorbbolt"?18:24)||M(key,"MaxVel")!=M(key,"Vel")||M(key,"VelLev")!=0
                ||!root&&(M(key,"Range")!=25||M(key,"InitSteps")!=0||M(key,"CollideKill")!=1||M(key,"pSrvHitFunc")!=0))
            {throw new NotSupportedException("Unsupported Frozen Orb missile row");}
        }
    }
    internal static int Frames(FrozenKind kind)=>kind==FrozenKind.Orb?30:25;
    internal static int Velocity(FrozenKind kind)=>kind==FrozenKind.Orb?1920:kind==FrozenKind.Bolt?3456:4608;
    internal static FrozenMissile Prepare(PlayerMobile p,Point3D aim,SkillValues values,DateTime at)
    {
        Route();if(values.Ability!=CombatAbility.FrozenOrb||values.Rank is <1 or >255||at.Kind!=DateTimeKind.Utc||!ValidAim(p,Serial.Zero,aim,out _))
        {throw new NotSupportedException("Unsupported prepared Frozen Orb");}
        int dx=aim.X-p.X,dy=aim.Y-p.Y;if(dx==0&&dy==0){dx=dy=1;}var v=MissileVectors.VelocityFp8(dx,dy,1920);
        return new(0,p.Serial.Value,values.Rank,p.Map.MapID,FrozenKind.Orb,p.X,p.Y,p.Z,v.X,v.Y,0,0,0,0,0,0,0,0,at.AddMilliseconds(1200),at.AddMilliseconds(40))
            {Source=new(1920,new([new(p.X*2,p.Y*2)]),DruidFirestorm.Modifiers(p),0)};
    }
    internal static bool Release(PlayerMobile p,Serial selected,Point3D point,SkillValues values)
    {
        if(!ValidAim(p,selected,point,out var aim)||!FireGround.Ready(p,CombatAbility.FrozenOrb)){return false;}var at=TimedEffects.Now;var prepared=Prepare(p,aim,values,at);
        if(!NecromancerEffects.Pay(p,values.Mana256)){return false;}FrozenOrbs.Commit(prepared);FireGround.Delay(p,CombatAbility.FrozenOrb,at);p.PlaySound(0x1E5);return true; // Identified cast audio placeholder.
    }
    internal static PreparedMonsterDamagePacket Roll(FrozenMissile m,ref ulong seed)
    {int raw=SorceressSpellMissiles.Roll(m.Min256,m.Max256,seed,out seed);return new(0,raw,0,m.ColdFrames,5,Cold256:raw);}
    internal static bool Valid(FrozenMissile m)=>m.Source==null||m.Rank is >=1 and <=255&&m.Owner!=0&&((Serial)m.Owner).IsMobile&&m.Map>=0&&m.Map<Map.Maps.Length&&Map.Maps[m.Map]!=null&&Map.Maps[m.Map]!=Map.Internal
        &&m.Source.VelocityFp8==Velocity(m.Kind)&&m.Source.Steps?.Cells is{Length:>0 and <=10}&&m.Source.Modifiers is{Potion:>=0}
        &&m.X>0&&m.Y>0&&m.X<Map.Maps[m.Map].Width&&m.Y<Map.Maps[m.Map].Height&&m.Z is >=-128 and <=127
        &&Math.Abs(m.Vx)<=.5625&&Math.Abs(m.Vy)<=.5625&&m.NextUtc.Kind==DateTimeKind.Utc&&m.UntilUtc.Kind==DateTimeKind.Utc
        &&m.UntilUtc-m.NextUtc==TimeSpan.FromMilliseconds((Frames(m.Kind)-m.Frame-1)*40)
        &&(m.Kind!=FrozenKind.Orb||m.Min256==0&&m.Max256==0&&m.ColdFrames==0&&m.VectorX==0&&m.VectorY==0)
        &&(m.Kind!=FrozenKind.Bolt||m.VectorX==0&&m.VectorY==0)&&(m.Kind!=FrozenKind.Nova||m.VectorX!=0||m.VectorY!=0);
}

public static partial class FrozenOrbs
{
    private static readonly HashSet<long> _loaded=new();
    internal static void Commit(FrozenMissile prepared)
    {
        var m=prepared with{Id=checked(++_nextId),Source=prepared.Source with{Seed=SourceRandom.InitLow(Blazes.SeedLow())}};
        if(!PlayerFrozenOrbs.Valid(m)){throw new NotSupportedException("Invalid Frozen Orb root");}_missiles.Add(m.Id,m);
    }
    internal static void Advance(PlayerMobile p,DateTime at,bool actions)=>TickSource(at,actions,p.Serial.Value);
    private static void TickSource(DateTime at,bool? actions,uint? onlyOwner=null)
    {
        var queue=new PriorityQueue<long,(DateTime At,long Id)>();foreach(var m in _missiles.Values){if(m.Source!=null&&m.NextUtc<=at&&(!onlyOwner.HasValue||m.Owner==onlyOwner)){queue.Enqueue(m.Id,(m.NextUtc,m.Id));}}
        while(queue.TryDequeue(out long id,out var priority))
        {
            if(!_missiles.TryGetValue(id,out var m)||m.NextUtc!=priority.At){continue;}var children=StepSource(m,at,actions);
            foreach(var child in children){if(child.NextUtc<=at){queue.Enqueue(child.Id,(child.NextUtc,child.Id));}}
            if(_missiles.TryGetValue(id,out var next)&&next.NextUtc<=at){queue.Enqueue(id,(next.NextUtc,id));}
        }
    }
    private static FrozenMissile SourceChild(PlayerMobile p,FrozenMissile origin,FrozenKind kind,int index,DateTime at)
    {
        var direction=Direction(index);var v=MissileVectors.VelocityFp8(direction.X,direction.Y,PlayerFrozenOrbs.Velocity(kind));
        // Reuse the existing source-subtile bridge used by path crossings.
        double x=Math.Floor(origin.X*2+.5)/2,y=Math.Floor(origin.Y*2+.5)/2;
        if(x<=0||y<=0||x>=p.Map.Width||y>=p.Map.Height){return null;}
        var values=SkillMechanics.Values(p,CombatAbility.FrozenOrb,origin.Rank);var mods=DruidFirestorm.Modifiers(p);var ends=DruidFirestorm.Endpoints(values,mods.ColdMastery,SlowMissilePayload.Normal);
        var m=new FrozenMissile(checked(++_nextId),origin.Owner,origin.Rank,origin.Map,kind,x,y,origin.Z,v.X,v.Y,0,index,kind==FrozenKind.Nova?direction.X:0,kind==FrozenKind.Nova?direction.Y:0,
            ends.Min,ends.Max,values.DurationFrames,0,at.AddMilliseconds(1000),at.AddMilliseconds(40))
            {Source=new(PlayerFrozenOrbs.Velocity(kind),new([new((int)(x*2),(int)(y*2))]),mods,SourceRandom.InitLow(Blazes.SeedLow()))};
        if(!PlayerFrozenOrbs.Valid(m)){throw new NotSupportedException("Invalid Frozen Orb child");}_missiles.Add(m.Id,m);return m;
    }
    private static List<FrozenMissile> StepSource(FrozenMissile m,DateTime at,bool? actions)
    {
        var children=new List<FrozenMissile>();var p=PlayerBlizzards.Owner(m.Owner);bool loaded=_loaded.Remove(m.Id);int frames=PlayerFrozenOrbs.Frames(m.Kind);
        if(p==null||p.Map!=Map.Internal&&p.Map?.MapID!=m.Map){_missiles.Remove(m.Id);return children;}
        if(loaded||p.Map==Map.Internal||!(actions??p.NetState is{Running:true}))
        {
            int skip=(int)Math.Min(frames-m.Frame,Math.Max(1,(at-m.NextUtc).Ticks/TimeSpan.TicksPerMillisecond/40+1));
            if(m.Frame+skip>=frames||m.UntilUtc<=at){_missiles.Remove(m.Id);}else{_missiles[m.Id]=m with{Frame=m.Frame+skip,NextUtc=m.NextUtc.AddMilliseconds(skip*40L)};}return children;
        }
        if(SpellHelper.IsTown(new Point3D((int)Math.Round(m.X),(int)Math.Round(m.Y),m.Z),p)){_missiles.Remove(m.Id);return children;}
        var next=m;bool removed=false,barrier=false;int remaining=frames-m.Frame;
        if(m.Kind==FrozenKind.Orb)
        {
            if(SourceChild(p,m,FrozenKind.Bolt,m.DirectionIndex,m.NextUtc) is{} child){children.Add(child);}
            next=next with{DirectionIndex=(m.DirectionIndex+19)&63}; // Advance even after an individual allocation refusal.
        }
        else if(m.Kind==FrozenKind.Nova&&m.Frame<6&&m.Frame%2==0)
        {
            var turn=Turn(m.VectorX,m.VectorY);if(turn.X==0&&turn.Y==0){PlayerFrozenOrbs.Gap("Zero-vector nova path completion needs its source path adapter");_missiles.Remove(m.Id);return children;}
            var v=MissileVectors.VelocityFp8(turn.X,turn.Y,4608);next=next with{VectorX=turn.X,VectorY=turn.Y,Vx=v.X,Vy=v.Y};
        }
        int Move()
        {
            next=next with{X=m.X+next.Vx,Y=m.Y+next.Vy,Source=next.Source with{Steps=SourceMissileSteps.Advance(m.X,m.Y,next.Vx,next.Vy,m.Source.Steps)}};
            barrier=next.X<=0||next.Y<=0||next.X>=p.Map.Width||next.Y>=p.Map.Height;return barrier?2:1;
        }
        Mobile Find(SourceMissileCell cell)
        {
            foreach(var t in p.Map.GetMobilesInRange<Mobile>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,m.Z),3))
            {
                if(t is not{Alive:true,Deleted:false,Blessed:false}||t.Serial.Value==m.LastCollision||Math.Abs(t.Z-m.Z)>=16||!ClassCombat.IsHostile(p,t)||!p.CanBeHarmful(t,false)||SpellHelper.IsTown(t.Location,p)){continue;}
                int size=t is BaseCreature c?CreatureProfiles.For(c).Traits.Size??2:2;
                if(CombatHitboxes.SourceTouches(1,size,cell.X-t.X*2,cell.Y-t.Y*2)){return t;}
            }
            return null;
        }
        bool Wall(SourceMissileCell cell)=>DruidFirestorm.CellBlocked(p.Map,m.Z,m.X,m.Y,cell.X,cell.Y);
        int Hit(uint id,bool terminal)
        {
            if(m.Kind==FrozenKind.Orb&&remaining==0)
            {
                for(int i=0;i<64;i+=4){if(SourceChild(p,next,FrozenKind.Nova,i,m.NextUtc) is{} child){children.Add(child);}}
            }
            else if(!terminal&&World.FindMobile((Serial)id) is{} t)
            {
                next=next with{LastCollision=id};SorceressEffects.MissileHit(p,t,Key(m.Kind));
                if(!Meteors.Supported(p,t,out string reason)){PlayerFrozenOrbs.Gap(reason);}
                else
                {
                    if(m.Kind==FrozenKind.Orb){PlayerFrozenOrbs.Gap("Root zero-packet event subscribers need their complete actor adapter;ordinary typed return is retained");}
                    ulong seed=next.Source.Seed;var packet=PlayerFrozenOrbs.Roll(m,ref seed);next=next with{Source=next.Source with{Seed=seed}};
                    if(!ClassAvoidance.Missile(t,Key(m.Kind))){PlayerBlizzards.Deliver(p,(BaseCreature)t,packet,m.Source.Modifiers,m.NextUtc,"Frozen Orb");}
                }
                if(m.Kind==FrozenKind.Orb){return 1;}
            }
            removed=true;return 2;
        }
        var result=BasicMissileUpdate.Run(new(true,true,m.Source.VelocityFp8,remaining,2,frames,true),new(Move,r=>{remaining=r;next=next with{Frame=frames-r};},()=>true,()=>true,()=>64,()=>next.Source.Steps.Cells,
            cell=>Wall(cell)?4:Find(cell)!=null?1:0,cell=>Find(cell)?.Serial.Value??0,Hit));
        if(removed||barrier||result.Result==2){_missiles.Remove(m.Id);return children;}
        next=next with{NextUtc=m.NextUtc.AddMilliseconds(40)};if(_missiles.ContainsKey(m.Id)){_missiles[m.Id]=next;}
        if(next.Frame%2==0){Effects.SendLocationEffect(new Point3D((int)Math.Round(next.X),(int)Math.Round(next.Y),next.Z),p.Map,m.Kind==FrozenKind.Orb?0x375A:0x36E4,3,10,0x480,0);} // Identified orb/ice placeholders.
        return children;
    }
}
