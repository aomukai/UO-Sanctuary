#!/usr/bin/env python3
"""Bind Maul's retained support form to unchanged real charge expiry."""
import argparse
from collections import Counter

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
BASE = ROOT / '.local/class-skills/druid-planned-acceptance'
STAGE = ROOT / '.local/class-skills/druid-maul-expiry-form-current'


def record(check=False):
    run = BASE / 'maul-expiry-form-focused-run-1.log'
    assert 'pass across3boots/2reloads' in run.read_text()
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(BASE / 'source-snapshot-17.json', STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    old = e.load(BASE / 'failed-run-6/birth-inputs.json')
    assert born['copiedData'] == old['copiedData']
    changed = {name for name, digest in old['sourceInputs'].items()
        if name.endswith('.cs') and digest != born['sourceInputs'][name]}
    source = 'tests/Sanctuary.Integration/DruidMaulChecks.cs'
    assert changed == {source}
    before = (BASE / 'failed-run-6/sources' / source).read_text()
    line = '        var deadline=TimedEffects.Now.AddSeconds(2);Hold(expired,State(expired) with{UntilUtc=deadline});'
    assert (ROOT / source).read_text() == before.replace(line,
        '        DruidFormWorldChecks.RetainForInspection(expired);\n' + line, 1)
    for name, digest in born['artifacts'].items():
        if name != 'Assemblies/Sanctuary.Integration.dll':
            assert digest == old['artifacts'][name], name
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    build = (BASE / 'integration-build-15.log').read_text()
    assert all(value in build for value in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    expected = Counter({'iron-golem-birth-check': 3, 'druid-maul-live-check': 3,
        'druid-maul-live-result': 3, 'druid-maul-snapshot-check': 3,
        'druid-maul-expiry-stage-check': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks', 'druid-maul'):
        expected[family + '-check'] = 1
        expected[family + '-reload-check'] = 2
    e.cohort(STAGE, expected, 24)
    files = [ROOT / 'tools/record_druid_maul_expiry_form.py', ROOT / source,
        ROOT / 'tests/Sanctuary.Integration/DruidFormWorldChecks.cs',
        ROOT / 'tests/check_druid_planned_acceptance.py',
        BASE / 'failed-run-6/sources' / source, BASE / 'failed-run-6/birth-inputs.json',
        BASE / 'failed-run-6/birth-native-checks.log',
        BASE / 'source-snapshot-17.json', BASE / 'integration-build-15.log', run,
        STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log']
    result = dict(component='Maul charge expires independently of retained support form',
        referenceVersion='3.3.93847', contentBuildReused=1, integrationBuild=15,
        sourceSnapshot=17, nativeRun=1, nativeMarkers=24, markers=dict(expected),
        boots=3, saves=3, reloads=2, changedSources=[source],
        gameplayChanged=False, runtimeDataChanged=False, installed=False,
        artifacts=born['artifacts'], classCertified=False,
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='One inspection-helper call added for the original expiry-control actor. Its charge still uses the original two-second deadline and expires across offline time; the support form retains its captured payload and resources. Main Maul wounds, mana, weapon, charges, bindings and paid-action no replay remain checked. Combined Druid acceptance is separate.')
    e.write('druid-maul-expiry-form-native-evidence.json', result, files, check)
    print('Maul support form:24 exact results/3saves/2reloads; charge deadline unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
