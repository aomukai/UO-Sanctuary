// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class PhoenixPathChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PhoenixPathChecks));
    public static void Configure(){Register("phoenix-path-check");Register("phoenix-path-reload-check");}
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Phoenix source path/curve/packet kernels; no learned handler",_=>Core.LoopContext.Post(()=>{try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static JsonDocument Fixture(string name)=>JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,name+"-source-probe.json")));
    private static bool Points(Point2D[] points,JsonElement expected)=>points.SequenceEqual(expected.EnumerateArray().Select(x=>new Point2D(x[0].GetInt32(),x[1].GetInt32())));
    private static void Check()
    {
        using var paths=Fixture("phoenix-chain-init");int count=0;
        foreach(var r in paths.RootElement.EnumerateArray())
        {int V(int i)=>r[i].GetInt32();var path=MartialPhoenixPaths.Chain(V(2),V(1),V(0),V(3),V(3));Require(path.Frames==V(4) && path.Frames==V(8) && (uint)path.Seed==r[9].GetUInt32() && (uint)(path.Seed>>32)==r[10].GetUInt32() && Points(path.Points,r[11]),"Complete chain initializer/path/seed");count++;}
        Require(count==4096,"Chain source matrix");
        using var direction=Fixture("phoenix-direction");count=0;
        foreach(var r in direction.RootElement.EnumerateArray())
        {int V(int i)=>r[i].GetInt32();int angle=MartialPhoenixPaths.Direction(V(0),V(1));Require(angle==V(2) && MissileVectors.Direction(V(0),V(1))==new Point2D(V(3),V(4)),"Complete source angle/vector lookup");var path=MartialPhoenixPaths.Chain(7,angle,60,1024,1024);Require(path.Frames==V(5) && (uint)path.Seed==r[6].GetUInt32() && (uint)(path.Seed>>32)==r[7].GetUInt32() && Points(path.Points,r[8]),"Complete composed source direction/path");count++;}
        Require(count==4225,"Direction source matrix");
        using var curves=Fixture("phoenix-chaos");count=0;
        foreach(var r in curves.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();bool active=V(5)==1 && MartialPhoenixPaths.Turn(V(0),V(1));Require(V(8)==(active?1:0),"Source elapsed-frame curve gate");
            if(active){int packed=unchecked((ushort)V(2)+((int)(ushort)V(3)<<16));var curve=MartialPhoenixPaths.Curve(V(4),packed);Require(curve.X==V(9)-40 && curve.Y==V(10)-70 && unchecked((int)curve.TargetSeed)==V(11) && curve.Packed==V(12) && (uint)curve.Seed==r[13].GetUInt32() && (uint)(curve.Seed>>32)==r[14].GetUInt32(),"Complete do35 curve/LCG/signed packing");}count++;
        }
        Require(count==5400 && MartialPhoenixPaths.Turn(0,3) && !MartialPhoenixPaths.Turn(1,3) && MartialPhoenixPaths.Turn(3,3),"Curve source matrix/first update");
        using var packets=Fixture("phoenix-damage");string[] names=["royalstrikemeteor","royalstrikechainlightning","royalstrikechaosice","royalstrikemeteorfire"];count=0;
        foreach(var r in packets.RootElement.EnumerateArray())
        {int V(int i)=>r[i].GetInt32();var values=new SkillValues(SkillMechanics.For(CombatAbility.PhoenixStrike),k=>k=="Royal Strike"?V(1):0,_=>V(2),99);var packet=MartialPhoenixPackets.Capture(names[V(0)],values,V(3));Require(packet.Minimum256==V(5) && packet.Maximum256==V(6) && packet.ColdFrames==V(7) && V(8)==0 && V(9)==0 && V(10)==V(5) && V(11)==0 && V(12)==0 && V(13)==0 && V(14)==0,"Complete source missile creator/endpoints/length/mastery/stat writer");count++;}
        Require(count==1152,"Packet source matrix");
        using var meteors=Fixture("phoenix-meteor");count=0;
        foreach(var r in meteors.RootElement.EnumerateArray())
        {int V(int i)=>r[i].GetInt32();if(V(0)!=1 || V(1)<=0 || V(2)!=1 || V(3)!=0 || V(4)!=1){continue;}var calls=r[12].EnumerateArray().ToArray();Require(calls.Length==19 && V(9)==6 && V(10)==0x8583 && V(11)==0,"Complete source meteor area/field count/filter");var offsets=MartialPhoenixPackets.MeteorOffsets();for(int i=0;i<offsets.Length;i++){var call=calls[i+1];Require(call[1].GetInt32()==40+offsets[i].X && call[2].GetInt32()==70+offsets[i].Y && call[3].GetInt32()==0x8001 && call[4].GetInt32()==MartialPhoenixPackets.MeteorFieldFrames(V(1)),"Complete source18field placement/rank lifetime");}count++;}
        Require(count==5,"Meteor source rank controls");
        bool refused=false;try{MartialPhoenixPaths.Direction(259,0);}catch(NotSupportedException){refused=true;}Require(refused,"Unverified source FP16 overflow domain accepted");
        Log.Information("Phoenix prepared kernels:4096 full chain paths/seeds,4225 full angle/vector/path compositions,5400 full elapsed curve/packing controls,1152 full packet cases and5 meteor rank layouts pass; learned action/input verified separately");
    }
}
