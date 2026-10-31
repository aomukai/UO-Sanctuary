// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class BlindPolicyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BlindPolicyChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("blind-policy-check","Blind AI source policy, not world controller acceptance",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("blind-policy-check: passed");}catch(Exception e){Log.Error(e,"blind-policy-check: failed");}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blind-policy-source-probe.json")));
        int tick=0,untargeted=0;
        foreach(var row in source.RootElement.GetProperty("tick").EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();int percentages=0;
            var request=BlindAiPolicy.Decide(true,V(0)!=0,V(1)>=0,V(2)!=0,V(4)!=0,V(3)!=0,V(5)!=0,true,false,false,0,()=>{percentages++;return V(6);});
            var expected=V(12)!=0?BlindAiAction.Attack:V(15)!=0?BlindAiAction.WalkNear:BlindAiAction.Idle;
            Require(request.Action==expected&&request.Value==(expected==BlindAiAction.WalkNear?3:expected==BlindAiAction.Idle?10:0)&&percentages==V(14),"Complete blind tick request/draw differential at "+tick);
            int raw=0,limited=0;uint[] values=[(uint)V(7),(uint)V(9),(uint)V(10)];
            if(request.Action==BlindAiAction.WalkNear)
            {
                var offset=BlindAiPolicy.Offset(request.Value,()=>values[raw++],bound=>{limited++;Require(bound==V(23),"Wander RNG bound");return V(8);});
                Require(offset.X==V(16)-100&&offset.Y==V(17)-200&&V(18)==2&&V(19)==1&&V(20)==0,"Complete walk-near displacement/entry metadata");
            }
            Require(raw==V(21)&&limited==V(22),"Walk-near RNG chronology");tick++;
        }
        foreach(var row in source.RootElement.GetProperty("targetWrapper").EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();if(V(0)!=0){continue;}
            var request=BlindAiPolicy.Decide(false,false,true,true,false,V(3)!=0,false,V(4)!=0,V(1)!=0,V(2)!=0,V(5),()=>throw new InvalidOperationException("No-target path called percentage service"));
            Require(V(6)==0&&request.Action==(V(8)!=0?BlindAiAction.WalkNear:BlindAiAction.Idle)&&request.Value==(V(8)!=0?V(8):V(7)),"Mode1 no-target wrapper differential at "+untargeted);untargeted++;
        }
        Require((tick,untargeted)==(12288,112),"Incomplete blind source policy cases");
        Log.Information("Blind policy:12288complete tick/tactics and112no-target wrapper comparisons pass;all draw counts/axis/signs/bounds,source idle and no artificial move-failure fallback match. Target acquisition,live controller,clock,mode/path/save integration remain unfinished.");
    }
}
