// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// The NPC is the actual summon owner. ResponsibleOwner is credit/allegiance
// provenance, not a player caster or a player18-head FIFO registration.
internal sealed record NpcHydraBirth(uint Caster,uint ResponsibleOwner,string Binding,HydraCreationStats Birth);
internal sealed record NpcHydraRequest(NpcHydraBirth Capture,int Index,int X,int Y,int Mode,DateTime ExpiryUtc);

internal static class NpcHydraCreation
{
    internal static ItemExecutionMetadata Require()
    {
        var row=ItemSkillExecution.Skill(62);
        if(row is not{Do:144,Start:14,ItemEffect:1,ItemTarget:0,ItemTargetDo:false,ItemCheckStart:true,InTown:false,
            UseManaOnDo:false,Lob:false,DecrementQuantity:false,Animation:10,Missile:-1}
            ||row.HasDelay("delay")||row.HasDelay("localdelay")||row.HasDelay("globaldelay")||row.HasDelay("perdelay"))
        {throw new NotSupportedException("Unqualified original NPC Hydra item metadata");}
        return row;
    }
    internal static HydraCreationStats Prepare(int rank,int level,Func<int,int,int> accrued,Func<int,int> hard)
    {
        _=Require();ArgumentNullException.ThrowIfNull(accrued);ArgumentNullException.ThrowIfNull(hard);
        if(rank is<1 or>63){throw new NotSupportedException("NPC item Hydra requires its six-bit proc rank");}
        int Synergy(string key)=>key switch{"Fire Bolt"=>hard(36),"Fire Ball"=>hard(47),_=>0};
        var row=SkillMechanics.For(CombatAbility.Hydra);
        var values=new SkillValues(row,key=>key==row.SourceKey?rank:Synergy(key),Synergy,level);
        return HydraCreation.Prepare(values,level,name=>accrued(EquipmentStatValues.Definition(name)?.Id
            ??throw new NotSupportedException("Unknown Hydra accrued stat:"+name),0));
    }
    internal static NpcHydraBirth Capture(SkeletonGuardian body,int rank)
    {
        if(body is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false} owner}
            ||body.Map==null||body.Map==Map.Internal||body.Map!=owner.Map||!body.Permitted(owner))
        {throw new NotSupportedException("Prepared NPC Hydra requires its original living owned source and facet");}
        // This read-only birth component deliberately does not admit item
        // callbacks or strip an unsupported Hydra proc from original equipment.
        var graph=OriginalNpcEquipment.Graph(body);string binding=OriginalNpcEquipment.Binding(body);
        int Hard(int id)=>body is AssassinShadowBody shadow?shadow.OwnedSkills.Rank(id,graph.Read,bonuses:false):0;
        return new(body.Serial.Value,owner.Serial.Value,binding,Prepare(rank,OriginalNpcEquipment.Level(body),graph.Read,Hard));
    }
    internal static bool Start(SkeletonGuardian body,NpcItemTarget target,(int X,int Y) path,int rank,out Point3D aim)
    {
        _=Require();aim=default;if(rank is<1 or>63){throw new NotSupportedException("NPC Hydra start requires its six-bit rank");}
        if(body is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false} owner}
            ||body.Map==null||body.Map==Map.Internal){return false;}
        aim=target?.Point(body.Map)??new Point3D(path.X,path.Y,body.Z);
        // Source start14 checks target room/town, not mana,LOS or a player
        // casting animation. Native coordinate bounds supply the room domain.
        return aim.X>0&&aim.Y>0&&aim.X<body.Map.Width&&aim.Y<body.Map.Height&&!SpellHelper.IsTown(aim,body);
    }
    internal static int EmitPrepared(SkeletonGuardian body,NpcItemTarget target,(int X,int Y) path,int rank,DateTime at,
        Func<NpcHydraRequest,bool> summon)
    {
        ArgumentNullException.ThrowIfNull(summon);
        if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddSeconds(-10)){throw new ArgumentException("NPC Hydra needs UTC");}
        if(!Start(body,target,path,rank,out var aim)){return 0;}
        var capture=Capture(body,rank);int result=0;
        for(int i=0;i<3;i++)
        {
            // Source positions are subtiles; retain half-tile offsets rather
            // than prematurely rounding the three independent allocations.
            var request=new NpcHydraRequest(capture,i,aim.X*2+i-1,aim.Y*2-(i==1?0:1),9,at.AddMilliseconds(capture.Birth.DurationFrames*40));
            if(summon(request)){result=1;}
        }
        return result;
    }
}

internal sealed class NpcHydraExecution(SkeletonGuardian source,NpcItemTarget target,(int X,int Y) path,DateTime at,
    Func<NpcHydraRequest,bool> summon):SkillExecutionServices
{
    private static NotSupportedException Missing()=>new("Prepared NPC Hydra needs its explicit additional service");
    internal int Run(int rank,int outgoing)
    {
        if(outgoing is<0 or>1){throw new ArgumentOutOfRangeException(nameof(outgoing));}
        return SkillExecution.Run(this,62,rank,false,true,outgoing!=0);
    }
    internal override bool HasGame=>source.Map!=null&&source.Map!=Map.Internal;
    internal override bool HasSource=>source is{Alive:true,Deleted:false};
    internal override int UnitType=>1;
    internal override SkillExecutionRecord Record(int id)=>id==62?NpcHydraCreation.Require().LegacyRecord(false,1):throw Missing();
    internal override ExecutionSkill UsedSkill()=>null;
    internal override int DoCount=>191;
    internal override bool HasDo(int function)=>function==144;
    internal override int Execute(int function,int skill,int rank)=>function==144&&skill==62?NpcHydraCreation.EmitPrepared(source,target,path,rank,at,summon):throw Missing();
    internal override int MissileCount=>ItemSkillExecution.MissileCount;
    internal override bool HasRoom=>throw Missing();internal override bool RoomInTown=>throw Missing();
    internal override void ClearUsedSkill()=>throw Missing();internal override void TownNeutral()=>throw Missing();
    internal override ExecutionSkill HighestSkill(int id)=>throw Missing();internal override int Rank(ExecutionSkill skill,bool bonuses)=>throw Missing();
    internal override bool ManaAllowed()=>throw Missing();internal override void SetServerDo()=>throw Missing();
    internal override (int X,int Y)? TargetPoint=>throw Missing();internal override (int X,int Y) FirstPathPoint=>throw Missing();internal override (int X,int Y) SourcePoint=>throw Missing();
    internal override void Launch(bool lob,int missile,int skill,int rank,int x,int y,int tx,int ty,int flags)=>throw Missing();
    internal override bool HasStart(int function)=>throw Missing();internal override void ConsumeMana(int skill,int rank)=>throw Missing();
    internal override void ConsumeQuantity()=>throw Missing();internal override int Delay(int formula,int skill,int rank)=>throw Missing();
    internal override uint SequenceFrame=>throw Missing();internal override void SetDelay(int frames)=>throw Missing();
}
