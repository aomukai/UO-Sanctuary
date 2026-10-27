#!/usr/bin/env python3
"""Header-backed complete raw monster-mode writer for all three Druid beasts."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
REFERENCE = ROOT / 'tools/reference/d2r-named-equipment'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    prior = json.loads((ROOT / 'tools/reference/druid-beast-attack-engine-evidence.json').read_text())
    parent = BASE / 'druid-beast-attack-source-probe.cpp'
    assert sha(parent) == prior['fixture']['generatedSourceSha256']
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == prior['legacyCommit'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    cpp, tail = parent.read_text().split('\nvoid prepare(int species)', 1)
    sources, routines = {}, {}
    for relative, names in [
        ('source/D2Common/src/D2Skills.cpp', ['SKILLS_CalculateDamageBonusByLevel', 'SKILLS_GetMinPhysDamage', 'SKILLS_GetMaxPhysDamage', 'SKILLS_GetToHitFactor']),
        ('source/D2Common/src/DataTbls/MonsterTbls.cpp', ['DATATBLS_CalculateMonsterStatsByLevel']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD14D20']),
        ('source/D2Game/src/MONSTER/MonsterMode.cpp', ['sub_6FC627B0']),
        ('source/D2Common/include/D2Monsters.h', []),
        ('source/D2Common/include/Units/Units.h', []),
        ('source/D2Common/include/DataTbls/MonsterTbls.h', []),
    ]:
        raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        sources[relative] = hashlib.sha256(raw).hexdigest()
        for name in names:
            body = function(raw.decode(), name)
            assert cpp.count(body) == 1, name
            routines[name] = hashlib.sha256(body.encode()).hexdigest()
    header = (BASE / 'd2moo-pinned-source/source/D2Common/include/D2Monsters.h').read_text()
    modes = {name:int(value,16) for name,value in re.findall(r'(MONMODE_\w+),\s*//0x([0-9A-Fa-f]+)', header)}
    assert len(modes) == 16 and sorted(modes.values()) == list(range(16))
    assert [modes[k] for k in ['MONMODE_ATTACK1', 'MONMODE_ATTACK2', 'MONMODE_BLOCK', 'MONMODE_CAST', 'MONMODE_SKILL1', 'MONMODE_SEQUENCE']] == [4, 5, 6, 7, 8, 14]
    assert cpp.count('MONMODE_BLOCK=3') == 1
    cpp = cpp.replace('MONMODE_BLOCK=3', 'MONMODE_BLOCK=6')
    units_header=(BASE/'d2moo-pinned-source/source/D2Common/include/Units/Units.h').read_text()
    monster_header=(BASE/'d2moo-pinned-source/source/D2Common/include/DataTbls/MonsterTbls.h').read_text()
    assert 'UNIT_ALIGNMENT_EVIL = 0' in units_header and 'UNIT_ALIGNMENT_GOOD = 2' in units_header and 'MONALIGN_GOOD = 1' in monster_header
    assert cpp.count('int STATLIST_GetUnitAlignment(D2UnitStrc*){return 1;}')==1
    cpp=cpp.replace('int STATLIST_GetUnitAlignment(D2UnitStrc*){return 1;}','int STATLIST_GetUnitAlignment(D2UnitStrc*){return 2;}')
    manifest = json.loads((REFERENCE / 'sources.json').read_text())['files']
    tables = {}
    for name in ['monstats', 'skills']:
        path = REFERENCE / f'json/{name}.json'
        assert sha(path) == manifest[f'json/{name}.json']['sha256']
        tables[name] = json.loads(path.read_text())
    cpp += '\nvoid prepare(int species){mon={};skill={};\n'
    for species, (monster, key) in enumerate([('spiritwolf', 'Summon Spirit Wolf'), ('fenris', 'Summon Fenris'), ('druidbear', 'Summon Grizzly')]):
        row = next(x for x in tables['monstats'].values() if x.get('Id') == monster)
        skill = next(x for x in tables['skills'].values() if x.get('skill') == key)
        assert row['noRatio'] == row['Align'] == 1 and row['SkillDamage'] == key
        assert not any(row.get(f'El{i}Mode', '') for i in range(1, 4))
        assert not any(skill.get(k, '') for k in ['SrcDam', 'DmgSymPerCalc', 'ToHitCalc', 'kick'])
        cpp += f'if(species=={species}){{mon.wSkillDamage={skill["*Id"]};\n'
        for member, field in [('nA1TH', 'A1TH'), ('nA1MinD', 'A1MinD'), ('nA1MaxD', 'A1MaxD'), ('nA2TH', 'A2TH'), ('nA2MinD', 'A2MinD'), ('nA2MaxD', 'A2MaxD'), ('nS1TH', 'S1TH'), ('nS1MinD', 'S1MinD'), ('nS1MaxD', 'S1MaxD')]:
            for d, suffix in enumerate(['', '(N)', '(H)']):
                cpp += f'mon.{member}[{d}]={row.get(field+suffix, 0)};\n'
        for member, field in [('dwToHit', 'ToHit'), ('dwLevToHit', 'LevToHit'), ('dwMinDam', 'MinDam'), ('dwMaxDam', 'MaxDam'), ('nToHitShift', 'HitShift')]:
            cpp += f'skill.{member}={skill.get(field, 0)};\n'
        for side in ['Min', 'Max']:
            for i in range(5):
                cpp += f'skill.dw{side}LvlDam[{i}]={skill.get(side+"LevDam"+str(i+1), 0)};\n'
        cpp += '}\n'
    tail = tail.split('bool first=true;', 1)[1]
    assert tail.count('for(int species:{0,1})') == tail.count('for(int mode:{4,5,7,8})') == 2
    tail = tail.replace('for(int species:{0,1})', 'for(int species:{0,1,2})').replace('for(int mode:{4,5,7,8})', 'for(int mode:{0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15})')
    cpp += '}\nbool first=true;' + tail
    path = BASE / 'beast-mode-stream-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    process = subprocess.run([str(binary)], capture_output=True)
    (BASE / 'beast-mode-stream-sanitizer.log').write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-3000:]
    cases = json.loads(process.stdout)
    assert len(cases) == 76416
    for species, rank, difficulty, expansion, mode, control, minimum, maximum, ar, cleared, writes, owners, ranks in cases:
        if control in [1, 2, 3, 4, 5]:
            assert [minimum, maximum, ar, cleared, writes] == [123, 456, 789, 0, 0]
            assert owners == ranks == int(control == 5)
            continue
        table_ar = [50, 150, 300][species] if mode not in [5, 6, 7, 8] else 0
        getter_ar = [75, 150, 300][species] + 20 * (rank - 1) if rank and control not in [6, 7] else 0
        if species == 0 or control in [6, 7]:
            endpoints = [0, 0]
        else:
            bases = [7, 12] if species == 1 else [30, 60]
            tiers = [[2, 4, 8, 10, 12], [3, 5, 9, 11, 13]] if species == 1 else [[10, 15, 20, 26, 30]] * 2
            endpoints = [bases[side] + sum(tiers[side][0 if i <= 8 else 1 if i <= 16 else 2 if i <= 22 else 3 if i <= 28 else 4] for i in range(2, rank + 1)) for side in [0, 1]]
        assert [minimum, maximum, ar, cleared, writes] == [*endpoints, table_ar + getter_ar, 1, 3], (species, rank, mode, control)
        assert owners == 1 and ranks == int(control != 6)
    fixture = BASE / 'beast-mode-stream-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, nativeConsumer=False,
        sourceHashes=sources, unchangedRoutineHashes=routines, monsterModes=modes, reusedAbiSha256=sha(parent),
        tableHashes={name:sha(REFERENCE / f'json/{name}.json') for name in ['monstats', 'skills']},
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=len(cases), ordinaryContexts=73728, guardContexts=2688, generatedSourceSha256=sha(path), sanitizers='ASan+UBSan'),
        established=[
            'Complete unchanged owner getters,physical tiers,to-hit,monster noRatio helper and flag1mode writer execute73728raw writer contexts:three species,rank0..255,three difficulties,two game contexts,all16actual monster modes.2688guard contexts retain null/nonmonster/metadata/list/owner/skill-row controls.',
            'Friendly alignment service now supplies header GOOD2instead of earlier NEUTRAL1;both are non-evil and the earlier ordinary outputs are unaffected. Header-backed actual BLOCK6selects S1along with cast7and skill1mode8;A2mode5selects A2;all other raw modes select A1,including GH3,dead12andsequence14. Raw sequence14mode-list AR gets literal A1addition; this does not certify the sequence clock or that every tested raw mode request is reachable/appropriate gameplay.',
            'Current owner skill getter/endpoints and copied birth/passives remain distinct. Live owner rank zero retains getter physical baseline but zero to-hit; missing owner/skill removes getter endpoints while literal monster lane AR remains. The shared earlier ABI BLOCK3was wrong;compared ordinary4/5/7/8contexts were unaffected,so frozen old receipts remain bounded to those modes.',
        ], limits=[
            'This is a direct raw writer proof with explicit owner/rank/stat-list/game/layout services. It does not implement or certify native full-mode entry timing,AI112world,GH interruption,sequence14clock/start/action/packet or modern D2R executable equivalence.',
            'No runtime C# changed during the existing Grizzly-body full regression. Apply the corrected mode mapping and native all-mode comparisons in a new dedicated package after that run terminates; do not overwrite accepted packages.',
        ])
    target = ROOT / 'tools/reference/beast-mode-stream-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert target.read_text() == rendered
    else:
        target.write_text(rendered)
    print('Beast raw mode stream:73728ordinary/2688guards,three species/all16header modes pass ASan+UBSan; native correction remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
