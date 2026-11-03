// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class FrenzyReleaseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FrenzyReleaseChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("frenzy-release-check","Complete legacy Frenzy release/charge/latch comparisons",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("frenzy-release-check: passed");}catch(Exception e){Log.Error(e,"frenzy-release-check: failed");}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private sealed class Services:FrenzyReleaseServices
    {
        private readonly JsonElement P;
        internal readonly List<object[]> Trace=[];
        internal SourceGuidTarget CurrentTarget;
        internal readonly FrenzyUsedSkill First,Replacement;
        internal FrenzyUsedSkill CurrentUsed;
        internal FrenzyReleaseList CurrentList;
        internal object[] Packet=[];
        private int Drains;
        private int I(string n)=>P.GetProperty(n).GetInt32();
        private uint U(string n)=>P.GetProperty(n).GetUInt32();
        private bool B(string n)=>P.GetProperty(n).GetBoolean();
        private void A(params object[] values)=>Trace.Add(values);
        internal Services(JsonElement p)
        {
            P=p;CurrentTarget=B("present")?new(1,U("anchor")):null;
            First=new(I("param"));Replacement=new(I("replacementParam"));CurrentUsed=B("used")?First:null;
            CurrentList=B("old")?new(I("count"),I("expiry")){State=I("state"),RemoveCallback=true,Posted=true,Velocity=17,AttackRate=23,OtherAnimationRate=23}:null;
        }
        internal int UsedToken=>CurrentUsed==null?0:CurrentUsed==First?1:2;
        internal override FrenzyReleaseRow Row(int skill)
        {A(8,skill,B("row")?1:0);return B("row")?new(checked((short)I("state")),checked((ushort)I("extra")),U("hitFlags"),U("hitClass"),checked((byte)I("element")),checked((byte)I("source"))):null;}
        internal override SourceGuidTarget Target(){A(1,CurrentTarget?.Handle??0);return CurrentTarget;}
        internal override FrenzyUsedSkill UsedSkill(){A(7,UsedToken,CurrentUsed?.Param1??0);return CurrentUsed;}
        internal override int StateCount=>5;
        internal override int GameFrame=>unchecked((int)U("gameFrame"));
        internal override int UnitKind=>0;
        internal override int UnitId=>999;
        internal override int Formula(int slot,int skill,int rank)
        {
            int value=slot switch{-1=>I("duration"),0=>I("damage"),3=>I("conversion"),200=>B("zeroAura")?0:rank*3,201=>B("zeroAura")?0:rank*2,_=>throw new InvalidOperationException("Unknown formula control")};
            A(12,slot,skill,rank,value);return value;
        }
        internal override FrenzyReleaseList List(int state){A(14,state,CurrentList!=null?1:0);return CurrentList;}
        internal override FrenzyReleaseList AllocateList(int flags,int expiry,int kind,int id)
        {A(16,flags,expiry,kind,id,B("allocate")?1:0);return B("allocate")?CurrentList=new(37,expiry):null;}
        internal override void SetRemoveCallback(FrenzyReleaseList list){A(17,list.Charges);list.RemoveCallback=true;}
        internal override void PostList(FrenzyReleaseList list,int flag){A(18,list.Charges,flag);list.Posted=true;}
        internal override void ToggleState(int state,int flag)=>A(19,state,flag);
        internal override void ScheduleRemoval(int frame,int zero,int zero2)=>A(20,frame,zero,zero2);
        internal override void ToggleGraphic(int state,int flag)=>A(21,state,flag);
        internal override void WriteAura(FrenzyReleaseList list,FrenzyReleaseRow row,int skill,int level)
        {
            // Explicit formula/stat service for the original shared aura
            // writer. These synthetic values do not certify Frenzy's curves.
            int velocity=Formula(200,skill,level);if(velocity!=0){list.Velocity=velocity;}
            int attack=Formula(201,skill,level);if(attack!=0){list.AttackRate=attack;list.OtherAnimationRate=attack;}
            A(22,list.Charges,list.Velocity,list.AttackRate,list.OtherAnimationRate);
        }
        internal override void SetTarget(SourceGuidTarget target){A(4,target.Handle);CurrentTarget=target;}
        internal override int ToHit(int skill,int rank){A(9,skill,rank);return 137;}
        internal override int ResultFlags(SourceGuidTarget target,int toHit,int zero){A(10,target.Handle,toHit,zero,I("result"));return I("result");}
        internal override void RollElemental(FrenzyReleasePacket packet,int skill,int rank){A(11,skill,rank);packet.Elemental=77;}
        internal override void AllocateCombat(SourceGuidTarget target,FrenzyReleasePacket packet,byte sourceDamage)
        {A(13,target.Handle,sourceDamage);Packet=[packet.ResultFlags,packet.HitFlags,packet.HitClass,packet.DamagePercent,packet.ConversionPercent,packet.ConversionType,packet.Elemental];}
        internal override void Drain(SourceGuidTarget target,int zero)
        {A(6,target.Handle,zero);if(++Drains==I("replace")){CurrentUsed=B("replacement")?Replacement:null;}}
        internal override void FreeCombat(SourceGuidTarget target)=>A(23,target.Handle);
        internal override void RefreshEquipment()=>A(24);
        internal override void RemoveBrokenWeapons()=>A(25);
        internal override int MeleeRange(){A(2,I("range"));return I("range");}
        internal override SourceGuidTarget Search(SourceGuidFindRequest request)
        {
            var candidates=P.GetProperty("candidates").EnumerateArray().Select((v,i)=>new SourceGuidTarget(i+2,v.GetUInt32())).ToArray();int count=777;
            IEnumerable<SourceGuidTarget> Enumerate(int x,int y,int range,uint filter){A(3,x,y,range,filter,request.Anchor);return candidates;}
            return SourceGuidTargets.Find(request,Enumerate,ref count);
        }
        internal object[] Result(int result,DoubleSwingPhaseUnit unit)
            =>[result,unit.Flags,CurrentTarget?.Handle??0,UsedToken,First.Param1,Replacement.Param1,
                CurrentList is{} list?new object[]{list.Charges,list.Expiry,list.State,list.RemoveCallback?1:0,list.Posted?1:0,list.Velocity,list.AttackRate,list.OtherAnimationRate}:Array.Empty<object>(),Packet,Trace];
    }
    private static void Check()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"frenzy-release-source-probe.json")));int count=0,stats=0,rolls=0,phases=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            string family=row[0].GetString();var p=row[1];var s=new Services(p);var unit=new DoubleSwingPhaseUnit(p.GetProperty("frame").GetInt32(),p.GetProperty("flags").GetUInt32());
            int skill=p.GetProperty("skill").GetInt32(),rank=p.GetProperty("rank").GetInt32(),result;
            switch(family)
            {
                case "stats":BarbarianFrenzyRelease.ApplyStats(skill,rank,s);result=1;stats++;break;
                case "roll":result=BarbarianFrenzyRelease.Roll(s.CurrentTarget,skill,rank,s);rolls++;break;
                case "phase":result=BarbarianFrenzyRelease.Execute(unit,skill,rank,s);phases++;break;
                default:throw new InvalidOperationException("Unknown Frenzy source family");
            }
            string actual=JsonSerializer.Serialize(s.Result(result,unit));Require(actual==JsonSerializer.Serialize(row[2]),"Frenzy case "+count+":"+actual+" expected:"+row[2]);count++;
        }
        Require(count==15024&&stats==1188&&rolls==12099&&phases==1737,"Incomplete Frenzy source matrix");
        Require(!BarbarianCombat.Implemented(CombatAbility.Frenzy),"Legacy release enabled unresolved modern start67");
        Log.Information("Frenzy:15024complete legacy contexts,1188charge/12099packet/1737composed releases;previous latch,equipment/GUID order,used-skill replacement and raw narrowing pass;modern start67 remains gated");
    }
}
