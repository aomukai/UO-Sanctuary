// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;

public static class BladeFuryChannelChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeFuryChannelChecks));
    public static void Configure(){Register("blade-fury-channel-check");Register("blade-fury-channel-reload-check");}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Source-compared st26/do48 component; no learned Blade Fury or Inferno input",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-fury-channel-source-probe.json")));int cases=0;
        foreach(var row in doc.RootElement.GetProperty("channel").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();bool B(int i)=>N(i)!=0;
            int mana=N(5),emits=0,made=0,calls=0,paid=0,rewinds=0,allocs=0;
            var services=new BladeFuryChannelServices(()=>{allocs++;return B(6);},()=>B(7),()=>{emits++;if(B(8)){made++;}return B(8);},
                ()=>{calls++;if(mana>=320){mana-=320;paid++;}},()=>rewinds++);
            var state=new BladeFuryChannelState(B(3),B(3)?1020:0,N(4));
            var step=B(10)?BladeFuryChannel.Do(state,1000,B(0),B(1),B(2)?506:-1,N(9),services)
                :BladeFuryChannel.Start(state,1000,B(0),B(1),B(2)?506:-1,mana,1536,N(9),services);
            Require(step.Success==B(11)&&step.State.NextShot==N(12)&&step.State.Active==B(14)
                &&(!step.State.Active||step.State.Expires==N(13))&&step.State.Flags==row[22].GetUInt32(),"Full st26/do48 source state differential "+cases);
            Require(emits==N(15)&&made==N(16)&&calls==N(17)&&paid==N(18)&&mana==N(19)&&rewinds==N(20)&&allocs==N(21),"Source service ordering/payment/attempt differential "+cases);
            var saved=JsonSerializer.Deserialize<BladeFuryChannelState>(JsonSerializer.Serialize(step.State));
            Require(saved==step.State,"Component JSON round-trip changed source channel state");cases++;
        }
        Require(cases==6912,"Channel case inventory");int expiry=0;
        foreach(var row in doc.RootElement.GetProperty("expiry").EnumerateArray())
        {
            var state=new BladeFuryChannelState(row[0].GetInt32()!=0,row[0].GetInt32()!=0?1020:0,1004,row[1].GetUInt32());
            var result=BladeFuryChannel.Expire(state);
            Require(!result.Active&&result.Expires==0&&result.Flags==row[4].GetUInt32()&&result.NextShot==1004,"Source expiry callback reset unrelated deadline/flags");expiry++;
        }
        Require(expiry==12&&!AssassinCombat.Implemented(CombatAbility.BladeFury),"Expiry inventory or unfinished channel exposed learned input");
        Log.Information("Blade Fury channel: {Count} complete source start/do and {Expiry} expiry cases match; component round-trips,21/7 leases, strict equality, failed allocation attempts and explicit resource/rewind services; production input/animation/missile persistence remain separate",cases,expiry);
    }
}
