#!/usr/bin/env python3
"""Bind the prepared saved wolf life/ownership slice to its isolated acceptance."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/spirit-wolf'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inventory = json.loads((STAGE / 'stats-inputs.json').read_text())
    expected = {str(p.relative_to(ROOT)) for directory in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / directory).glob('*.cs')}
    assert {name for name in inventory['sourceInputs'] if name.endswith('.cs')} == expected
    for name, digest in inventory['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inventory['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for kind in ['content', 'integration']:
        filename = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / filename) == inventory['artifacts']['Assemblies/' + filename]
        build = (STAGE / (kind + '-build.log')).read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log
    families = ['spirit-wolf-body', 'battle-orders', 'shout', 'summon-difficulty', 'class-history', 'full-wardrobe', 'inner-sight', 'decoy']
    for name in families:
        assert log.count(name + '-check: passed') == 1, name
        assert log.count(name + '-reload-check: passed') == 2, name
    for name in ['battle-orders', 'shout', 'decoy']:
        assert log.count(name + '-result: passed') == 3, name
    for name in ['spirit-wolf-stats', 'skill-linear', 'creeper-stats', 'raven-stats', 'raven-flight', 'toward-path', 'valkyrie-creation', 'named-gear', 'first-loop-catalogue']:
        assert log.count(name + '-check: passed') == 1, name
    assert log.count('decoy-disconnect-check: passed') == 3 and log.count('Shutting down') == 3
    assert 'pass3boots/2actual reloads' in (STAGE / 'stats-run.log').read_text()
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        audit = json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit == json.loads((STAGE / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        audits.append(audit)
    baseline = json.loads(subprocess.check_output(['git', 'show', 'cc77749:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    fixture = json.loads((STAGE / 'body-fixtures.json').read_text())
    assert fixture['State']['Profile'] == 'prepared-life-and-ownership-v1'
    assert fixture['Maximum256'] == 56159 and fixture['Regeneration256'] == 54
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert len([s for s in status['skills'] if s['implementation'] == 'handler-present']) == 166 and status['completedClasses'] == []
    paths = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'body-fixtures.json', 'python-tests.log',
        'content-build.log', 'integration-build.log', 'content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll']]
    paths += [ROOT / name for name in ['tools/record_spirit_wolf_body.py', 'tools/reference/named-item-runtime-audit.json',
        'tools/reference/first-loop-affix-audit.json', 'tools/reference/battle-orders-engine-evidence.json',
        'tools/reference/druid-beasts-preflight.json', 'tools/reference/spirit-wolf-creation-source-evidence.json']]
    evidence = dict(referenceVersion='3.3.93847', installed=False, savedPreparedBody=True, learnedHandler=False,
        scope='Life-only factory acceptance; the separately recorded world-controller factory connects individual AI and automatic movement.',
        ordinaryAttack=False, lifeOnlyFactoryHasIndividualAi=False, lifeOnlyFactoryHasAutomaticClock=False, fullClassRegression=False,
        boots=3, actualReloads=2, passedMarkers=len(re.findall(r': passed', log)), pythonTests=70,
        buildWarnings=0, buildErrors=0, handlers=166, druidHandlers=10, completedClasses=[], goalComplete=False,
        namedAuditFingerprints=len(audits[0]['InputHashes']), firstLoopFingerprints=len(audits[1]['InputHashes']), unchangedNamedDefinitions=573,
        hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
        established=[
            'Actual saved vulnerable Spirit Wolf body retains immutable effective-rank wolf/Dire/Grizzly birth context, owner/history permission and the shared persistent-minion relationship. Explicit preparation alone can construct it; learned skill remains unavailable.',
            'Source162.5 life projects to native163 while health/healing caps remain41600FP. Two individual recovery frames add32FP each and clamp at the fractional maximum. Battle Orders35% uses the existing source floating-percentage rule:56159FP/native220, preserving fractional current life proportionally. A changed maximum recomputes regeneration from the wolf DamageRegen4 row and the truncated whole source maximum:54FP on application,40 on removal.',
            'Copied wolf armor joins Shout in one percent pool, with Inner Sight reducing the raw basis first. Explicit wolf identity prevents inherited undead/zero-drain classification; size2,100drain,50chill magnitude,zero block and7frame/256speed recovery metadata are retained. Full recovery-mode/current-executable scheduling is not inferred from metadata.',
            'Actual Tarnhelm changes fresh capture without rewriting an existing birth. Atomic malformed-state/JSON controls, offline no-heal, wardrobe parking/resume, owner-facet rescale with proportional life and orphan deletion pass. Two real world reloads retain saved owner/birth/clock, fractional injury, Battle Orders and regeneration, then resume one explicit active recovery frame without replaying offline recovery.',
            'Prepared numeric Spirit Wolf and Creeper, shared rank/curse fixes, Raven stats/flight/path, toward paths,Valkyrie and named-item components pass. Retained Battle Orders/Shout/difficulty/history/wardrobe/Inner Sight and Decoy checks survive two actual reloads; three real loopback Decoy casts, natural expiries and disconnect cleanup pass. Fresh audits retain all573 named decisions and all70Python tests pass.'
        ], remaining=[
            'This record tests the life-only CreatePrepared factory and explicit AdvanceRecovery operation. The separate world-controller factory, individual movement/target/teleport and natural-clock acceptance are recorded in spirit-wolf-world-native-evidence.json. Ordinary attack/cold hit/chill, learned placement/payment/FIFO-cap/coexistence, connected wolf input and complete world lifecycle acceptance remain pending.',
            'The user expects100% cold damage. Their prior observation was inconclusive for a tiny physical floor; old ordinary melee supplies one, so the modern branch remains uncertain rather than either result being asserted. Nonzero cold sunder is rejected by prepared actor creation/restore, while numeric source data remains retained separately.',
            'Exact modern AI fallback polarity, room/geometry/global RNG/event ordering, equipment request consumers, graphical client parity and full-class regression/installation remain pending. Main installed build and saves are untouched.',
            'Named wolf body0xE1 with explicit placeholder name and silent sound is temporary. Final emergence/body/movement/bite/cold/teleport/hurt/death/audio and talent/right/quick icons remain in separate effect/icon registers. No new handler or class completion is claimed.'
        ])
    path = ROOT / 'tools/reference/spirit-wolf-body-native-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Prepared saved Spirit Wolf life/ownership:3boots/2reloads, retained health/defense consumers and70Python tests; separate world-controller acceptance; combat/casting pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
