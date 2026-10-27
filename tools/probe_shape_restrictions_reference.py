#!/usr/bin/env python3
"""Execute complete shape-restriction functions with current playable rows/state masks."""
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
    local = ROOT / '.local/research/d2r-3.3.93847'
    skills = list(csv.DictReader((local / 'skills.txt').read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
    states = list(csv.DictReader((local / 'states-creeper-verified.txt').read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
    states = [row for row in states if row.get('state') and row.get('*ID', '').isdigit()]
    ids = {row['state']:int(row['*ID']) for row in states}
    catalogue = json.loads((ROOT / 'data/Sanctuary/Balance/class-skills.json').read_text())
    public = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((public / 'sources.json').read_text())
    primary, compared, pins = [], 0, {}
    for table, filename, key, selected, fields in [
        ('skills', 'skills.txt', 'skill', {row['sourceKey'] for row in catalogue}, ['restrict', 'State1', 'State2', 'State3']),
        ('states', 'states-creeper-verified.txt', 'state', set(ids), ['restrict']),
    ]:
        path = public / 'json' / (table + '.json')
        assert sha(path) == manifest['files']['json/' + table + '.json']['sha256']
        prior = {row[key]:row for row in json.loads(path.read_text()).values() if key in row}
        for row in skills if table == 'skills' else states:
            if row.get(key) not in selected:
                continue
            for field in fields:
                assert str(prior[row[key]].get(field, '')) == row.get(field, ''), (table, row[key], field)
                compared += 1
        pins[table] = dict(filename=filename, sha256=sha(local / filename), publicSha256=sha(path))
    for item in catalogue:
        row = next(row for row in skills if row.get('skill') == item['sourceKey'])
        restricted = int(row.get('restrict') or 0)
        assert restricted in [0, 1, 2]
        primary.append(dict(sourceId=item['sourceId'], talentId=item['talentId'], restrict=restricted,
            states=[ids[row['State' + str(i)]] if row.get('State' + str(i)) else -1 for i in range(1, 4)]))
    assert len(primary) == 240 and ids['wolf'] == 139 and ids['bear'] == 140
    attack = next(row for row in skills if row.get('*Id') == '0')
    assert attack['skill'] == 'Attack' and attack['restrict'] == '1'
    common_attack = {key:attack[key] for key in ['skill', '*Id', 'restrict', 'srvdofunc', 'anim', 'SrcDam']}
    state_count = max(ids.values()) + 1
    masks = [0] * ((state_count + 31) // 32)
    for row in states:
        if row.get('restrict') == '1':
            index = int(row['*ID']);masks[index // 32] |= 1 << (index % 32)
    assert all(masks[ids[name] // 32] & (1 << (ids[name] % 32)) for name in ['wolf', 'bear'])
    assert not (masks[ids['freeze'] // 32] & (1 << (ids['freeze'] % 32)))
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    source_hashes, bodies = {}, {}
    for relative, names in [
        ('source/D2Common/src/D2Skills.cpp', ['D2Common_SKILLS_CheckShapeRestriction_6FDB1380']),
        ('source/D2Common/src/D2States.cpp', ['STATES_CheckStateMaskOnUnit', 'STATES_CheckStateMaskRestrictOnUnit']),
        ('source/D2Common/include/DataTbls/SkillsTbls.h', []),
        ('source/D2Common/include/D2States.h', []),
    ]:
        path = BASE / 'd2moo-pinned-source' / relative;raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(row['sha'] for row in tree['tree'] if row['path'] == relative)
        source_hashes[relative] = hashlib.sha256(raw).hexdigest()
        for name in names:
            bodies[name] = function(raw.decode(), name)
    header = (BASE / 'd2moo-pinned-source/source/D2Common/include/DataTbls/SkillsTbls.h').read_text()
    assert re.search(r'int16_t\s+nState\[3\]', header)
    enum = (BASE / 'd2moo-pinned-source/source/D2Common/include/D2States.h').read_text().split('enum D2C_StateMasks', 1)[1].split('};', 1)[0]
    assert re.findall(r'\bSTATEMASK_\w+', enum).index('STATEMASK_RESTRICT') == 17
    cpp = r'''
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <iterator>
#define __fastcall
#define __stdcall
#define ARRAY_SIZE(x) std::size(x)
using BOOL=int;constexpr int TRUE=1,FALSE=0,STATEMASK_RESTRICT=17;
'''
    words = len(masks)
    cpp += f'struct D2SkillsTxt{{int nRestrict=0;int16_t nState[3]={{-1,-1,-1}};}};struct D2SkillStrc{{D2SkillsTxt*pSkillsTxt=nullptr;}};\nstruct D2UnitStrc{{bool hasFlags=true;std::array<uint32_t,{words}> flags{{}};}};\nstruct Tables{{int nStatesTxtRecordCount={state_count};uint32_t fStateMasks[40][{words}]{{}};}}tables,*sgptDataTables=&tables;\n'
    cpp += r'''
uint32_t* D2COMMON_STATES_GetStatFlags_6FDB8A90(D2UnitStrc*p){return p&&p->hasFlags?p->flags.data():nullptr;}
BOOL STATES_CheckState(D2UnitStrc*p,int state){return p&&p->hasFlags&&state>=0&&state<tables.nStatesTxtRecordCount&&(p->flags[state/32]&(1u<<(state%32)));}
'''
    for name in ['STATES_CheckStateMaskOnUnit', 'STATES_CheckStateMaskRestrictOnUnit', 'D2Common_SKILLS_CheckShapeRestriction_6FDB1380']:
        cpp += bodies[name] + '\n'
    cpp += 'int main(){\n'
    for index, mask in enumerate(masks):
        cpp += f'tables.fStateMasks[17][{index}]={mask}u;\n'
    cpp += 'printf("[");bool first=true;\n'
    for row in primary:
        cpp += '{D2SkillsTxt row;row.nRestrict=' + str(row['restrict']) + ';'
        cpp += ''.join(f'row.nState[{i}]={value};' for i, value in enumerate(row['states']))
        cpp += f'''D2SkillStrc skill{{&row}};for(int context=0;context<5;context++)for(int hasFlags=0;hasFlags<2;hasFlags++){{D2UnitStrc owner;owner.hasFlags=hasFlags;
if(context==1||context==3)owner.flags[139/32]|=1u<<(139%32);if(context==2||context==3)owner.flags[140/32]|=1u<<(140%32);if(context==4)owner.flags[{ids['freeze']}/32]|=1u<<({ids['freeze']}%32);
int result=D2Common_SKILLS_CheckShapeRestriction_6FDB1380(&owner,&skill);printf("%s[{row['sourceId']},%d,%d,%d]",first?"":",",context,hasFlags,result);first=false;}}}}\n'''
    cpp += r'''
D2UnitStrc owner;D2SkillsTxt row;D2SkillStrc skill{&row};
assert(!D2Common_SKILLS_CheckShapeRestriction_6FDB1380(nullptr,&skill));assert(!D2Common_SKILLS_CheckShapeRestriction_6FDB1380(&owner,nullptr));
skill.pSkillsTxt=nullptr;assert(!D2Common_SKILLS_CheckShapeRestriction_6FDB1380(&owner,&skill));skill.pSkillsTxt=&row;
assert(!STATES_CheckStateMaskOnUnit(&owner,-1)&&!STATES_CheckStateMaskOnUnit(&owner,40));
row.nRestrict=2;row.nState[0]=-1;row.nState[1]=139;owner.flags[139/32]|=1u<<(139%32);
assert(!D2Common_SKILLS_CheckShapeRestriction_6FDB1380(&owner,&skill));
row.nRestrict=1;for(int state:{-1,139,140}){owner.flags.fill(0);if(state>=0)owner.flags[state/32]|=1u<<(state%32);assert(D2Common_SKILLS_CheckShapeRestriction_6FDB1380(&owner,&skill));}
printf("]\n");}
'''
    generated = BASE / 'shape-restrictions-source-probe.cpp';generated.write_text(cpp)
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(generated.with_suffix(''))], check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    sanitizer = BASE / 'shape-restrictions-sanitizer.log';sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()
    cases = json.loads(process.stdout);assert len(cases) == 2400
    lookup = {row['sourceId']:row for row in primary}
    for source_id, context, has_flags, result in cases:
        row = lookup[source_id];active = {139} if context == 1 else {140} if context == 2 else {139, 140} if context == 3 else set()
        if not has_flags:
            active = set()
        expected = row['restrict'] == 1 or (not active if row['restrict'] == 0 else bool(active.intersection(row['states'])))
        assert result == expected, (source_id, context, has_flags)
    fixture = BASE / 'shape-restrictions-source-probe.json';fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', sourcePreparation=True, nativeAccepted=False, legacyCommit=tree['sha'],
        currentTables=pins, corroboratedFields=compared, statesCount=state_count, currentSkills=primary, commonAttack=common_attack, contexts=2400, guards=9,
        sourceHashes=source_hashes, routineHashes={name:hashlib.sha256(body.encode()).hexdigest() for name,body in bodies.items()},
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)), generatedSourceSha256=sha(generated), sanitizerLogSha256=sha(sanitizer),
        established=[
            'Three complete legacy functions execute2400current playable-skill/state-context/flag-presence cases under ASan/UBSan,plus9null/mask/early-state/common-Attack guards. Header-backed nState[3]and restrict-mask17are explicit; current full state mask words are built from the licensed table.',
            'Restrict0requires no active restrict-mask state; restrict1allows either form or human; restrict2requires the restrict mask and a listed state,stopping on the first negative state. Wolf139/bear140are restrictive; frozen alone is not. The mask helper ignores its skill argument.',
            'Current Druid summons,Cyclone Armor,Hurricane and Armageddon use restrict1; Firestorm/Boulder/Arctic Blast/Fissure/Twister/Volcano/Tornado default0. Maul/Shock Wave require bear,Feral Rage/Rabies/Fury wolf,and Fire Claws/Hunger permit both.',
            'Current common Attack0uses restrict1,do1,A1and SrcDam128; the three human/wolf/bear allowance guards are separate from the240talent rows. This allowance does not establish transformed weapon range,projectile suppression,damage or attack timing.',
        ], remaining=[
            'No native ClassCombat availability/start/release consumer is added by this preparation. Overall skill usability still separately checks rank,mana,items,town and equipment; this restriction result cannot replace those guards.',
            'Current Param12cross-form timing/payment,modern form rates,forced removal and exact current engine/client behavior remain separate. Frozen is only a mask control here,not a claim that a frozen unit can act.',
        ])
    output = ROOT / 'tools/reference/shape-restrictions-engine-evidence.json';rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print(f'Shape restrictions:2400complete source cases,9guards,{compared}current fields; native consumer not enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
