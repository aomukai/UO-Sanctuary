#!/usr/bin/env python3
"""Execute complete source knockback entry/completion and frozen event dispatch."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/telekinesis-motion-source'


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def probe(check=False):
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes={}
    def read(rel):
        p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=sha(p);return raw.decode()
    monster=read('source/D2Game/src/MONSTER/MonsterMode.cpp');unit=read('source/D2Game/src/UNIT/SUnit.cpp');events=read('source/D2Game/include/GAME/Event.h')
    bodies={n:function(monster,n) for n in ('sub_6FC64B60','sub_6FC64CD0','MONSTERMODE_EventHandler')};bodies['SUNIT_IsDead']=function(unit,'SUNIT_IsDead')
    event_enum=re.search(r'enum D2C_EventTypes\s*:\s*uint8_t\s*\{.*?\};',events,re.S).group()
    cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <array>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,MONMODE_DEATH=0,MONMODE_DEAD=12,PLRMODE_DEATH=0,PLRMODE_DEAD=17,MONMODE_GETHIT=3,MONSTER_SANDLEAPER1=78;
constexpr int PATHTYPE_KNOCKBACK_SERVER=10,STATE_STUNNED=1,STATE_FREEZE=2;
struct D2GameStrc{int dwGameFrame=100;}game;struct D2DynamicPathStrc{int type=-1,distance=-1,resets=0,updates=0;}path;
struct D2UnitStrc{int dwUnitType=1,dwClassId=0,dwAnimMode=1,dwLastHitClass=109,dwFlags=0;D2DynamicPathStrc* pDynamicPath=&path;}unit;
struct D2ModeChangeStrc{D2UnitStrc* pUnit=&unit;int nMode=-1;unsigned char unk0x14[2]={};}change;
struct D2MonStats2Txt{unsigned dwModeFlags=0;}modes;struct D2MonStatsTxt{int nBaseId=0;}row;
struct Tables{D2MonStatsTxt* pMonStatsTxt=&row;}tables;auto* sgptDataTables=&tables;
unsigned gdwBitMasks[32];bool hasRow=true,stunned=false,frozen=false;int combatMode=-1,used=-1,requested=-1,targetByte=-1,eventFrame=-1,eventType=-1,calls=0;
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return &modes;}D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return hasRow?&row:nullptr;}
void PATH_SetType(D2DynamicPathStrc* p,int n){p->type=n;}void D2COMMON_10190_PATH_SetDistance(D2DynamicPathStrc* p,int n){p->distance=n;}
void D2Common_10142(D2DynamicPathStrc* p,D2UnitStrc*,int){p->updates++;}void SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc*,int n){combatMode=n;}
void PATH_ResetToPreviousType(D2DynamicPathStrc* p){p->resets++;}bool STATES_CheckState(D2UnitStrc*,int n){return n==STATE_STUNNED?stunned:n==STATE_FREEZE?frozen:false;}
void UNITS_SetUsedSkill(D2UnitStrc*,int n){used=n;}void D2GAME_ModeChange_6FC65220(D2GameStrc*,D2ModeChangeStrc* p,int){requested=p->nMode;targetByte=p->unk0x14[1];}
'''+event_enum+r'''
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int,int){eventType=type;eventFrame=frame;}
using MonEventFunc=void(*)(D2GameStrc*,D2UnitStrc*,int,int);std::array<MonEventFunc,EVENTTYPE_COUNT>gpMonsterEventFunctions;
void Callback(D2GameStrc*,D2UnitStrc*,int,int){calls++;}
'''
    cpp+='\n'+bodies['SUNIT_IsDead']+'\n'+'\n'.join(b for n,b in bodies.items() if n!='SUNIT_IsDead')
    cpp+=r'''
int main(){for(int n=0;n<32;n++)gdwBitMasks[n]=1u<<n;gpMonsterEventFunctions.fill(Callback);printf("{\"mode\":[");bool first=true;
for(int mask:{0,1<<3,1<<13,(1<<3)|(1<<13)})for(int record:{0,1})for(int mode:{0,1,12})for(int sand:{0,1})for(int stun:{0,1})for(int freeze:{0,1}){
path={};unit={};change={};unit.dwAnimMode=mode;row.nBaseId=sand?78:0;modes.dwModeFlags=mask;hasRow=record;stunned=stun;frozen=freeze;combatMode=used=requested=targetByte=eventFrame=eventType=-1;
int ret=sub_6FC64B60(&game,&change);int type=path.type,distance=path.distance,updates=path.updates;
sub_6FC64CD0(&game,&unit);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",mask,record,mode,sand,stun,freeze,ret,type,distance,updates,combatMode,path.resets,used,requested,targetByte,eventFrame,unit.dwLastHitClass);first=false;
}printf("],\"events\":[");first=true;
for(int freeze:{0,1})for(int dead:{0,1})for(int n=-1;n<=EVENTTYPE_COUNT;n++){
frozen=freeze;unit.dwAnimMode=dead?0:1;calls=0;MONSTERMODE_EventHandler(&game,&unit,(D2C_EventTypes)n,0,0);
bool exempt=n==EVENTTYPE_STATREGEN||n==EVENTTYPE_TRAP||n==EVENTTYPE_RESET||n==EVENTTYPE_PERIODICSKILLS||n==EVENTTYPE_REMOVESTATE;
printf("%s[%d,%d,%d,%d,%d]",first?"":",",freeze,dead,n,int(exempt),calls);first=false;
}printf("],\"eventCount\":%d}\n",int(EVENTTYPE_COUNT));}
'''
    STAGE.mkdir(exist_ok=True);path=STAGE/'probe.cpp';path.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
    data=json.loads(subprocess.check_output([str(binary)]));assert len(data['mode'])==192
    for r in data['mode']:
        mask,record,mode,sand,stun,freeze=r[:6];start=bool(mask&(1<<3) and mask&(1<<13));move=start and mode!=0
        assert r[6:12]==[int(start),10 if move else -1,(10 if sand and record else 5) if move else -1,int(move),13 if move else -1,1],r
        if mask&(1<<3):
            assert r[12:]==([-1,-1,-1,145 if stun else 115,109] if sand else [0,3,100,-1,160]),r
        else:assert r[12:]==[-1,-1,-1,101,109],r
    for freeze,dead,event,exempt,calls in data['events']:
        assert calls==int(0<=event<data['eventCount'] and (not freeze or dead or exempt))
    san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
    with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==data
    assert not log.read_bytes();cases=STAGE/'cases.json';cases.write_text(json.dumps(data,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],modeCases=len(data['mode']),eventCases=len(data['events']),sanitizers='ASan/UBSan clean',sourceHashes=hashes,
                routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},
                established=['Complete entry requires GH+KB modes and sets server-knockback path/distance5 (10 for mapped Sand Leaper) without a local freeze gate. Full outer mode-change/path services remain declared.',
                             'Complete ordinary completion resets path, clears used skill, sets last hit class160 and requests GH. Sand Leaper instead schedules AI at15/45frames; no-GH schedules1frame. This special completion must not be replaced with generic recovery.',
                             'Complete event dispatcher suppresses ordinary live-frozen events but continues statregen/trap/reset/periodic-skill/remove-state and permits dead-unit events. This supports separate motion/status clocks; complete dynamic-path freeze timing is not executed.'],
                declaredServices=['Mode/path/room/animation/AI callback and stat/state storage are explicit services, not a full dynamic-path or native-world simulation. Ordinary post-KB GH uses existing native FHR/AI adapters; Sand Leaper remains unsupported in Telekinesis until its AI completion is adapted.',
                                  'Native prepared trajectories pause while the host reports frozen/paralyzed; exact modern initial dispatch, sub-tile rounding and frozen path/animation cadence remain open. Historical trajectories keep their original policy.'],
                artifacts={str(p.relative_to(ROOT)):sha(p) for p in (Path(__file__).resolve(),path,binary,san,log,cases)})
    out=ROOT/'tools/reference/telekinesis-motion-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print(f'Telekinesis motion:{len(data["mode"])}complete entry/completion and{len(data["events"])}complete event-dispatch cases;ASan/UBSan clean;full dynamic path/current engine remain separate.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');probe(p.parse_args().check)
