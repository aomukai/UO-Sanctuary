// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class PetMovementChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PetMovementChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-movement-check","Complete source common/six-mode pet movement and Spirit Wolf policy",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("pet-movement-check: passed");}catch(Exception e){Log.Error(e,"pet-movement-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Movement()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-movement-source-probe.json")));
        int direct=0,common=0,queries=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            var input=row[0];int N(int i)=>input[i].GetInt32();uint random=input[15].GetUInt32();
            var trail=Enumerable.Range(0,20).Select(i=>new Point2D(N(11)==0?0:N(11)==2&&i%2==0?0:100+2*i,
                N(11)==0?0:N(11)==2&&i%2!=0?0:100-i)).ToArray();
            var context=new PetMoveContext(new(100,100),new(200,220),new(210+((N(8)&1)!=0?1:0),230+((N(8)&2)!=0?1:0)),
                new(210,230),N(5),N(6),N(7),N(9),N(10),trail,new(100+N(12),100),N(19)!=0);
            var actual=new List<PetMoveRequest>();int attempts=0,flags=0;
            int Action(PetMoveRequest request)
            {
                if(request.Kind==PetMoveKind.MarkTeleported){flags|=1;return 0;}
                actual.Add(request);int kind=(int)request.Kind;
                if(kind==12){return N(17);}if(kind==9){return (N(18)&4)!=0?1:0;}
                if(kind is 1 or 2 or 3 or 4 or 5 or 13 or 14){attempts++;return N(14)>0&&attempts==N(14)?1:0;}
                return 0;
            }
            int Room(Point2D p)
            {
                int value=N(13)==0?1:N(13)==1?(p.Y>=220?1:2):(p==new Point2D(200,220)?1:0);
                actual.Add(new((PetMoveKind)15,p.X,p.Y,value));return value;
            }
            int Distance(Point2D p)
            {int value=Math.Abs(p.X-100)+Math.Abs(p.Y-100);actual.Add(new((PetMoveKind)16,p.X,p.Y,value));return value;}
            Point2D Direction(int d)
            {
                Point2D[] offsets=[new(0,1),new(-1,1),new(-1,0),new(-1,-1),new(0,-1),new(1,-1),new(1,0),new(1,1)];
                var value=offsets[d];actual.Add(new((PetMoveKind)17,d,value.X,value.Y));return value;
            }
            uint Random(){actual.Add(new((PetMoveKind)18,(int)(random>>16),(int)(random&65535)));return random;}
            int Percentage(){actual.Add(new((PetMoveKind)19,N(16)));return N(16);}
            Point2D? Find(){actual.Add(new((PetMoveKind)20));return (N(18)&1)!=0?new Point2D(501,502):null;}
            bool TeleportRoom(Point2D p){actual.Add(new((PetMoveKind)21,p.X,p.Y));return (N(18)&2)!=0;}
            var services=new PetMoveServices(Action,Room,Distance,Direction,Random,Percentage,Find,TeleportRoom);
            bool result=N(0)==0?SourcePetMovement.Move(context,services,N(1),N(2)!=0,N(3),N(4))
                :SourcePetMovement.Common(context,services,N(20)!=0,N(21)!=0,N(22)!=0,N(23),N(4));
            var expected=row[4].EnumerateArray().Select(x=>new PetMoveRequest((PetMoveKind)x[0].GetInt32(),x[1].GetInt32(),x[2].GetInt32(),x[3].GetInt32(),x[4].GetInt32())).ToArray();
            Require(result==(row[1].GetInt32()!=0) && attempts==row[2].GetInt32() && flags==row[3].GetInt32()
                && actual.SequenceEqual(expected),$"Complete pet movement context{direct+common}: "+input+" actual="+JsonSerializer.Serialize(actual)+" expected="+row[4]);
            if(N(0)==0){direct++;}else{common++;}queries+=actual.Count;
        }
        Require(direct==8312 && common==39936,"Incomplete shared pet movement matrix");
        Log.Information("Common pet movement:8312complete direct and39936complete common contexts, {Queries}ordered service requests match; actual actor services pending",queries);
    }
    private static void Spirit(bool corrected=false)
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,corrected?"spirit-wolf-corrected-ai-source-probe.json":"spirit-wolf-ai-source-probe.json")));int count=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            var input=row[1];int N(int i)=>input[i].GetInt32();var actual=new List<SpiritWolfAiRequest>();int rolls=0;
            var context=new SpiritWolfAiContext(N(0)!=0,N(1),N(2)!=0,17,40,70,N(3)!=0,N(4)!=0?10:0,N(5)!=0?20:0,
                N(6)!=0,N(7)!=0,N(8),N(9),N(10),N(14)!=0,N(15)!=0,
                OwnerTargetDistance:corrected?N(16):0,Velocity:corrected?N(17):5,Run:corrected?N(18):10);
            bool Common(int target,bool combat,int flag,int leash){actual.Add(new(1,target,combat?1:0,flag,leash));return N(12)!=0;}
            bool Move(int mode,bool run,int velocity,int leash){actual.Add(new(5,mode,run?1:0,velocity,leash));return N(11)!=0;}
            int Percentage(){rolls++;return N(13);}
            var services=new SpiritWolfAiServices(actual.Add,Common,Move,Percentage);
            int remembered=corrected?DruidSpiritWolfAi.CorrectedThink(context,services):DruidSpiritWolfAi.LegacyThink(context,services);
            var expected=row[4].EnumerateArray().Select(x=>new SpiritWolfAiRequest(x[0].GetInt32(),x[1].GetInt32(),x[2].GetInt32(),x[3].GetInt32(),x[4].GetInt32())).ToArray();
            Require(remembered==row[2].GetInt32() && rolls==row[3].GetInt32() && actual.SequenceEqual(expected),"Complete Spirit Wolf AI context"+count+": "+input);count++;
        }
        Require(count==(corrected?6366:1802),"Incomplete individual Spirit Wolf AI matrix");
        if(corrected){Log.Information("Spirit Wolf corrected policy:6366complete source contexts/ordered requests match; source-backed combat/chase, owner-target leash and velocity boundaries retained; actual actor execution pending");}
        else{Log.Information("Spirit Wolf legacy policy:1802complete source contexts/ordered requests match; literal fallback visibility, duplicate failed follow and delayed teleport requests retained; suspect nonnull-monstats attack/chase branch is not approved live AI");}
    }
    private static void Check()
    {Require(DruidCombat.Implemented(CombatAbility.SummonSpiritWolf),"Learned wolf route missing");Movement();Spirit();Spirit(true);}
}
