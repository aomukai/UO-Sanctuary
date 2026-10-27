#!/usr/bin/env python3
"""Execute complete pinned Fenris corpse release, predicates and buff allocation/writer."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    preflight = ROOT / 'tools/reference/dire-wolf-preflight.json'
    current = json.loads(preflight.read_text())
    rage = current['currentRows']['skills']['fenris rage']
    assert [rage[k] for k in ['srvdofunc', 'Param1', 'Param2', 'aurastat1']] == ['137', '500', '100', 'damagepercent']
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == current['legacyCommit']
    sources, routines = {}, {}

    def original(relative, name):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        sources[relative] = hashlib.sha256(raw).hexdigest()
        body = function(raw.decode(), name)
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    cpp = r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#include <vector>
#define __fastcall
#define __stdcall
#define REMOVE_LATER_Trace(x) ((void)0)
using BOOL=int;
constexpr int TRUE=1,FALSE=0,UNIT_PLAYER=0,UNIT_MONSTER=1,MONMODE_DEAD=12;
constexpr int MONSTATS2FLAGINDEX_CORPSESEL=0,MONSTATS2FLAGINDEX_DEADCOL=1,MONSTATS2FLAG_SOFT=4;
constexpr int UNITFLAG_SKSRVDOFUNC=0x40,STATE_CORPSE_NODRAW=104,STAT_ATTACKRATE=93,STAT_OTHER_ANIMRATE=94,EVENTTYPE_REMOVESTATE=1;
unsigned gdwBitMasks[32];
struct D2UnitStrc{int dwUnitType=UNIT_MONSTER,dwAnimMode=MONMODE_DEAD,dwClassId=0,dwUnitId=17,dwFlags=0,hp=12345,mana=678;bool udead=false,rage=false;}caster,target;
struct D2MonStats2Txt{unsigned dwFlags=5;}model;
struct D2SkillsTxt{int nAuraState=138,dwAuraLenCalc=1,wAuraStat[6]={25,-1,-1,-1,-1,-1},dwAuraStatCalc[6]={2};}skill;
struct D2StatListStrc{int flags=0,expiry=0,type=0,owner=0,state=-1,percent=0;}list;
struct D2GameStrc{void*pMemoryPool=nullptr;int dwGameFrame=0;}game;
struct Tables{int nStatesTxtRecordCount=300;}tables;auto*sgptDataTables=&tables;
bool validSkill=true,hasModel=true,hasTarget=true,allocate=true;
int queries=0,allocations=0,refreshes=0,updates=0,removals=0;std::vector<int>events;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return validSkill&&id==314?&skill:nullptr;}
bool STATES_CheckStateMaskUdeadOnUnit(D2UnitStrc*p){return p&&p->udead;}
D2MonStats2Txt* UNITS_GetMonStats2TxtRecordFromMonsterId(int){return hasModel?&model:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*p){queries++;return p&&hasTarget?&target:nullptr;}
void STATES_ToggleState(D2UnitStrc*p,int state,int on){if(p==&target&&state==104&&on){p->udead=true;events.push_back(1);}else if(p==&caster&&on){p->rage=true;events.push_back(7);}}
void UNITROOM_RefreshUnit(D2UnitStrc*){refreshes++;events.push_back(2);}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int,int){return f==1?500:f==2?100:0;}
D2StatListStrc* STATLIST_AllocStatList(void*,int flags,int expiry,int type,int owner){allocations++;events.push_back(3);if(!allocate)return nullptr;list.flags=flags;list.expiry=expiry;list.type=type;list.owner=owner;return &list;}
void STATLIST_SetState(D2StatListStrc*p,int state){p->state=state;events.push_back(4);}
void sub_6FD10E50(){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)()){events.push_back(5);}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){events.push_back(6);}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int expiry,int,int){removals++;if(expiry!=list.expiry)__builtin_trap();events.push_back(8);}
void* SKILLS_GetItemStatCostTxtRecord(int stat){return stat==25?&list:nullptr;}
void STATLIST_SetStat(D2StatListStrc*p,int stat,int value,int){if(stat!=25)__builtin_trap();p->percent=value;events.push_back(9);}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*,int,int,int){__builtin_trap();}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){updates++;events.push_back(10);}
'''
    for relative, names in [
        ('source/D2Common/src/D2Skills.cpp', ['SKILLS_CanUnitCorpseBeSelected']),
        ('source/D2Common/src/Units/Units.cpp', ['UNITS_IsSoftMonster', 'UNITS_IsCorpseUseable']),
        ('source/D2Common/src/D2Skills.cpp', ['D2Common_11020']),
        ('source/D2Common/src/UselessOrdinals.cpp', ['D2COMMON_11018']),
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['sub_6FCFE0E0']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD10250']),
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo137_FenrisRage']),
    ]:
        for name in names:
            cpp += original(relative, name)
    cpp += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool comma=false;printf("[");
for(int unit:{0,1})for(int valid:{0,1})for(int state:{-1,138,300})for(int alloc:{0,1})for(int rank:{1,20})for(int frame:{0,1001})for(int kind=0;kind<10;kind++){
caster={};target={};list={};model={};skill={};game.dwGameFrame=frame;hasTarget=kind!=0;hasModel=kind!=3;validSkill=valid;allocate=alloc;skill.nAuraState=state;
target.dwUnitType=kind==1?UNIT_PLAYER:kind==9?6:UNIT_MONSTER;target.dwAnimMode=kind==2?1:MONMODE_DEAD;model.dwFlags=kind==4?4:kind==5?1:kind==6?7:5;target.udead=kind==8;
queries=allocations=refreshes=updates=removals=0;events.clear();D2UnitStrc*body=unit?&caster:nullptr;D2UnitStrc*corpse=hasTarget?&target:nullptr;
int search=UNITS_IsCorpseUseable(corpse),release=D2COMMON_11018(corpse);
int result=SKILLS_SrvDo137_FenrisRage(&game,body,314,rank);std::vector<int>first=events;int firstQueries=queries;
int repeat=SKILLS_SrvDo137_FenrisRage(&game,body,314,rank);
if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",unit,valid,state,alloc,rank,frame,kind,search,release,result,repeat,caster.dwFlags,target.udead,refreshes,allocations,list.flags,list.expiry,list.type,list.owner,list.state,list.percent,updates);
bool sep=false;for(int e:first){if(sep)printf(",");sep=true;printf("%d",e);}printf("],%d,%d,%d,%d,%d]",caster.hp,caster.mana,caster.rage,firstQueries,removals);
}printf("]\n");}
'''
    path = STAGE / 'fenris-rage-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    process = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'fenris-rage-sanitizer.log').write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-3000:]
    cases = json.loads(process.stdout)
    assert len(cases) == 960
    successes = 0
    for row in cases:
        unit, valid, state, alloc, rank, frame, kind, search, release, result, repeat, flags, hidden, refreshes, allocations, list_flags, expiry, owner_type, owner_id, list_state, percent, updates, events, hp, mana, raging, queries, removals = row
        assert search == int(kind in [5, 7]) and release == int(kind in [6, 7]), row
        routed = valid and state == 138
        success = routed and unit and release
        buff = success and alloc
        assert [result, repeat, flags, hidden, refreshes, allocations, queries] == [int(success), 0, 64 if routed and unit else 0, int(kind == 8 or success), int(success), int(success), int(routed)], row
        assert [list_flags, expiry, owner_type, owner_id, list_state, percent, updates, raging, removals] == ([2, frame + 500, 1, 17, 138, 100, 1, 1, 1] if buff else [0, 0, 0, 0, -1, 0, 0, 0, 0]), row
        assert events == ([1, 2, 3, 4, 5, 6, 7, 8, 9, 10] if buff else [1, 2, 3] if success else []), row
        assert hp == 12345 and mana == 678, row
        successes += bool(success)
    fixture = STAGE / 'fenris-rage-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
        preflightSha256=sha(preflight), sourceHashes=sources, unchangedRoutineHashes=routines,
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=len(cases), successfulConsumptionCases=successes, generatedSourceSha256=sha(path), sanitizers='ASan+UBSan'),
        established=[
            '960 complete do137/11018/11020/selectable/soft/usable and self-buff allocation/six-stat writer compositions execute unchanged. Corpse search predicate and release predicate differ for soft/DEADCOL controls.',
            'Consumption hides and refreshes before buff allocation. Successful allocation supplies a fresh flag2list,owner type/GUID,state138,expiry frame+500,one removal event and damagepercent100,then animation/velocity refresh. No HP/mana write occurs; repeat use of the hidden corpse fails.',
            'Consumption still succeeds if buff allocation fails. Missing skill/invalid state returns before the server-do flag; missing/invalid target fails after that flag for a nonnull caster. Rank controls do not change fixed500frame/100percent values.',
        ], boundedServices=[
            'Metadata records/flags,udead mask lookup,formula values,stat-list allocation/link/removal scheduling and animation refresh are declared services. Actual native world/corpse/loot concurrency and expiration/removal execution are not instantiated.',
            'This executes the usable predicate,not full AI corpse search or Fenris AI/attack scheduling. Complete room/find/interaction order,modern executable parity and event ties remain separate.',
            'No Dire Wolf actor or learned handler enabled. Internal rage visuals/audio and parent talent/right/quick icons remain separately pending.',
        ])
    output = ROOT / 'tools/reference/fenris-rage-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print(f'Fenris rage:{len(cases)}complete corpse/release/buff compositions,{successes}consumptions pass ASan/UBSan; no native actor enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
