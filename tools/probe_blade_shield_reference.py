#!/usr/bin/env python3
"""Execute pinned Blade Shield start/area/callback/periodic routines.

State allocation, formula evaluation, area selection, hit resolution and weapon
fill are explicit service fixtures. This proves orchestration, not those services
or equivalence to the current D2R binary.
"""
from pathlib import Path
import argparse, hashlib, json, re, subprocess
ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
p = argparse.ArgumentParser(); p.add_argument('--check', action='store_true'); args = p.parse_args()
tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
preflight = json.loads((ROOT / 'tools/reference/blade-shield-preflight.json').read_text())
row = preflight['sourceSkill']
assert (row['*Id'], row['srvstfunc'], row['srvdofunc'], row['SrcDam'], row['ResultFlags'], row['Param3']) == (277, 28, 54, 96, 32, 25)
hashes = {}; routines = {}
def extract(rel, name):
    raw = (STAGE / 'd2moo-pinned-source' / rel).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == rel)
    hashes[rel] = hashlib.sha256(raw).hexdigest(); source = raw.decode()
    match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', source, re.M); assert match, name
    end = source.index('{', match.start()) + 1; depth = 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}'); end += 1
    body = source[match.start():end]; routines[name] = hashlib.sha256(body.encode()).hexdigest(); return body
cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <string>
#define __fastcall
struct D2GameStrc{int dwGameFrame=0;};struct D2UnitStrc{int id=0;};struct D2ActiveRoomStrc{};
struct D2SkillsTxt{int dwAuraLenCalc=1,nAuraState=10,dwAuraRangeCalc=2,dwHitClass=3,dwAuraFilter=33667,wResultFlags=32,dwHitFlags=8,nSrcDam=96,dwPerDelay=3;unsigned dwFlags[2]={64,0};};
struct D2StatListStrc{int skill=0,rank=0;};struct D2CurseStrc{D2UnitStrc* pUnit=nullptr;D2UnitStrc* pTarget=nullptr;int nSkill=0,nSkillLevel=0,nStat=0,nDuration=0,nState=0;};
struct D2SkillStrc{};D2SkillStrc usedSkill;
struct D2DamageStrc{int dwHitFlags=0,dwHitClass=0,wResultFlags=0,physical=0,element=0;};
struct D2SrvDo142Strc{D2DamageStrc* pDamage=nullptr;int nResultFlags=0,nHitFlags=0,nSrcDam=0,nToHit=0;};
struct D2AuraCallbackStrc{D2GameStrc* pGame=nullptr;D2UnitStrc* pOwner=nullptr;void* pArgs=nullptr;};
struct Tables{int nStatesTxtRecordCount=200;} tables;auto* sgptDataTables=&tables;
constexpr int STAT_MODIFIERLIST_SKILL=1,STAT_MODIFIERLIST_LEVEL=2,DAMAGERESULTFLAG_SUCCESSFULHIT=1,EVENTTYPE_PERIODICSKILLS=9;
D2SkillsTxt row;D2StatListStrc stats;D2ActiveRoomStrc room;unsigned gdwBitMasks[32];
bool hasRow=true,hasSkill=true,factory=true,town=false;int missile=553,duration=3000,progressive=0,delay=25,count=0,hitMask=7;
int removed=0,allocated=0,auraWrites=0,gfx=0,physicalRolls=0,elementRolls=0,toHitCalls=0,areas=0,hits=0,fills=0,combats=0,wear=0,range=0;
int deletedEvents=0,setEvents=0,eventFrame=0,eventKey=0,eventRank=0;std::string order;
bool usedPresent=true,rightPresent=true,statePresent=true,listPresent=true,dead=false;int effectiveRank=3,doCalls=0,doRank=0,doResult=1;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}
void* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return hasSkill?&stats:nullptr;}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int){return missile;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return formula==1?duration:formula==2?6:delay;}
void sub_6FD11C90(D2UnitStrc*,int,int){removed++;order+='R';}
D2StatListStrc* sub_6FD10EC0(D2CurseStrc* c){allocated++;order+='A';assert(c->pUnit==c->pTarget&&c->nSkill==277&&c->nStat==-1&&c->nDuration==duration&&c->nState==row.nAuraState);return factory?&stats:nullptr;}
void sub_6FCFE0E0(D2UnitStrc*,D2StatListStrc*,D2SkillsTxt*,int,int){auraWrites++;order+='W';}
void STATLIST_SetStatIfListIsValid(D2StatListStrc* s,int stat,int value,int){if(stat==1)s->skill=value;else s->rank=value;order+='S';}
void STATES_ToggleGfxStateFlag(D2UnitStrc*,int,int){gfx++;order+='G';}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}int DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return town;}
int SKILLS_EvaluateProgressiveSkillCalc(D2UnitStrc*,int,int){return progressive;}
void D2GAME_RollPhysicalDamage_6FD14EC0(D2UnitStrc*,D2DamageStrc* d,int,int){physicalRolls++;d->physical=101;order+='P';}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc* d,int,int){elementRolls++;d->element=17;order+='E';}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int){toHitCalls++;return 123;}
void sub_6FD15650(D2GameStrc*,D2UnitStrc*,D2UnitStrc* t,int flags,int ar,D2DamageStrc* d,int melee){assert(melee==1&&d->physical==101&&d->element==17);assert(ar==((flags&1)?0:123));hits++;d->wResultFlags=flags;if((flags&1)||(hitMask&(1<<t->id)))d->wResultFlags|=1;}
void SUNITDMG_FillDamageValues(D2GameStrc*,D2UnitStrc*,D2UnitStrc* t,D2DamageStrc* d,int a,int fraction){assert(a==0&&fraction==row.nSrcDam);fills++;d->physical+=1000+t->id;}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc* t,D2DamageStrc* d,int fraction){assert(fraction==128&&d->physical==101+(row.nSrcDam?1000+t->id:0)&&d->dwHitFlags==(2|row.dwHitFlags)&&d->dwHitClass==row.dwHitClass);combats++;}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){wear++;}
using Callback=int32_t(*)(D2AuraCallbackStrc*,D2UnitStrc*);
void sub_6FD0FE80(D2GameStrc* g,D2UnitStrc* owner,int,int,int r,int filter,Callback callback,void* args,int,const char*,int){assert(filter==33667);areas++;range=r;D2AuraCallbackStrc aura{g,owner,args};for(int i=0;i<count;i++){D2UnitStrc target{i};assert(callback(&aura,&target)==1);}}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int key){deletedEvents++;eventKey=key;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int frame,int key,int rank){setEvents++;assert(eventKey==key);eventFrame=frame;eventKey=key;eventRank=rank;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return usedPresent?&usedSkill:nullptr;}D2SkillStrc* UNITS_GetRightSkill(D2UnitStrc*){return rightPresent?&usedSkill:nullptr;}
int SKILLS_GetSkillIdFromSkill(D2SkillStrc*,const char*,int){return 277;}int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int){return effectiveRank;}
int SUNIT_IsDead(D2UnitStrc*){return dead;}int STATES_CheckState(D2UnitStrc*,int){return statePresent;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return listPresent?&stats:nullptr;}
int STATLIST_GetStatValue(D2StatListStrc* s,int stat,int){return stat==STAT_MODIFIERLIST_LEVEL?s->rank:s->skill;}
int D2GAME_SKILLS_Handler_6FD12BA0(D2GameStrc*,D2UnitStrc*,int id,int rank,int a,int b,int c){assert(id==277&&a==1&&b==0&&c==0);doCalls++;doRank=rank;return doResult;}
void reset(){row={};stats={};hasRow=hasSkill=factory=true;town=false;missile=553;duration=3000;progressive=0;delay=25;count=0;hitMask=7;removed=allocated=auraWrites=gfx=physicalRolls=elementRolls=toHitCalls=areas=hits=fills=combats=wear=range=0;deletedEvents=setEvents=eventFrame=eventKey=eventRank=0;order.clear();}
'''
ass = 'source/D2Game/src/SKILLS/SkillAss.cpp'; skills = 'source/D2Game/src/SKILLS/Skills.cpp'
for name in ['SKILLS_SrvSt28_BladeShield', 'SKILLS_AuraCallback_SrvDo142', 'SKILLS_SrvDo142_Unused', 'SKILLS_SrvDo054_BladeShield']:
    cpp += extract(ass, name) + '\n'
for name in ['SKILL_ComputePeriodicRate', 'sub_6FD13260', 'sub_6FD13410', 'D2GAME_MONSTERS_AiFunction09_6FD13470']:
    cpp += extract(skills, name) + '\n'
cpp += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;D2GameStrc game;D2UnitStrc owner;int starts=0,pulses=0,schedules=0,initials=0,dispatches=0;
for(int record:{0,1})for(int known:{0,1})for(int m:{-1,0,553})for(int dur:{-1,0,3000})for(int state:{-1,10,200})for(int make:{0,1})for(int rank:{1,3,20,255}){
 reset();hasRow=record;hasSkill=known;missile=m;duration=dur;row.nAuraState=state;factory=make;
 bool eligible=record&&known&&m>0&&dur>0&&state>=0&&state<200;int result=SKILLS_SrvSt28_BladeShield(&game,&owner,277,rank);
 assert(result==(eligible&&make));assert(removed==eligible&&allocated==eligible&&auraWrites==(eligible&&make)&&gfx==(eligible&&make));
 assert(order==(eligible?(make?"RAWSSG":"RA"):""));if(result)assert(stats.skill==277&&stats.rank==rank);starts++;
}
for(int record:{0,1})for(int known:{0,1})for(int state:{-1,10,200})for(int inTown:{0,1})for(int n:{0,1,3})for(int mask:{0,2,7})for(int autoHit:{0,1})for(int source:{0,96})for(int overrideRange:{0,9}){
 reset();hasRow=record;hasSkill=known;row.nAuraState=state;town=inTown;count=n;hitMask=mask;row.wResultFlags=32|autoHit;row.nSrcDam=source;progressive=overrideRange;
 bool valid=record&&known&&state>=0&&state<200;bool pulse=valid&&!inTown;int result=SKILLS_SrvDo054_BladeShield(&game,&owner,277,20);int successes=0;for(int i=0;i<n;i++)successes+=autoHit||bool(mask&(1<<i));
 assert(result==valid&&areas==pulse&&physicalRolls==pulse&&elementRolls==pulse&&hits==(pulse?n:0));
 assert(toHitCalls==(pulse&&!autoHit)&&fills==(pulse&&source?successes:0)&&combats==(pulse?successes:0)&&wear==combats);if(pulse)assert(range==(overrideRange?overrideRange:6));pulses++;
}
for(int record:{0,1})for(int flags:{0,32,64,96})for(int frame:{0,1,24,25,26,100000})for(int d:{-1,0,5,6,25,100})for(int aura:{0,1}){
 reset();hasRow=record;row.dwFlags[0]=flags;game.dwGameFrame=frame;delay=d;int result=sub_6FD13260(&game,&owner,277,21,aura);bool ok=record&&flags;
 assert(result==ok&&deletedEvents==ok&&setEvents==ok);if(ok)assert(eventFrame==frame+(d<=5?5:d)&&eventKey==(aura?-1:277)&&eventRank==(aura?0:21));schedules++;
}
for(int used:{0,1})for(int rank:{1,3,21})for(int result:{0,1}){
 reset();usedPresent=used;effectiveRank=rank;doResult=result;doCalls=doRank=0;sub_6FD13410(&game,&owner);
 assert(doCalls==used&&setEvents==used);if(used)assert(doRank==rank&&eventRank==rank);initials++;
}
for(int bits=0;bits<128;bits++)for(int state:{-1,10,200})for(int flags:{32,64})for(int captured:{0,1,20})for(int current:{3,21})for(int key:{-1,0,277})for(int result:{0,1}){
 reset();hasRow=bits&1;hasSkill=bits&2;statePresent=bits&4;listPresent=bits&8;rightPresent=bits&16;dead=bits&32;
 row.nAuraState=state;row.dwFlags[0]=flags;stats.skill=(bits&64)?277:99;stats.rank=captured;effectiveRank=current;doResult=result;doCalls=doRank=0;
 D2GAME_MONSTERS_AiFunction09_6FD13470(&game,&owner,key,0);
 bool positive=key>0;bool call=positive?(hasRow&&state>=0&&state<200&&statePresent&&listPresent&&hasSkill&&stats.skill==277):key==-1&&rightPresent&&hasRow&&(flags&32)&&!dead;
 assert(doCalls==call&&setEvents==call&&deletedEvents==(key!=0));if(call)assert(doRank==(positive?captured:current)&&eventRank==(positive?captured:0));dispatches++;
}
printf("{\"startCases\":%d,\"areaCases\":%d,\"periodicCases\":%d,\"initialDispatchCases\":%d,\"recurringDispatchCases\":%d}\n",starts,pulses,schedules,initials,dispatches);}
'''
source = STAGE / 'blade-shield-source-probe.cpp'; source.write_text(cpp)
binary = STAGE / 'blade-shield-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(source),'-o',str(binary)],check=True)
counts = json.loads(subprocess.check_output([str(binary)]))
subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(source),'-o',str(binary)+'-san'],check=True)
with (STAGE/'blade-shield-sanitizer.log').open('wb') as log:
    sanitized = subprocess.check_output([str(binary)+'-san'],stderr=log)
assert json.loads(sanitized)==counts and not (STAGE/'blade-shield-sanitizer.log').read_bytes()
evidence = dict(referenceVersion='3.3.93847',legacyCommit=preflight['legacyCommit'],sourceHashes=hashes,routineHashes=routines,verification=counts,
    scope='Unchanged complete start28, do54, area do142, per-victim callback, periodic scheduler and initial/recurring dispatchers executed with explicit service fixtures; ASan/UBSan clean.',
    findings=['Start validates learned skill, missile, duration and state before replacing the self-state; failed allocation can follow removal.',
              'One skill physical/elemental roll per area pulse is copied separately to every victim; hit and live weapon fill/durability are per victim.',
              'Pinned result32 does not force successful-hit. Successful callbacks use SrcDam96 in weapon fill, then combat allocation128; the cosmetic attachment Half2HSrc is not consulted.',
              'Town do54 returns success without rolling or selecting victims. The minimum periodic delay is5; pinned25 schedules at current frame+25 for the tested non-overflow range.',
              'Initial used-skill dispatch uses current effective rank. Recurring positive-key dispatch retains the modifier-list cast rank and requires its matching skill/state/list; it reschedules even when the supplied do-handler returns failure. The right-aura branch instead refreshes current rank.'],
    limits=['Not the modern D2R binary; current table fields are pinned separately.','Hit resolution, full weapon fill, formula evaluation, target selection, state allocation and event services remain fixtures, not newly verified implementations.','Does not verify player cast-start payment, animation/initial pulse ordering, actual state expiry or native input.','Signed arithmetic overflow at late game frames is outside the tested scheduling domain.'],handlerEnabled=False)
path = ROOT/'tools/reference/blade-shield-source-evidence.json'
if args.check:
    assert json.loads(path.read_text())==evidence
else:
    path.write_text(json.dumps(evidence,indent=2)+'\n')
print(json.dumps(counts), 'complete pinned routines; sanitizers clean; service boundaries explicit')
