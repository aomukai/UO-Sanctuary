// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class CreeperFieldsChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CreeperFieldsChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("creeper-fields-check","Prepared Creeper charged motion/finite mats",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("creeper-fields-check: passed");}catch(Exception e){Log.Error(e,"creeper-fields-check: failed");}}));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static CreeperFields Tick(CreeperFields s,bool actions=true,uint victim=0,bool hostile=true,bool blocked=false)
        =>DruidCreeperFields.Step(s,actions,(_,_,_)=>blocked,_=>victim,_=>hostile);
    private static void Check()
    {
        foreach(var (rank,frames) in new[]{(1,125),(20,600),(21,625),(255,6475)})
        {Require(DruidCreeperFields.Lifetime(rank)==frames,"Current expression lifetime lost rank");}
        var launched=DruidCreeperFields.Launch(DruidCreeperFields.Empty(),1,DruidCreeperVines.Heads(1,14000,4000),z:100);
        Require(launched.Heads.Length==3 && launched.Heads.All(h=>h.Velocity256==1344&&h.Remaining==20&&h.X==7000&&h.Y==2000),"Flags3origin or velocity route");
        var first=launched.Heads[0];var third=launched.Heads[2];
        Require(first.Path.SequenceEqual(ChargedBolts.Path(14000,2,20)) && third.Path.SequenceEqual(ChargedBolts.Path(14003,0,20)),"Aim-X/ordinal initializer or cardinal direction wrong");
        var s=Tick(Tick(launched));Require(s.Mats.Length==0 && s.Heads.All(h=>h.Remaining==18),"Trail emitted before remaining18");
        var before=s.Heads.ToDictionary(h=>h.Id);s=Tick(s);
        Require(s.Mats.Length==3 && s.Mats.All(m=>m.Total==125&&m.Remaining==125&&m.Z==100)
            && s.Mats.Select(m=>(m.X,m.Y)).Order().SequenceEqual(before.Values.Select(h=>((int)Math.Floor(h.X*2+.5),(int)Math.Floor(h.Y*2+.5))).Order()),"do26did not emit before movement/decrement");
        for(int tick=4;tick<=12;tick++){s=Tick(s);}
        Require(s.Mats.Length==6 && s.Mats.Count(m=>m.Remaining==125)==3 && s.Heads.All(h=>h.Remaining==8),"Second emission remaining9or per-mat age wrong");
        var clone=DruidCreeperFields.Copy(s);var original=s.Heads[0].Path[1];clone.Heads[0].Path[1]=new(99,99);
        Require(s.Heads[0].Path[1]==original,"Field path alias escaped");
        var roundtrip=JsonSerializer.Deserialize<CreeperFields>(JsonSerializer.Serialize(s));DruidCreeperFields.Validate(roundtrip);
        Require(JsonSerializer.Serialize(roundtrip)==JsonSerializer.Serialize(s),"Field path/lifetime/history roundtrip");
        for(int tick=13;tick<=20;tick++){s=Tick(s);}
        Require(s.Heads.Length==0&&s.Mats.Length==6,"Head lifetime or unexpected terminal field");
        for(int tick=21;tick<=137;tick++){s=Tick(s);}
        Require(s.Mats.Length==0,"Finite fields outlived current range");
        s=new([], [new(1,1,14000,4000,125,111)],1);
        s=Tick(s,victim:111);Require(s.Contacts==0&&s.Mats[0].LastCollision==111,"Age15must remember early contact without damage");
        var active=Tick(s,victim:222);Require(active.Contacts==1&&active.Mats[0].Remaining==109,"Age16did not open the contact window");
        s=Tick(s,victim:111);Require(s.Contacts==0,"Same most-recent collider repeated");
        s=Tick(s,victim:222);s=Tick(s,victim:111);
        Require(s.Contacts==2&&s.LastVictim==111&&s.Mats[0].LastCollision==111,"Alternating victim was treated as a permanent hit set");
        s=Tick(s,victim:333,hostile:false);Require(s.Contacts==2&&s.Mats[0].LastCollision==333,"Refused hostile check lost collider update");
        var inactive=DruidCreeperFields.Inactive(launched,2);
        Require(inactive.Heads.All(h=>h.Remaining==18&&h.X==7000&&h.Y==2000)&&inactive.Mats.Length==0,"Inactive head moved or emitted");
        Require(DruidCreeperFields.Inactive(s,long.MaxValue).Mats.Length==0,"Long inactive lifetime did not expire");
        var slow=DruidCreeperFields.Launch(DruidCreeperFields.Empty(),1,DruidCreeperVines.Heads(1,14000,4000),33);
        Require(slow.Heads.All(h=>h.Velocity256==443),"Source slow-speed integer order");
        var obstructed=launched;
        for(int i=0;i<10&&obstructed.Heads.Length!=0;i++){obstructed=Tick(obstructed,blocked:true);}
        Require(obstructed.Heads.Length==0,"Declared terrain service did not remove heads at the first crossed cell");
        string valid=JsonSerializer.Serialize(roundtrip);bool refused=false;
        try{DruidCreeperFields.Validate(roundtrip with{Mats=[roundtrip.Mats[0] with{Total=126}]});}catch(InvalidDataException){refused=true;}
        Require(refused&&JsonSerializer.Serialize(roundtrip)==valid,"Malformed field state changed valid snapshot");
        Log.Information("Creeper fields: current rank lifetimes125/600/625/6475, cardinal aim seed,20frame motion, remaining18/9emission, finite mats, age15/16gate, recent-collider history, inactive expiry, slow443, copying and JSON restoration pass; contacts are capture-only");
    }
}
