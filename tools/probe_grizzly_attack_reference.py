#!/usr/bin/env python3
"""Reuse the verified beast ABI for Grizzly's ordinary mode-stat composition."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
REFERENCE = ROOT / 'tools/reference/d2r-named-equipment'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    parent = STAGE / 'druid-beast-attack-source-probe.cpp'
    prior = json.loads((ROOT / 'tools/reference/druid-beast-attack-engine-evidence.json').read_text())
    assert sha(parent) == prior['fixture']['generatedSourceSha256']
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == prior['legacyCommit'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    cpp, tail = parent.read_text().split('\nvoid prepare(int species)', 1)
    hashes, routines = {}, {}
    for relative, names in [
        ('source/D2Common/src/D2Skills.cpp', ['SKILLS_CalculateDamageBonusByLevel', 'SKILLS_GetMinPhysDamage', 'SKILLS_GetMaxPhysDamage', 'SKILLS_GetToHitFactor']),
        ('source/D2Common/src/DataTbls/MonsterTbls.cpp', ['DATATBLS_CalculateMonsterStatsByLevel']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD14D20']),
        ('source/D2Game/src/MONSTER/MonsterMode.cpp', ['sub_6FC627B0']),
    ]:
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        for name in names:
            body = function(raw.decode(), name)
            assert cpp.count(body) == 1, name
            routines[name] = hashlib.sha256(body.encode()).hexdigest()
    manifest = json.loads((REFERENCE / 'sources.json').read_text())['files']
    tables = {}
    for name in ['monstats', 'skills']:
        path = REFERENCE / f'json/{name}.json'
        assert sha(path) == manifest[f'json/{name}.json']['sha256']
        tables[name] = json.loads(path.read_text())
    row = next(x for x in tables['monstats'].values() if x.get('Id') == 'druidbear')
    skill = next(x for x in tables['skills'].values() if x.get('skill') == 'Summon Grizzly')
    assert row['noRatio'] == row['Align'] == 1 and row['SkillDamage'] == 'Summon Grizzly'
    assert not any(row.get(f'El{i}Mode', '') for i in range(1, 4))
    assert not any(skill.get(k, '') for k in ['SrcDam', 'DmgSymPerCalc', 'ToHitCalc', 'kick'])
    cpp += f'\nvoid prepare(int species){{assert(species==0);mon={{}};skill={{}};mon.wSkillDamage={skill["*Id"]};\n'
    for member, field in [('nA1TH', 'A1TH'), ('nA1MinD', 'A1MinD'), ('nA1MaxD', 'A1MaxD'), ('nA2TH', 'A2TH'), ('nA2MinD', 'A2MinD'), ('nA2MaxD', 'A2MaxD'), ('nS1TH', 'S1TH'), ('nS1MinD', 'S1MinD'), ('nS1MaxD', 'S1MaxD')]:
        for d, suffix in enumerate(['', '(N)', '(H)']):
            cpp += f'mon.{member}[{d}]={row.get(field+suffix, 0)};\n'
    for member, field in [('dwToHit', 'ToHit'), ('dwLevToHit', 'LevToHit'), ('dwMinDam', 'MinDam'), ('dwMaxDam', 'MaxDam'), ('nToHitShift', 'HitShift')]:
        cpp += f'skill.{member}={skill.get(field, 0)};\n'
    for side in ['Min', 'Max']:
        for i in range(5):
            cpp += f'skill.dw{side}LvlDam[{i}]={skill.get(side+"LevDam"+str(i+1), 0)};\n'
    cpp += '}\nbool first=true;' + tail.split('bool first=true;', 1)[1]
    cpp = cpp.replace('for(int species:{0,1})', 'for(int species:{0})')
    path = STAGE / 'grizzly-attack-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    process = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'grizzly-attack-sanitizer.log').write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()[-3000:]
    cases = json.loads(process.stdout)
    assert len(cases) == 6368
    for species, rank, difficulty, expansion, mode, control, minimum, maximum, ar, cleared, writes, owners, ranks in cases:
        assert species == 0
        if control in [1, 2, 3, 4, 5]:
            assert [minimum, maximum, ar, cleared, writes] == [123, 456, 789, 0, 0]
            assert owners == ranks == int(control == 5)
            continue
        bonus = sum(10 if i <= 8 else 15 if i <= 16 else 20 if i <= 22 else 26 if i <= 28 else 30 for i in range(2, rank + 1))
        endpoints = [30 + bonus, 60 + bonus] if control not in [6, 7] else [0, 0]
        assert [minimum, maximum, ar, cleared, writes] == [*endpoints, (300 if mode == 4 else 0) + (300 + 20 * (rank - 1) if rank and control not in [6, 7] else 0), 1, 3], (rank, mode, control)
        assert owners == 1 and ranks == int(control != 6)
    fixture = STAGE / 'grizzly-attack-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
        reusedAbiSha256=sha(parent), sourceHashes=hashes, unchangedRoutineHashes=routines,
        tableHashes={name: sha(REFERENCE / f'json/{name}.json') for name in ['monstats', 'skills']},
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=len(cases), ordinaryContexts=6144, guardContexts=224, generatedSourceSha256=sha(path), sanitizers='ASan+UBSan'),
        established=[
            'Complete owner damage/AR hook, physical tier/min/max/to-hit getters, mode writer and noRatio helper execute together at live effective ranks0..255,three difficulties,two game contexts and modes4/5/7/8. Birth passives remain separate.',
            'Grizzly has30..60base physical endpoints with10/15/20/26/30tier increments; rank28raw496..526. Current live player-skill AR getter is300+20*(rank-1)when rank>0,zero at rank0; A1 literal monster mode AR300 is separate from birth monlvl and copied Spirit Wolf AR. A2/cast/S1 table lanes are zero,with the same live owner getter. Missing owner/skill row removes owner endpoints but retains table mode AR.',
        ], limits=[
            'BearSmite NPC326,start32/do2,sequence12timing/action and post-combat flat physical add are separate and not certified by ordinary modes4/5/7/8. No learned Grizzly handler or native actor enabled.',
            'Owner/rank/statlist/game/layout are explicit services; hostile,ratio,elemental and equipment branches are not exercised. This is supplementary older engine composition plus pinned current tables,not a modern executable equivalence certificate.',
        ])
    target = ROOT / 'tools/reference/grizzly-attack-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert target.read_text() == rendered
    else:
        target.write_text(rendered)
    print('Grizzly ordinary mode composition:6144ordinary/224guard contexts pass ASan+UBSan; BearSmite sequence remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
