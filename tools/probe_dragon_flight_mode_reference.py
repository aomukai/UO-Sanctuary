#!/usr/bin/env python3
"""Compose Flight mode entry, full rate dispatch and the accepted SQ21 scheduler."""
import argparse
import csv
import hashlib
import json
import re
import subprocess
from pathlib import Path
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'dragon-flight-mode'


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(exist_ok=True)
    prior=json.loads((ROOT/'tools/reference/dragon-flight-source-evidence.json').read_text())
    original=BASE/'dragon-flight-source/source.cpp'
    assert sha(original)==prior['artifacts'][str(original.relative_to(ROOT))]
    cpp=original.read_text().split('int main(){',1)[0]
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha']==prior['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs={str(original.relative_to(ROOT)):sha(original)};bodies={}
    def source(rel):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    units=source('source/D2Common/src/Units/Units.cpp')
    sunits=source('source/D2Game/src/UNIT/SUnit.cpp')
    seq=source('source/D2Common/src/DataTbls/SequenceTbls.cpp')
    modes=source('source/D2Game/src/PLAYER/PlrModes.cpp')
    monster=source('source/D2Common/include/D2Monsters.h')
    flags=source('source/D2Common/include/D2Skills.h')
    stats=source('source/D2Common/include/DataTbls/MonsterTbls.h')
    ids=source('source/D2Common/include/DataTbls/MonsterIds.h')
    assert re.search(r'MONMODE_BLOCK,\s*//0x06',monster) and re.search(r'MONMODE_KNOCKBACK,\s*//0x0D',monster)
    assert 'SKFLAG_STUN = 0x00001000' in flags
    index=re.search(r'enum D2C_MonStatsTxtFlagIndices\s*\{(.*?)\};',stats,re.S).group(1)
    assert re.findall(r'\bMONSTATSFLAGINDEX_\w+',index).index('MONSTATSFLAGINDEX_NPC')==8
    values={};value=-1
    for name,explicit in re.findall(r'^\s*(MONSTER_\w+)(?:\s*=\s*(\w+))?\s*,',ids,re.M):
        value=values[explicit] if explicit in values else int(explicit,0) if explicit else value+1
        values[name]=value
    assert values['MONSTER_FIRST_EXPANSION']==410
    owned=ROOT/'.local/research/d2r-3.3.93847/skills.txt'
    with owned.open(encoding='utf-8-sig') as f:row=next(r for r in csv.DictReader(f,delimiter='\t') if r.get('skill')=='Dragon Flight')
    assert row['UseAttackRate']=='' and row['seqtrans']=='A1' and row['seqnum']=='21' and row['anim']=='SQ'
    inputs[str(owned.relative_to(ROOT))]=sha(owned)
    cached=ROOT/'tools/reference/d2r-named-equipment/json/skills.json'
    assert json.loads(cached.read_text())['275'].get('UseAttackRate',0)==0
    inputs[str(cached.relative_to(ROOT))]=sha(cached)
    def replace(old,new):
        nonlocal cpp
        assert cpp.count(old)==1,old;cpp=cpp.replace(old,new,1)
    replace('#define __fastcall','#include <algorithm>\n#include <limits>\n#define __forceinline\n#define __fastcall')
    replace('struct Anim{int pFrameFlags[144]={};}', 'struct Anim{int dwAnimSpeed=256,dwFrames=23;int pFrameFlags[144]={};}')
    replace('int dwSeqFrame=0,nSeqCurrentFramePrecise=0,','int nActionFrame=0,dwSeqMode=0;void* pInventory=nullptr;int dwSeqFrame=0,nSeqCurrentFramePrecise=0,')
    replace('struct D2SkillsTxt{int dwAuraRangeCalc=7,','struct D2SkillsTxt{int nSkillId=275,nSeqTrans=7;unsigned dwFlags[1]={};int dwAuraRangeCalc=7,')
    replace('struct D2SkillStrc{} skill;','struct D2SkillStrc{D2SkillsTxt* pSkillsTxt=&row;unsigned dwFlags=0;} skill;')
    cpp+=r'''
using BOOL=int;using D2AnimSeqEvent=int;
constexpr int TRUE=1,FALSE=0,NUMBER_OF_PLRMODES=20,NUMBER_OF_MONMODES=16,UNIT_ITEM=4,UNIT_TILE=5;
constexpr int PLRMODE_SEQUENCE=18,PLRMODE_CAST=10,PLRMODE_BLOCK=9,PLRMODE_GETHIT=4,PLRMODE_KNOCKBACK=19;
constexpr int MONMODE_SEQUENCE=14,MONMODE_CAST=7,MONMODE_BLOCK=6,MONMODE_GETHIT=3,MONMODE_KNOCKBACK=13,MONMODE_WALK=2,MONMODE_RUN=15;
constexpr int MONSTER_FIRST_EXPANSION=410,MONSTATSFLAG_NPC=1<<8,SKFLAG_PASSIVE=1,SKFLAG_STUN=0x1000,SKILLSFLAG_USEATTACKRATE=1<<19,UNITFLAG_SQGFXCHANGE=0x4000,ANIMSEQ_EVENT_NONE=0;
struct D2AnimUnkStrc{BOOL bCanUseVelocityModifierByPassiveSkill,bCanUseVelocityModifier,bCanUseSkillAttackRate,bCanUseAttackRate,bAllowAnimSpeed;};
struct D2MonStatsTxt{unsigned dwMonStatsFlags=0;};
template<class T>T D2Clamp(T x,T lo,T hi){return std::clamp(x,lo,hi);}
int otherRate=100,ias=0,fcr=0,attackRate=100,attackUpdates=0,castUpdates=0,startCalls=0,deletes=0,animationReads=0;
bool sequenceFound=true;
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){assert(id==275);return hasRow?&row:nullptr;}
D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int){std::abort();}
void FOG_Trace(const char*,...){std::abort();}
void UNITS_SetAnimData(D2UnitStrc* p,int,int,int){p->pAnimData=&anim;animationReads++;}
Anim* DATATBLS_GetAnimDataRecord(D2UnitStrc*,int,int,int,void*){animationReads++;return &anim;}
void UNITS_UpdateCastAnimRateAndVelocity(D2UnitStrc* p){castUpdates++;p->dwSeqSpeed=p->wAnimSpeed=768;}
void UNITS_UpdateAttackAnimRateAndVelocity(D2UnitStrc* p){attackUpdates++;p->dwSeqSpeed=p->wAnimSpeed=512;}
void UNITS_UpdateBlockAnimRateAndVelocity(D2UnitStrc*){std::abort();}
void UNITS_UpdateGetHitAnimRateAndVelocity(D2UnitStrc*){std::abort();}
void UNITS_UpdateKnockbackAnimRateAndVelocity(D2UnitStrc*,int,int,int){std::abort();}
void UNITS_UpdateRunWalkAnimRateAndVelocity(D2UnitStrc*,int,int,int,const char*,int){std::abort();}
constexpr int STAT_OTHER_ANIMRATE=334;
// The old probe's progressive-AR supplier remains unchanged. This declared
// wrapper supplies OTHER_ANIMRATE to the newly compiled Other writer only.
int OtherStat(D2UnitStrc*,int stat,int){assert(stat==STAT_OTHER_ANIMRATE);return otherRate;}
D2AnimSeqTxt* DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc* p){return sequenceFound&&p->dwAnimMode==18?sequence:nullptr;}
int DATATBLS_GetSeqFramePointsCount(D2UnitStrc*){return 23*256;}
int DATATBLS_GetSeqFrameCount(D2UnitStrc*){return 23;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int custom){assert(custom==0);deletes++;std::erase_if(events,[&](auto e){return e.type==type;});}
void SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc* p,int mode){p->dwAnimMode=mode;}
void UNITS_SetTargetUnitForDynamicUnit(D2UnitStrc*,D2UnitStrc* p){hasTarget=p!=nullptr;}
int sub_6FD12FD0(D2GameStrc* g,D2UnitStrc* p){startCalls++;return SKILLS_SrvSt12_Telekinesis_DragonFlight(g,p,275,20);}
'''
    # Keep all three original modulator arrays, including negative branch data.
    for name in ('gaPlayerModesAnimModulators','gaMonModesAnimModulators','gaMonModesAnimModulatorsExpansion'):
        start=units.index(('static ' if name=='gaPlayerModesAnimModulators' else '')+'D2AnimUnkStrc '+name+'[')
        block=units[start:units.index('};',start)+2]
        cpp+='\n'+block
        bodies[name]=block
    def add(text,name):
        nonlocal cpp
        body=function(text,name);bodies[name]=body;cpp+='\n'+body
    for name in ('UNITS_IsSeqAnimSpeedModulatedByFCR','UNITS_IsAnimModeBlocking','UNITS_IsAnimModeGetHit','UNITS_IsAnimModeKnockBack',
                 'UNITS_CanAnimModeUseVelocityModifier','UNITS_CanAnimModeUseAttackRate','UNITS_AnimModeAllowsAnimSpeed','UNITS_SetAnimationSpeed'):
        add(units,name)
    # Supply a distinct stat service without changing a byte in the body.
    cpp+='\n#define STATLIST_UnitGetStatValue OtherStat\n'
    add(units,'UNITS_UpdateOtherAnimRateAndVelocity')
    cpp+='\n#undef STATLIST_UnitGetStatValue\n'
    add(units,'UNITS_UpdateAnimRateAndVelocity')
    add(seq,'DATATBLS_ComputeSequenceAnimation')
    add(units,'UNITS_InitializeSequence')
    add(sunits,'sub_6FCBC4D0')
    add(sunits,'D2GAME_DeletePlayerPerFrameEvents_6FCBCE50')
    add(modes,'PLRMODE_StartID_AttackCastThrowKickSpecialSequence')
    cpp+=r'''
int main(){printf("[");comma=false;
for(int attackFlag:{0,1})for(int transition:{7,10})for(int usedSkill:{0,1})for(int base:{128,208,256,32767})for(int other:{-100,15,100,175,250})for(int itemIas:{0,120,10000})for(int castRate:{0,200}){
 reset();anim.dwAnimSpeed=base;otherRate=other;ias=itemIas;fcr=castRate;attackUpdates=castUpdates=startCalls=deletes=animationReads=0;
 owner.dwSeqSpeed=913;owner.dwSeqFrame=999;owner.nActionFrame=8;owner.dwFrameCountPrecise=777;owner.dwFlags=UNITFLAG_SKSRVDOFUNC;
 row.dwFlags[0]=attackFlag?SKILLSFLAG_USEATTACKRATE:0;row.nSeqTrans=transition;used=usedSkill;
 events.push_back({EVENTTYPE_MODECHANGE,999,1,99});events.push_back({EVENTTYPE_ENDANIM,999,0,0});
 PLRMODE_StartID_AttackCastThrowKickSpecialSequence(&game,&owner,18,&target);
 if(comma)printf(",");comma=true;printf("[");list({attackFlag,transition,usedSkill,base,other,itemIas,castRate});printf(",");
 list({owner.dwSeqSpeed,owner.wAnimSpeed,owner.dwSeqFrameCount,owner.dwFrameCountPrecise,owner.dwSeqFrame,owner.dwSeqMode,owner.nActionFrame,(int)owner.dwFlags,attackUpdates,castUpdates,startCalls,deletes,animationReads});printf(",[");
 for(size_t i=0;i<events.size();i++){auto e=events[i];if(i)printf(",");list({e.type,e.frame-100,e.event,e.param});}printf("]]\n");
}printf("]\n");}
'''
    generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
    with (STAGE/'compiler.log').open('w') as log:
        subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()
    (STAGE/'raw-cases.json').write_bytes(run.stdout)
    data=json.loads(run.stdout);assert len(data)==960,len(data)
    for args,result,events in data:
        flag,transition,used,base,other,ias,fcr=args
        cast=bool(used and transition==10);attack=bool(used and not cast and flag)
        speed=768 if cast else 512 if attack else 256
        displayed=speed if cast or attack else min(32767,base*max(15,min(175,other))//100)
        assert result==[speed,displayed,23*256,23*256,0,10,0,16384,int(attack),int(cast),1,2,2],(args,result)
        assert events==[[1,(9*256+speed-1)//speed,1,0],[1,(14*256+speed-1)//speed,1,1],[2,(23*256+speed-1)//speed,0,0]],(args,events)
    cases=STAGE/'cases.json';cases.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',talentId='assassin.dragonflight',legacyCommit=tree['sha'],
        currentExecutableCodeParity=False,handlerEnabled=False,installed=False,
        currentFields=dict(anim='SQ',seqtrans='A1',seqnum=21,UseAttackRate=0),inputs=inputs,
        reusedSourceEvidence='tools/reference/dragon-flight-source-evidence.json',
        compiledUnchangedRoutineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},
        cases=960,flightClockCases=sum(a[0]==0 and a[1]==7 and a[2]==1 for a,_,_ in data),
        compiledRoutineCount=len(bodies)-3,modulatorArrays=3,sanitizers='ASan+UBSan clean',
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in (generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',cases,Path(__file__).resolve())},
        established=['Owned current row and cached data leave UseAttackRate disabled and select SQ21/A1. Complete source targeted mode entry initializes256sequence advance,clears old mode/end events,schedules both ordinals,clears do64,and only then calls the declared start service.',
                     'Complete initialization and full rate dispatcher leave sequence speed256 for Flight. Other-animation rate changes wAnimSpeed only;it does not change dwSeqSpeed. Thus the older reference projection requests teleport9,kick14,end23 regardless of supplied animation base/OTHER_ANIMRATE. IAS/FCR services are not read in this selected branch.',
                     'Actual source player mode modulators require UseAttackRate for SQ attack-speed dispatch. Explicit flag-on and SC-transition controls select the declared attack/cast writers,proving this is not a missing dispatcher call. Missing-used-skill controls remain on the Other branch.'],
        services=['Identity mode conversion selects an ordinary human Assassin. Current sequence rows and the older scheduler/frame getter/ordinal bridge are reused from the separately hashed full-body probe.',
                  'Animation lookup supplies bases128/208/256/32767;the current animation record lookup is not compiled. Only wAnimSpeed can depend on that base in this selected Other branch. The lookup is observed twice because full dispatcher repeats it after SetAnimData.',
                  'Positive attack/cast controls are observed services writing512/768,not full rate math. IAS/FCR input columns are supplied but unread by Flight. No dual-wield,shapeshift,passive/stun movement or current executable equivalence is claimed.',
                  'Start-service seam invokes complete Flight start12 directly;generic start/payment/used-skill validation and failed-start cleanup are not substituted or certified. Native action/landing/contact still require separate integration.'],
        remaining=['Adopt the bounded table/older-engine fixed clock in the native prepared supplier and verify actual two-event plans;keep current-code parity explicitly unverified.',
                   'Flight landing/payment policy remains unanswered and Tail-specific policy must not be silently extended. No learned Flight cast/input/save acceptance or final assets are claimed.'])
    out=ROOT/'tools/reference/dragon-flight-mode-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Dragon Flight mode reference stale'
    else:out.write_text(encoded)
    print('Dragon Flight:960 complete mode-entry/rate-dispatch contexts;fixed256 advance/actions9,14/end23 in selected Flight branch;ASan+UBSan clean;modern code parity unclaimed.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
