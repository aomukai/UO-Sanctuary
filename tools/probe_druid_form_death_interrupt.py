#!/usr/bin/env python3
"""Execute complete older form death/removal and interruption gates; no life-floor claim."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import subprocess

from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    prior_path = ROOT / 'tools/reference/druid-form-modes-engine-evidence.json'
    prior = json.loads(prior_path.read_text())
    original = BASE / 'druid-form-modes-source-probe.cpp'
    assert sha(original) == prior['generatedSourceSha256']
    cpp = original.read_text().split('bool first=true;\nvoid emit', 1)[0]
    routines = dict(prior['completeRoutineHashes'])
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == prior['legacyCommit']
    preflight_path = ROOT / 'tools/reference/druid-forms-preflight.json'
    preflight = json.loads(preflight_path.read_text())
    skill_path = ROOT / '.local/research/d2r-3.3.93847' / preflight['localTables']['skills']['filename']
    assert sha(skill_path) == preflight['localTables']['skills']['sha256']
    bear = next(row for row in csv.DictReader(skill_path.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t') if row['skill'] == 'Wearbear')
    assert [bear[key] for key in ['aurastat4', 'aurastatcalc4', 'Param6']] == ['skill_concentration', 'par6', '100']
    hashes = {}

    def source(relative):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(row['sha'] for row in tree['tree'] if row['path'] == relative)
        hashes[relative] = sha(path)
        return raw.decode()

    def extract(relative, name):
        body = function(source(relative), name)
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    def enum(relative, name):
        match = re.search(r'enum ' + name + r'\s*\{.*?\};', source(relative), re.S)
        assert match, name
        return match.group() + '\n'

    def replace(before, after):
        nonlocal cpp
        assert cpp.count(before) == 1, before
        cpp = cpp.replace(before, after, 1)

    declarations = r'''
#define __stdcall
#define D2_ASSERT assert
using BOOL=int;constexpr int TRUE=1,FALSE=0;
'''
    declarations += enum('source/D2Common/include/Units/Player.h', 'D2C_PlayerModes')
    declarations += enum('source/D2Common/include/D2States.h', 'D2C_States')
    declarations += enum('source/D2Common/include/DataTbls/SkillsTbls.h', 'D2C_SkillsTxtFlagIndices')
    replace('PLRMODE_CAST=10,', '')
    replace('STATE_SKILLDELAY=121,', '')
    replace('#define __fastcall', '#define __fastcall\n' + declarations)
    replace('struct D2UnitStrc;struct D2StatListStrc;', 'struct D2UnitStrc;struct D2StatListStrc;using D2StatListExStrc=D2StatListStrc;')
    replace('wMana=15,wLevelMana=0,nManaShift=8;', 'wMana=15,wLevelMana=0,nManaShift=8;unsigned dwFlags[2]={};')
    replace('int nStatesTxtRecordCount=150;', 'int nTransformStates=2,pTransformStates[2]={139,140};int nStatesTxtRecordCount=150;')
    replace('int dwFlags=0,dwUnitId=42,dwUnitType=0,dwClassId=5;', 'int dwFlags=0,dwUnitId=42,dwUnitType=0,dwClassId=5,dwAnimMode=PLRMODE_NEUTRAL,pSeed=0,life=0;')
    cpp += r'''
constexpr int STAT_SKILL_CONCENTRATION=0,STAT_HITPOINTS=11,UNITFLAG_TARGETABLE=2,CLIENTSAVEFLAG_DEAD=1;
constexpr int EVENTTYPE_ENDANIM=2,EVENTTYPE_PERIODICSKILLS=3,EVENTTYPE_PERIODICSTATS=4;
unsigned gdwBitMasks[32];int endFrame=100,roll=0,rngCalls=0;
struct D2ClientStrc{}client;
int SKILLS_GetSkillIdFromSkill(D2SkillStrc*,const char*,int){return form?228:223;}
unsigned ITEMS_RollRandomNumber(int*){rngCalls++;return roll;}
int EVENT_GetEventFrame(D2GameStrc*,D2UnitStrc*,int type){assert(type==EVENTTYPE_ENDANIM);return endFrame;}
void sub_6FC817D0(D2GameStrc*,D2UnitStrc*p,D2SkillStrc*s,int mode,int x,int y,int force)
{assert(!s&&mode==PLRMODE_NEUTRAL&&!x&&!y&&force==1);p->dwAnimMode=mode;events.push_back({20,mode,0});}
void PLAYER_ApplyDeathPenalty(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){events.push_back({21,0,0});}
void SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc*p,int mode){p->dwAnimMode=mode;events.push_back({22,mode,0});}
D2ClientStrc* SUNIT_GetClientFromPlayer(D2UnitStrc*,const char*,int){return &client;}
void CLIENTS_ToggleFlag(D2ClientStrc*,int flag,int on){assert(flag==CLIENTSAVEFLAG_DEAD&&on);events.push_back({23,flag,on});}
void sub_6FCBC4D0(D2UnitStrc*){events.push_back({24,0,0});}
void STATLIST_SetUnitStat(D2UnitStrc*p,int stat,int n,int layer){assert(stat==STAT_HITPOINTS&&n==0&&layer==0);p->life=n;events.push_back({25,stat,n});}
void D2Common_10469(D2UnitStrc*){events.push_back({26,0,0});}
void STATES_UpdateStayDeathFlags(D2UnitStrc*,int on){assert(!on);events.push_back({27,0,0});}
void D2GAME_SetClientDead_6FC33830(D2ClientStrc*,void*p){assert(!p);events.push_back({28,0,0});}
void PATH_SetUnitDeadCollision(D2UnitStrc*,int on){assert(on);events.push_back({29,0,0});}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int param){assert(!param);events.push_back({30,type,0});}
void D2GAME_DeletePlayerPerFrameEvents_6FCBCE50(D2GameStrc*,D2UnitStrc*){events.push_back({31,0,0});}
void sub_6FCBCE70(D2GameStrc*,D2UnitStrc*){events.push_back({32,0,0});}
D2UnitStrc* AIGENERAL_GetMinionOwner(D2UnitStrc*){assert(false);return nullptr;}
void ITEMS_DropPlayerEar(D2GameStrc*,D2UnitStrc*){assert(false);}
void D2GAME_PLRMSG_Last_6FC89450(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){assert(false);}
void ARENA_ProcessKillEvent(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){assert(false);}
void PLAYER_StopInteractions(D2GameStrc*,D2UnitStrc*){events.push_back({33,0,0});}
'''
    cpp += extract('source/D2Common/src/D2Skills.cpp', 'SKILLS_RemoveTransformStatesFromShapeshiftedUnit')
    cpp += extract('source/D2Game/src/PLAYER/PlrModes.cpp', 'D2GAME_PLRMODES_First_6FC7F340')
    cpp += extract('source/D2Game/src/PLAYER/PlrModes.cpp', 'PLRMODE_StartID_Death')
    cpp += r'''
void reset(){unit={};row={};skill={};used=true;hasSkill=true;allocated=0;failure=0;events.clear();rngCalls=0;game.dwGameFrame=100;endFrame=100;}
void add(int state,bool list){unit.states[state]=true;if(list){auto*p=&storage[allocated++];*p={};p->state=state;p->posted=true;p->stats[7]=state==139?223:228;p->callback=sub_6FCFE030;unit.lists[state]=p;}}
int main(){
 for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;
 tables.states[139].wGroup=tables.states[140].wGroup=3;
 int deaths=0,gates=0;
 for(int f=0;f<3;f++)for(bool list:{false,true})for(bool trained:{false,true})for(int life:{0,1,255,256,1000}){
  reset();unit.life=life;unit.dwAnimMode=PLRMODE_ATTACK1;hasSkill=trained;form=f==2;
  if(f)add(f==1?139:140,list);PLRMODE_StartID_Death(&game,&unit,0,nullptr);
  assert(!unit.states[139]&&!unit.states[140]&&!unit.lists[139]&&!unit.lists[140]);
  if(f){assert(unit.dwAnimMode==PLRMODE_NEUTRAL);assert(unit.life==life);assert(events.back()[0]==20);for(auto e:events)assert(e[0]<21);}
  else{assert(unit.dwAnimMode==PLRMODE_DEATH&&unit.life==0);assert(events.front()[0]==21&&events.back()[0]==33);}
  deaths++;
 }
 for(int current:{PLRMODE_NEUTRAL,PLRMODE_ATTACK1,PLRMODE_CAST,PLRMODE_DEATH,PLRMODE_DEAD})
 for(int requested:{PLRMODE_GETHIT,PLRMODE_BLOCK,PLRMODE_DEATH,PLRMODE_NEUTRAL,PLRMODE_ATTACK1})
 for(bool active:{false,true})for(bool bypass:{false,true})for(int kind=0;kind<4;kind++)
 for(int chance:{0,20,100})for(int seed:{0,19,20,99}){
  reset();unit.dwAnimMode=current;used=active;roll=seed;row.dwFlags[0]=gdwBitMasks[SKILLSFLAGINDEX_INTERRUPT];
  if(kind==1){unit.states[STATE_CONCENTRATION]=true;auto*p=&storage[allocated++];*p={};p->stats[STAT_SKILL_CONCENTRATION]=chance;unit.lists[STATE_CONCENTRATION]=p;}
  if(kind==2)unit.states[STATE_CONCENTRATE]=true;
  if(kind==3)unit.states[STATE_UNINTERRUPTABLE]=true;
  bool locked=kind==3||current==PLRMODE_DEATH||current==PLRMODE_DEAD;
  bool resist=kind==2||(kind==1&&seed<chance);
  int expected=locked?0:requested==PLRMODE_DEATH||bypass||!active?1:resist&&current!=PLRMODE_NEUTRAL?0:1;
  int result=D2GAME_PLRMODES_First_6FC7F340(&game,&unit,requested,bypass?0:1,nullptr);
  assert(result==expected);assert(rngCalls==(!locked&&requested!=PLRMODE_DEATH&&!bypass&&active&&kind==1?1:0));
  gates++;
 }
 printf("{\"deathCases\":%d,\"interruptCases\":%d}\n",deaths,gates);
}
'''
    for name, digest in routines.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest
    generated = BASE / 'druid-form-death-interrupt-probe.cpp'
    generated.write_text(cpp)
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(generated.with_suffix(''))], check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    log = BASE / 'druid-form-death-interrupt-sanitizer.log'
    log.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()
    counts = json.loads(process.stdout)
    assert counts == dict(deathCases=60, interruptCases=4800)
    result = dict(referenceVersion='3.3.93847', sourcePreparation=True, nativeAccepted=False, legacyCommit=tree['sha'],
        predecessor=dict(path=str(prior_path.relative_to(ROOT)), sha256=sha(prior_path)), sourceHashes=hashes,
        licensedSkills=dict(filename=skill_path.name, sha256=sha(skill_path), bearConcentration=100),
        completeRoutineHashes=routines, generatedSourceSha256=sha(generated), sanitizerLogSha256=sha(log), verified=counts,
        releasedModernReference=dict(url='https://news.blizzard.com/en-gb/article/23788293/diablo-ii-resurrected-patch-2-4-ladder-now-live',
            scope='Released2.4 Werebear explicitly cannot be interrupted while performing attacks or skills; current licensed bear aurastat4 skill_concentration calculates100. This source gate predates that modern bear-specific change.'),
        established=[
            'Complete older player death entry first calls the complete transform-state removal helper. A shifted player removes all declared transform states/lists through the original group/removal callbacks and returns through a forced neutral-mode service before any death penalty,dead-client flag,dead collision or ordinary death event cleanup. Missing trained skill/list does not prevent transform flags being cleared. Human control follows the ordinary death branch.',
            'Interruption gate executes4800 supported contexts for ordinary interruptible used skills across neutral/attack/cast/dead modes,requested gethit/block/death/neutral/attack,used-skill presence,bypass,concentration0/20/100,Concentrate and Uninterruptable. Death/bypass/absent used skill differ from concentration. Concentration resists an action transition while an interruptible skill is active; neutral mode remains permitted.',
            'The older gate reads the Concentration state list and Concentrate/Uninterruptable flags; it does not read the bear stat from a bear list. Porting that older gate unchanged would omit the released modern Werebear guarantee.',
        ], declaredServices=[
            'Inherited stat allocation/free/formula/conversion services preserve the predecessor scope. Stat detach/free executes the original form/group callbacks but does not implement numeric maximum/current-life recomputation. The life sentinel is deliberately unchanged on the shifted branch; this is a service boundary,not a claim that D2R leaves zero life.',
            'Forced-neutral dispatch,death penalties/client/path/event side effects are ordered recording services. Attacker-specific player-ear/arena branches are guarded unexecuted. Null unit is not a supported gate input because the source dereferences it in the first state query.',
        ], remaining=[
            'Establish the numeric forced-unshift life floor and complete stat recomputation before implementing survival. This trace proves early death diversion,not a current-executable1life result. Current foreign-class/item-only forms remain unsupported.',
            'Native bear concentration should protect actual pending attack/skill action intervals only; do not turn it into permanent stun/knockback/death immunity or prevent deliberate Stop/movement. Other form hurt/block/movement clocks and actual modern client acceptance remain separate.',
        ])
    output = ROOT / 'tools/reference/druid-form-death-interrupt-engine-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Druid death/interruption:60complete death/removal contexts and4800gate cases,ASan/UBSan clean; numeric life-floor/current bear consumer remain separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
