#!/usr/bin/env python3
"""Reuse the bounded do65 ABI for Barbs's single flat-ATD stat."""
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


def flat(rank):
    # Current edmn tier inputs; formula evaluation is a bounded service here.
    return 32 + sum(max(0, min(rank, end) - start) * step for start, end, step in [(1, 8, 9), (8, 16, 18), (16, 22, 27), (22, 28, 36), (28, 255, 45)])


def build(check=False):
    preflight = ROOT / 'tools/reference/spirit-of-barbs-preflight.json'
    current = json.loads(preflight.read_text())['currentRows']
    aura = current['skills']['Barbs Aura']
    assert aura['aurastat1'] == 'item_attackertakesdamage' and aura['aurastatcalc1'] == 'edmn' and 'aurastat2' not in aura
    assert [aura[k] for k in ['EMin', 'EMinLev1', 'EMinLev2', 'EMinLev3', 'EMinLev4', 'EMinLev5', 'HitShift']] == ['32', '9', '18', '27', '36', '45', '8']
    assert current['states']['barbs']['*ID'] == '147' and current['states']['barbscontrol']['*ID'] == '163'
    item = current['itemstatcost']['item_attackertakesdamage']
    assert item['*ID'] == '78' and item['itemeventfunc1'] == '6'
    prior = json.loads((ROOT / 'tools/reference/wolverine-aura-engine-evidence.json').read_text())
    parent = STAGE / 'wolverine-aura-source-probe.cpp'
    assert sha(parent) == prior['fixture']['generatedSourceSha256']
    cpp = parent.read_text()
    changes = {
        'nAuraState=162,wAuraTargetState=148': 'nAuraState=163,wAuraTargetState=147',
        'wAuraStat[6]={119,25,-1,-1,-1,-1}': 'wAuraStat[6]={78,-1,-1,-1,-1,-1}',
        'dwAuraStatCalc[6]={2,4}': 'dwAuraStatCalc[6]={2}',
        'which==2?25+7*(rank-1):which==4?20+7*(rank-1):0': 'which==2?BarbsFlat(rank):0',
        '&skill,297,rank': '&skill,296,rank', '&caster,297,rank': '&caster,296,rank',
    }
    for before, after in changes.items():
        assert before in cpp, before
        cpp = cpp.replace(before, after)
    formula = 'int BarbsFlat(int rank){int value=32;for(int i=2;i<=rank;i++)value+=i<=8?9:i<=16?18:i<=22?27:i<=28?36:45;return value;}\n'
    cpp = cpp.replace('int SKILLS_EvaluateSkillFormula(', formula + 'int SKILLS_EvaluateSkillFormula(', 1)
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == prior['legacyCommit']
    sources, routines = {}, {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD0FA00', 'sub_6FD0FE50', 'sub_6FD0FE80', 'SKILL_ComputePeriodicRate']),
        ('source/D2Game/src/SKILLS/SkillPal.cpp', ['SKILLS_SrvDo065_BasicAura']),
    ]:
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        sources[relative] = hashlib.sha256(raw).hexdigest()
        for name in names:
            original = function(raw.decode(), name)
            assert function(cpp, name) == original, name
            routines[name] = hashlib.sha256(original.encode()).hexdigest()
    path = STAGE / 'barbs-aura-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True, check=True)
    assert not result.stderr, result.stderr.decode()
    rows = json.loads(result.stdout)
    assert len(rows) == 440
    for rank, frame, kind, source_town, next_frame, success, calls in rows:
        assert next_frame == frame + 5 and success == 1
        call = [100, 163, rank, 78, flat(rank), 0, 0, 6]
        expected = [call]
        if kind in [0, 7, 8, 9]:
            expected.append([200, 147, rank, *call[3:]])
        assert calls == expected, (rank, frame, kind, source_town, calls, expected)
    fixture = STAGE / 'barbs-aura-source-probe.json'
    fixture.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
        preflightSha256=sha(preflight), reusedAuraAbiSha256=sha(parent), sourceHashes=sources, unchangedRoutineHashes=routines,
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=len(rows), generatedSourceSha256=sha(path), sanitizers='ASan+UBSan'),
        established=[
            '440 complete do65/self/recipient/filter/periodic contexts pass: five-frame refresh, six-frame leases, self163/recipient147 and only flat ATD stat78. No percentage Thorns stat or aura mana payment is added.',
            'Closed radius, live allied eligibility and source-town self behavior reuse the same complete outer source routines as Oak/Heart. Town recipients/dead/NPC/NOAURA/hostile/outside targets are excluded; LOS and CANBEATTACKED are not extra gates for this filter.',
        ], remaining=[
            'Formula evaluation uses current tier inputs as a bounded service; this is not a compiled edmn damage-formula proof. BasicAura callback records requests rather than allocating real stat lists.',
            'Native leases, aggregate item+aura flat ATD mitigation/event delivery, body/movement/cast/save and current executable/client acceptance remain unfinished. No learned handler enabled.',
        ])
    output = ROOT / 'tools/reference/barbs-aura-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Barbs aura:440 complete do65/filter/iterator/self/periodic contexts pass ASan/UBSan; callback/formula services and native consumers remain explicit.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
