// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// An intrinsic NPC action's real selected-skill/start/do context. The actor's
// animation conductor supplies the release event; this does not invent an AI
// clock, call a player talent or turn an item proc into an intrinsic skill.
internal static class ShadowSkillCasting
{
    internal sealed class Cast(AssassinShadowBody body,BaseCreature target,Point3D aim,int skill=251)
    {
        internal readonly AssassinShadowBody Body=body;
        internal readonly int Skill=skill;
        internal readonly Map Map=body.Map;
        internal BaseCreature Target=target;
        internal readonly Point3D Aim=aim;
        internal bool Released;
    }
    private static NotSupportedException Missing()=>new("Shadow intrinsic skill needs an additional qualified NPC action service");
    private static ItemExecutionMetadata Row(int id)
    {
        if(id==253){return ShadowPsychicHammers.Row();}
        if(id==273){return ShadowMindBlasts.Row();}
        var row=ItemSkillExecution.Skill(id);
        bool buff=ShadowDefensiveBuffs.Handles(id);
        if(id!=251&&!buff||row is not{Start:0,UseManaOnDo:false,DecrementQuantity:false}
            ||(buff?row.Do!=18||row.Lob||!row.InTown||row.Missile!=-1:row.Do!=0||!row.Lob||row.InTown||row.Missile!=ItemSkillExecution.MissileId("bomb in air"))
            ||row.Number("TargetableOnly")!=0
            ||row.Number("TargetCorpse")!=0||row.Number("TargetAlly")!=0||row.Number("LineOfSight")!=0
            ||row.HasDelay("delay")||row.HasDelay("localdelay")||row.HasDelay("globaldelay")||row.HasDelay("perdelay"))
        {throw Missing();}
        return row;
    }
    internal static bool Attached(AssassinShadowBody body)=>body?.ControlMaster is PlayerMobile owner
        &&NpcItemSpellFlights.Attached(body,owner,body.Map?.MapID??-1);
    internal static SkillValues Values(AssassinShadowBody body,int rank,int skill=251)
    {
        var book=body.OwnedSkills;var graph=OriginalNpcEquipment.Graph(body);
        int Id(string key)=>SkillMechanics.Source(key).SourceId;
        _=Row(skill);
        return new(SkillMechanics.All.Single(s=>s.SourceId==skill),key=>Id(key)==skill?rank:book.Rank(Id(key),graph.Read,BattleCommand.Bonus(body)),
            key=>book.Find(Id(key))?.Base??0,OriginalNpcEquipment.Level(body));
    }
    internal static Cast Start(AssassinShadowBody body,BaseCreature target,Point3D aim,int skill=251)
    {
        var row=Row(skill);
        if(!Attached(body)||row.Lob&&!LobProjectiles.Valid(body,aim)||target!=null&&(!Enemy(body,target)||target.Map!=body.Map)
            ||skill==253&&!ShadowPsychicHammers.Valid(body,target)||skill==273&&!ShadowMindBlasts.Valid(body,target?.Location??aim)
            ||body.OwnedSkillRank(skill)<=0){return null;}
        body.ChangeSkills(book=>book.Use(skill));var cast=new Cast(body,target,aim,skill);
        return SkillStartup.Selected(new Startup(cast))!=0?cast:null;
    }
    internal static int Release(Cast cast,DateTime at)
    {
        if(cast==null||cast.Released||!Attached(cast.Body)||cast.Body.Map!=cast.Map||cast.Body.OwnedSkills.Used!=cast.Skill){return 0;}
        cast.Released=true;
        return SkillExecution.Run(new Execution(cast,at),cast.Skill,cast.Body.OwnedSkillRank(cast.Skill),true,false,false);
    }
    internal static void Complete(Cast cast)
    {if(cast==null){return;}cast.Released=true;if(cast.Body is{Alive:true,Deleted:false} body&&body.OwnedSkills.Used==cast.Skill){body.ChangeSkills(book=>book.Use(null));}}
    private static bool Enemy(AssassinShadowBody body,BaseCreature target)=>body.ControlMaster is PlayerMobile owner
        &&target is{Alive:true,Deleted:false}&&ClassCombat.IsHostile(owner,target)&&!ClassCombat.Friendly(body,target)&&body.CanBeHarmful(target,false);
    private sealed class Startup(Cast cast):SkillStartupServices
    {
        private AssassinShadowBody Body=>cast.Body;
        internal override bool HasGame=>Attached(Body);
        internal override bool HasSource=>Body is{Alive:true,Deleted:false};
        internal override int UnitType=>1;
        internal override ExecutionSkill UsedSkill()=>Body.OwnedSkills.Used is{} id?new(id,0):null;
        internal override SkillStartupRecord Record(int skill){var row=Row(skill);return new(row.Number("TargetableOnly")!=0,false,false,row.InTown,row.Number("LineOfSight"),row.Start,false,false);}
        internal override SkillStartupTarget Target()=>cast.Target is{} target?new(1,target.Alive?1:12,target.Alive&&target.CanBeDamaged()):null;
        internal override bool EnemyAllowed(SkillStartupTarget target)=>Enemy(Body,cast.Target);
        internal override bool PetAllowed(SkillStartupTarget target,int skill)=>false;
        internal override bool AllyAllowed(SkillStartupTarget target,int skill)=>cast.Target!=null&&ClassCombat.Friendly(Body,cast.Target);
        internal override void ClearTarget()=>cast.Target=null;
        internal override int Rank(ExecutionSkill skill)=>Body.OwnedSkillRank(skill.Id);
        internal override (int Rank,int Charges)? ItemInfo(ExecutionSkill skill)=>null;
        internal override SkillStartupRoom Room()=>Body.Map==null||Body.Map==Map.Internal?null:new(SpellHelper.IsTown(Body.Location,Body.ControlMaster));
        internal override void ClearUsedSkill()=>Body.ChangeSkills(book=>book.Use(null));
        internal override void TownNeutral()=>throw Missing();
        internal override bool PlayerManaAllowed()=>true; // This source gate only checks player mana; Kind1 has no debit.
        internal override (int X,int Y) TargetPoint(SkillStartupTarget target)=>(cast.Target.X*2,cast.Target.Y*2);
        internal override (int X,int Y) FirstPathPoint=>(cast.Aim.X*2,cast.Aim.Y*2);
        internal override bool ClearLine(int x,int y,uint mask)=>cast.Skill==253&&mask==SourceMeleeGeometry.FlyingCollisionMask
            ?cast.Target!=null&&x==cast.Target.X*2&&y==cast.Target.Y*2&&Body.InLOS(cast.Target):throw Missing(); // Shared native LOS projection;exact D2 terrain remains separate.
        internal override int StartCount=>23;
        internal override bool HasStart(int function)=>function==0?false:function==22?true:throw Missing();
        internal override int ExecuteStart(int function,int skill,int rank)=>function==22&&skill==253?ShadowPsychicHammers.Valid(Body,cast.Target)?1:0:throw Missing();
        internal override void ConsumeMana(int skill,int rank){} // Ordinary NPC, intrinsic ownership, no player/item charge.
        internal override void DeletePeriodicEvent()=>throw Missing();
    }
    private sealed class Execution(Cast cast,DateTime at):SkillExecutionServices
    {
        private AssassinShadowBody Body=>cast.Body;
        internal override bool HasGame=>Attached(Body);
        internal override bool HasSource=>Body is{Alive:true,Deleted:false};
        internal override int UnitType=>1;
        internal override SkillExecutionRecord Record(int skill)=>Row(skill).LegacyRecord(true,1);
        internal override ExecutionSkill UsedSkill()=>Body.OwnedSkills.Used is{} id?new(id,0):null;
        internal override bool HasRoom=>Body.Map!=null&&Body.Map!=Map.Internal;
        internal override bool RoomInTown=>SpellHelper.IsTown(Body.Location,Body.ControlMaster);
        internal override void ClearUsedSkill()=>Body.ChangeSkills(book=>book.Use(null));
        internal override void TownNeutral()=>throw Missing();
        internal override ExecutionSkill HighestSkill(int skill)=>Body.OwnedSkills.Find(skill)!=null?new(skill,0):null;
        internal override int Rank(ExecutionSkill skill,bool bonuses)=>Body.OwnedSkills.Rank(skill.Id,OriginalNpcEquipment.Graph(Body).Read,BattleCommand.Bonus(Body),bonuses);
        internal override int DoCount=>191;
        internal override bool ManaAllowed()=>true;
        internal override bool HasDo(int function)=>function==0?false:function is 18 or 33 or 51?true:throw Missing();
        internal override int Execute(int function,int skill,int level)=>function==18&&ShadowDefensiveBuffs.Handles(skill)
            ?GeneratedNpcLiveResources.ApplyShadowBuff(Body,skill,Values(Body,level,skill),at)?1:0
                :function==33&&skill==253?ShadowPsychicHammers.Release(Body,cast.Target,level,at)
                :function==51&&skill==273?ShadowMindBlasts.Release(Body,cast.Target,cast.Aim,level,at):throw Missing();
        internal override int MissileCount=>ItemSkillExecution.MissileCount;
        internal override void SetServerDo(){}
        internal override (int X,int Y)? TargetPoint=>cast.Target is{Alive:true,Deleted:false} target?(target.X*2,target.Y*2):null;
        internal override (int X,int Y) FirstPathPoint=>(cast.Aim.X*2,cast.Aim.Y*2);
        internal override (int X,int Y) SourcePoint=>(Body.X*2,Body.Y*2);
        internal override void Launch(bool lob,int missile,int skill,int level,int x,int y,int tx,int ty,int flags)
        {
            if(!lob||skill!=251||missile!=Row(skill).Missile||x!=0||y!=0||tx!=0||ty!=0||flags!=0){throw Missing();}
            var point=cast.Target is{Alive:true,Deleted:false} target?target.Location:cast.Aim;
            FireBlasts.EmitShadow(Body,point,level,at);
        }
        internal override bool HasStart(int function)=>function==0?false:function==22?true:throw Missing();
        internal override void ConsumeMana(int skill,int level){}
        internal override void ConsumeQuantity()=>throw Missing();
        internal override int Delay(int formula,int skill,int level)=>formula==0?0:throw Missing();
        internal override uint SequenceFrame=>throw Missing();
        internal override void SetDelay(int frames)=>throw Missing();
    }
}
