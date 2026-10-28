// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class MiasmaLaunchChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MiasmaLaunchChecks));
    public static void Configure(){Register("miasma-launch-check");Register("miasma-launch-reload-check");}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Miasma launch and footprint inventory",_=>Core.LoopContext.Post(()=>
    {try{Footprints();Models();OrderedServices();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool v,string why){if(!v){throw new InvalidOperationException(why);}}
    private static void Footprints()
    {
        MissileFootprints.ValidateSources();var at=TimedEffects.Now;
        var rows=BalanceCatalog.Read<SummonSource>("necromancer-summons").Where(r=>r.Fields.TryGetValue("Collision",out var c)&&c.ValueKind==JsonValueKind.Number&&c.GetInt32()!=0).ToArray();
        Require(rows.Length==17,"Closed projected Collision1 inventory changed");int active=0,deferred=0;
        foreach(var row in rows)
        {
            var p=new MissileFootprint(row.Key,1,20,20,0,at.AddSeconds(1));
            if(row.Key is "miasmaboltcloud" or "miasmaboltdot")
            {
                try{MissileFootprints.Covers(p,1,20,20,0,at);throw new InvalidOperationException("Reference-only row became a footprint");}catch(NotSupportedException){}
                try{MissileFootprints.RegisterTransient(p,()=>true);throw new InvalidOperationException("Reference-only transient registered");}catch(NotSupportedException){}deferred++;
            }
            else{Require(MissileFootprints.Covers(p,1,20,20,0,at),"Existing active footprint lost: "+row.Key);active++;}
        }
        Require(active==15&&deferred==2&&!MissileFootprints.Snapshot().Any(x=>x.Source.StartsWith("miasma",StringComparison.Ordinal)),"Reference rows entered active snapshot");
        try{MissileFootprints.Covers(new("unreviewed",1,20,20,0,at.AddSeconds(1)),1,20,20,0,at);throw new InvalidOperationException("Unknown footprint accepted");}catch(NotSupportedException){}
    }
    private static void Models()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"miasma-launch-source-probe.json")));int count=0;
        foreach(var row in source.RootElement.GetProperty("factory").EnumerateArray())
        {
            int rank=row[0].GetInt32(),scene=row[1].GetInt32(),delta=row[2].GetInt32(),axis=row[3].GetInt32(),pct=row[4].GetInt32(),allocation=row[5].GetInt32(),gate=row[6].GetInt32();
            var from=new Point2D(400,700);var aimed=new Point2D(400+(axis==0?delta:0),700+(axis==1?delta:0));
            var cursor=scene is 1 or 2?new Point2D(450,750):scene==3?new Point2D(0,700):scene==4?from:aimed;int calls=0;
            var p=MiasmaLaunch.Prepare(from,scene==1?aimed:null,cursor,scene==2?aimed:null,rank,pct,gate!=1,gate!=2,()=>{calls++;return allocation!=0;});
            Require((p!=null)==(row[7].GetInt32()!=0)&&calls==row[8].GetInt32()&&row[9].GetInt32()==0,"Complete wrapper/factory allocation/aim guard differs");
            if(p!=null)
            {
                var expected=row[10];Require(new[]{p.Flags,p.X,p.Y,p.AimX,p.AimY,p.Velocity256,p.Frames,p.Frames,p.ActivateFrame,p.Skill,p.Rank}.SequenceEqual(expected.EnumerateArray().Take(11).Select(x=>x.GetInt32()))&&p.MaximumVelocity256==5120,"Complete factory plan differs");
                Require(expected[11].GetInt32()==0&&expected[12].GetInt32()==91,"Full source owner setter metadata");
            }
            count++;
        }
        Require(count==3600,"Missing factory contexts");count=0;
        foreach(var row in source.RootElement.GetProperty("collision").EnumerateArray())
        {
            int frame=row[0].GetInt32(),velocity=row[1].GetInt32(),move=row[2].GetInt32(),mode=row[3].GetInt32(),flags=row[4].GetInt32(),activate=row[5].GetInt32(),target=row[6].GetInt32(),mask=row[7].GetInt32();
            int moves=0,finds=0,hits=0,end=-1,writes=0,written=frame;
            var p=BasicMissileUpdate.Run(new(true,true,velocity,frame,mode,activate,mode==3),new(
                ()=>{moves++;return move;},n=>{writes++;written=n;},()=>true,()=>true,()=>flags,()=>[new(10,20)],_=>mask,
                _=>{finds++;return target!=0?42u:0u;},(id,ending)=>{hits++;end=ending?1:0;Require(ending?id==0:id==42,"Hit callback target changed");return 7;}));
            Require(new[]{p.Result,p.Remaining,moves,finds,hits,end,writes}.SequenceEqual(row.EnumerateArray().Skip(8).Select(x=>x.GetInt32()))&&written==p.Remaining&&p.WroteFrame==(writes==1),"Full generic do1 control flow differs");count++;
        }
        Require(count==5120&&!WarlockCombat.Implemented(CombatAbility.MiasmaBolt),"Missing update cases or premature learned handler");
        Log.Information("Miasma launch:3600complete wrapper/factory/owner and5120complete update comparisons pass;15active footprints retained,2reference rows remain unsupported");
    }
    private static void OrderedServices()
    {
        int frame=50,flags=0,masks=0,finds=0,hits=0;uint recipient=0;string order="";
        var service=new BasicMissileServices(()=>{order+="M";flags=64;return 1;},n=>{order+="W";frame=n;},()=>true,()=>true,
            ()=>{Require(frame==49&&flags==64,"Path flags read before movement/frame write");return flags;},()=>[new(1,1),new(2,2)],
            _=>{masks++;return 64;},cell=>{finds++;return cell.X==2?42u:0;},(id,ending)=>{Require(frame==49&&!ending,"Hit before frame write");hits++;recipient=id;return 7;});
        var p=BasicMissileUpdate.Run(new(true,true,3840,50,3,50,true),service);
        Require(order=="MW"&&p.Result==7&&masks==finds&&masks==2&&hits==1&&recipient==42,"Ordered saved-step selection");
        masks=finds=hits=0;frame=50;
        var wall=service with{Mask=_=>{masks++;return 4;},Find=_=>{finds++;return 0;},Hit=(id,ending)=>{Require(ending&&id==0&&frame==49,"Wall end dispatch order");hits++;return 7;}};
        p=BasicMissileUpdate.Run(new(true,true,3840,50,3,50,true),wall);Require(p.Result==2&&masks==1&&finds==1&&hits==1,"Wall callback return replaced removal or scanned later steps");
        frame=50;int writes=0,reads=0;
        var failure=service with{Move=()=>2,WriteRemaining=_=>writes++,PathFlags=()=>{reads++;return 64;},Hit=(id,ending)=>{Require(id==0&&ending&&frame==50,"Movement failure order");return 7;}};
        p=BasicMissileUpdate.Run(new(true,true,3840,50,3,50,true),failure);Require(!p.WroteFrame&&p.Remaining==50&&writes==0&&reads==0,"Movement failure decremented or scanned");
        var expiry=service with{WriteRemaining=n=>frame=n,Move=()=>1,Mask=_=>throw new InvalidOperationException("Expired missile scanned contacts"),Hit=(id,ending)=>{Require(id==0&&ending&&frame==0,"Expiry handler preceded frame write");return 7;}};
        p=BasicMissileUpdate.Run(new(true,true,3840,1,3,50,true),expiry);Require(p.Result==7&&p.Remaining==0,"Expiry did not propagate handler result");
        var missing=service with{HasRoom=()=>false,PathFlags=()=>throw new InvalidOperationException("Missing room read flags"),WriteRemaining=_=>{}};
        Require(BasicMissileUpdate.Run(new(true,true,0,50,3,50,true),missing).Result==2,"Missing-room guard");
    }
}
