#!/usr/bin/env python3
"""Execute complete older do25 and its non-curse state-list helper together."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/enchant-source'


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def probe(check=False):
    tree = json.loads((ROOT / '.local/class-skills/d2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    names = {'source/D2Game/src/SKILLS/Skills.cpp': 'sub_6FD10EC0',
             'source/D2Game/src/SKILLS/SkillSor.cpp': 'SKILLS_SrvDo025_Enchant'}
    sources, blocks = {}, {}
    for rel, name in names.items():
        p = ROOT / '.local/class-skills/d2moo-pinned-source' / rel
        raw = p.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == rel)
        sources[rel] = sha(p)
        blocks[name] = function(raw.decode(), name)
    tables = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((tables / 'sources.json').read_text())
    for name in ('skills', 'states'):
        assert sha(tables / 'json' / (name + '.json')) == manifest['files']['json/' + name + '.json']['sha256']
    row = next(r for r in json.loads((tables / 'json/skills.json').read_text()).values() if r.get('skill') == 'Enchant')
    state = next(r for r in json.loads((tables / 'json/states.json').read_text()).values() if r.get('state') == 'enchant')
    table_fields = {'*Id': 52, 'srvdofunc': 25, 'aurastate': 'enchant', 'auralencalc': 'ln12', 'mana': 25, 'lvlmana': 1,
                'manashift': 8, 'aurastat1': 'firemindam', 'aurastatcalc1': 'enma', 'aurastat2': 'firemaxdam',
                'aurastatcalc2': 'exma', 'aurastat3': 'item_tohit_percent', 'aurastatcalc3': 'toht',
                'Param1': 3600, 'Param2': 600, 'ToHit': 20, 'LevToHit': 9, 'HitShift': 7,
                'EDmgSymPerCalc': "(skill('Warmth'.blvl))*par8", 'Param8': 9}
    assert {k: row.get(k) for k in table_fields} == table_fields and state['*ID'] == 16 and not state.get('curse')
    cpp = r'''
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int UNIT_MONSTER=1,MONSTATSFLAGINDEX_NPC=0,MONSTATS2FLAGINDEX_ISATT=1;
constexpr int STAT_CURSE_RESISTANCE=174,STATE_ATTRACT=57,EVENTTYPE_REMOVESTATE=1;
uint32_t gdwBitMasks[2]={1,2};
struct D2GameStrc{int dwGameFrame=100;} game;
struct D2UnitStrc{int dwUnitType=0,dwClassId=0,dwUnitId=0;void* pMemoryPool=nullptr;} owner,target;
struct D2StatesTxt{int nStateFlags[1]={0};} state;
struct D2MonStatsTxt{int dwMonStatsFlags=0;} mon;
struct D2MonStats2Txt{int dwFlags=2;} mon2;
struct D2SkillsTxt{int wAuraStat[6]={1,2,3,-1,-1,-1},nAuraState=16,dwAuraLenCalc=99,dwAuraStatCalc[6]={0,1,2,3,4,5};} row;
struct Tables{int nItemStatCostTxtRecordCount=256,nStatesTxtRecordCount=200;} tables,*sgptDataTables=&tables;
struct D2StatListStrc{int state=16,skill=52,rank=0,until=0,owner=101,values[256]={};} list;
using StatListRemoveCallback=void(*)();
struct D2CurseStrc{D2UnitStrc* pUnit=nullptr;D2UnitStrc* pTarget=nullptr;int nSkill=0,nSkillLevel=0,nStat=0,nDuration=0,nState=0,nStatValue=0;StatListRemoveCallback pStateRemoveCallback=nullptr;};
bool hasRow=true,selected=true,aligned=true,present=false;
int duration=3600,mode=0,writes=0,allocations=0,frees=0,refresh=0,gfx=0,events=0;
D2GameStrc* SUNIT_GetGameFromUnit(D2UnitStrc*){return &game;}
D2StatesTxt* SKILLS_GetStatesTxtRecord(int){return &state;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return &mon;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return &mon2;}
int STATES_CheckStateMaskCurseByStateId(int){return 0;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return 0;}
int MONSTERUNIQUE_CalculatePercentage(int a,int b,int c){return a*b/c;}
bool STATES_CheckState(D2UnitStrc*,int){return false;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndFlag(D2UnitStrc*,uint32_t){return present?&list:nullptr;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return present?&list:nullptr;}
int STATLIST_GetState(D2StatListStrc* p){return p->state;}
int STATLIST_GetSkillId(D2StatListStrc* p){return p->skill;}
int STATLIST_GetSkillLevel(D2StatListStrc* p){return p->rank;}
void STATLIST_SetExpireFrame(D2StatListStrc* p,int n){p->until=n;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){events++;}
void D2Common_10474(D2UnitStrc*,D2StatListStrc*){}
void STATLIST_FreeStatList(D2StatListStrc*){frees++;present=false;}
void UNITROOM_RefreshUnit(D2UnitStrc*){refresh++;}
void STATES_ToggleState(D2UnitStrc*,int,int){}
int STATES_CheckStateMaskExpByStateId(int){return 0;}
D2StatListStrc* STATLIST_AllocStatList(void*,uint32_t,int until,int,int id){allocations++;list={};list.until=until;list.owner=id;present=true;return &list;}
void STATLIST_SetState(D2StatListStrc* p,int n){p->state=n;}
void STATLIST_SetSkillId(D2StatListStrc* p,int n){p->skill=n;}
void STATLIST_SetSkillLevel(D2StatListStrc* p,int n){p->rank=n;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc* p,int stat,int value,int){writes++;p->values[stat]=value;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,StatListRemoveCallback){}
void sub_6FD10E50(){}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected?&target:nullptr;}
bool SUNIT_AreUnitsAligned(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return aligned;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return formula==99?duration:mode&&formula==1?0:1000+formula;}
void STATES_ToggleGfxStateFlag(D2UnitStrc*,int,int){gfx++;}
'''
    cpp += '\n'.join(blocks.values())
    cpp += r'''
int main(){bool first=true;printf("[");owner.dwUnitId=202;target.dwUnitId=303;
for(int guard=0;guard<6;guard++)for(int recipient=0;recipient<6;recipient++)for(int rank:{1,10,20})for(int old:{0,1,10,20,30})for(int same:{0,1})for(int life:{0,3600})for(int slots:{0,1}){
hasRow=guard!=1;row.wAuraStat[0]=guard==2?-2:guard==3?256:1;row.nAuraState=guard==4?-1:guard==5?200:16;
row.wAuraStat[1]=2;row.wAuraStat[2]=3;row.wAuraStat[3]=slots?256:-1;
selected=recipient!=0;aligned=recipient!=1;target.dwUnitType=recipient>=3?1:0;
mon.dwMonStatsFlags=recipient==4?1:0;mon2.dwFlags=recipient==5?0:2;
duration=life;mode=slots;present=old!=0;list={};list.rank=old;list.state=16;list.skill=same?52:999;list.until=9999;list.owner=101;list.values[1]=7;list.values[2]=8;list.values[3]=9;
writes=allocations=frees=refresh=gfx=events=0;
int ret=SKILLS_SrvDo025_Enchant(&game,&owner,52,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",guard,recipient,rank,old,same,life,slots,ret,writes,allocations,frees,refresh,gfx,events,list.rank,list.until,list.owner,list.values[1],list.values[2]);first=false;}
printf("]\n");}
'''
    STAGE.mkdir(parents=True, exist_ok=True)
    p = STAGE / 'probe.cpp'
    p.write_text(cpp)
    binary = STAGE / 'probe'
    subprocess.run(['g++', '-std=c++20', '-O2', str(p), '-o', str(binary)], check=True)
    cases = json.loads(subprocess.check_output([str(binary)]))
    assert len(cases) == 4320
    for r in cases:
        guard, target, rank, old, same, duration, slots = r[:7]
        accepted = guard == 0 and target not in (4, 5) and not (old and same and old > rank)
        reused = bool(accepted and old and same and old == rank)
        allocated = bool(accepted and not reused)
        expected = [int(accepted), (2 if slots else 3) if accepted else 0, int(allocated), int(allocated and old > 0),
                    int(allocated), int(accepted), int(accepted and duration > 0), rank if accepted else old,
                    (100 + duration if duration else 9999 if reused else 0) if accepted else 9999,
                    101 if reused or not accepted else 202, 1000 if accepted else 7,
                    (8 if reused else 0) if accepted and slots else 1001 if accepted else 8]
        assert r[7:] == expected, (r, expected)
    san = STAGE / 'probe-san'
    subprocess.run(['g++', '-std=c++20', '-g', '-fsanitize=address,undefined', '-fno-omit-frame-pointer', str(p), '-o', str(san)], check=True)
    log = STAGE / 'sanitizer.log'
    with log.open('wb') as err:
        assert json.loads(subprocess.check_output([str(san)], stderr=err)) == cases
    assert not log.read_bytes()
    fixture = STAGE / 'cases.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceHashes=sources,
                    routineHashes={n: hashlib.sha256(b.encode()).hexdigest() for n, b in blocks.items()},
                    currentTable={str((tables / 'json' / (n+'.json')).relative_to(ROOT)): sha(tables / 'json' / (n+'.json')) for n in ('skills','states')},
                    currentRow=table_fields,
                    cases=len(cases), sanitizers='ASan/UBSan clean',
                    established=['Complete do25 and shared state-list helper compose for missing/invalid rows, fallback-to-self, aligned players/monsters, NPC/nonattackable monster exclusions, weaker/equal/stronger/different-skill state lists, duration and nonzero valid stat writes.',
                                 'Equal-rank reuse refreshes expiration and nonzero values, retaining original list owner. Weaker same-skill rank refuses without extending; stronger or different skill replaces owner and list.'],
                    declaredServices=['This is the pinned older engine, not execution of D2R 3.3.93847. Noncurse Enchant state is supplied from the current table; curse-only branches are compiled but not exercised.',
                                      'Alignment, monster metadata, state-list allocation/events/stat writes and evaluated formula values are explicit test services. Allocation succeeds; allocator failure behavior is not inferred. Mastery, mana, weapon delivery and exact modern events/RNG remain separate.'],
                    artifacts={str(q.relative_to(ROOT)): sha(q) for q in (Path(__file__).resolve(),p,binary,san,log,fixture)})
    out = ROOT / 'tools/reference/enchant-source-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded
    else:
        out.write_text(encoded)
    print('Enchant:4320 complete older do25/state-helper compositions; ASan/UBSan clean; formula/weapon/modern services explicitly separate.')


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--check', action='store_true')
    probe(ap.parse_args().check)
