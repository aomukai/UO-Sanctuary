#!/usr/bin/env python3
"""Compile unchanged Charge callbacks, player event gate, rewind and velocity routines.

All world/path/combat/packet stubs are declared. Legacy mana rank-one disagreement
is recorded rather than promoted over the pinned modern tables.
"""
from pathlib import Path
import hashlib
import json
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT/'.local/class-skills'
COMMIT = '0c0bbd1e6bef9181a3da1c046523af622a925416'
tree_path = STAGE/'d2moo-source-tree.json'
assert hashlib.sha256(tree_path.read_bytes()).hexdigest() == '5f4e5eae5648ab16176939d390482b2009f7b5e156f2f517411b56b4ffcd6915'
tree = json.loads(tree_path.read_text())
assert tree['sha'] == COMMIT and not tree['truncated']
entries = {r['path']: r['sha'] for r in tree['tree']}
sources = {
    'pal': (STAGE/'SkillPal.cpp.reference', 'source/D2Game/src/SKILLS/SkillPal.cpp'),
    'skills': (ROOT/'.local/research/d2moo-Skills.cpp', 'source/D2Game/src/SKILLS/Skills.cpp'),
    'player': (ROOT/'.local/research/d2moo-PlrModes.cpp', 'source/D2Game/src/PLAYER/PlrModes.cpp'),
    'unit': (ROOT/'.local/research/d2moo-SUnit.cpp', 'source/D2Game/src/UNIT/SUnit.cpp'),
    'seq': (ROOT/'.local/research/d2moo-SequenceTbls.cpp', 'source/D2Common/src/DataTbls/SequenceTbls.cpp'),
    'path': (ROOT/'.local/research/d2moo-Path.cpp', 'source/D2Common/src/Path/Path.cpp'),
    'misc': (STAGE/'PathMisc.cpp.charge-reference', 'source/D2Common/src/Path/PathMisc.cpp'),
    'step': (STAGE/'Step.cpp.charge-reference', 'source/D2Common/src/Path/Step.cpp'),
    'msg': (STAGE/'SUnitMsg.cpp.charge-reference', 'source/D2Game/src/UNIT/SUnitMsg.cpp'),
}
for key, upstream in {
    'playerstats':'source/D2Game/src/PLAYER/PlayerStats.cpp',
    'playersave':'source/D2Game/src/PLAYER/PlrSave2.cpp',
    'units':'source/D2Common/src/Units/Units.cpp',
    'statlist':'source/D2Common/src/D2StatList.cpp',
    'commonSkills':'source/D2Common/src/D2Skills.cpp',
    'damage':'source/D2Game/src/UNIT/SUnitDmg.cpp',
    'monsterMode':'source/D2Game/src/MONSTER/MonsterMode.cpp',
}.items():
    sources[key]=(STAGE/'d2moo-pinned-source'/upstream,upstream)
for path, upstream in sources.values():
    blob = path.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(blob)).encode()+b'\0'+blob).hexdigest() == entries[upstream], path


def routine(key, name):
    text = sources[key][0].read_text()
    at = text.index(name+'(')
    start = text.rfind('\n',0,at)+1
    end = text.index('{',at)+1
    depth = 1
    while depth:
        depth += (text[end]=='{')-(text[end]=='}')
        end += 1
    return text[start:end]


seq_text = sources['seq'][0].read_text()
start = seq_text.index('D2AnimSeqTxt gPlayerSequenceCharge[15]')
sequence = seq_text[start:seq_text.index('};',start)+2]
cpp = r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <string>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_ASSERT(x)
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_ITEM=4,MONMODE_ATTACK1=4,MONMODE_ATTACK2=5,MONMODE_SKILL1=8,PLRMODE_ATTACK1=4,PLRMODE_RUN=3;
constexpr int MONSTER_CLAWVIPER1=74,MONSTER_DURIEL=211,MONSTER_REANIMATEDHORDE1=439,STATE_UNINTERRUPTABLE=1,STATE_SKILL_MOVE=2,STATE_BLOOD_MANA=3;
constexpr int STAT_VELOCITYPERCENT=0,STAT_MANA=1,STAT_ITEM_CHARGED_SKILL=2,EVENTTYPE_MODECHANGE=1,EVENTTYPE_ENDANIM=2,UNITFLAG_SKSRVDOFUNC=64;
constexpr int ANIMSEQ_EVENT_MELEE_ATTACK=1,ANIMSEQ_EVENT_NONE=0,ANIMSEQ_EVENT_PLAY_SOUND=3;
struct D2GameStrc{int dwGameFrame=100;};
struct D2AnimSeqTxt{int wSequence,nMode,nFrame,nDir,nEvent;};
struct D2DynamicPathStrc{int velocity=0,dwAcceleration=0,dwAccelerationsCounter=0,dwMaxVelocity=0,dwVelocity=0;struct{int nX=4096,nY=0;}tDirectionVector;struct{int nX=0,nY=0;}tVelocityVector;};
struct Anim{uint8_t pFrameFlags[144]={};};
struct D2UnitStrc{int dwUnitType=0,dwClassId=3,dwUnitId=100,dwFlags=0,nSeqCurrentFramePrecise=0,dwSeqSpeed=256,dwSeqFrameCount=15*256,wAnimSpeed=256,dwFrameCountPrecise=15*256,velocityPercent=0,mana=200*256,usedId=107;D2DynamicPathStrc* pDynamicPath=nullptr;D2AnimSeqTxt* pAnimSeq=nullptr;Anim* pAnimData=nullptr;bool inRange=false,dead=false,uninterruptible=false,skillMove=false;};
struct D2SkillStrc{int p[4]={},flags=0,id=107;};
struct D2SkillsTxt{int dwParam[8]={150,0,100,25,0,0,0,20},dwCalc[4]={0,1,2,3},nEType=0,nSrcDam=128,wMana=9,wLevelMana=0,nManaShift=8,wMinMana=1;};
struct D2MonStatsTxt{int nRun=10,nBaseId=0;};struct D2CharStatsTxt{int nRunSpeed=9;};
struct D2DamageStrc{int wResultFlags=0,dwEnDmgPct=0,dwConvPct=0,nConvType=0,dwHitClass=0;};
struct D2ModeChangeStrc{D2UnitStrc* pTargetUnit=nullptr;};struct D2StatListStrc{};
struct Event{int type,frame,event,param;};
struct Tables{int nShiftedStuff=63,nStuff=6;} tables;Tables* sgptDataTables=&tables;
D2SkillStrc used,basic;D2SkillsTxt row;D2UnitStrc target,alternative;D2MonStatsTxt monster;D2CharStatsTxt character;
bool hasRow=true,hasSkill=true,hasTarget=true,hasAlternative=false,hasCharRow=true,hasSequence=true,pathFinished=false;
int basicCalls=0,pathCalls=0,allocations=0,drains=0,elements=0,overlays=0,stops=0,lastRewind=-1,hitFlag=1,lastTarget=-1,lastMode=-1,doCalls=0;D2DamageStrc damage;std::vector<Event> events;
int32_t SKILLS_SrvDo067_Charge(D2GameStrc*,D2UnitStrc*,int32_t,int32_t);
void D2GAME_SKILLS_RewindSkillEx_6FCBD260(D2GameStrc*,D2UnitStrc*,int32_t);
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return hasSkill?&used:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return hasTarget?&target:nullptr;}
int UNITS_IsInMeleeRange(D2UnitStrc*,D2UnitStrc* p,int){return p->inRange;}
void SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc*,int n){lastMode=n;}
void UNITS_SetUsedSkill(D2UnitStrc* p,int){p->usedId=-1;}
D2SkillStrc* SKILLS_GetSkillById(D2UnitStrc*,int,int){basic.id=0;return &basic;}
int D2GAME_PLAYERMODE_Change_6FC81A00(D2GameStrc*,D2UnitStrc* p,D2SkillStrc* skill,int,int,int,int){p->usedId=skill->id;basicCalls++;return 1;}
void MONSTERMODE_GetModeChangeInfo(D2UnitStrc*,int,D2ModeChangeStrc*){}
int D2GAME_ModeChange_6FC65220(D2GameStrc*,D2ModeChangeStrc*,int){basicCalls++;return 1;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return &monster;}
int STATES_CheckState(D2UnitStrc* p,int state){return state==STATE_UNINTERRUPTABLE?p->uninterruptible:0;}
void STATES_ToggleState(D2UnitStrc* p,int state,int enabled){if(state==STATE_SKILL_MOVE)p->skillMove=enabled;}
void SKILLS_SetParam1(D2SkillStrc* p,int n){p->p[0]=n;}void SKILLS_SetParam2(D2SkillStrc* p,int n){p->p[1]=n;}void SKILLS_SetParam4(D2SkillStrc* p,int n){p->p[3]=n;}
int SKILLS_GetParam1(D2SkillStrc* p){return p->p[0];}int SKILLS_GetParam2(D2SkillStrc* p){return p->p[1];}
void SKILLS_SetFlags(D2SkillStrc* p,int n){p->flags=n;}int SKILLS_GetFlags(D2SkillStrc* p){return p->flags;}
void D2COMMON_10204_PATH_ClearPoint2(D2DynamicPathStrc*){}void PATH_SetNewDistance(D2DynamicPathStrc*,int){}
D2CharStatsTxt* PLRSAVE2_GetCharStatsTxtRecord(int){return hasCharRow?&character:nullptr;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_MANA?p->mana:p->velocityPercent;}
int MONSTERUNIQUE_CalculatePercentage(int value,int percent,int divisor){return int(int64_t(value)*percent/divisor);}
void PATH_SetVelocity(D2DynamicPathStrc* p,int n,const char*,int){p->velocity=p->dwVelocity=n;}
int D2Common_10142(D2DynamicPathStrc*,D2UnitStrc*,int){pathCalls++;return 1;} // Marked BROKEN in the immutable reconstruction; never treated as path proof.
D2AnimSeqTxt* DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc* p){return hasSequence?p->pAnimSeq:nullptr;}
int DATATBLS_GetSeqFrameCount(D2UnitStrc*){return 15;}
D2UnitStrc* SUNIT_GetServerUnit(D2GameStrc*,int type,int id){return hasTarget&&type==target.dwUnitType&&id==target.dwUnitId?&target:nullptr;}
void sub_6FCC63D0(D2UnitStrc* p,int16_t){p->skillMove=false;stops++;} // Original helper clears skill_move and queues packet A5.
D2UnitStrc* sub_6FD107F0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int){return hasAlternative?&alternative:nullptr;}
int SUNIT_IsDead(D2UnitStrc* p){return p->dead;}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int){return 335;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int){return hitFlag;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int){return calc==0?1375:0;}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*,int,int){elements++;}
void sub_6FC627B0(D2UnitStrc*,int){}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc* t,D2DamageStrc* p,int){damage=*p;allocations++;lastTarget=t->dwUnitId;}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){drains++;}
void UNITS_SetOverlay(D2UnitStrc*,int overlay,int){overlays=overlay;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int){std::erase_if(events,[&](auto e){return e.type==type;});}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int event,int param){events.push_back({type,frame,event,param});}
void UNITS_SetAnimationFrame(D2UnitStrc*,int){}
void sub_6FCBC7E0(D2GameStrc*,D2UnitStrc*){}
int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){return pathFinished?2:1;}
void sub_6FD13410(D2GameStrc* game,D2UnitStrc* p){doCalls++;SKILLS_SrvDo067_Charge(game,p,107,20);}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc* p,const char*,int){return p->id;}
int SKILLS_GetOwnerGUIDFromSkill(D2SkillStrc*){return -1;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int){return 20;}
int D2GAME_SKILLS_BloodMana_6FD025E0(D2UnitStrc*,int){return 0;}
void STATLIST_AddUnitStat(D2UnitStrc* p,int,int delta,int){p->mana+=delta;}
int ITEMMODS_GetItemCharges(D2UnitStrc*,int,int,int*,D2StatListStrc**){return 0;}
int STATLIST_GetStatValue(D2StatListStrc*,int,int){return 0;}
void STATLIST_SetBaseStat2(D2StatListStrc*,int,int,int,D2UnitStrc*){}
void* SUNIT_GetClientFromPlayer(D2UnitStrc*,const char*,int){return nullptr;}
void D2GAME_PACKETS_SendPacket0x3E_6FC3EC20(void*,D2UnitStrc*,int,int,int,int){}
int UNITS_GetFrameBonus(D2UnitStrc*){return 0;}
'''
cpp += 'struct SequenceStorage{D2AnimSeqTxt precedingZero{};\n'+sequence+'\n} chargeSequenceStorage;\n#define gPlayerSequenceCharge chargeSequenceStorage.gPlayerSequenceCharge\n'
cpp += routine('seq','DATATBLS_GetSequenceEvent')+'\n'
cpp += routine('unit','D2GAME_SKILLS_RewindSkillEx_6FCBD260')+'\n'
cpp += routine('unit','sub_6FCBCE70')+'\n'
cpp += routine('skills','sub_6FD150A0')+'\n'
cpp += routine('pal','SKILLS_SrvSt31_Charge')+'\n'
cpp += routine('pal','SKILLS_SrvDo067_Charge')+'\n'
cpp += routine('player','sub_6FC80EE0')+'\n'
cpp += routine('misc','sub_6FD5CEB0')+'\n'
cpp += routine('skills','D2GAME_SKILLMANA_Consume_6FD10A50')+'\n'
cpp += r'''
void reset(D2UnitStrc& p,D2DynamicPathStrc& path){p={};path={};p.pDynamicPath=&path;p.pAnimSeq=gPlayerSequenceCharge;used={};used.id=107;target={};target.dwUnitType=1;target.dwUnitId=10;alternative={};alternative.dwUnitType=1;alternative.dwUnitId=20;alternative.inRange=true;hasRow=hasSkill=hasTarget=hasCharRow=hasSequence=true;hasAlternative=pathFinished=false;basicCalls=pathCalls=allocations=drains=elements=overlays=stops=doCalls=0;lastRewind=lastTarget=lastMode=-1;damage={};events.clear();hitFlag=1;}
int main(){D2GameStrc game;D2UnitStrc owner;D2DynamicPathStrc path;bool first=true;
printf("{\"startCases\":[");
for(int guard:{0,1,2,3,4,5,6})for(int velocity:{-50,0,49,50,100,250}){reset(owner,path);owner.velocityPercent=velocity;
if(guard==1)hasRow=false;if(guard==2)hasSkill=false;if(guard==3)owner.pDynamicPath=nullptr;if(guard==4)target.inRange=true;if(guard==5)hasTarget=false;if(guard==6)target.uninterruptible=true;
int ok=SKILLS_SrvSt31_Charge(&game,&owner,107,20);if(!first)printf(",");first=false;
printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",guard,velocity,ok,used.p[0],used.p[1],used.p[3],path.velocity,used.flags,int(owner.skillMove),basicCalls,pathCalls);}
printf("],\"phaseCases\":[");first=true;
for(int flags:{0,1,3,4097})for(int situation:{0,1,2,3,4,5}){reset(owner,path);used.flags=flags;used.p[0]=1;used.p[1]=10;owner.skillMove=true;owner.nSeqCurrentFramePrecise=93*256;
if(situation==1)target.inRange=true;if(situation==2)hasTarget=false;if(situation==3){hasTarget=false;hasAlternative=true;}if(situation==4)owner.dead=true;if(situation==5){hasTarget=false;hasAlternative=true;alternative.inRange=false;}
int ok=SKILLS_SrvDo067_Charge(&game,&owner,107,20);if(!first)printf(",");first=false;
printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",flags,situation,ok,used.flags,used.p[0],used.p[1],int(owner.skillMove),allocations,lastTarget,damage.wResultFlags,damage.dwEnDmgPct,stops);}
printf("],\"impactCases\":[");first=true;
for(int hit:{0,1,16}){reset(owner,path);used.p[0]=1;used.p[1]=10;target.inRange=true;hitFlag=hit;int ok=SKILLS_SrvDo067_Charge(&game,&owner,107,20);if(!first)printf(",");first=false;
printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d]",hit,ok,allocations,damage.wResultFlags,damage.dwEnDmgPct,damage.dwHitClass,elements,drains,overlays);}
printf("],\"gateCases\":[");first=true;
for(int done:{0,1})for(int flag:{0,64})for(int event:{1,2,3}){reset(owner,path);used.flags=1;used.p[0]=1;used.p[1]=10;target.inRange=true;owner.dwFlags=flag;pathFinished=done;int ok=sub_6FC80EE0(&game,&owner,event,0);if(!first)printf(",");first=false;
printf("[%d,%d,%d,%d,%d,%d,%d]",done,flag,event,ok,doCalls,used.flags,allocations);}
printf("],\"rewindCases\":[");first=true;
for(int advance:{128,256,341,448})for(int point:{0,7,8}){reset(owner,path);owner.dwSeqSpeed=advance;game.dwGameFrame=100;D2GAME_SKILLS_RewindSkillEx_6FCBD260(&game,&owner,point);if(!first)printf(",");first=false;printf("[%d,%d,[",advance,point);bool sep=false;for(auto e:events){printf("%s[%d,%d,%d]",sep?",":"",e.type,e.frame-100,e.event);sep=true;}printf("]]");}
printf("],\"velocityCases\":[");first=true;
for(int percent:{-100,0,50,100,250,500})for(int x:{0,2896,4096})for(int y:{0,2896,4096}){reset(owner,path);owner.velocityPercent=percent;SKILLS_SrvSt31_Charge(&game,&owner,107,20);path.tDirectionVector={x,y};sub_6FD5CEB0(&path,1024);if(!first)printf(",");first=false;printf("[%d,%d,%d,%d,%d,%d]",percent,x,y,path.dwVelocity,path.tVelocityVector.nX,path.tVelocityVector.nY);}
printf("],\"legacyManaCases\":[");first=true;
for(int level:{1,2,20})for(int close:{0,1}){reset(owner,path);target.inRange=close;int start=SKILLS_SrvSt31_Charge(&game,&owner,107,level);used.id=owner.usedId;int before=owner.mana;int ok=D2GAME_SKILLMANA_Consume_6FD10A50(&game,&owner,107,level);if(!first)printf(",");first=false;printf("[%d,%d,%d,%d,%d,%d]",level,close,start,owner.usedId,ok,before-owner.mana);}
printf("],\"flowCases\":[");first=true;
for(int arrival:{1,2,7,8,15})for(int speed:{128,256,448}){reset(owner,path);owner.dwSeqSpeed=speed;game.dwGameFrame=100;sub_6FCBCE70(&game,&owner);SKILLS_SrvSt31_Charge(&game,&owner,107,20);int hitAt=-1,endAt=-1;
for(int frame=101;frame<200;frame++){game.dwGameFrame=frame;target.inRange=frame>=100+arrival;bool ended=false;
while(true){auto it=std::find_if(events.begin(),events.end(),[&](auto e){return e.frame==frame;});if(it==events.end())break;Event e=*it;events.erase(it);
if(e.type==EVENTTYPE_ENDANIM){endAt=frame-100;ended=true;break;}int before=allocations;sub_6FC80EE0(&game,&owner,e.event,0);if(allocations>before)hitAt=frame-100;}
if(ended)break;}
if(!first)printf(",");first=false;printf("[%d,%d,%d,%d,%d,%d]",arrival,speed,allocations,hitAt,endAt,int(owner.dwFlags&64));}
printf("]}\n");}
'''
path = STAGE/'charge-source-probe.cpp'
path.write_text(cpp)
binary = STAGE/'charge-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data = json.loads(subprocess.check_output([str(binary)]))
for guard, velocity, ok, kind, guid, frame, speed, flags, moving, basic, paths in data['startCases']:
    if guard in (1,2,3): assert (ok,paths,basic)==(0,0,0)
    elif guard == 4: assert (ok,basic,paths,moving)==(1,1,0,0)
    else:
        assert (ok,frame,speed,flags,moving,basic,paths)==(1,7,9*256*(150+max(50,velocity))//100,4097,1,0,1)
        assert (kind,guid)==((6,0) if guard in (5,6) else (1,10))
for percent,x,y,velocity,vx,vy in data['velocityCases']:
    assert velocity==9*256*(150+max(50,percent))//100
    magnitude=1024*velocity>>6
    assert (vx,vy)==(magnitude*x>>12,magnitude*y>>12)
for level,close,start,used,ok,cost in data['legacyManaCases']:
    assert (start,used,ok,cost)==(1,0 if close else 107,1,256 if level==1 else 2304)
for hit,ok,allocations,flags,damage,hitclass,elements,drains,overlay in data['impactCases']:
    assert (ok,allocations,flags,damage,hitclass,elements,drains,overlay)==(1,1,hit|8,1375,112,1,1,147)
for flags,situation,ok,after,kind,guid,moving,allocations,target,hit,damage,stops in data['phaseCases']:
    if flags == 0:
        expected_target = 10 if situation==1 else 20 if situation==3 else -1
        assert (ok,after,kind,guid,moving,allocations,target,hit,damage,stops)==(
            0 if situation==2 else 1,0,0,0,0,int(expected_target!=-1),expected_target,
            9 if expected_target!=-1 else 0,1375 if expected_target!=-1 else 0,1)
    elif flags in (1,4097):
        reached=situation==1
        assert (ok,after,kind,guid,moving,allocations,target,hit,damage,stops)==(1,0 if reached else 1,1,10,0 if reached else 1,0,-1,0,0,int(reached))
    else:
        assert (ok,after,kind,guid,moving,allocations,target,hit,damage,stops)==(int(situation in (1,3)),0,1,20 if situation==3 else 10,0,0,-1,0,0,1)
for done,unit_flag,event,ok,calls,flags,allocations in data['gateCases']:
    called=done==1 or unit_flag==0 and event in (1,2)
    assert (ok,calls,flags,allocations)==(1,int(called),0 if called else 1,0)
assert next(events for speed,point,events in data['rewindCases'] if (speed,point)==(256,8))==[[1,1,1],[1,2,3],[1,3,1],[2,7,0]]
for speed,point,events in data['rewindCases']:
    assert events[-1]==[2,max(2,((15-point)*256+speed-1)//speed),0]
    assert all(kind==1 and 1<=frame<=events[-1][1] and event in (1,3) for kind,frame,event in events[:-1])
for arrival,speed,allocations,hit,end,done in data['flowCases']:
    assert (allocations,hit,done)==(1,arrival+1,64)
    # Arrival rewinds to8; impact one frame later rewinds again to7 before committing damage.
    assert end==arrival+1+max(2,(8*256+speed-1)//speed)
(STAGE/'charge-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
evidence = {'sourceCommit':COMMIT,'referenceVersion':'3.3.93847','sourceHashes':{str(path.relative_to(ROOT)):hashlib.sha256(path.read_bytes()).hexdigest() for path,_ in sources.values()},'probe':data,
    'sourceTreesVerifiedAgainstGitBlobs':True,
    'officialSupplementarySource':'https://classic.battle.net/diablo2exp/skills/paladin-combat.shtml',
    'derivedContracts':{'playerSequence':4,'movingFlag':1,'pathFinishedFlag':2,'serverDoFlag':64,'arrivalRewind':8,'impactRewind':7,'impactFramesAfterReachedRange':1,'impactCountPerSourceEventFlow':1,'impactEndFramesAfterHitAtSpeed256':8,'knockbackResultFlag':8,'impactOverlayId':147,'manaFromModernTable256':2304},
    'limits':['World/path initialization, collision, damage allocation, AR/block, formula and packet delivery are explicit stubs.','D2Common_10142 is explicitly marked BROKEN in the pinned legacy reconstruction; the callback probe is not path correctness evidence.','Legacy mana helper consumes only MinMana at rank1, contradicting pinned mana9 and official documentation; do not port that discrepancy silently.','Close-range fallback changes the used skill to basic attack; the legacy caller still selects Charge mana. Exact modern fallback/input behavior needs comparison.','Per-frame velocity and sequence/event/gating primitives do not certify a modern executable, geometry or live client.','Supplied direction vectors, path-finished signals and arrival frames are explicit inputs; the composed event trace does not simulate real world movement.','Earliest modern input eligibility may differ from scheduled ENDANIM; compare both before class acceptance.','The raw legacy rewind can request sequence index-1 at rewind0. The probe supplies a preceding zero record; modern lookup/boundary behavior is unverified and the unsafe lookup must not be copied into game code.']}
evidence['sourceInspection']={
    'playerVelocityBase':100,
    'creation':'PlayerStats.cpp initializes STAT_VELOCITYPERCENT to100; PlrSave2.cpp restores100.',
    'modeTransition':'UNITS_ChangeAnimMode deactivates temporary stat lists before initialization. Run/knockback temporary velocity is not retained by ordinary SQ Charge.',
    'sequence':'Normal unshifted player ConvertMode preserves SQ. UNITS_InitializeSequence starts speed256; Charge lacks UseAttackRate.',
    'velocitySources':'The staged Charge consumer includes armor/shield penalty, Vigor, Blaze and slow effects. Item faster-run/walk belongs to the ordinary walking/running helper; approved second-wind running-only bonus is excluded.',
    'knockback':'SUnitDmg checks monstats2 mKB; monster knockback entry checks mGH/mKB and requests a five-subtile path, ten for sand leapers. Stun boss/velocity rules and a one-tile displacement are not substitutes.',
    'boundary':'Source inspection supplements168 compiled cases; it is not an additional compiled initialization/mode/knockback case count or modern capture.',
}
(ROOT/'tools/reference/charge-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print('Charge original-source probes: '+', '.join(f'{len(rows)} {key}' for key,rows in data.items()))
