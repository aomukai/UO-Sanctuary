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
    private static void SourceFrames()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"creeper-frame-source-probe.json")));
        var vectors=source.RootElement.EnumerateArray().ToDictionary(row=>(row[0][0].GetInt32(),row[0][1].GetInt32()),row=>row[1][6].GetInt32()==2);
        int compared=0,steps=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {
            int total=row[0][0].GetInt32(),age=row[0][1].GetInt32();var expected=row[1];
            Require(DruidCreeperVines.EmitsTrail(age)==(expected[3].GetInt32()!=0),"Complete getter/do26elapsed-age comparison");
            var hit=DruidCreeperVines.Contact(total,age,0,42,true,true,false);
            Require(hit.Damage==(expected[6].GetInt32()==2)&&!hit.Remove&&hit.LastCollision==42,"Complete getter/hit50elapsed-age comparison");compared++;
        }
        foreach(var (rank,total) in new[]{(1,125),(3,175),(20,600),(255,6475)})
        {
            var fields=new CreeperFields([], [new(1,rank,14000,4000,total,total)],1);
            for(int age=1;age<=total;age++)
            {
                int before=fields.Contacts;fields=Tick(fields,victim:(uint)(1000+age));steps++;
                Require(fields.Contacts-before==(vectors[(total,age)]?1:0),"Actual field countdown-to-age composition disagrees with complete source");
                Require(age==total?fields.Mats.Length==0:fields.Mats.Single().Remaining==total-age,"Mat expiry/countdown lost during contact");
            }
        }
        Require(compared==7400&&steps==7375,"Incomplete elapsed-frame differential");
        Log.Information("Creeper frame composition:7400complete getter/callback vectors and7375actual mat steps pass; all four lifetime windows include early damage and final15frame exclusion");
    }
    private static void Legacy(CreeperFields launched)
    {
        const string old="charged-motion-current-range-contact-capture-v1",current="charged-motion-elapsed-age-contact-v2";
        var legacy=launched with{Profile=old,Heads=launched.Heads.Select(h=>h with{Remaining=12}).ToArray(),
            Mats=[new(4,1,14000,4000,125,100,42){Poison=new(new(32,48,100),100,123)}],NextId=4,Contacts=1,LastVictim=42};
        var restored=JsonSerializer.Deserialize<CreeperFields>(JsonSerializer.Serialize(legacy));DruidCreeperFields.Validate(restored);
        var migrated=DruidCreeperFields.Copy(restored);
        Require(migrated.Profile==current&&JsonSerializer.Serialize(migrated with{Profile=old})==JsonSerializer.Serialize(legacy),
            "Legacy field migration changed position,countdown,history,packet,seed or counters");
        var node=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(legacy)).AsObject();node.Remove("Profile");
        var missing=JsonSerializer.Deserialize<CreeperFields>(node.ToJsonString());DruidCreeperFields.Validate(missing);
        Require(missing.Profile==current&&JsonSerializer.Serialize(missing)==JsonSerializer.Serialize(migrated),"Missing-profile save did not preserve field state");
        var next=Tick(migrated);Require(next.Mats.Length==1&&next.Heads.All(h=>h.Remaining==11),"Migrated age8replayed an old emission");
        next=Tick(next);Require(next.Mats.Length==4&&next.Heads.All(h=>h.Remaining==10),"Migrated age9did not resume one future emission");
        var inactive=DruidCreeperFields.Inactive(restored,1);
        Require(inactive.Profile==current&&inactive.Mats.Length==1&&inactive.Mats[0].Remaining==99&&inactive.Heads.All(h=>h.Remaining==11)
            &&inactive.Contacts==1&&inactive.LastVictim==42,"Inactive migration replayed damage/movement/emission");
    }
    private static void Check()
    {
        SourceFrames();
        foreach(var (rank,frames) in new[]{(1,125),(20,600),(21,625),(255,6475)})
        {Require(DruidCreeperFields.Lifetime(rank)==frames,"Current expression lifetime lost rank");}
        var launched=DruidCreeperFields.Launch(DruidCreeperFields.Empty(),1,DruidCreeperVines.Heads(1,14000,4000),z:100);
        Require(launched.Heads.Length==3 && launched.Heads.All(h=>h.Velocity256==1344&&h.Remaining==20&&h.X==7000&&h.Y==2000),"Flags3origin or velocity route");
        var first=launched.Heads[0];var third=launched.Heads[2];
        Require(first.Path.SequenceEqual(ChargedBolts.Path(14000,2,20)) && third.Path.SequenceEqual(ChargedBolts.Path(14003,0,20)),"Aim-X/ordinal initializer or cardinal direction wrong");
        Legacy(launched);
        var before=launched.Heads.ToDictionary(h=>h.Id);var s=Tick(launched);
        Require(s.Mats.Length==3 && s.Mats.All(m=>m.Total==125&&m.Remaining==125&&m.Z==100)
            && s.Mats.Select(m=>(m.X,m.Y)).Order().SequenceEqual(before.Values.Select(h=>((int)Math.Floor(h.X*2+.5),(int)Math.Floor(h.Y*2+.5))).Order()),"do26did not emit before movement/decrement");
        for(int tick=2;tick<=9;tick++){s=Tick(s);Require(s.Mats.Length==3,"Head emitted between elapsed0and9");}
        s=Tick(s);Require(s.Mats.Length==6&&s.Mats.Count(m=>m.Remaining==125)==3,"Second emission at elapsed9missing");
        for(int tick=11;tick<=12;tick++){s=Tick(s);}
        Require(s.Heads.All(h=>h.Remaining==8),"Head countdown did not remain separate from elapsed phase");
        var clone=DruidCreeperFields.Copy(s);var original=s.Heads[0].Path[1];clone.Heads[0].Path[1]=new(99,99);
        Require(s.Heads[0].Path[1]==original,"Field path alias escaped");
        var roundtrip=JsonSerializer.Deserialize<CreeperFields>(JsonSerializer.Serialize(s));DruidCreeperFields.Validate(roundtrip);
        Require(JsonSerializer.Serialize(roundtrip)==JsonSerializer.Serialize(s),"Field path/lifetime/history roundtrip");
        for(int tick=13;tick<=20;tick++){s=Tick(s);}
        Require(s.Heads.Length==0&&s.Mats.Length==9,"Head lifetime or elapsed18emission");
        for(int tick=21;tick<=144;tick++){s=Tick(s);}
        Require(s.Mats.Length==0,"Finite fields outlived current range");
        s=new([], [new(1,1,14000,4000,125,125)],1);
        s=Tick(s,victim:111);Require(s.Contacts==1&&s.Mats[0].LastCollision==111,"Initial contact did not damage");
        s=Tick(s,victim:111);Require(s.Contacts==1,"Same most-recent collider repeated");
        s=Tick(s,victim:222);s=Tick(s,victim:111);
        Require(s.Contacts==3&&s.LastVictim==111&&s.Mats[0].LastCollision==111,"Alternating victim was treated as a permanent hit set");
        s=Tick(s,victim:333,hostile:false);Require(s.Contacts==3&&s.Mats[0].LastCollision==333,"Refused hostile check lost collider update");
        var tail=new CreeperFields([], [new(1,1,14000,4000,125,17)],1);
        tail=Tick(tail,victim:111);Require(tail.Contacts==1&&tail.Mats[0].Remaining==16,"Age109should still damage");
        tail=Tick(tail,victim:222);Require(tail.Contacts==1&&tail.Mats[0].Remaining==15&&tail.Mats[0].LastCollision==222,"Final15frame contact should only update history");
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
        Log.Information("Creeper fields: current rank lifetimes125/600/625/6475, cardinal aim seed,20frame motion, elapsed0/9/18emission, finite mats, early contact/final15frame exclusion, recent-collider history, inactive expiry, slow443 and legacy/missing-profile JSON migration pass; poison delivery is checked separately");
    }
}
