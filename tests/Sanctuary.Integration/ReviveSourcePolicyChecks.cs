// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class ReviveSourcePolicyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveSourcePolicyChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private sealed class World(int[] p):ReviveSourceServices
    {
        internal readonly List<int[]> Trace=new();
        internal int Mode=12,Maximum=25600,Life,Level=p[10],X=100,Y=200;
        internal bool Right=p[14]!=0;
        internal uint Flags;
        internal ulong Seed=SourceRandom.InitLow(p[13]);
        private void T(int op,int unit,int a=0,int b=0,int c=0,int d=0)=>Trace.Add([op,unit,a,b,c,d]);
        internal override ReviveSourceSkill Skill(int id){T(0,id,p[0]);return p[0]!=0?new((byte)p[17],p[18]):null;}
        internal override int Target(){int h=p[1]<0?0:2;T(1,h);return h;}
        internal override ReviveSourceUnit Unit(int h)=>h switch
        {
            1=>new(p[7],p[8],101,p[9],25600,7,100,200,11,2,false,19),
            2=>new(p[1],12,202,Level,Maximum,7,X,Y,11,2,Right,9),
            _=>throw new NotSupportedException("No bound Revive unit")
        };
        internal override bool? Species(int cls){Require(cls==12,"Wrong source species");T(2,cls,p[2]);return p[2]<0?null:p[2]!=0;}
        internal override bool Dead(int target){T(3,target,p[3]);return p[3]!=0;}
        internal override bool Consumable(int target){T(4,target,p[4]);return p[4]!=0;}
        internal override int RevivedAi(int target){T(5,target,7,p[5]);return p[5];}
        internal override int FreeCoordinates(int target,SourceGridPoint point){T(32,target,7,2,0x3c01,1);point.X+=3;point.Y+=5;return p[6]!=0?3:0;}
        internal override (int Minimum,int Maximum) BaseLife(int target){T(28,target,12,Level,p[11],p[12]);return(p[11],p[12]);}
        internal override int Roll(int target,int bound){int result=(int)SourceRandom.Limited(ref Seed,bound);T(29,target,bound,result);return result;}
        internal override int PetMaximum(int caster,int skill,int rank){T(30,caster,skill,rank,p[19]);return p[19];}
        internal override int Duration(int caster,int skill,int rank){T(31,caster,skill,rank,p[20]);return p[20];}
        internal override int Frame=>p[21];
        internal override void MarkRevived(int target){Require(target==2,"Wrong revived original");Flags|=0x80000000;}
        internal override void Act(ReviveSourceAction a)
        {
            T(a.Kind,a.Unit,a.A,a.B,a.C,a.D);
            switch(a.Kind)
            {
                case ReviveSourcePolicy.Flag:Flags|=(uint)a.A;break;
                case ReviveSourcePolicy.Neutral:if(p[22]!=0){Mode=a.A;}break;
                case ReviveSourcePolicy.RightAttack:Right=false;break;
                case ReviveSourcePolicy.Move:X=a.B;Y=a.C;break;
                case ReviveSourcePolicy.SetStat:
                    if(a.A==7){Maximum=a.B;}else if(a.A==6){Life=a.B;}else{Require(a.A==12,"Unexpected stat");Level=a.B;}break;
                case ReviveSourcePolicy.Passives:
                    if(p[23]!=0){Maximum+=SourcePercentage.Calculate(Maximum,p[24],100);Life=Maximum;}break;
            }
        }
        internal string Output(int result)=>JsonSerializer.Serialize(new object[]{result,new[]{Mode,Maximum,Life,Level,X,Y,Right?1:0},Flags,Seed,Trace});
    }
    private static int[] Values(JsonElement raw)=>raw.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static void Register(string name)=>ConsoleInputHandler.RegisterCommand(name,"Complete older Revive sequence and approved expiry omission",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure(){Register("revive-source-policy-check");Register("revive-source-policy-reload-check");}
    private static void Compare(World world,int result,JsonElement expected,string why)=>Require(world.Output(result)==expected.GetRawText(),why+":"+world.Output(result));
    private static string WithoutExpiry(JsonElement source)
    {
        var rows=source[4].EnumerateArray().Select(Values).Where(r=>r[0]!=24&&!(r[0]==20&&r[2]==7)).ToArray();
        return JsonSerializer.Serialize(new object[]{source[0].GetInt32(),Values(source[1]),source[2].GetUInt32(),source[3].GetUInt64(),rows});
    }
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"revive-source-policy-source-probe.json")));
        int casts=0,owners=0,approved=0,expiryOmissions=0,noRoom=0;
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int[] p=Values(entry[0]);var world=new World(p);Compare(world,ReviveSourcePolicy.Start(world)?1:0,entry[1],"Revive start differs:"+casts);
            world=new(p);Compare(world,ReviveSourcePolicy.ReferenceRelease(1,p[15],p[16],world)?1:0,entry[2],"Revive release differs:"+casts);
            if(p[15]==95)
            {
                world=new(p);int result=ReviveSourcePolicy.Release(1,p[15],p[16],world)?1:0;
                Require(world.Output(result)==WithoutExpiry(entry[2]),"Approved lifetime changed another effect:"+casts+":"+world.Output(result));
                Require(!world.Trace.Any(r=>r[0]==24||r[0]==20&&r[2]==7),"Approved route scheduled source lifetime expiry");
                approved++;if(entry[2][4].EnumerateArray().Any(r=>r[0].GetInt32()==24)){expiryOmissions++;}
                if(result!=0&&p[6]==0){Require(world.Mode==12&&world.Flags==0x80000000,"No-room branch was silently repaired");noRoom++;}
            }
            casts++;
        }
        foreach(var entry in doc.RootElement.GetProperty("owners").EnumerateArray())
        {
            int[] p=Values(entry[0]);var world=new World(p);ReviveSourcePolicy.ReferenceOwner(entry[1].GetInt32(),entry[2].GetInt32()!=0?2:0,p[15],p[16],world);
            Compare(world,1,entry[3],"Revive owner conversion differs:"+owners);owners++;
        }
        Require(casts==8930&&owners==64&&approved==8929&&expiryOmissions>0&&noRoom>0,"Incomplete Revive policy matrix:"+casts+"/"+owners+"/"+approved);
        Log.Information("Revive source policy:8930complete older cast/start contexts,64owner contexts and8929approved no-expiry comparisons pass;in-place target,HP/level/seed,ordered services,failed placement and ignored mode/passive failures retained;live actor/stat/lifecycle acceptance is recorded separately");
    }
}
