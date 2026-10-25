#!/usr/bin/env python3
"""Compose Inferno's real custom do, generic dispatcher, sequence and timer lists.

Single-burst entry/end and world services are declared. AI shot ownership,
neutral rescheduling, native beams and modern executable fidelity stay separate.
"""
import argparse,hashlib,json,re,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable,str(ROOT/'tools/probe_inferno_sentry_channel_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());parent=json.loads((ROOT/'tools/reference/inferno-sentry-channel-source-evidence.json').read_text())
    hashes=dict(parent['sourceHashes']);routines=dict(parent['compiledRoutineHashes'])
    def source(relative):
        raw=(STAGE/'d2moo-pinned-source'/relative).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=hashlib.sha256(raw).hexdigest();return raw.decode()
    def block(relative,name,kind='routine'):
        text=source(relative)
        pattern=r'^.*\b'+name+(r'\([^;\n]*\)\s*\n\{' if kind=='routine' else r'\s*\n\{')
        m=re.search(pattern,text,re.M);assert m,name;end,depth=text.index('{',m.start())+1,1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        body=text[m.start():end]+(';' if kind!='routine' else '');routines[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
    units=source('source/D2Common/include/Units/Units.h')
    constants={name:int(re.search(r'\b'+name+r'\s*=\s*(0x[0-9A-Fa-f]+)',units)[1],16) for name in ['UNITFLAG_SKSRVDOFUNC','UNITFLAG_CANBEATTACKED','UNITFLAG_SQGFXCHANGE']}
    skill_raw=(ROOT/'tools/reference/d2r-named-equipment/json/skills.json').read_bytes()
    assert hashlib.sha256(skill_raw).hexdigest()==parent['pinnedInputHashes']['skills']
    rows=json.loads(skill_raw)
    for key in ['311','313']:
        row=rows[key]
        assert all(row.get(flag)==1 for flag in ['InGame','enhanceable','interrupt'])
        assert not any(row.get(flag) for flag in ['intown','periodic','aura','usemanaondo'])
    assert rows['311']['srvstfunc']==53 and rows['311']['srvdofunc']==95 and not rows['311'].get('srvmissile')
    assert rows['313']['srvmissile']=='sentrylightningbolt' and not rows['313'].get('srvdofunc')
    mode_rows=json.loads((ROOT/parent['localModeEvidence']).read_text())['rows']
    assert mode_rows[9]['code']=='S2'
    assert all(r['mode']=='S2' and not r['dir'] and int(r['frame'])==int(i==9) and int(r['event'] or 0)==int(i==9)
               for i,r in enumerate(parent['currentSequence']['records']))
    base=(STAGE/'inferno-sentry-channel-source-probe.cpp').read_text().split('int main()',1)[0]
    def replace(old,new):
        nonlocal base
        assert base.count(old)==1,old;base=base.replace(old,new)
    def remove(name):
        nonlocal base
        m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\{[^\n]*\}\s*$',base,re.M);assert m,name
        base=base[:m.start()]+base[m.end():]
    replace('#include <algorithm>','#include <algorithm>\n#include <cstring>\n#define __forceinline inline')
    replace('UNITFLAG_SKSRVDOFUNC=32','UNITFLAG_SKSRVDOFUNC='+str(constants['UNITFLAG_SKSRVDOFUNC']))
    replace('UNITFLAG_CANBEATTACKED=16','UNITFLAG_CANBEATTACKED='+str(constants['UNITFLAG_CANBEATTACKED']))
    replace('struct D2GameStrc{int dwGameFrame=100;};','struct D2EventTimerQueueStrc;struct D2GameStrc{int dwGameFrame=100;D2EventTimerQueueStrc* pTimerQueue=nullptr;};')
    replace('struct D2UnitStrc{','struct D2AnimSeqTxt;struct D2EventTimerStrc;struct D2UnitStrc{D2AnimSeqTxt* pAnimSeq=nullptr;D2EventTimerStrc* timer=nullptr;unsigned dwSeqSpeed=256,dwSeqMode=9;int dwAnimMode=1,nActionFrame=0;')
    replace('int nMonAnim=0,dwCalc[4]={0,1,2,3};','unsigned dwFlags[2]={};int wSrvStartFunc=53,wSrvDoFunc=95,wSrvMissile=-1,wItemEffect=0,dwDelay=3,nAnim=18,nMonAnim=14,dwCalc[4]={0,1,2,3};')
    replace('struct Tables{int nMissilesTxtRecordCount=1000;}','struct Tables{int nMissilesTxtRecordCount=1000;D2MissilesTxt pMissilesTxt[1000];}')
    replace('struct D2SkillStrc{int param=0;}used;','struct D2SkillStrc{int param=0,id=311,rank=1,flags=0;}used;')
    replace('assert(formula>=0&&formula<3);return formulas[formula];','assert(formula>=0&&formula<4);return formula==3?0:formulas[formula];')
    remove('D2GAME_EVENTS_Delete_6FC34840');remove('EVENT_SetEvent')
    # New event operations need declarations before the already compiled do95.
    needle='int32_t SKILLS_UpdateInfernoAnimationParameters('
    at=base.index(needle)
    base=base[:at]+'void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int);\nvoid EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int);\n'+base[at:]
    replace('D2UnitStrc* SUNIT_AllocUnitData(int type,','std::vector<int> emissions;\nD2UnitStrc* SUNIT_AllocUnitData(int type,')
    replace('if(!allocated)return nullptr;created={};','if(!allocated)return nullptr;emissions.push_back(game.dwGameFrame);created={};')
    base+='\nconstexpr unsigned UNITFLAG_SQGFXCHANGE='+str(constants['UNITFLAG_SQGFXCHANGE'])+';\n'
    base+=r'''
using D2C_EventTypes=int;constexpr int UNIT_TYPES_COUNT=6,D2UnitInvalidGUID=-1,EVENTTYPE_COUNT=15,EVENTTYPE_AITHINK=2,EVENTTYPE_PERIODICSKILLS=8,STATE_UNINTERRUPTABLE=3;
constexpr int EVENTFLAG_0x1=1,EVENTFLAG_0x2=2,EVENTFLAG_0x4=4,EVENTFLAG_0x8=8;
using EventTimerCallback=int32_t(*)(D2GameStrc*,D2UnitStrc*,int,int32_t,int32_t);
using ExecuteEventCallback=void(*)(D2GameStrc*,D2UnitStrc*,int,int32_t,int32_t);
'''
    event_header='source/D2Game/include/GAME/Event.h'
    for name in ['D2EventTimerStrc','D2EventTimerSlabListStrc','D2EventTimerQueueStrc']:base+=block(event_header,name,'struct')
    base+=r'''
D2EventTimerStrc* SUNIT_GetTimerFromUnit(D2UnitStrc* u){return u?u->timer:nullptr;}
void SUNIT_SetTimerOnUnit(D2UnitStrc* u,D2EventTimerStrc* t){if(u)u->timer=t;}
D2EventTimerSlabListStrc* EVENT_AllocTimerSlab(D2GameStrc*){assert(false);return nullptr;}
void sub_6FCBDE90(D2UnitStrc*,int){assert(false);}
D2EventTimerStrc** sub_6FC353D0(D2GameStrc*,int,int);
void sub_6FC34890(D2GameStrc*,D2EventTimerStrc*);
void D2GAME_InitTimer_6FC351D0(D2GameStrc*,D2UnitStrc*,int,int,EventTimerCallback,int,int);
D2EventTimerStrc* EVENT_AllocateUnitTimer(D2GameStrc*,D2UnitStrc*);
void MONSTERMODE_EventHandler(D2GameStrc*,D2UnitStrc*,int,int,int);
'''
    event='source/D2Game/src/GAME/Event.cpp'
    event_text=source(event)
    assert event_text.index('pInfinite[nMonsterEventIdx], 0')<event_text.index('pHead[nMonsterEventIdx]')
    for name in ['EVENT_MapUnitTypeToIndex','sub_6FC353D0','sub_6FC34890','D2GAME_EVENTS_Delete_6FC34840','EVENT_AssignTimerToUnit','EVENT_AllocateTimer','EVENT_AllocateUnitTimer','D2GAME_InitTimer_6FC351D0','EVENT_SetEvent','sub_6FC35570','EVENT_ExecuteEventsImpl','EVENT_ExecuteMonsterEvents']:
        base+=block(event,name)
    # All source sequence bodies, with the current 14-entry source rows.
    base+=r'''
using D2AnimSeqEvent=int;constexpr int ANIMSEQ_EVENT_NONE=0;
struct D2AnimSeqTxt{unsigned nMode,nFrame;int nDir,nEvent;};D2AnimSeqTxt sequence[14];
D2AnimSeqTxt* DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc*){return sequence;}
int DATATBLS_GetSeqFramePointsCount(D2UnitStrc*){return 14*256;}int DATATBLS_GetSeqFrameCount(D2UnitStrc*){return 14;}
void UNITS_SetAnimActionFrame(D2UnitStrc*,int){assert(false);}int UNITS_GetFrameBonus(D2UnitStrc*){assert(false);return 0;}
'''
    for name in ['DATATBLS_ComputeSequenceAnimation','DATATBLS_GetSequenceEvent']:
        base+=block('source/D2Common/src/DataTbls/SequenceTbls.cpp',name)
    for name in ['UNITS_InitializeSequence','UNITS_StopSequence','UNITS_IsAtEndOfFrameCycle']:
        base+=block('source/D2Common/src/Units/Units.cpp',name)
    # Generic skill dispatch uses the actual skill flag enum, including bit31.
    base+=block('source/D2Common/include/DataTbls/SkillsTbls.h','D2C_SkillsTxtFlagIndices','enum')
    base+=r'''
using SkillDoFunc=int32_t(*)(D2GameStrc*,D2UnitStrc*,int32_t,int32_t);
SkillDoFunc gpSkillSrvDoFnTable_6FD40A20[160]={},gpSkillSrvStartFnTable_6FD408B0[70]={};
int SKILLS_GetFlags(D2SkillStrc* s){return s->flags;}void SKILLS_SetFlags(D2SkillStrc* s,int value){s->flags=value;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc* s,const char*,int){return s->id;}int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc* s,int){return s->rank;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return &used;}
void UNITS_SetUsedSkill(D2UnitStrc*,D2SkillStrc*){assert(false);}
void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int){assert(false);}
int sub_6FD10CE0(D2UnitStrc*){assert(false);return 0;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc* p){return p->x;}int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc* p){return p->y;}
int sub_6FD118C0(D2GameStrc*,D2UnitStrc*){assert(false);return 0;}
int D2GAME_SKILLMANA_Consume_6FD10A50(D2GameStrc*,D2UnitStrc*,int,int){return 0;}
void D2GAME_SKILLS_SetDelay_6FD11C00(D2GameStrc*,D2UnitStrc*,int){assert(false);}
D2UnitStrc* sub_6FD11710(D2GameStrc*,int,D2UnitStrc*,int,int,int,int,int,int,int){assert(false);return nullptr;}
int SKILL_ComputePeriodicRate(D2GameStrc*,D2UnitStrc*,D2SkillsTxt*,int,int){assert(false);return 0;}
constexpr int PLRMODE_TOWNNEUTRAL=1,PLRMODE_SEQUENCE=18;
'''
    skills='source/D2Game/src/SKILLS/Skills.cpp'
    for name in ['sub_6FD11420','D2GAME_SKILLS_Handler_6FD12BA0','sub_6FD13260','sub_6FD13410']:base+=block(skills,name)
    base+=r'''
struct D2ModeChangeStrc{D2UnitStrc* pUnit=nullptr;};int ended=-1,doCalls=0;
void SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc* u,int mode){u->dwAnimMode=mode;UNITS_InitializeSequence(u);}
int sub_6FD12FD0(D2GameStrc* g,D2UnitStrc* u){return used.id==311?SKILLS_SrvSt53_MonInferno(g,u,311,used.rank):1;}
void sub_6FC63680(D2GameStrc* g,D2UnitStrc* u){ended=g->dwGameFrame;u->dwAnimMode=1;D2GAME_EVENTS_Delete_6FC34840(g,u,0,0);D2GAME_EVENTS_Delete_6FC34840(g,u,1,0);}
int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){assert(false);return 0;}
'''
    mode='source/D2Game/src/MONSTER/MonsterMode.cpp'
    mode_text=source(mode)
    info=mode_text.split('constexpr D2MonModeInfoTableStrc stru_6FD28738[16] =',1)[1].split('};',1)[0]
    assert re.findall(r'\{\s*(\d),\s*(\d),\s*(\d)\s*\}',info)[14]==('1','0','0')
    assert 'else\n            {\n                sub_6FCBD3A0(pGame, pUnit);\n            }' in mode_text
    for name in ['sub_6FC64E60','sub_6FC64E90']:base+=block(mode,name)
    base+=block('source/D2Game/src/UNIT/SUnit.cpp','sub_6FCBD3A0')
    base+=r'''
void MONSTERMODE_EventHandler(D2GameStrc* g,D2UnitStrc* u,int type,int,int){if(type==0){doCalls++;sub_6FC64E90(g,u);}else if(type==1){sub_6FC63680(g,u);}else assert(false);}
'''
    # The trace and its independent timeline checks are appended below.
    build_trace(base,hashes,routines,constants,parent,check)


def build_trace(base,hashes,routines,constants,parent,check):
    base+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int rank:{1,20})for(int hard:{0,1,2,20})for(int failure:{-1,0,12,16,40})for(int kind:{0,1,2})for(int direct:{0,1}){
 reset();game={};game.dwGameFrame=25;D2EventTimerQueueStrc queue={};D2EventTimerSlabListStrc slab={};game.pTimerQueue=&queue;queue.pSlabListHead=&slab;
 for(int i=0;i<599;i++)slab.tEventTimersStorage[i].pNextFreeEventTimer=&slab.tEventTimersStorage[i+1];slab.pFreeEventTimerListHead=&slab.tEventTimersStorage[0];
 for(int i=0;i<14;i++)sequence[i]={9,unsigned(i==9),0,i==9?1:0};
 owner.dwUnitType=UNIT_MONSTER;owner.dwClassId=414;owner.dwUnitId=77;owner.dwFlags=UNITFLAG_SKSRVDOFUNC;owner.path.x=50;owner.path.y=70;selected=&target;
 used={};used.id=direct?313:311;used.rank=rank;haveUsed=haveModel=true;hasSkill=hasRecord=coordinates=allocated=true;inferno=false;emissions.clear();ended=-1;doCalls=0;
 formulas[0]=20+hard;formulas[1]=15+hard;formulas[2]=3;
 skill={};skill.dwFlags[0]=gdwBitMasks[SKILLSFLAGINDEX_INGAME]|gdwBitMasks[SKILLSFLAGINDEX_ENHANCEABLE]|gdwBitMasks[SKILLSFLAGINDEX_INTERRUPT];
 skill.wSrvStartFunc=direct?0:53;skill.wSrvDoFunc=direct?0:95;skill.wSrvMissile=direct?526:-1;skill.wSrvMissileA=523;
 row={};row.nVel=row.nMaxVel=12;row.wRange=50;row.wLevRange=0;row.dwDamageRate=82;
 gpSkillSrvDoFnTable_6FD40A20[95]=SKILLS_SrvDo095_MonInferno;gpSkillSrvStartFnTable_6FD408B0[53]=SKILLS_SrvSt53_MonInferno;
 D2ModeChangeStrc change{&owner};int started=sub_6FC64E60(&game,&change);sub_6FCBD3A0(&game,&owner);
 printf("%s{\"input\":[%d,%d,%d,%d,%d],\"started\":%d,\"deadline\":%d,\"trace\":[",first?"":",",rank,hard,failure,kind,direct,started,used.param);first=false;
 for(int frame=26;frame<=145;frame++){
  game.dwGameFrame=frame;if(failure>=0&&frame>=25+failure){if(kind==0){coordinates=false;selected=nullptr;owner.path.x=owner.path.y=0;}else if(kind==1)allocated=false;else inferno=false;}
  queue.nArrayIndex=frame%64;const int idx=EVENT_MapUnitTypeToIndex(UNIT_MONSTER);
  EVENT_ExecuteMonsterEvents(&game,&queue,queue.pInfinite[idx],0);EVENT_ExecuteMonsterEvents(&game,&queue,queue.pHead[idx][queue.nArrayIndex],1);
  int modeEvent=-2,endEvent=-2,timers=0;for(auto* t=owner.timer;t;t=t->pNext){assert(!(t->nFlags&(EVENTFLAG_0x1|EVENTFLAG_0x8)));timers++;if(t->nEventType==0){assert(modeEvent==-2);modeEvent=t->nExpireFrame;}else if(t->nEventType==1){assert(endEvent==-2);endEvent=t->nExpireFrame;}else assert(false);}assert(timers<=2);
  printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%zu,%d,%d,%d]",frame==26?"":",",frame,owner.dwSeqFrame,owner.dwFrameCountPrecise,owner.nActionFrame,!!(owner.dwFlags&UNITFLAG_SKSRVDOFUNC),owner.dwAnimMode,doCalls,ended,emissions.size(),creates,modeEvent,endEvent);
 }printf("],\"emissions\":[");for(size_t i=0;i<emissions.size();i++)printf("%s%d",i?",":"",emissions[i]);printf("]}");
}printf("]\n");}
'''
    path=STAGE/'inferno-sentry-controller-source-probe.cpp';path.write_text(base);binary=path.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'inferno-sentry-controller-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
    cases=json.loads(run.stdout);assert len(cases)==240
    for case in cases:
        rank,hard,failure,kind,direct=case['input']
        assert case['started']==1 and case['deadline']==(0 if direct else 40+hard)
        threshold=25+failure if failure>=0 else 10**6
        attempts=[];expected=[];failed=False
        if direct:
            attempts=[35]
            if threshold>35 or kind==2:expected=[35]
            end=40
        else:
            t=35
            while True:
                attempts.append(t);failed=t>=threshold and kind!=2
                if not failed:expected.append(t)
                if failed or t>=40+hard or t>=threshold and kind==2:break
                t+=3
            end=attempts[-1]+14
        assert case['emissions']==expected,(case['input'],case['emissions'],expected)
        assert len(case['trace'])==120
        for frame,point,remaining,action,flag,mode,calls,ended,emitted,creates,mode_event,end_event in case['trace']:
            count=sum(t<=frame for t in attempts)
            expected_creates=sum(t<=frame and (t<threshold or kind!=0) for t in attempts)
            assert emitted==sum(t<=frame for t in expected) and creates==expected_creates
            assert (mode,ended)==((1,end) if frame>=end else (14,-1))
            assert flag==int(direct and frame>=35)
            if direct:
                n=min(frame-25,14)
                want=(n%14*256,(14-n)*256,int(frame==34),min(frame-25,15),-1 if frame<40 else -2,-2)
            elif frame<35:
                n=frame-25;want=(n*256,(14-n)*256,int(frame==34),n,-1,-2)
            else:
                final=count==len(attempts)
                stop_failed=failed and final
                rem=4 if len(attempts)==1 else 8
                want=((10 if stop_failed else 9)*256,(rem if stop_failed else 9)*256,int(not stop_failed),9+count,attempts[count] if not final else -2,end if final and frame<end else -2)
            assert (point,remaining,action,calls,mode_event,end_event)==want,(case['input'],frame,(point,remaining,action,calls,mode_event,end_event),want)
    fixture=STAGE/'inferno-sentry-controller-source-probe.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',talentId='assassin.wakeofinferno',handlerEnabled=False,
        legacyCommit=parent['legacyCommit'],sourceHashes=hashes,compiledRoutineHashes=routines,sourceConstants=constants,
        pinnedInputHashes=parent['pinnedInputHashes'],currentSequence=parent['currentSequence'],
        parentChannelEvidence='tools/reference/inferno-sentry-channel-source-evidence.json',
        localModeEvidence=parent['localModeEvidence'],
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),contexts=240,framesPerContext=120,traceFrames=28800),
        inputs=dict(effectiveRanks=[1,20],hardWakeRanks=[0,1,2,20],failureOffsets=[-1,0,12,16,40],failureKinds=['coordinate acquisition','missile allocation','Inferno state removal'],control='NPC313 generic direct dispatcher, intentionally fed the same Inferno sequence; its actual Lightning Sentry sequence remains separate.'),
        established=[
            'Complete mode14 entry clears actual server-do bit0x40. Complete source sequence initialization/advance/end detection and current14-row Inferno sequence are executed together with NPC53/95, generic skill wrapper/dispatcher and complete missile factory.',
            'Complete monster event-list allocation/assignment, insertion, delete/unlink and execution bodies are composed. Active infinite mode callbacks are marked for deferred removal, then replaced by finite mode events. Each simulated monster phase uses the source infinite-before-current-finite-bucket ordering. No flag8/active timer or duplicate mode/end timer survives a phase.',
            'With entry at25 and source sequence speed256, initial action9 is consumed at35. Successful custom do95 leaves server-do bit clear, rewinds to8 then advances back to action9 with9 remaining frames. The sequence pauses between the requested3-frame callbacks; elapsed wall frames alone do not advance it.',
            'Hard Wake0 stores deadline40, emits35/38/41 and requests end55. Hard Wake20 stores60, emits35 through62 every3frames and requests end76. The final successful emission occurs on the first requested callback at/after the deadline, or on the first callback after Inferno state removal.',
            'Failed coordinate acquisition or allocation stops repetition and requests model14-frame end from that callback. Coordinate failure makes no factory attempt; allocation failure makes an attempt without an emission. A failed callback does not rewind the sequence; the remaining finite queue contains only the end event.',
            'Comparison NPC313 generic direct-missile dispatch sets server-do bit0x40 even when its target coordinates/allocation fail. With the intentionally shared Inferno sequence it dispatches once at35, then the unpaused sequence ends at40. This control distinguishes generic direct missiles from custom Inferno emission without claiming it is the real Lightning Sentry sequence.'
        ],
        services=[
            'Single-burst mode entry is invoked at declared frame25; start-dispatch service calls complete NPC53 directly. Full skill selection/AI101, outer monster event bridge and mode-end/neutral service are not compiled here. End service records the requested end and removes mode/end events; no neutral animation, next AI delay, shot debit or retirement claim follows.',
            'One600-record source-shaped timer slab is preallocated and source allocation/recycling runs within it; requesting slab growth asserts. Source event structs are compiled with native host layout; this is a source-control-flow proof, not a historical or modern packed ABI certificate.',
            'Current NPC311/313 flags are verified against the pinned skill rows. Actual Unit header flags are0x40/0x4/0x4000; inherited factory harness abstract32/16 aliases are replaced for this composition. The remaining unrelated service aliases do not certify modern numeric ABI.',
            'Room/target/path/damage-stat/seed/allocation services and formula values remain declared as bounded by the parent channel/factory proof. Failure inputs are synthetic controls. Beam motion, first-step/contact/damage, target turning and room/grid order are not exercised.',
            'No modern executable trace, native Inferno actor, saved channel state, learned placement/payment, final graphics, retirement or class completion is certified.'
        ],sanitizers='ASan/UBSan: exit0 and empty runtime stderr required; independent expected timeline checks every emitted frame and all28800 sequence/flag/queue snapshots.')
    out=ROOT/'tools/reference/inferno-sentry-controller-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Inferno controller evidence stale'
    else:out.write_text(encoded)
    print('Inferno controller:240 contexts/28800 trace frames, complete sequence/dispatcher/event-list composition; independent chronology and ASan/UBSan clean. AI/native beam/retirement remain separate.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
