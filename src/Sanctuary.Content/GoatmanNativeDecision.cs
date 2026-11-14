// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record GoatmanAiScore(int Id,byte Flags37,GenericPetScoreSkill Score);

// Source decision-root binding. The surrounding AI-entry/event host must
// supply actual search traversal, movement and timed skill execution leaves.
// This module never installs a timer or substitutes a second targeting AI.
internal static class GoatmanNativeDecision
{
    private static readonly Lazy<GoatmanAiScore[]> Scores=new(()=>
    {
        var goat=BalanceCatalog.Read<GoatmanAiScore>("goatman-ai-scores");
        var tainted=BalanceCatalog.Read<GoatmanAiScore>("tainted-ai-scores");
        var rows=goat.Concat(tainted).Concat(BalanceCatalog.Read<GoatmanAiScore>("defiler-ai-scores")).ToArray();
        if(rows.Length!=7||!rows.Select(r=>r.Id).SequenceEqual(new[]{416,418,417,419,422,420,405})
            ||rows.Where(r=>r.Id is not(420 or 405)).Any(r=>r.Score is not{Flags36:0,State162:-1,Class272:-1,OwnerCompare274:-1,Calc276:-1}))
        {throw new InvalidDataException("Unsupported captured Goatman AI score rows");}
        foreach(var r in rows)
        {
            var meta=GenericPetMetadata.Table.Skill(r.Id);
            if(meta==null||meta.AiType!=(r.Id==405?2:r.Id==420?6:r.Id==422?11:4)||meta.Flags36!=r.Score.Flags36||meta.Flags39!=r.Score.Flags39)
            {throw new InvalidDataException("Goatman score/catalog identity disagrees");}
        }
        return rows;
    });
    internal static GenericPetScoreSkill Score(int skill)=>Scores.Value.FirstOrDefault(r=>r.Id==skill)?.Score
        ??throw new NotSupportedException("No captured Goatman AI score for this skill");
    internal static byte Flags37(int skill)=>Scores.Value.FirstOrDefault(r=>r.Id==skill)?.Flags37
        ??throw new NotSupportedException("No captured Goatman action flags for this skill");
    private static int[] Order(SkeletonGuardian body)=>body switch
    {WarlockGoatmanBody=>[416,418,417,419],WarlockTaintedBody=>[422,420],WarlockDefilerBody=>[405],_=>throw new NotSupportedException("Unbound ground demon skill order")};
    private static int Mode(int skill)=>skill is 420 or 405?8:skill is 417 or 422?5:4;
    private static int Rank(SkeletonGuardian body,int skill,bool bonuses=true)=>body switch
    {WarlockGoatmanBody goat=>GoatmanNpcContact.Rank(goat,skill),WarlockTaintedBody tainted=>TaintedNpcRanks.Level(tainted,skill,bonuses),WarlockDefilerBody defiler when skill==405=>DefilerLinkNumbers.Capture(defiler.Prepared.Snapshot.Birth.Suppliers.HealthLinkRank,defiler.Prepared.Snapshot.Birth.Suppliers.SelfSkillRank,bonuses?BattleCommand.Bonus(defiler):0).Rank,_=>throw new NotSupportedException("Unbound ground demon skill rank")};
    private static int Id(Mobile m)=>checked((int)m.Serial.Value);
    private static Mobile Mobile(int id)=>World.FindMobile((Serial)(uint)id)is{Deleted:false} m?m
        :throw new NotSupportedException("Goatman decision unit is no longer present");
    internal static List<GenericPetCombatCandidate> Gather(SkeletonGuardian body,GenericPetParameters parameters,GenericPetGatherState state,
        GenericPetGatherServices services,Func<bool,int,ushort> scratch)
    {
        int sourceClass=WarlockDemonPlacement.ClassId(body);int[] order=Order(body);var entries=order.Where(id=>Rank(body,id,parameters.AllowSkillBonuses)>0)
            .Select(id=>new GenericPetGatherEntry(id,checked((short)id),Mode(id),-1,0)).ToArray();
        var source=services with
        {
            SkillLevel=(entry,bonuses)=>Rank(body,entry.SkillId,bonuses),
            SkillInfo=(selector,id)=>GenericPetMetadata.Table.Bind(1).SkillInfo(selector,id),
            MonsterSkill=(selector,cls,slot)=>selector==1&&cls==WarlockDemonPlacement.ClassId(body)&&slot is>=0 and<8
                ?slot<order.Length?checked((short)order[slot]):(short)-1:throw new NotSupportedException("Unbound Goatman skill-row lookup"),
            SkillCount=selector=>selector==1?GenericPetMetadata.Table.SkillCount:throw new NotSupportedException("Unbound Goatman skill selector"),
            ScratchTail=scratch
        };
        return GenericPetGather.Build(new(true,sourceClass,1,entries),parameters,state,source);
    }

    // Creates a root callback for the actual registered header and three saved
    // decision words. Execution leaves remain mandatory,including source path
    // position/traversal:they are not replaced with fixture/default values.
    internal static GenericPetEventBinding Bind(SkeletonGuardian body,PlayerMobile owner,GenericPetActionRandom random,
        Func<int,GenericPetDecisionUnit> position,GenericPetInitializationServices initialization,
        GenericPetSearchWrapperServices search,GenericPetCombatServices combatLeaves,GenericPetFollowServices follow,
        GenericPetFollowExecutionServices followExecution,GenericPetActionServices actions,Func<bool,int,ushort> scratch,
        Func<GenericPetDecisionUnit,int?> level,Action<int> idle)
    {
        ArgumentNullException.ThrowIfNull(random);ArgumentNullException.ThrowIfNull(position);ArgumentNullException.ThrowIfNull(initialization);
        ArgumentNullException.ThrowIfNull(search);ArgumentNullException.ThrowIfNull(combatLeaves);ArgumentNullException.ThrowIfNull(follow);
        ArgumentNullException.ThrowIfNull(followExecution);ArgumentNullException.ThrowIfNull(actions);ArgumentNullException.ThrowIfNull(scratch);ArgumentNullException.ThrowIfNull(level);ArgumentNullException.ThrowIfNull(idle);
        if(body is not{Alive:true,Deleted:false}||body.ControlMaster!=owner||owner is not{Alive:true,Deleted:false})
        {throw new NotSupportedException("Source Goatman decision needs its living captured owner/body");}
        var saved=DeathMarkWorld.State(body);var header=DeathMarkWorld.Header(body);
        if(saved is not{Selector:1,Control:not null}||header.Control is not{State:0,Function:0x4b60c0,Executing:false}||header.Control.Queue.Count!=0)
        {throw new NotSupportedException("Native Goatman decision is not bound to a normal registered GenericPet header");}
        var parameters=GenericPetProfiles.Raw(body.SourceMonsterId);int basic=body is WarlockGoatmanBody or WarlockDefilerBody?4:0;int basicType=body is WarlockDefilerBody?2:basic;
        _=DemonPathWorld.Size(body);
        if(parameters is not{SwarmRaw:0,ChooseType:1}||parameters.ModeAttack!=basic||parameters.ModeAiType!=basicType)
        {throw new NotSupportedException("Goatman source profile needs a different host binding");}
        if(NpcEquipmentRandom.Require(body).Seed!=random.Seed)
        {throw new NotSupportedException("Goatman AI random view is stale");}
        int actor=Id(body),master=Id(owner);var identity=new GenericPetWorldIdentity(header.Capture().Game);
        var metadata=GenericPetMetadata.Table.Bind(1);GenericPetInitializedFrame current=null;
        var gatherServices=new GenericPetGatherServices((_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
            _=>throw new NotSupportedException("Goatman has no progressive charge skills"),(_,_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),scratch);
        // AI selection and combat allocation share the existing actor stream.
        // Flush before external execution and reload afterwards so nested hit
        // callbacks cannot be overwritten by a detached AI seed copy.
        bool Execute(Func<bool> action)
        {
            NpcEquipmentRandom.Set(body,random.Seed);
            try{return action();}
            finally{if(body is{Alive:true,Deleted:false}&&NpcEquipmentRandom.State(body)is{} state){random.Seed=state.Seed;}}
        }
        var boundActions=actions with
        {
            SkillByte37=(selector,id)=>selector==1?Flags37(id):throw new NotSupportedException("Goatman action selector"),
            ExecuteSkill=(mode,id,target,x,y,flag)=>Execute(()=>actions.ExecuteSkill(mode,id,target,x,y,flag)),
            ExecuteMode=request=>Execute(()=>actions.ExecuteMode(request))
        };
        var owned=initialization with{OwnerUnits=(id,visit)=>
        {
            if(id!=master){throw new NotSupportedException("Wrong native owner census");}
            // SwarmRaw0 consumes only Visits. The native follower store is the
            // declared census,not a fabricated D2 pet-type bucket structure.
            foreach(var pet in owner.AllFollowers??[]){if(pet is BaseCreature{Deleted:false} native){visit(Id(native));}}
        }};
        GenericPetScoreUnit Unit(int id)
        {
            var native=Mobile(id);int cls=native is SkeletonGuardian guardian?GenericPetSearchTables.SourceId(guardian.SourceMonsterId)??-1
                :native is BaseCreature creature?GenericPetSearchTables.SourceId(CreatureProfiles.Definition(creature).Reference)??-1:-1;
            return new(position(id),cls,id,1,true,native is BaseCreature,cls>=0);
        }
        int Stat(int id,int stat)
        {
            var target=Mobile(id);var(code,value)=stat switch
            {36=>("physical",target.PhysicalResistance),37=>("mag",NamedElements.MagicResistance(target)),39=>("fire",target.FireResistance),
             41=>("ltng",target.EnergyResistance),43=>("cold",target.ColdResistance),45=>("pois",target.PoisonResistance),
             _=>throw new NotSupportedException("Goatman scorer requested an unbound stat")};
            // Scoring reads the target pool,not this attacker's Sanctuary or
            // item piercing. Existing target curses/auras still affect it.
            return NamedElements.Resistance(null,target,code,value,pierceOverride:0);
        }
        var scoring=new GenericPetScoreServices((selector,id)=>selector==1?Score(id):throw new NotSupportedException("Goatman score selector"),
            (_,_)=>throw new NotSupportedException("Goatman has no progressive skill"),Unit,(selector,stat)=>selector==1&&stat is 36 or 37 or 39 or 41 or 43 or 45,
            Stat,(id,x,y)=>GenericPetSearch.Squared(position(id),x,y),_=>GenericPetMetadata.Table.StateCount,
            (selector,state)=>metadata.StateGroup(selector,state) is short group?checked((ushort)group):null,
            (id,state)=>body is WarlockDefilerBody?DefilerHealthLink.HasState(Mobile(id),state,TimedEffects.Now):throw new NotSupportedException("Attack-only ground demon scorer requested a buff state"),(_,_)=>throw new NotSupportedException("Goatman has no summon action"),
            _=>throw new NotSupportedException("Goatman has no summon cap scorer"),(_,_,_,_,_)=>throw new NotSupportedException("Goatman has no summon formula"),
            (_,_)=>throw new NotSupportedException("Goatman has no type12 descriptor"),_=>throw new NotSupportedException("Goatman has no type13 descriptor"));
        GenericPetCombatServices Combat(GenericPetDecisionFrame frame)
        {
            var context=new GenericPetCombatContext(1,position(actor));
            var wrapper=GenericPetOwnerSurvey.Bind(new GenericPetSearchWrapperContext(1,actor,master,frame.Parameters,[]),current.Survey);
            GenericPetCombatCandidate Rate(GenericPetCombatCandidate row,GenericPetCombatSelection selection)
                =>GenericPetRatingRouting.Rate(1,frame.Parameters,current.PetLifePercent,selection,row,
                    new((selector,id)=>metadata.SkillInfo(selector,id)?.Flags39,
                        (candidate,w40,w48)=>GenericPetTargetScoring.Rate(current.ScoreContext(1,0,actor,master),frame.Parameters,w40,w48,candidate,random,scoring)));
            var outer=combatLeaves with
            {
                Gather=()=>Gather(body,frame.Parameters,current.Gather,gatherServices,scratch),BaseModeSkill=selector=>selector==1?0:throw new NotSupportedException("Goatman base skill selector"),
                Range=rows=>GenericPetGather.SearchMask(new(true,WarlockDemonPlacement.ClassId(body),1,null),frame.Parameters.Flags,rows,gatherServices with{SkillInfo=metadata.SkillInfo}),
                Search=GenericPetSearchWrapper.Provider(wrapper,search),
                RateRemembered=(row,selection)=>GenericPetTargetScoring.Rate(current.ScoreContext(1,0,actor,master),frame.Parameters,
                    selection.Word40,selection.Word48,row,random,scoring),Rate=Rate,
                Prepare=rows=>GenericPetPreparation.Apply(new(1,actor,master),rows,new(metadata.BuffSkill,metadata.StateCount,metadata.StateGroup,metadata.RawStateGroup,
                    (_,_)=>throw new NotSupportedException("Goatman has no buff descriptors"),_=>[],_=>[],position,(id,x,y)=>GenericPetSearch.Squared(position(id),x,y)))
            };
            return GenericPetActions.Bind(outer,context,frame,random,boundActions);
        }
        var root=new GenericPetRootServices(()=>position(master),_=>
        {
            current=DeathMarkWorld.InitializePrepared(body,master,identity,owned);return current.Decision;
        },_=>throw new NotSupportedException("Bind the real timed-event store before root execution"),GenericPetSearch.Squared,
            level,
            (unit,frame)=>
            {
                var context=GenericPetOwnerSurvey.Bind(new GenericPetFollowExecution(position(actor),unit,0,frame.Parameters.CloseRaw,frame.Parameters.WanderRaw,0),current.Survey);
                return GenericPetFollow.Try(new(position(actor),unit,frame.Parameters.WarpSquared,frame.Parameters.WanderRaw),
                    GenericPetFollowExecutor.Bind(follow,context,random,followExecution));
            },(_,frame)=>GenericPetCombat.Try(new(1,position(actor)),frame,Combat(frame)),idle,
            memory=>DeathMarkWorld.FinishPrepared(body,memory.Target==0?null:(1,memory.Target)),(_,_,_)=>throw new InvalidOperationException("Unbound control publication"));
        root=DeathMarkWorld.BindControlPersistence(body,root);
        var publish=root.Persist;
        root=root with{Persist=(a,b,c)=>{publish(a,b,c);NpcEquipmentRandom.Set(body,random.Seed);}};
        return new(new(position(actor),true,true,true,[]),root);
    }
}
