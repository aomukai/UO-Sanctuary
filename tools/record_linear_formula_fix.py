#!/usr/bin/env python3
"""Record rank-zero formula and supplied-rank curse fixes without claiming new skills."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'beasts'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inventory(path):
    data = json.loads(path.read_text())
    expected = {str(p.relative_to(ROOT)) for directory in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / directory).glob('*.cs')}
    assert {name for name in data['sourceInputs'] if name.endswith('.cs')} == expected, 'Candidate source inventory changed'
    for name, digest in data['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in data['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == data['artifacts']['Assemblies/' + name]
    return data


def record(check=False, full_class=False):
    focused = inventory(STAGE / 'stats-inputs.json')
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log
    for name in ['skill-linear', 'creeper-stats', 'raven-stats', 'raven-flight', 'toward-path', 'valkyrie-creation', 'named-gear']:
        assert log.count(name + '-check: passed') == 1, name
    assert '9252 unchanged source arms match' in log and 'no new summon handler enabled' in (STAGE / 'stats-run.log').read_text()
    before = STAGE / 'linear-before'
    receipt = json.loads((before / 'receipt.json').read_text())
    for name, digest in receipt['hashes'].items():
        assert sha(before / name) == digest
    assert receipt['expectedFailure'] in (before / 'stats-native-checks.log').read_text()
    first_full = STAGE / 'linear-only-full-failure'
    assert 'Attract incorrectly blocked or was removed by Inner Sight' in (first_full / 'class-native-checks.log').read_text()
    assert sha(first_full / 'Sanctuary.Content.dll') != sha(STAGE / 'content/Sanctuary.Content.dll')
    source = json.loads((ROOT / 'tools/reference/skill-linear-source-evidence.json').read_text())
    assert source['fixture']['cases'] == 9252 and sha(ROOT / source['fixture']['path']) == source['fixture']['sha256']
    assert sha(BASE / 'skill-linear-source-probe.cpp') == source['generatedSourceSha256']
    for name, digest in source['sourceHashes'].items():
        assert sha(BASE / 'd2moo-pinned-source' / name) == digest
    warnings = {}
    for kind in ['content', 'integration']:
        text = (STAGE / (kind + '-build.log')).read_text()
        assert 'Build succeeded.' in text and '0 Error(s)' in text
        warnings[kind] = int(re.findall(r'(\d+) Warning\(s\)', text)[-1]); assert warnings[kind] == 0
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    present = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(present) == 166 and sum(x['characterClass'] == 'Druid' for x in present) == 10 and not status['completedClasses']
    paths = [ROOT / name for name in ['src/Sanctuary.Content/SkillMechanics.cs', 'src/Sanctuary.Content/NecromancerEffects.cs',
        'tests/Sanctuary.Integration/SkillLinearChecks.cs', 'tests/check_druid_beasts.py', 'tools/probe_skill_linear_reference.py',
        'tools/record_linear_formula_fix.py', 'tools/reference/skill-linear-source-evidence.json']]
    paths += [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log',
        'content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll', 'content-build.log', 'integration-build.log',
        'linear-before/receipt.json', 'linear-before/stats-native-checks.log', 'linear-only-full-failure/class-native-checks.log']]
    markers, named_count, loop_count = None, None, None
    if full_class:
        full = inventory(STAGE / 'class-inputs.json')
        assert full['artifacts'] == focused['artifacts'] and full['copiedData'] == focused['copiedData']
        full_log = (STAGE / 'class-native-checks.log').read_text()
        assert ' ERR]' not in full_log and ': failed' not in full_log
        assert 'Native class skills and projectile checks passed' in (STAGE / 'full-run.log').read_text()
        for name in ['raven-body', 'raven-casting', 'armageddon', 'hurricane', 'tornado', 'volcano', 'twister', 'fissure',
                     'molten-boulder', 'firestorm', 'cyclone-armor', 'corpse-cast', 'death-explosion', 'death-control',
                     'corpse-base', 'inferno-sentry', 'lightning-sentry', 'class-skills', 'class-names', 'named-gear', 'inner-sight']:
            assert full_log.count(name + '-check: passed') == 1, name
            assert full_log.count(name + '-reload-check: passed') == 2, name
        for name in ['raven-body-live-result', 'raven-right-result', 'raven-quick-result']:
            assert full_log.count(name + ': passed') == 3, name
        assert full_log.count('Shutting down') == 3
        markers = len(re.findall(r': passed', full_log))
        audits = []
        for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
            data = json.loads((ROOT / 'tools/reference' / name).read_text())
            assert data == json.loads((STAGE / name).read_text())
            assert data['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
            for item, digest in data['InputHashes'].items():
                assert sha(ROOT / item) == digest, item
            audits.append(data)
        baseline = json.loads(subprocess.check_output(['git', 'show', 'cb23dd6:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
        assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
        named_count, loop_count = (len(x['InputHashes']) for x in audits)
        python = (STAGE / 'python-tests.log').read_text()
        assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
        paths += [STAGE / name for name in ['class-inputs.json', 'class-native-checks.log', 'full-run.log', 'python-tests.log']]
        paths += [ROOT / 'tools/reference' / name for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']]
    evidence = dict(referenceVersion='3.3.93847', installed=False, handlers=166, druidHandlers=10,
                    completedClasses=[], goalComplete=False, newHandlers=0, focusedBoots=1, fullClassRegression=full_class,
                    fullBoots=3 if full_class else 0, actualReloads=2 if full_class else 0, fullClassPassedMarkers=markers,
                    sourceAndNativeLinearCases=9252, retainedCreeperComparisons=14016, buildWarnings=warnings,
                    pythonTests=70 if full_class else None, unchangedNamedDefinitions=573 if full_class else None,
                    namedAuditFingerprints=named_count, firstLoopFingerprints=loop_count,
                    hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
                    scope='Shared formula/curse corrections and retained prepared/learned consumers. No new learned summon, actor or whole-class parity claim.',
                    established=[
                        'A disposable before-fix run reproduces the absent-Grizzly damage-synergy bug. Rank-zero linear evaluation now returns0, matching9252 compiled unchanged source switch-arm cases and the native nested formula resolver. Positive-rank baseline/increment behavior remains intact; absent golem mastery controls pass.',
                        'The first broader run exposed duration being evaluated at live owner rank while the curse stored a separately supplied rank. Duration now uses the supplied rank, and nonpositive/out-of-range ranks are refused. Actual learned Necromancer fixtures compare supplied rank1 against current rank3 and a fresh rank3 cast.',
                        'Prepared Creeper numeric/vine callbacks and retained Raven stats/blind/flight/path, toward paths, Valkyrie creation and named items pass the focused boot. The installed world/client are untouched by these isolated runs.'
                    ], remaining=[
                        'Complete broad regression, fresh native audits and Python acceptance before installation.' if not full_class else 'Prepare preserved exact-package installation and original-world smoke if promoting this shared correction.',
                        'Spirit Wolf source creation/cold proof is separate; its actual saved actor, learned casting and six-slot/sunder consumer acceptance remain pending.',
                        'Creeper actor/current Range/state/owner-stat gaps and final effects/icons remain separate. Handler coverage remains166/Druid10 and no class is complete.'
                    ])
    path = ROOT / 'tools/reference/linear-formula-fix-native-evidence.json'; encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print(f'Linear/curse rank fixes:9252 source/native cases; fullClass={full_class}; no new summon handler or installation claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true'); args = parser.parse_args(); record(args.check, args.full_class)
