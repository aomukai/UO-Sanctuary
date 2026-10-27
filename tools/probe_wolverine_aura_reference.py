#!/usr/bin/env python3
"""Reuse the missile search ABI to execute complete pinned Wolverine do65/filter/iterator paths."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    preflight = ROOT / 'tools/reference/heart-of-wolverine-preflight.json'
    current = json.loads(preflight.read_text())['currentRows']
    assert [current['skills']['Wolverine Aura']['Param' + str(i)] for i in range(1, 7)] == ['30', '2', '25', '7', '20', '7']
    assert current['states']['wolverine']['*ID'] == '148' and current['states']['wolverinecontrol']['*ID'] == '162'
    stat_path = ROOT / 'tools/reference/d2r-named-equipment/json/itemstatcost.json'
    manifest = json.loads((stat_path.parent.parent / 'sources.json').read_text())
    assert sha(stat_path) == manifest['files']['json/itemstatcost.json']['sha256']
    stats = json.loads(stat_path.read_text())
    assert stats['item_tohit_percent']['*ID'] == 119 and stats['damagepercent']['*ID'] == 25
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}

    def source(path):
        raw = (STAGE / 'd2moo-pinned-source' / path).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == path)
        hashes[path] = hashlib.sha256(raw).hexdigest()
        return raw.decode()

    def routine(text, name):
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end, depth = text.index('{', match.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}'); end += 1
        body = text[match.start():end]
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    skills = source('source/D2Game/src/SKILLS/Skills.cpp')
    pal = source('source/D2Game/src/SKILLS/SkillPal.cpp')
    prefix = (STAGE / 'lightning-fury-search-source-probe.cpp').read_text().split('int32_t __fastcall sub_6FD0FA00', 1)[0]
    prefix = prefix.replace('#include <vector>', '#include <vector>\n#include <initializer_list>')
    prefix = prefix.replace('struct D2GameStrc{}', 'struct D2GameStrc{int dwGameFrame=0;}')
    old = 'struct D2SkillsTxt{int dwAuraRangeCalc=0,dwCalc[1]={1},dwAuraFilter=42371;};'
    assert old in prefix
    prefix = prefix.replace(old, 'struct D2SkillsTxt{int dwAuraRangeCalc=1,dwPerDelay=3,nPassiveState=0,nAuraState=162,wAuraTargetState=148;unsigned dwAuraFilter=65795;int wAuraStat[6]={119,25,-1,-1,-1,-1},dwAuraStatCalc[6]={2,4},nPassiveStat[5]={-1,-1,-1,-1,-1},dwPassiveCalc[5]={};};')
    old = 'int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int which,int,int rank){return which==0?15:rank+1;}'
    assert old in prefix
    prefix = prefix.replace(old, 'int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int which,int,int rank){return which==1?30+2*(rank-1):which==2?25+7*(rank-1):which==4?20+7*(rank-1):0;}')
    cpp = prefix + r'''
constexpr int STAT_MANA=8,STATE_NOMANAREGEN=10;
struct Tables{int nStatesTxtRecordCount=256,nItemStatCostTxtRecordCount=512;}tables;auto* sgptDataTables=&tables;
struct D2StatListStrc{}list;
struct D2BasicAuraParamStrc{int nSkillId=0,nSkillLevel=0,nDuration=0,nPassiveState=0,nAuraState=0,nAuraStat[6]={},nAuraStatCalcValue[6]={},field_40=0;D2StatListStrc*pStatList=nullptr;};
struct Event{int unit,state,rank,stat1,value1,stat2,value2,duration;};std::vector<Event>calls;
int SKILLS_GetManaCosts(int,int){return 0;}int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return 0;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*,int,int,int){}void STATES_ToggleState(D2UnitStrc*,int,int){}
void D2GAME_SKILLMANA_AuraConsume_6FD10C90(D2UnitStrc*,int){__builtin_trap();}
int SKILLS_AuraCallback_BasicAura(D2AuraCallbackStrc* cb,D2UnitStrc*target){auto* a=(D2BasicAuraParamStrc*)cb->pArgs;calls.push_back({target->id,a->nAuraState,a->nSkillLevel,a->nAuraStat[0],a->nAuraStatCalcValue[0],a->nAuraStat[1],a->nAuraStatCalcValue[1],a->nDuration});a->pStatList=&list;a->field_40++;return 1;}
'''
    for name in ['sub_6FD0FA00', 'sub_6FD0FE50', 'sub_6FD0FE80', 'SKILL_ComputePeriodicRate']:
        cpp += routine(skills, name)
    cpp += routine(pal, 'SKILLS_SrvDo065_BasicAura')
    cpp += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool comma=false;printf("[");
for(int rank:{1,20,21,255})for(int frame:{0,1,4,5,1001})for(int kind=0;kind<=10;kind++)for(int sourceTown:{0,1}){
caster={};caster.dwUnitType=UNIT_MONSTER;caster.dwClassId=1;caster.id=100;caster.hostile=false;caster.room=&rooms[0];
for(auto&m:monsters)m={};for(auto&r:rooms)r={};rooms[0].town=sourceTown;rooms[0].pUnitFirst=&caster;adjacent[0]=&rooms[0];adjacent[1]=&rooms[1];
targets[0]={};auto&t=targets[0];t.id=200;t.dwUnitType=kind==0?UNIT_PLAYER:UNIT_MONSTER;t.hostile=kind==6;t.room=&rooms[1];t.x=caster.x+1;t.y=caster.y;
rooms[1].pUnitFirst=kind==10?nullptr:&t;rooms[1].town=kind==1;
if(kind==2)t.dwAnimMode=MONMODE_DEAD;if(kind==3)monsters[0].dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_NPC];if(kind==4)monsters[0].dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_NOAURA];
if(kind==5)t.x=caster.x+30+2*(rank-1)+1;if(kind==9)t.x=caster.x+30+2*(rank-1);if(kind==8)t.dwFlags=0;wall=kind==7;
game.dwGameFrame=frame;calls.clear();int next=SKILL_ComputePeriodicRate(&game,&caster,&skill,297,rank);int result=SKILLS_SrvDo065_BasicAura(&game,&caster,297,rank);
if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,[",rank,frame,kind,sourceTown,next,result);bool start=true;
for(auto c:calls){if(!start)printf(",");start=false;printf("[%d,%d,%d,%d,%d,%d,%d,%d]",c.unit,c.state,c.rank,c.stat1,c.value1,c.stat2,c.value2,c.duration);}printf("]");printf("]");
}printf("]\n");}
'''
    path = STAGE / 'wolverine-aura-source-probe.cpp'; path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True, check=True)
    assert not result.stderr, result.stderr.decode()
    rows = json.loads(result.stdout); assert len(rows) == 440
    for rank, frame, kind, source_town, next_frame, success, calls in rows:
        assert next_frame == frame + 5 and success == 1
        call = [100, 162, rank, 119, 25 + 7 * (rank - 1), 25, 20 + 7 * (rank - 1), 6]
        expected = [call]
        if kind in [0, 7, 8, 9]:
            expected.append([200, 148, rank, *call[3:]])
        assert calls == expected, (rank, frame, kind, source_town, calls, expected)
    fixture = STAGE / 'wolverine-aura-source-probe.json'; fixture.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    reused = {}
    for name in ['inner-sight-engine-evidence.json']:
        evidence = ROOT / 'tools/reference' / name
        assert evidence.is_file()
        reused[name] = sha(evidence)
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
        preflightSha256=sha(preflight), itemStatCostSha256=sha(stat_path), sourceHashes=hashes, unchangedRoutineHashes=routines,
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=len(rows), generatedSourceSha256=sha(path), sanitizers='ASan+UBSan'),
        reusedEvidenceHashes=reused,
        established=[
            '440complete do65/self-wrapper/recipient-iterator/filter/periodic-rate contexts:5frame refresh,6frame duration,self162and target148states,no mana payment and current radius/AR/damage values at four ranks.',
            'Allied live players/monsters are selected at the closed source radius. Town recipients,dead monsters,NPCs,NOAURA monsters,hostiles,outside radius and the emitter itself are excluded. LOS and CANBEATTACKED are not required by0x10103; source town does not suppress the separate self callback.',
            'The generic same-skill/state rank and creator/expiry contract reuses complete sub_6FD10EC0from the recorded Inner Sight probe. Existing AR/damage consumers still need native integration; no life resource callback is asserted. Current Wolverine states148/162have no curse flag.'
        ], boundedServices=[
            'BasicAura callback is a recording service here:actual stat-list allocation/setting,derived AR/physical damage updates and saved ownership require separate native tests; neither that integration nor a compiled full callback is claimed.',
            'Rooms,coordinates,alignment,types/modes,NPC/NOAURA metadata,formula evaluation and mana are bounded ABI services. The five named outer routines execute unchanged. Native party friendliness,2subtiles/tile,16height,serial ordering and no-lag-pulse-replay remain declared adapters.',
            'Modern executable timing/global event ties,controller movement and learned casting remain unverified. No official source-equivalence certificate is inferred from this older reconstruction.'
        ])
    output = ROOT / 'tools/reference/wolverine-aura-engine-evidence.json'; rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Wolverine aura:440complete do65/filter/iterator/self/periodic contexts pass ASan/UBSan; stat-list callback remains an explicit recording service.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
