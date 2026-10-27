#!/usr/bin/env python3
"""Execute legacy transformed Attack allocation with current form skill fields."""
import argparse
import csv
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
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}

    def extract(name, symbol):
        raw = (BASE / 'd2moo-pinned-source' / name).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == name)
        hashes[name] = hashlib.sha256(raw).hexdigest()
        body = function(raw.decode(), symbol)
        routines[symbol] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    preflight_path = ROOT / 'tools/reference/druid-forms-preflight.json'
    preflight = json.loads(preflight_path.read_text())
    local = ROOT / '.local/research/d2r-3.3.93847' / preflight['localTables']['skills']['filename']
    assert sha(local) == preflight['localTables']['skills']['sha256']
    licensed = {row['skill']: row for row in csv.DictReader(local.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t')}
    public_path = ROOT / 'tools/reference/d2r-named-equipment/json/skills.json'
    manifest = json.loads((ROOT / 'tools/reference/d2r-named-equipment/sources.json').read_text())
    assert sha(public_path) == manifest['files']['json/skills.json']['sha256']
    public = json.loads(public_path.read_text())
    fields = {}
    for id_, name in [('223', 'Wearwolf'), ('228', 'Wearbear')]:
        row = licensed[name]
        fields[id_] = {key: int(row.get(key) or 0) for key in ['ToHit', 'LevToHit', 'SrcDam']}
        assert not row.get('ToHitCalc')
        assert fields[id_] == {key: int(public[id_].get(key) or 0) for key in fields[id_]}
    assert fields['223'] == dict(ToHit=50, LevToHit=15, SrcDam=128)
    assert fields['228'] == dict(ToHit=0, LevToHit=0, SrcDam=128)
    cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define __stdcall
struct D2GameStrc{}game;
struct D2UnitStrc{}owner,target;
struct D2StatesTxt{}state;
struct D2SkillsTxt{unsigned dwToHitCalc=unsigned(-1);int dwToHit=0,dwLevToHit=0;uint8_t nSrcDam=128;}wolf,bear;
struct D2StatListStrc{int skill=0;}wolfList{223},bearList{228};
struct D2DamageStrc{int dwHitClass=0;unsigned wResultFlags=0;};
constexpr int STAT_MODIFIERLIST_SKILL=1;
int transformStates[]={139,140};
struct Tables{int nTransformStates=2;int* pTransformStates=transformStates;}tables;auto*sgptDataTables=&tables;
int active=0,guard=0,toHit=0,allocations=0,sourceDamage=-1,hitClass=0,resultFlags=0;
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return guard==1?nullptr:&target;}
bool STATES_CheckState(D2UnitStrc*,int id){assert(id==139||id==140);return active&(id==139?1:2);}
D2StatesTxt* SKILLS_GetStatesTxtRecord(int id){return id==139&&guard==2?nullptr:&state;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int id){return id==139?(guard==3?nullptr:&wolfList):&bearList;}
int STATLIST_GetStatValue(D2StatListStrc* list,int stat,int layer){assert(stat==STAT_MODIFIERLIST_SKILL&&layer==0);return list->skill;}
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){assert(id==223||id==228);return id==223?(guard==4?nullptr:&wolf):&bear;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return DATATBLS_GetSkillsTxtRecord(id);}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,unsigned,int,int){assert(false);return 0;}
unsigned SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int factor,int flags){assert(flags==0);toHit=factor;return 0x1234;}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* damage,uint8_t src)
{allocations++;sourceDamage=src;hitClass=damage->dwHitClass;resultFlags=damage->wResultFlags;}
'''
    cpp += extract('source/D2Common/src/D2Skills.cpp', 'SKILLS_GetToHitFactor')
    cpp += extract('source/D2Game/src/SKILLS/SkillDruid.cpp', 'sub_6FD00370')
    cpp += r'''
int main(){wolf.dwToHit=50;wolf.dwLevToHit=15;printf("[");bool first=true;
 for(int level:{0,1,3,20,28,60,255})for(int mask:{0,1,2,3})for(int failure:{0,1,2,3,4})for(int override_:{0,1,2}){
  active=mask;guard=failure;toHit=allocations=hitClass=resultFlags=0;sourceDamage=-1;
  wolf.nSrcDam=override_==1?64:override_==2?0:128;bear.nSrcDam=override_==1?0:override_==2?192:128;
  int result=sub_6FD00370(&game,&owner,level);
  printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",level,mask,failure,override_,result,toHit,allocations,sourceDamage,hitClass,resultFlags);first=false;
 }printf("]\n");}
'''
    generated = BASE / 'druid-form-basic-packet-source-probe.cpp';generated.write_text(cpp)
    compiler = BASE / 'druid-form-basic-packet-compiler.log'
    with compiler.open('wb') as output:
        # Original -1 sentinel is compared with an unsigned field.
        subprocess.run(['g++', '-std=c++20', '-Wall', '-Wextra', '-Werror', '-Wno-sign-compare', '-O1', '-g',
            '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(generated.with_suffix(''))], stdout=output, stderr=subprocess.STDOUT, check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    sanitizer = BASE / 'druid-form-basic-packet-sanitizer.log';sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-2000:]
    cases = json.loads(process.stdout)
    assert len(cases) == 420
    for level, mask, guard, override_, result, ar, allocations, src, hit_class, flags in cases:
        if guard == 1:
            assert [result, ar, allocations, src, hit_class, flags] == [0, 0, 0, -1, 0, 0]
            continue
        expected_ar = 50 + (level - 1) * 15 if level > 0 and mask & 1 and guard not in [2, 3, 4] else 0
        expected_src = 128
        if mask & 1 and guard not in [2, 3, 4] and override_ != 2:
            expected_src = 64 if override_ == 1 else 128
        if mask & 2 and override_ != 1:
            expected_src = 192 if override_ == 2 else 128
        assert [result, ar, allocations, src, hit_class, flags] == [1, expected_ar, 1, expected_src, 1, 0x1234]
    fixture = BASE / 'druid-form-basic-packet-source-probe.json';fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourcePreparation=True, nativeAccepted=False, currentExecutableParity=False,
        currentFields=fields, licensedSkillsSha256=sha(local), publicSkillsSha256=sha(public_path), sourceHashes=hashes, completeRoutineHashes=routines,
        contexts=420, generatedSourceSha256=sha(generated), compilerLogSha256=sha(compiler), sanitizerLogSha256=sha(sanitizer),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)),
        established=[
            'Complete transformed Attack allocation and ToHit-factor routines execute unchanged with current licensed/public-correlated Werewolf50+15per-rank fields,Werebear0and both SrcDam128. Supplied hit-roll flags reach allocation unchanged with hitClass1. Missing target prevents allocation; missing state metadata,list or skill skips that contribution.',
            'The old extra ToHit uses the incoming attack skill-level argument,not a fetched transformation rank. With incoming rank1it supplies50when wolf is active. This is separate from the captured form item_tohit_percent modifier. Zero-source defaults128;later nonzero transform contributions replace it. Simultaneous forms and non128source values are synthetic controls,not valid current gameplay.',
        ], remaining=[
            'Whether current3.3.93847retains the older extra ToHit path is unresolved. Do not silently add it to the current native form item-percent bonus. This source execution does not establish the current outer Attack argument,modern stat/roll/damage ordering or a current-executable observation.',
            'State/list/table lookup and combat allocation/result rolling are declared services. Full damage filling,weapon type/ammunition,range,timing,hit/block and native form casting remain separate. Native default weapon SrcDam128is already the full weapon fraction; no learned form handler is enabled by this proof.',
        ])
    path = ROOT / 'tools/reference/druid-form-basic-packet-engine-evidence.json';rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Druid transformed Attack:420complete allocation/ToHit contexts,ASan/UBSan clean; modern extra-AR remains explicitly unverified.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
