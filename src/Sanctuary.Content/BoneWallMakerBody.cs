// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record BoneWallArm(double X,double Y,double Vx,double Vy,int Velocity256,int Budget,int Remaining,
    bool Changed,SourceStepTrace Steps,bool Done=false,uint LastCollider=0);
internal sealed record BoneWallGrowth(int Rank,int Frames,int Budget,int SlowPercent,DateTime NextUtc,BoneWallArm Left,BoneWallArm Right);

// A separate subtype keeps historical plain and prepared barrier save layouts
// and growth behavior intact. Only new Wall roots opt into missile makers.
[SerializationGenerator(0)]
public partial class BoneWallMakerBody:BoneBarrierBody
{
    [SerializableField(0,setter:"private")] private string _growthJson="";
    [SerializableField(1,setter:"private")] private List<BoneBarrierSegment> _madeChildren=new();
    private BoneWallGrowth _growth;
    internal BoneWallGrowth Growth=>_growth??(string.IsNullOrEmpty(_growthJson)?null:_growth=JsonSerializer.Deserialize<BoneWallGrowth>(_growthJson));
    [Constructible]public BoneWallMakerBody(){}
    internal void Made(BoneBarrierSegment child){_madeChildren.Add(child);}
    public override void OnDelete()
    {
        // A maker may travel beyond the historical16tile cleanup scan after
        // many failed placements. Keep explicit minion links, including saves.
        foreach(var child in _madeChildren.ToArray()){child?.Delete();}
        _madeChildren.Clear();base.OnDelete();
    }
    private void Store(BoneWallGrowth s){Validate(s);_growth=s;_growthJson=JsonSerializer.Serialize(s);}
    internal static void Validate(BoneWallGrowth s)
    {
        if(s==null||s.Rank is<1 or>255||s.Frames!=7+2*s.Rank||s.Budget!=4||s.SlowPercent is<0 or>100||s.NextUtc.Kind!=DateTimeKind.Utc)
        {throw new InvalidDataException("Unsupported Bone Wall maker recipe");}
        foreach(var a in new[]{s.Left,s.Right})
        {
            if(a==null||!double.IsFinite(a.X)||!double.IsFinite(a.Y)||!double.IsFinite(a.Vx)||!double.IsFinite(a.Vy)
                ||a.Velocity256!=SlowMissilePayload.VelocityFp8(12,0,s.Rank,33,0,true,s.SlowPercent)
                ||a.Budget<0||a.Budget>s.Budget||a.Remaining<0||a.Remaining>s.Frames||a.Steps?.Cells==null||a.Steps.Cells.Length>10)
            {throw new InvalidDataException("Invalid saved Bone Wall arm");}
        }
    }
    internal void BeginGrowth(PlayerMobile owner,SkillValues values)
    {
        var row=new MissileValues("bonewallmaker",values);
        if(values.Ability!=CombatAbility.BoneWall||row.Number("pSrvDoFunc")!=13||row.Number("Vel")!=12||row.Number("MaxVel")!=12
            ||row.Number("CollideType")!=8||row.Number("Size")!=1||row.Number("CanSlow")!=1||row.Number("InitSteps")!=0
            ||row.Number("pSrvHitFunc")!=0||row.Number("CollideKill")!=0||row.Number("LastCollide")!=1||values.Eval("calc2")!=8)
        {throw new NotSupportedException("Unmapped Bone Wall maker route");}
        int frames=checked((int)row.Eval("Range")),budget=(int)values.Eval("calc2")/2;
        int slow=AmazonSlowMissiles.Capture(owner,true).VelocityPercent;
        int velocity=SlowMissilePayload.VelocityFp8(12,0,values.Rank,33,0,true,slow);
        int dx=owner.X-X,dy=owner.Y-Y;if(dx==0&&dy==0){dx=1;}
        BoneWallArm Arm(int x,int y)
        {
            var v=MissileVectors.VelocityFp8(x,y,velocity);
            // The existing stable-path adapter starts at a centered source
            // cell; full factory/path flag initialization remains unverified.
            return new(X,Y,v.X,v.Y,velocity,budget,frames,false,new([new(X*2,Y*2)]));
        }
        Store(new(values.Rank,frames,budget,slow,TimedEffects.Now.AddMilliseconds(40),Arm(-dy,dx),Arm(dy,-dx)));
    }
    protected override void AdvanceGrowth()
    {
        var s=Growth;if(s==null){return;}
        while(s.NextUtc<=TimedEffects.Now&&!(s.Left.Done&&s.Right.Done))
        {
            // Same first/second arm order as the source factory calls.
            s=s with{Left=Advance(s.Left),Right=Advance(s.Right),NextUtc=s.NextUtc.AddMilliseconds(40)};
            Store(s);
        }
    }
    internal void InspectGrowth(DateTime at)
    {
        var s=Growth;if(s==null||s.Left.Done&&s.Right.Done){return;}
        Store(s with{Left=Advance(s.Left),Right=Advance(s.Right),NextUtc=at.AddMilliseconds(40)});
    }
    internal void HoldInspectedGrowth(DateTime at){if(Growth is{} s){Store(s with{NextUtc=at});}}
    private static int Cell(double x)=>(int)Math.Floor(x*SkillGeometry.SubtilesPerTile+0.5);
    private BoneWallArm Advance(BoneWallArm a)
    {
        if(a.Done){return a;}
        var next=a;var owner=SummonMaster as PlayerMobile;var map=Map;
        bool Spawn()
        {
            // Existing world-fit/town projection, explicitly not recovered
            // source 2x2 room/collision allocation. Never retry flags13.
            var p=new Point3D(BoneBarriers.Round(SkillGeometry.Tiles(Cell(a.X))),BoneBarriers.Round(SkillGeometry.Tiles(Cell(a.Y))),Z);
            return owner is{Deleted:false}&&owner.Map==map&&BoneBarriers.Space(owner,p)
                &&BoneBarriers.Allocate(owner,CombatAbility.BoneWall,p,null,this)!=null;
        }
        int Update()
        {
            int Move()
            {
                double x=a.X+a.Vx,y=a.Y+a.Vy;
                next=next with{X=x,Y=y,Changed=Cell(x)!=Cell(a.X)||Cell(y)!=Cell(a.Y),Steps=SourceMissileSteps.Advance(a.X,a.Y,a.Vx,a.Vy,a.Steps)};
                return x<=0||y<=0||x>=map.Width||y>=map.Height?2:1;
            }
            Mobile Find(SourceMissileCell cell)
            {
                foreach(var target in map.GetMobilesInRange<Mobile>(new Point3D((cell.X+1)/2,(cell.Y+1)/2,Z),3))
                {
                    if(target.Serial.Value==next.LastCollider||target is not{Deleted:false,Alive:true,Blessed:false}
                        ||Math.Abs(target.Z-Z)>=16||owner==null||owner.Map!=map||!ClassCombat.IsHostile(owner,target)||!owner.CanBeHarmful(target,false)) {continue;}
                    int size=target is BaseCreature creature?CreatureProfiles.For(creature).Traits.Size??2:2;
                    if(CombatHitboxes.SourceTouches(1,size,cell.X-target.X*2,cell.Y-target.Y*2)){return target;}
                }
                return null;
            }
            bool Blocked(SourceMissileCell cell)=>DruidFirestorm.CellBlocked(map,Z,a.X,a.Y,cell.X,cell.Y);
            int Hit(uint target,bool terminal)
            {
                if(terminal){return 2;}
                // Maker has no damage/hit callback or CollideKill. Retain its
                // last collider without substituting spell or weapon damage.
                next=next with{LastCollider=target};return 1;
            }
            var result=BasicMissileUpdate.Run(new(true,true,a.Velocity256,a.Remaining,8,Growth.Frames,true),
                new(Move,n=>next=next with{Remaining=n},()=>map!=null&&map!=Map.Internal,()=>true,()=>64,()=>next.Steps.Cells,
                    c=>Blocked(c)?4:Find(c)!=null?256:0,c=>Blocked(c)?0:Find(c)?.Serial.Value??0,Hit));
            return result.Result;
        }
        int result=BoneWallMakerUpdate.Run(a.Budget,new(()=>owner is{Deleted:false},()=>a.Changed,()=>Prepared!=null,()=>true,
            ()=>!Deleted,Spawn,n=>next=next with{Budget=n},Update));
        return next with{Done=result==2||result==0};
    }
    [AfterDeserialization]private void AfterMakerLoad(){if(Growth is{} s){Validate(s);}}
}
