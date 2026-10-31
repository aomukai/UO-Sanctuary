#!/usr/bin/env python3
"""Compose unchanged defensive-buff dispatch, group, factory and stat writers."""
import argparse
import ast
import hashlib
import json
import re
import subprocess
from pathlib import Path

from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'bone-armor-cast-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(parents=True, exist_ok=True)
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs, routines = {}, {}

    def body(relative, name):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        inputs[str(path.relative_to(ROOT))] = sha(path)
        result = function(raw.decode(), name)
        routines[name] = hashlib.sha256(result.encode()).hexdigest()
        return result + '\n'

    ref = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((ref / 'sources.json').read_text())
    tables = {}
    for name in ['skills', 'states', 'itemstatcost']:
        path = ref / f'json/{name}.json'
        assert sha(path) == manifest['files'][f'json/{name}.json']['sha256']
        inputs[str(path.relative_to(ROOT))] = sha(path)
        tables[name] = json.loads(path.read_text())
    skill = tables['skills']['68']
    assert skill['srvdofunc'] == 18 and skill['aurastate'] == 'bonearmor'
    assert skill['auraevent1'] == 'absorbdamage' and skill['auraeventfunc1'] == 22
    assert not skill.get('auralencalc') and skill['aurastatcalc1'] == skill['aurastatcalc2']
    group = sorted(s['*ID'] for s in tables['states'].values() if s.get('group') == 1)
    assert group == [10, 14, 20, 86, 88, 203, 206]
    assert [tables['itemstatcost'][n]['*ID'] for n in ['bonearmor', 'bonearmormax', 'modifierlist_skill', 'modifierlist_level']] == [132, 133, 350, 351]
    event_path = BASE / 'd2moo-pinned-source/source/D2Game/include/UNIT/SUnitEvent.h'
    raw = event_path.read_bytes()
    relative = str(event_path.relative_to(BASE / 'd2moo-pinned-source'))
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    inputs[str(event_path.relative_to(ROOT))] = sha(event_path)
    event_enum = raw.decode().split('enum D2C_UnitEventTypes:', 1)[1].split('};', 1)[0]
    assert re.findall(r'\bUNITEVENT_[A-Z]+\b', event_enum).index('UNITEVENT_ABSORBDAMAGE') == 11

    scaffold = ROOT / 'tools/probe_assassin_buff_reference.py'
    inputs[str(scaffold.relative_to(ROOT))] = sha(scaffold)
    prefix = next(x.value for x in ast.walk(ast.parse(scaffold.read_text()))
                  if isinstance(x, ast.Constant) and isinstance(x.value, str) and x.value.startswith('#include <algorithm>'))
    prefix = prefix[:prefix.index('D2StatListStrc* sub_6FD10EC0(')]
    replacements = {
        '[200]': '[256]', 'nStatesTxtRecordCount=200': 'nStatesTxtRecordCount=256', 'state<200': 'state<256',
        '[16]': '[512]', 'nItemStatCostTxtRecordCount=16': 'nItemStatCostTxtRecordCount=512', 'id<16': 'id<512',
        'STAT_MODIFIERLIST_SKILL=3,STAT_MODIFIERLIST_LEVEL=4': 'STAT_MODIFIERLIST_SKILL=350,STAT_MODIFIERLIST_LEVEL=351',
        'int state,rank,skill,expiry;': 'int state,rank,skill,expiry;int flags=0,creator=-1,creatorType=6;',
        'int dwFlags=0,dwUnitType=UNIT_PLAYER;': 'int dwFlags=0,dwUnitType=UNIT_PLAYER,dwClassId=0,dwUnitId=123;void* pMemoryPool=nullptr;',
        'int wGroup=0;': 'int wGroup=0;int nStateFlags[1]={};',
        'int nSkill=0,nSkillLevel=0,nStat=0,nDuration=0,nState=0;': 'int nSkill=0,nSkillLevel=0,nStat=0,nDuration=0,nState=0,nStatValue=0;',
        'void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int,int){unregisters++;}':
            'void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int,int);',
        'void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int){}':
            'void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int);',
        'void STATES_ToggleGfxStateFlag(D2UnitStrc*,int,int){gfx++;}':
            'void STATES_ToggleGfxStateFlag(D2UnitStrc*,int,int);',
    }
    for old, new in replacements.items():
        assert old in prefix, old
        prefix = prefix.replace(old, new)
    prefix = '#include <cassert>\n#include <vector>\n#include <array>\n' + prefix
    prefix += r'''
constexpr int UNIT_MONSTER=1,MONSTATSFLAGINDEX_NPC=0,MONSTATS2FLAGINDEX_ISATT=0,STAT_CURSE_RESISTANCE=109,STATE_ATTRACT=57,EVENTTYPE_REMOVESTATE=6;
int gdwBitMasks[32]={1,2};
struct D2MonStatsTxt{int dwMonStatsFlags=0;}mon;
struct D2MonStats2Txt{int dwFlags=1;}mon2;
int allocations=0,posted=0,scheduled=0,roomRefresh=0;std::vector<int> trace;
std::vector<std::array<int,6>> events;
D2GameStrc* SUNIT_GetGameFromUnit(D2UnitStrc* p){assert(p);return p->pGame;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return &mon;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return &mon2;}
int STATES_CheckStateMaskCurseByStateId(int state){assert(state==14);return 0;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){assert(false);return 0;}
int MONSTERUNIQUE_CalculatePercentage(int,int,int){assert(false);return 0;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndFlag(D2UnitStrc*,unsigned){assert(false);return nullptr;}
int STATLIST_GetState(D2StatListStrc* p){return p->state;}
int STATLIST_GetSkillId(D2StatListStrc* p){return p->skill;}
int STATLIST_GetSkillLevel(D2StatListStrc* p){return p->rank;}
void STATLIST_SetExpireFrame(D2StatListStrc* p,int frame){p->expiry=frame;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){scheduled++;}
void UNITROOM_RefreshUnit(D2UnitStrc*){roomRefresh++;}
bool STATES_CheckStateMaskExpByStateId(int state){assert(state==14);return false;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned flags,int expiry,int type,int creator){
 allocations++;trace.push_back(1);auto* p=new D2StatListStrc{};p->flags=flags;p->expiry=expiry;p->creator=creator;p->creatorType=type;return p;
}
void STATLIST_SetState(D2StatListStrc* p,int state){p->state=state;}
void STATLIST_SetSkillId(D2StatListStrc* p,int skill){p->skill=skill;}
void STATLIST_SetSkillLevel(D2StatListStrc* p,int rank){p->rank=rank;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc* u,D2StatListStrc* p,int flag){assert(flag==1);u->lists[p->state]=p;posted++;trace.push_back(2);}
void sub_6FD10E50(D2UnitStrc*,int,D2StatListStrc*){assert(false);}
void STATLIST_SetStatRemoveCallback(D2StatListStrc* p,Callback cb){p->callback=cb;}
void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int type,int state){assert(type==1&&state==14);unregisters++;trace.push_back(3);}
void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int event,int id,int rank,int cb,int type,int state){events.push_back({event,id,rank,cb,type,state});trace.push_back(4);}
void STATES_ToggleGfxStateFlag(D2UnitStrc*,int state,int on){assert(state==14&&on==1);gfx++;trace.push_back(5);}
'''
    units = [
        ('Skills.cpp', ['sub_6FD11C90', 'sub_6FD10EC0']),
        ('SkillDruid.cpp', ['sub_6FCFE0E0', 'sub_6FCFE200']),
        ('SkillSor.cpp', ['SKILLS_CurseStateCallback_DefensiveBuff', 'SKILLS_SrvDo018_DefensiveBuff']),
    ]
    cpp = prefix + ''.join(body('source/D2Game/src/SKILLS/' + file, name) for file, names in units for name in names)
    cpp += r'''
int main(){int ids[7]={10,14,20,86,88,203,206};for(int id:ids)states[id].wGroup=1;bool first=true;printf("{\"casts\":[");
for(int mask=0;mask<128;mask++)for(int oldRank:{1,20})for(int rank:{1,20,255})for(int invalid:{0,1,2,3}){
 D2UnitStrc owner;
 for(int i=0;i<7;i++)if(mask&(1<<i)){int id=ids[i];owner.active[id]=true;owner.lists[id]=new D2StatListStrc{id,oldRank,id==14?68:40,999,0,-1,6,{{132,1},{133,999}},nullptr};}
 for(int id:{31,159}){owner.active[id]=true;owner.lists[id]=new D2StatListStrc{id,20,278,999,0,-1,6,{},nullptr};}
 guard=invalid==1?1:0;skill={};skill.nAuraState=invalid==2?-1:invalid==3?256:14;
 skill.wAuraStat[0]=132;skill.wAuraStat[1]=133;skill.wAuraEvent[0]=11;skill.wAuraEvent[1]=-1;skill.wAuraEventFunc[0]=22;
 values[0]=0;values[1]=values[2]=(20+15*(rank-1))*256;
 frees=updates=refreshes=unregisters=gfx=allocations=posted=scheduled=roomRefresh=0;trace.clear();events.clear();
 int ok=SKILLS_SrvDo018_DefensiveBuff(&game,&owner,68,rank);auto* current=STATLIST_GetStatListFromUnitAndState(&owner,14);
 int active=0;for(int i=0;i<7;i++)if(owner.active[ids[i]])active|=1<<i;
 bool event=events==std::vector<std::array<int,6>>{{11,68,rank,22,1,14}};
 bool order=trace==std::vector<int>{1,2,3,4,5};
 bool independent=owner.active[31]&&owner.active[159]&&owner.lists[31]->expiry==999&&owner.lists[159]->expiry==999;
 bool metadata=current&&current->skill==68&&current->rank==rank&&current->stats[350]==68&&current->stats[351]==rank&&current->callback==SKILLS_CurseStateCallback_DefensiveBuff&&current->creator==123&&current->creatorType==UNIT_PLAYER;
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",mask,oldRank,rank,invalid,ok,active,frees,allocations,posted,scheduled,roomRefresh,current?current->stats[132]:0,current?current->stats[133]:0,current?current->expiry:0,event,order,independent,metadata,updates,gfx,owner.dwFlags);first=false;
 for(auto& pair:owner.lists)delete pair.second;
}
printf("],\"removals\":[");first=true;
for(int dead:{0,1})for(int retain:{0,1}){D2UnitStrc owner;owner.active[14]=true;owner.dead=dead;stay=retain;unregisters=updates=refreshes=0;trace.clear();SKILLS_CurseStateCallback_DefensiveBuff(&owner,14,nullptr);
 printf("%s[%d,%d,%d,%d,%d,%d]",first?"":",",dead,retain,owner.active[14],unregisters,updates,refreshes);first=false;}
printf("]}\n");}
'''
    path = STAGE / 'probe.cpp'
    path.write_text(cpp)
    compiler, sanitizer = STAGE / 'compiler.log', STAGE / 'sanitizer.log'
    with compiler.open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(STAGE / 'probe')], stdout=log, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(STAGE / 'probe')], capture_output=True)
    sanitizer.write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-3000:]
    cases = json.loads(run.stdout)
    assert len(cases['casts']) == 3072 and len(cases['removals']) == 4
    for mask, old, rank, invalid, ok, active, freed, allocated, posted, scheduled, room, minimum, maximum, expiry, event, order, independent, metadata, updates, gfx, flags in cases['casts']:
        if invalid:
            assert (ok, active, freed, allocated, posted, scheduled, room, event, order, independent, metadata, updates, gfx, flags) == (0, mask, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0)
            assert (minimum, maximum, expiry) == ((1, 999, 999) if mask & 2 else (0, 0, 0))
        else:
            assert (ok, active, freed, allocated, posted, scheduled, room, event, order, independent, metadata, updates, gfx, flags) == (1, 2, mask.bit_count(), 1, 1, 0, 1, 1, 1, 1, 1, 2, 1, 8)
            assert minimum == maximum == (20 + 15 * (rank - 1)) * 256 and expiry == 0
    for dead, retain, active, unregisters, updates, refreshes in cases['removals']:
        kept = int(bool(dead and retain))
        assert (active, unregisters, updates, refreshes) == (kept, 1, 1 - kept, 1 - kept)
    fixture = STAGE / 'cases.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    artifacts = [path, compiler, sanitizer, fixture, Path(__file__).resolve()]
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], inputs=inputs, routines=routines,
                  castCases=3072, removalCases=4, currentGroup1=group,
                  artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
                  established=[
                      'Six complete unchanged source bodies compose do18, group clearing, the actual shared state factory, both stat writers and the defensive removal callback. All128 group masks, old ranks1/20, new ranks1/20/255 and missing-row/invalid-state guards execute with clean ASan/UBSan.',
                      'Valid group1 casts clear all old group members including Bone Armor before the factory. Lower-rank recasts therefore allocate a fresh zero-duration list rather than hitting the factory lower-rank rejection. Independent Venom/Fade remain.',
                      'The new list receives both supplied pool values, original creator/type, skill/rank and defensive removal callback. One absorbed-damage descriptor captures skill68/rank/callback22/type1/state14. Observed order is allocate, post, unregister, register, gfx. No timed removal is scheduled.',
                      'Removal unregisters before the live/dead state rule; ordinary removal clears state and refreshes animation/passives. The synthetic stay-death exception is separate.'],
                  services=[
                      'The Assassin scaffold supplies host stat storage and allocation, state/event sinks, room refresh and animation refresh. Current group membership/stat IDs are pinned; curse/exp masks and state flag word are supplied noncurse/false/zero for this Bone Armor context.',
                      'Capacity formula outputs are explicit opaque inputs, not a compiled evaluator or synergy proof. Successful host allocation is supplied. No real memory-allocation failure or complete event registry/scheduler is certified.',
                      'Native learned cast/pool/reload acceptance remains the separately pinned Bone Armor build3/run3; this source increment changes no production behavior.'],
                  remaining=[
                      'Modern event22 overflow and dispatch identity remain unverified; see bone-armor-current-reference.json. The older breaking-hit discrepancy is not changed by cast composition.',
                      'Full modern stat/event graph, incoming Energy Shield priority, special producers, state gfx/client and old-binary migration remain open. No class completion or new native acceptance is claimed.'])
    receipt = ROOT / 'tools/reference/bone-armor-cast-source-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert receipt.read_text() == rendered
    else:
        receipt.write_text(rendered)
    print('Bone Armor:3072 complete cast/group/factory/writer contexts and4 removal contexts pass; modern overflow remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
