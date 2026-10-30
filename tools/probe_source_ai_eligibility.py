#!/usr/bin/env python3
"""Execute complete older AI-permission predicates against explicit source state."""
import argparse
import csv
import hashlib
import itertools
import json
from pathlib import Path
import re
import subprocess

from probe_warlock_runtime import ROOT, digest

STAGE = ROOT / '.local/class-skills/revive-ai-eligibility'
FIXTURE = ROOT / '.local/class-skills/source-ai-eligibility-source-probe.json'


def build(check=False):
    STAGE.mkdir(parents=True, exist_ok=True)
    tree_path = ROOT / '.local/class-skills/d2moo-source-tree.json'
    tree = json.loads(tree_path.read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    paths = [__file__, tree_path]
    routines = {}

    def block(rel, name):
        path = ROOT / '.local/class-skills/d2moo-pinned-source' / rel
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(r['sha'] for r in tree['tree'] if r['path'] == rel)
        paths.append(path)
        text = raw.decode()
        start = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert start
        end = text.index('{', start.start()) + 1
        depth = 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[start.start():end]
        routines[name] = digest(body.encode())
        return body + '\n'

    primary_path = ROOT / '.local/class-skills/generic-pet-search-services/monstats.txt'
    secondary_path = ROOT / '.local/class-skills/generic-pet-search-nested/monstats2.txt'
    rule_path = ROOT / 'data/Sanctuary/Balance/conversion-source-rules.json'
    names_path = ROOT / 'data/Sanctuary/Balance/generic-pet-search-monsters.json'
    primary = [r for r in csv.DictReader(primary_path.read_text().splitlines(), delimiter='\t') if r['Id'] != 'Expansion']
    secondary = {r['Id']: r for r in csv.DictReader(secondary_path.read_text().splitlines(), delimiter='\t') if r['Id'] != 'Expansion'}
    rules = {r['source']: r for r in json.loads(rule_path.read_text())}
    names = json.loads(names_path.read_text())
    assert len(primary) == len(rules) == len(names) == 751
    ids = {r['Id']: i for i, r in enumerate(primary)}
    assert ids['imp1'] == 492
    metadata = []
    for i, row in enumerate(primary):
        rule = rules[row['Id']]
        flags = (bool(int(secondary.get(row['MonStatsEx'], {}).get('mWL') or 0)), bool(int(row['boss'] or 0)), bool(int(row['switchai'] or 0)))
        assert names[i]['id'] == i and names[i]['name'] == row['Id']
        assert rule['base'] == row['BaseId']
        assert (rule['walk'], rule['boss'], rule['switchAi']) == flags
        metadata.append([ids[row['BaseId']], *map(int, flags)])
    paths += [primary_path, secondary_path, rule_path, names_path]
    cpp = r'''#include <cassert>
#include <cstdint>
#include <iostream>
#define __fastcall
#define BOOL int
#define TRUE 1
#define FALSE 0
enum D2C_AiSpecialState:uint32_t {AISPECIALSTATE_NONE=0,AISPECIALSTATE_REVIVED=7,AISPECIALSTATE_DIMVISION=10,AISPECIALSTATE_TERROR=11,AISPECIALSTATE_TAUNT=12,AISPECIALSTATE_INVALID=19};
constexpr int UNIT_MONSTER=1,MONSTER_IMP1=492,STATE_ATTACHED=1,STATE_UNINTERRUPTABLE=2;
constexpr uint32_t UNITFLAG_CANBEATTACKED=4,MONTYPEFLAG_UNIQUE=1,MONTYPEFLAG_SUPERUNIQUE=2,MONSTATS2MODEFLAG_WL=1,MONSTATSFLAG_BOSS=1,MONSTATSFLAG_SWITCHAI=2;
struct MonsterData{uint16_t nTypeFlag=0;}monsterData;
struct D2UnitStrc{int dwUnitType=1,dwClassId=0;uint32_t dwFlags=0;bool dead=false,uninterruptible=false,unique=false,super=false,attached=false;MonsterData* pMonsterData=nullptr;}unit;
struct D2MonStatsTxt{int nBaseId=0,wMonStatsEx=0;uint32_t dwMonStatsFlags=0;}primary[751];
struct D2MonStats2Txt{uint32_t dwModeFlags=0;}secondary;
struct Tables{int nMonStatsTxtRecordCount=751;D2MonStatsTxt* pMonStatsTxt=primary;}tables;
auto* sgptDataTables=&tables;bool hasSecondary=true;
D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int cls){return cls>=0&&cls<751?&primary[cls]:nullptr;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int cls){return DATATBLS_GetMonStatsTxtRecord(cls);}
D2MonStats2Txt* UNITS_GetMonStats2TxtRecord(int id){assert(id==0);return hasSecondary?&secondary:nullptr;}
bool STATES_CheckState(D2UnitStrc* u,int state){assert(u);assert(state==STATE_ATTACHED||state==STATE_UNINTERRUPTABLE);return state==STATE_ATTACHED?u->attached:u->uninterruptible;}
bool SUNIT_IsDead(D2UnitStrc* u){assert(u);return u->dead;}
'''
    for rel, name in [('source/D2Common/src/Units/Units.cpp', 'UNITS_CanSwitchAI'), ('source/D2Game/src/MONSTER/MonsterUnique.cpp', 'MONSTERUNIQUE_CheckMonTypeFlag'), ('source/D2Game/src/AI/AiThink.cpp', 'AITHINK_CanUnitSwitchAi'), ('source/D2Game/src/AI/AiUtil.cpp', 'AIUTIL_CanUnitSwitchAi'), ('source/D2Game/src/SKILLS/Skills.cpp', 'sub_6FD15190')]:
        cpp += block(rel, name)
    cpp += r'''
int main(){int present,kind,cls,bits,base,walk,boss,sw,secondaryPresent;uint32_t state;bool comma=false;std::cout<<'[';
while(std::cin>>present>>kind>>cls>>state>>bits>>base>>walk>>boss>>sw>>secondaryPresent){
unit={};unit.dwUnitType=kind;unit.dwClassId=cls;unit.dwFlags=(bits&1)?4:0;unit.dead=bits&2;unit.uninterruptible=bits&4;unit.unique=bits&8;unit.super=bits&16;unit.attached=bits&32;monsterData.nTypeFlag=(unit.unique?1:0)|(unit.super?2:0);unit.pMonsterData=(bits&64)?nullptr:&monsterData;
for(auto& r:primary){r.nBaseId=base;r.dwMonStatsFlags=(boss?1:0)|(sw?2:0);}secondary.dwModeFlags=walk?1:0;hasSecondary=secondaryPresent;
auto* u=present?&unit:nullptr;auto s=static_cast<D2C_AiSpecialState>(state);
if(comma){std::cout<<',';}comma=true;std::cout<<'['<<sub_6FD15190(u,s)<<','<<AIUTIL_CanUnitSwitchAi(u,s)<<','<<AITHINK_CanUnitSwitchAi(u,nullptr,s,1)<<','<<AITHINK_CanUnitSwitchAi(u,nullptr,s,0)<<','<<UNITS_CanSwitchAI(cls)<<']';}
std::cout<<"]\n";}
'''
    source = STAGE / 'source-probe.cpp'
    source.write_text(cpp)
    binary = STAGE / 'source-probe'
    compile_ = subprocess.run(['g++', '-std=c++17', '-O1', '-g', '-Wall', '-Wextra', '-Werror', '-Wno-unused-parameter', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(source), '-o', str(binary)], capture_output=True)
    (STAGE / 'compiler.log').write_bytes(compile_.stdout + compile_.stderr)
    assert compile_.returncode == 0, compile_.stderr.decode()[-3000:]
    units = [(0, 1, 0), (1, 1, 0), (1, 1, 492), (1, 1, 493), (1, 0, 0), (1, 3, 0), (1, 1, -1), (1, 1, 751)]
    cases = []
    for u, bits, base, walk, boss, sw, has in itertools.product(units, range(128), [0, 492], [0, 1], [0, 1], [0, 1], [0, 1]):
        cases.append([*u, 7, bits, base, walk, boss, sw, has])
    for u, bits, state, flags in itertools.product(units, range(128), [0, 10, 11, 12, 19, 20, 4294967295], [(0, 1, 0, 1), (492, 1, 0, 1), (0, 0, 0, 1), (0, 1, 1, 1)]):
        cases.append([*u, state, bits, *flags, 1])
    current = [[1, 1, cls, 7, bits, *row, 1] for cls, row in enumerate(metadata) for bits in range(128)]
    requests = cases + current
    run = subprocess.run([str(binary)], input=''.join(' '.join(map(str, r)) + '\n' for r in requests).encode(), capture_output=True)
    (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-3000:]
    results = json.loads(run.stdout)
    assert len(results) == len(requests)
    fixture = dict(cases=[[p, r] for p, r in zip(cases, results[:len(cases)], strict=True)], current=[[p[2], p[4], r[0]] for p, r in zip(current, results[len(cases):], strict=True)], metadata=metadata)
    encoded = json.dumps(fixture, separators=(',', ':')) + '\n'
    if check:
        assert FIXTURE.read_text() == encoded
    else:
        FIXTURE.write_text(encoded)
    paths += [source, STAGE / 'compiler.log', STAGE / 'sanitizer.log', FIXTURE]
    receipt = dict(referenceVersion='3.3.93847', component='Complete older skill/utility/think/species AI-switch predicates using existing current Conversion catalog', processMemoryRead=False, handlerEnabled=False, modernCodeEquivalenceClaimed=False,
        verified=dict(completeRoutines=5, syntheticContexts=len(cases), predicatesPerContext=5, currentSpecies=751, currentRevivedContexts=len(current), currentCatalogFieldsChecked=3004, sanitizers='ASan/UBSan exit0,empty stderr'),
        routineHashes=routines, legacyCommit=tree['sha'], artifacts={str(Path(p).absolute().relative_to(ROOT)): digest(Path(p).read_bytes()) for p in sorted(set(map(str, paths)))},
        boundaries=['Dynamic source attackable/dead/uninterruptible/unique/superunique/attached fields are explicit inputs. Their native corpse producer is not established by these comparisons.', 'Source table access and nested state/unique/dead services are explicit fixture boundaries. The current Conversion table matches the owned current text walking/boss/switchAI/base-ID fields for751species; this does not prove modern executable AI equivalence.', 'No native actor restoration, original special abilities, GUID reservation, current passive/event transfer or full Revive lifecycle is enabled.'])
    output = ROOT / 'tools/reference/source-ai-eligibility-evidence.json'
    encoded = json.dumps(receipt, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print(f'AI eligibility:{len(cases)} synthetic contexts/five predicates,{len(current)} current Revive contexts;5 complete older routines;no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
