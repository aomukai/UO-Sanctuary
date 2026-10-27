#!/usr/bin/env python3
"""Execute complete legacy transform lifecycle with explicit current-table services."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    preparation = ROOT / 'tools/reference/druid-forms-preflight.json'
    pinned = json.loads(preparation.read_text())
    # Formula services below are deliberate injected inputs, pinned to these
    # current rows; a data change must not silently reuse stale constants.
    rows = pinned['currentRows']['skills']
    assert [rows['Wearwolf'][key] for key in ['Param1', 'Param2', 'Param3', 'Param4', 'ToHit', 'LevToHit']] == ['25', '25', '10', '80', '50', '15']
    assert [rows['Wearbear']['Param' + str(i)] for i in range(1, 7)] == ['55', '15', '40', '10', '75', '100']
    assert [rows['Shape Shifting']['Param' + str(i)] for i in range(1, 5)] == ['1000', '500', '20', '5']
    assert [rows['Wearwolf']['aurastat' + str(i)] for i in range(1, 5)] == ['skill_staminapercent', 'attackrate', 'item_tohit_percent', 'item_maxhp_percent']
    assert [rows['Wearbear']['aurastat' + str(i)] for i in range(1, 5)] == ['damagepercent', 'skill_armor_percent', 'item_maxhp_percent', 'skill_concentration']
    bodies = {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo116_Wearwolf_Wearbear', 'sub_6FCFE030', 'sub_6FCFE0E0']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD10CE0', 'sub_6FD11BA0', 'D2GAME_SKILLS_SetDelay_6FD11C00', 'sub_6FD11C90']),
    ]:
        path = BASE / 'd2moo-pinned-source' / relative
        assert sha(path) == pinned['sourceHashes'][relative]
        for name in names:
            body = function(path.read_text(), name)
            assert hashlib.sha256(body.encode()).hexdigest() == pinned['reviewedRoutineHashes'][name]
            bodies[name] = body
    cpp = r'''
#include <algorithm>
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
enum{UNIT_PLAYER=0,UNIT_MONSTER=1,PLRMODE_CAST=7,UNITFLAG_SKSRVDOFUNC=64,EVENTTYPE_REMOVESTATE=1,STATE_SKILLDELAY=121,STAT_ATTACKRATE=1,STAT_OTHER_ANIMRATE=6,STAT_MODIFIERLIST_SKILL=7,STAT_MODIFIERLIST_LEVEL=8,STAT_MANA=9};
struct D2UnitStrc;struct D2StatListStrc;
using Callback=void(*)(D2UnitStrc*,int,D2StatListStrc*);
struct D2StatListStrc{int state=-1,expire=0;bool posted=false;std::array<int,10> stats{};Callback callback=nullptr;};
struct D2SkillStrc{int mode=7,nCharges=0,owner=-1,rank=1;};
struct D2SkillsTxt{int nAuraState=139,dwAuraLenCalc=10,dwDelay=11,wSrvDoFunc=116,wMana=15,wLevelMana=0,nManaShift=8;int wAuraStat[6]={0,1,2,3,-1,-1},dwAuraStatCalc[6]={0,1,2,3,4,5};};
struct D2StatesTxt{int wGroup=0;};
struct Tables{int nStatesTxtRecordCount=150;D2StatesTxt states[150];} tables,*sgptDataTables=&tables;
struct D2UnitStrc{int dwFlags=0,dwUnitId=42,dwUnitType=0,dwClassId=5;std::array<bool,150> states{};std::array<D2StatListStrc*,150> lists{};};
struct D2GameStrc{void*pMemoryPool=nullptr;int dwGameFrame=100;} game;
D2SkillsTxt row;D2SkillStrc skill;D2UnitStrc unit;
std::array<D2StatListStrc,8> storage;int allocated=0,rank=1,lycan=0,form=0,failure=0,scenario=0;
bool used=true,hasSkill=true;std::vector<std::array<int,3>> events;
int sub_6FD11C90(D2UnitStrc*,int,int);void sub_6FCFE030(D2UnitStrc*,int,D2StatListStrc*);
void sub_6FCFE0E0(D2UnitStrc*,D2StatListStrc*,D2SkillsTxt*,int,int);
void sub_6FD11BA0(D2GameStrc*,D2UnitStrc*,int,int);void D2GAME_SKILLS_SetDelay_6FD11C00(D2GameStrc*,D2UnitStrc*,int);
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return failure==1?nullptr:&row;}
D2StatesTxt* SKILLS_GetStatesTxtRecord(int s){return s>=0&&s<150?&tables.states[s]:nullptr;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return hasSkill?&skill:nullptr;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return used?&skill:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecordFromSkill(D2SkillStrc*){return SKILLS_GetSkillsTxtRecord(0);}
int SKILLS_GetOwnerGUIDFromSkill(D2SkillStrc*p){return p->owner;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*p,int){return p->rank;}
bool STATES_IsUnitShapeShifted(D2UnitStrc*p){return p->states[139]||p->states[140];}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return 0;}
int linear(int base,int step,int level){return level>0?base+step*(level-1):0;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int,int level){
 if(f==10)return 1000+linear(1000,500,lycan);if(f==11)return 25;
 if(!form){if(f==0)return 25;if(f==1)return 10+70*std::min(100,110*level/(level+6))/100;if(f==2)return 50+15*(level-1);if(f==3)return 25+linear(20,5,lycan);}
 else{if(f==0)return 55+15*(level-1);if(f==1)return 40+10*(level-1);if(f==2)return 75+linear(20,5,lycan);if(f==3)return 100;}
 return 0;
}
void* SKILLS_GetItemStatCostTxtRecord(int s){return s>=0&&s<10?&row:nullptr;}
void SKILLS_SetSkillMode(D2SkillStrc*p,int mode){p->mode=mode;events.push_back({8,mode,0});}
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int*type,int*cls,int*mode,const char*,int){*type=1;*cls=430+form;*mode=10;events.push_back({9,*cls,*mode});}
bool STATES_CheckState(D2UnitStrc*p,int s){return p&&p->states[s];}
void STATES_ToggleState(D2UnitStrc*p,int s,int on){assert(p);p->states[s]=on;events.push_back({1,s,on});}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*p,int s){return p?p->lists[s]:nullptr;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int expiry,int,int){events.push_back({2,expiry,0});if(failure==3)return nullptr;assert(allocated<8);auto*p=&storage[allocated++];*p={};p->expire=expiry;return p;}
void STATLIST_SetState(D2StatListStrc*p,int s){p->state=s;}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*p,Callback cb){p->callback=cb;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*p,D2StatListStrc*s,int){p->lists[s->state]=s;s->posted=true;events.push_back({3,s->state,0});}
void D2Common_10474(D2UnitStrc*p,D2StatListStrc*s){p->lists[s->state]=nullptr;s->posted=false;events.push_back({4,s->state,0});}
void STATLIST_FreeStatList(D2StatListStrc*p){events.push_back({5,p->state,0});if(p->callback)p->callback(&unit,p->state,p);}
void STATLIST_SetStat(D2StatListStrc*p,int s,int v,int){p->stats[s]=v;events.push_back({6,s,v});}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*p,int s,int v,int i){if(p)STATLIST_SetStat(p,s,v,i);}
int STATLIST_GetStatValue(D2StatListStrc*p,int s,int){return p->stats[s];}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int expiry,int,int){events.push_back({7,expiry,0});}
void D2COMMON_10476(D2StatListStrc*p,int expiry){p->expire=expiry;}
void sub_6FD10E50(D2UnitStrc*,int,D2StatListStrc*){}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){events.push_back({10,0,0});}
'''
    for name in ['sub_6FD11C90', 'sub_6FCFE030', 'sub_6FCFE0E0', 'D2GAME_SKILLS_SetDelay_6FD11C00', 'sub_6FD11BA0', 'SKILLS_SrvDo116_Wearwolf_Wearbear', 'sub_6FD10CE0']:
        cpp += bodies[name] + '\n'
    cpp += r'''
bool first=true;
void emit(int f,int r,int l,int type,int scene){
 form=f;rank=r;lycan=l;scenario=scene;failure=scene==3?1:scene==4?2:scene==5?3:0;unit={};unit.dwUnitType=type;row={};skill={};skill.rank=rank;allocated=0;used=scene!=9;hasSkill=scene!=8;events.clear();
 row.nAuraState=f?140:139;if(f)row.wAuraStat[1]=4;
 if(failure==2)row.nAuraState=150;
 if(scene==1||scene==2||scene==6||scene==7){int s=scene==2?(f?139:140):(f?140:139);auto*p=&storage[allocated++];*p={};p->state=s;p->posted=true;p->stats[7]=s==139?223:228;p->callback=sub_6FCFE030;unit.lists[s]=p;unit.states[s]=scene!=6;}
 if(scene==7){auto*p=&storage[allocated++];*p={};p->state=121;p->posted=true;unit.lists[121]=p;unit.states[121]=true;}
 const int allowed=sub_6FD10CE0(&unit);const int result=SKILLS_SrvDo116_Wearwolf_Wearbear(&game,&unit,f?228:223,rank);
 auto*list=unit.lists[f?140:139];
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",f,r,l,type,scene,allowed,result,unit.dwFlags,unit.states[139],unit.states[140],skill.mode,list?list->expire:0);first=false;
 for(int i=0;i<10;i++)printf("%s%d",i?",":"",list?list->stats[i]:0);printf("],[");
 for(unsigned i=0;i<events.size();i++)printf("%s[%d,%d,%d]",i?",":"",events[i][0],events[i][1],events[i][2]);printf("]]");
 // Expire a successful form through the real callback and the declared stat-list service.
 if(result){D2Common_10474(&unit,list);STATLIST_FreeStatList(list);assert(!unit.states[f?140:139]);assert(skill.mode==(scene==8||type==UNIT_PLAYER?PLRMODE_CAST:10));}
}
int main(){tables.states[139].wGroup=tables.states[140].wGroup=3;printf("[");for(int f:{0,1})for(int r:{1,3,20,28,255})for(int l:{0,1,20,28,255})for(int t:{0,1})for(int scene=0;scene<10;scene++)emit(f,r,l,t,scene);printf("]\n");}
'''
    source = BASE / 'druid-forms-source-probe.cpp'
    source.write_text(cpp)
    binary = source.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(source), '-o', str(binary)], check=True)
    process = subprocess.run([str(binary)], capture_output=True)
    log = BASE / 'druid-forms-source-sanitizer.log'
    log.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()
    contexts = json.loads(process.stdout)
    assert len(contexts) == 1000
    for f, r, l, unit_type, scene, allowed, result, flags, wolf, bear, mode, expiry, stats, events in contexts:
        assert result == (scene in [0, 8, 9])
        assert flags == (0 if scene in [3, 4] else 64)
        if result:
            assert [wolf, bear] == ([1, 0] if f == 0 else [0, 1])
            assert expiry == 1100 + (1000 + (l - 1) * 500 if l else 0)
            assert mode == (7 if scene == 8 else 10)
            assert stats[7:9] == [223 if f == 0 else 228, r]
            assert events[-1][0] == (6 if scene == 8 else 8)
        if scene in [1, 2, 7]:
            assert not wolf and not bear
            assert not any(e[0] == 6 for e in events)  # No new form-stat writer.
            assert [7, 125, 0] in events if unit_type == 0 else not any(e[0] == 7 for e in events)
        assert allowed == (1 if unit_type == 1 or scene in [1, 2, 7] else 0)
    fixture = BASE / 'druid-forms-source-contexts.json'
    fixture.write_text(json.dumps(contexts, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', legacyCommit=pinned['legacyCommit'], sourcePreparation=True, nativeAccepted=False, handlerEnabled=False,
        contexts=len(contexts), completeRoutines=list(bodies), preparationSha256=sha(preparation),
        generatedSourceSha256=sha(source), fixtureSha256=sha(fixture), sanitizerLogSha256=sha(log),
        sourceRoutineHashes={name:pinned['reviewedRoutineHashes'][name] for name in bodies},
        established=[
            'Seven complete legacy routines execute under ASan/UBSan over1000current-form/rank/Lycan/player-monster/failure/state contexts. Successful entry orders allocation/post/toggle/expiry before formula stats and skill mode10; actual removal callback clears the form and restores playerCAST versus converted monster mode.',
            'Same/cross-group removal returns0 without a new form writer and schedules25-frame delay only for player units. Already-present stat list,invalid row/state,failed allocation and missing skill/used-skill controls remain distinct. The zero-mana check permits already-shifted player units.',
        ], limits=[
            'Allocation/state/event/skill/anim services and formula values are declared fixture services. Current formula input uses the retained native two-stage diminishing model and absent-skill linear0; this does not independently establish modern formula interpreter or global event semantics.',
            'The outer handler/payment function was reviewed and hashed by preflight,not executed here. No mana deduction,rank1cost,current life rescale,full combat/animation/skill restriction,world or save/reload consumer is accepted by this source probe.',
            'Current tables/user free-unshift observation corroborate bounded behavior; reconstructed legacy execution alone is not current executable parity or a learned handler.',
        ])
    path = ROOT / 'tools/reference/druid-forms-engine-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Druid forms:1000complete-source lifecycle contexts,7routines,ASan/UBSan clean; native handler not enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
