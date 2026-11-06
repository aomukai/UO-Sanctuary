// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class DoubleSwingSequenceChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DoubleSwingSequenceChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"double-swing-sequence-cases.json")));
        string[] codes=["HTH","1HT","2HT","1HS","2HS","BOW","XBW","STF","1JS","1JT","1SS","1ST","HT1","HT2"];
        int[] poses=[1,2,4,6,7,9,11,13,14,2,4,5,6,8,9,10,11];int count=0;
        foreach(string code in codes)
        {
            var profile=PlayerWeaponSequences.Profile(11,code);
            Require(profile.Sequence==11&&profile.Points==17&&profile.Frames==17&&profile.RawAuxiliary==256,"Current SQ11 descriptor differs");
            for(int i=0;i<17;i++)Require(profile.Rows[i].SequenceEqual(new[]{0,i<9?7:15,poses[i],0,i is 4 or 9?1:0}),"Current SQ11 pose/action row differs");
            profile.Rows[4][4]=0;profile.Rows[9][1]=0;
            Require(PlayerWeaponSequences.Profile(11,code).Rows[4][4]==1&&PlayerWeaponSequences.Profile(11,code).Rows[9][1]==15,"Caller mutated shared SQ11 profile");
            foreach(var row in source.RootElement.GetProperty("cases").EnumerateArray())
            {
                var args=row[0];int advance=args[0].GetInt32(),entry=args[1].GetInt32(),fraction=args[2].GetInt32();
                var plan=PlayerWeaponSequences.Schedule(11,code,advance,0,entry);
                var expected=row[1].EnumerateArray().Select(e=>new SourceAnimationEvent(e[0].GetInt32(),e[1].GetInt32(),e[2].GetInt32(),e[3].GetInt32())).ToArray();
                Require(plan.Events.SequenceEqual(expected),"Complete scheduler SQ11 trace differs: "+code+"/"+count);
                var actions=plan.Events.Where(e=>e.Type==SourceAnimationEvents.ModeChange).ToArray();
                Require(actions.Select(e=>e.Param).SequenceEqual(row[2].EnumerateArray().Select(v=>v.GetInt32()))
                    &&row[3].EnumerateArray().All(v=>v.GetInt32()==fraction)
                    &&row[4].GetInt32()==(actions.Length>0?(actions[^1].Param<<8)|fraction:fraction),"Callback ordinal/fraction contract differs");
                Require(actions.Select(e=>WeaponHands.SecondaryForSequence(e.Param,true)).SequenceEqual(actions.Select(e=>e.Param==1)),"Shared damage-hand parity does not follow callback ordinal");
                if(advance==256)Require(plan.Events.SequenceEqual(new[]{new SourceAnimationEvent(1,entry+4,1,0),new SourceAnimationEvent(1,entry+9,1,1),new SourceAnimationEvent(2,entry+17,0)}),"Supplied-rate event points differ");
                count++;
            }
        }
        foreach(int sequence in new[]{21,24,25})foreach(string code in codes)
        {
            var retained=PlayerWeaponSequences.Profile(sequence,code);Require(retained.Sequence==sequence&&retained.Rows.Length==retained.Points,"Shared loader lost an existing profile");
            _=PlayerWeaponSequences.Schedule(sequence,code,256,0,100);
        }
        Require(count==1890&&BarbarianCombat.Implemented(CombatAbility.DoubleSwing),"Incomplete SQ11 matrix or approved startup missing");
        bool refused=false;try{_=PlayerWeaponSequences.Profile(11,"???");}catch(NotSupportedException){refused=true;}Require(refused,"Unknown weapon class accepted");
        File.WriteAllText(Path.Join(Core.BaseDirectory,"double-swing-sequence-fixtures.json"),JsonSerializer.Serialize(new{Profiles=14,Comparisons=count,LearnedCast=false}));
        Log.Information("Double Swing SQ11:14current profiles/1890source scheduler and callback comparisons;17pose rows/action indices4,9;ordinals0,1/fraction retained/shared hand parity;no startup68/payment/real-rate claim");
    }
    public static void Configure()
    {
        foreach(string name in new[]{"double-swing-sequence-check","double-swing-sequence-reload-check"})ConsoleInputHandler.RegisterCommand(name,"Current Double Swing sequence through shared scheduler",_=>Core.LoopContext.Post(()=>
        {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    }
}
