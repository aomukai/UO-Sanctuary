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
    internal sealed class Cast(AssassinShadowBody body,BaseCreature target,Point3D aim,int skill=251,DateTime? started=null)
    {
        internal readonly AssassinShadowBody Body=body;
        internal readonly int Skill=skill;
        internal readonly Map Map=body.Map;
        internal BaseCreature Target=target;
        internal readonly Point3D Aim=aim;
        internal readonly DateTime StartedUtc=started??TimedEffects.Now;
        internal bool Released;
        internal int? SequencePoint;
        internal int LastSequencePoint=-1;
        internal bool ServerDo;
    }
    private static NotSupportedException Missing()=>new("Shadow intrinsic skill needs an additional qualified NPC action service");
    private static ItemExecutionMetadata Row(int id)
    {
        if(id==260){return ShadowDragonClaw.Row();}
        if(ShadowChargeStrikes.Handles(id)){return ShadowChargeStrikes.Row(id);}
        if(ShadowSentries.Handles(id)){return ShadowSentries.Row(id);}
        if(id==253){return ShadowPsychicHammers.Row();}
        if(id==264){return ShadowCloaks.Row();}
        if(id==277){return ShadowBladeShields.Row();}
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
    internal static int Range(AssassinShadowBody body,int id)
    {
        var range=ItemSkillExecution.Skill(id).Text("range");
        if(range=="both")
        {
            // Complete D2Common_11016 selects melee for a non-bow NPC.
            // Shadows retain their approved actual original dual-claw binding.
            if(ShadowEquipmentDefense.Composite(body)!=13){throw new NotSupportedException("Shadow both-range needs its original dual-claw composite");}
            return 1;
        }
        return range switch{"h2h"=>1,"rng"=>2,"0"or"none"=>0,_=>throw new NotSupportedException("Unmapped Shadow skill range: "+range)};
    }
    internal static SkillValues Values(AssassinShadowBody body,int rank,int skill=251)
    {_=Row(skill);return OwnValues(body,rank,skill);}
    // Producers with separately qualified execution metadata can share the
    // same own-book/stat composition without granting their action route.
    internal static SkillValues OwnValues(AssassinShadowBody body,int rank,int skill)
    {
        var book=body.OwnedSkills;var graph=OriginalNpcEquipment.Graph(body);
        int Id(string key)=>SkillMechanics.Source(key).SourceId;
        return new(SkillMechanics.All.Single(s=>s.SourceId==skill),key=>Id(key)==skill?rank:book.Rank(Id(key),graph.Read,BattleCommand.Bonus(body)),
            key=>book.Find(Id(key))?.Base??0,OriginalNpcEquipment.Level(body));
    }
    internal static Cast Start(AssassinShadowBody body,BaseCreature target,Point3D aim,int skill=251,DateTime? at=null)
    {
        var row=Row(skill);
        if(!Attached(body)||row.Lob&&!LobProjectiles.Valid(body,aim)||target!=null&&(!Enemy(body,target)||target.Map!=body.Map)
            ||skill==253&&!ShadowPsychicHammers.Valid(body,target)||skill==273&&!ShadowMindBlasts.Valid(body,target?.Location??aim)
            ||skill==264&&!ShadowCloaks.Valid(body)
            ||body.OwnedSkillRank(skill)<=0){return null;}
        if(ShadowSentries.Handles(skill)&&!ShadowSentries.Destination(body,target,aim,out aim)){return null;}
        body.ChangeSkills(book=>book.Use(skill));var cast=new Cast(body,target,aim,skill,at);
        return SkillStartup.Selected(new Startup(cast))!=0?cast:null;
    }
    internal static int Release(Cast cast,DateTime at)
    {
        if(cast==null||ShadowClawSequence.Handles(cast.Skill)||cast.Released||!Attached(cast.Body)||cast.Body.Map!=cast.Map||cast.Body.OwnedSkills.Used!=cast.Skill){return 0;}
        cast.Released=true;
        return SkillExecution.Run(new Execution(cast,at),cast.Skill,cast.Body.OwnedSkillRank(cast.Skill),true,false,false);
    }
    internal static int ReleaseSequence(Cast cast,int point,DateTime at)
    {
        if(cast==null||!ShadowClawSequence.Handles(cast.Skill)||cast.Released||!Attached(cast.Body)||cast.Body.Map!=cast.Map
            ||cast.Body.OwnedSkills.Used!=cast.Skill||point is <0 or >=ShadowClawSequence.Points||point<=cast.LastSequencePoint){return 0;}
        var pending=ShadowActions.State(cast.Body);
        if(pending?.Cast!=cast||pending.Sequence is not{} sequence||sequence.Clock.Point!=point||!ShadowClawSequence.Releases(sequence)){return 0;}
        cast.LastSequencePoint=point;cast.SequencePoint=point;
        try{return SkillExecution.Run(new Execution(cast,at),cast.Skill,cast.Body.OwnedSkillRank(cast.Skill),true,false,false);}
        finally{cast.SequencePoint=null;}
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
        internal override SkillStartupRecord Record(int skill){var row=Row(skill);return new(row.Number("TargetableOnly")!=0,false,false,row.InTown,row.Number("LineOfSight"),row.Start,row.Number("periodic")!=0,false);}
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
        internal override (int X,int Y) TargetPoint(SkillStartupTarget target)=>ShadowSentries.Handles(cast.Skill)
            ?(cast.Aim.X*2,cast.Aim.Y*2):(cast.Target.X*2,cast.Target.Y*2);
        internal override (int X,int Y) FirstPathPoint=>(cast.Aim.X*2,cast.Aim.Y*2);
        internal override bool ClearLine(int x,int y,uint mask)=>mask==SourceMeleeGeometry.FlyingCollisionMask
            ?cast.Skill==253?cast.Target!=null&&x==cast.Target.X*2&&y==cast.Target.Y*2&&Body.InLOS(cast.Target)
                :ShadowSentries.Handles(cast.Skill)?Body.InLOS(new Point3D(x/2,y/2,cast.Target?.Z??cast.Aim.Z)):throw Missing()
            :throw Missing(); // Shared native LOS projection;exact D2 terrain remains separate.
        internal override int StartCount=>29;
        internal override bool HasStart(int function)=>function==0?false:function is 22 or 23 or 25 or 28?true:throw Missing();
        internal override int ExecuteStart(int function,int skill,int rank)=>function==22&&skill==253?ShadowPsychicHammers.Valid(Body,cast.Target)?1:0
            :function==23&&ShadowChargeStrikes.Handles(skill)?ShadowChargeStrikes.Start(Body,cast.Target)?1:0
            :function==25&&skill==260?cast.Target!=null?1:0
            :function==28&&skill==277?ShadowBladeShields.Start(Body,rank,cast.StartedUtc):throw Missing();
        internal override void ConsumeMana(int skill,int rank){} // Ordinary NPC, intrinsic ownership, no player/item charge.
        internal override void DeletePeriodicEvent()
        {if(cast.Skill!=277||BladeShields.State(Body)is not{NextPulseUtc:null}){throw Missing();}} // Start28 already replaced the old periodic state.
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
        internal override bool HasDo(int function)=>function==0?false:function is 18 or 33 or 34 or 35 or 45 or 46 or 47 or 51 or 54?true:throw Missing();
        internal override int Execute(int function,int skill,int level)=>function==18&&ShadowDefensiveBuffs.Handles(skill)
            ?GeneratedNpcLiveResources.ApplyShadowBuff(Body,skill,Values(Body,level,skill),at)?1:0
                :function==33&&skill==253?ShadowPsychicHammers.Release(Body,cast.Target,level,at)
                :function==34&&ShadowChargeStrikes.Handles(skill)?ShadowChargeStrikes.Release(Body,cast.Target,skill,level,at)
                :function==35&&ShadowChargeStrikes.Claws(skill)?ClawRelease(skill,level)
                :function==46&&skill==260?DragonRelease()
                :function==47&&skill==264?ShadowCloaks.Release(Body,level,at)
                :function==45&&ShadowSentries.Handles(skill)?ShadowSentries.Release(Body,cast.Target,cast.Aim,skill,level,at)
                :function==54&&skill==277?ShadowBladeShields.Release(Body,at)
                :function==51&&skill==273?ShadowMindBlasts.Release(Body,cast.Target,cast.Aim,level,at):throw Missing();
        internal override int MissileCount=>ItemSkillExecution.MissileCount;
        private int ClawRelease(int skill,int level)
        {
            if(cast.SequencePoint is not{} point){throw Missing();}
            cast.ServerDo=((point>>8)&1)!=0;
            _=ShadowChargeStrikes.Release(Body,cast.Target,skill,level,at);return 1; // Complete do35 returns1 even if do34 declines.
        }
        private int DragonRelease()
        {
            if(cast.SequencePoint is not{} point){throw Missing();}
            if(cast.Target==null){return 0;}
            int result=ShadowDragonClaw.Release(Body,cast.Target,at);
            cast.ServerDo=((point>>8)&1)!=0; // do46 updates the flag after contact.
            return result;
        }
        internal override void SetServerDo(){if(ShadowClawSequence.Handles(cast.Skill)){cast.ServerDo=true;}}
        internal override (int X,int Y)? TargetPoint=>cast.Target is{Alive:true,Deleted:false} target?(target.X*2,target.Y*2):null;
        internal override (int X,int Y) FirstPathPoint=>(cast.Aim.X*2,cast.Aim.Y*2);
        internal override (int X,int Y) SourcePoint=>(Body.X*2,Body.Y*2);
        internal override void Launch(bool lob,int missile,int skill,int level,int x,int y,int tx,int ty,int flags)
        {
            if(!lob||skill!=251||missile!=Row(skill).Missile||x!=0||y!=0||tx!=0||ty!=0||flags!=0){throw Missing();}
            var point=cast.Target is{Alive:true,Deleted:false} target?target.Location:cast.Aim;
            FireBlasts.EmitShadow(Body,point,level,at);
        }
        internal override bool HasStart(int function)=>function==0?false:function is 22 or 23 or 25 or 28?true:throw Missing();
        internal override void ConsumeMana(int skill,int level){}
        internal override void ConsumeQuantity()=>throw Missing();
        internal override int Delay(int formula,int skill,int level)=>formula==0?0:throw Missing();
        internal override uint SequenceFrame=>throw Missing();
        internal override void SetDelay(int frames)=>throw Missing();
    }
}
