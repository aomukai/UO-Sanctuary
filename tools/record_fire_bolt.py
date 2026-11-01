#!/usr/bin/env python3
"""Bind the player Fire Bolt increment without certifying a whole class."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import sys

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import current

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/sorceress-fire-bolt'
sha = inventory.sha


def record(check=False):
    inventory.STAGE = STAGE
    # Preserve the exact tested annotation bytes, then prove the single-row
    # documentation transition separately. Do not rewrite the native inventory
    # or relax the shared strict snapshot/older acceptance recorders.
    annotation = 'data/Sanctuary/Balance/class-skill-status.json'
    copied_annotation = 'Data/Sanctuary/Balance/class-skill-status.json'
    original = STAGE / 'pre-annotation/class-skill-status.json'
    assert original.read_bytes() == subprocess.check_output(['git', 'show', 'b05b356:' + annotation], cwd=ROOT)
    before_status = json.loads(original.read_text())
    status = json.loads((ROOT / annotation).read_text())
    old_row = next(x for x in before_status['skills'] if x['talentId'] == 'sorceress.firebolt')
    new_row = next(x for x in status['skills'] if x['talentId'] == 'sorceress.firebolt')
    assert old_row['verification'] == 'build-only' and new_row['verification'] == 'partial-native-coverage'
    assert '105-marker' in new_row['remaining']
    old_row.update({k: new_row[k] for k in ('verification', 'remaining')})
    assert before_status == status and not status['completedClasses']
    frozen = json.loads((STAGE / 'pre-build-6.json').read_text())['inputs']
    now = current()
    assert frozen['sourceInputs'] == now['sourceInputs'] and frozen['projectInputs'] == now['projectInputs']
    assert frozen['copiedData'][copied_annotation] == sha(original)
    assert {**frozen['copiedData'], copied_annotation: sha(ROOT / annotation)} == now['copiedData']
    tested = json.loads((STAGE / 'birth-inputs.json').read_text())
    assert {p: h for p, h in tested['sourceInputs'].items() if p.endswith('.cs')} == frozen['sourceInputs']
    for path, digest in tested['sourceInputs'].items():
        assert sha(ROOT / path) == digest, path
    assert tested['copiedData'] == frozen['copiedData']
    for kind in ('content', 'integration'):
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == tested['artifacts']['Assemblies/' + name]
    inherited = json.loads((ROOT / '.local/class-skills/sorceress-acceptance/birth-inputs.json').read_text())
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert tested['artifacts'][name] == inherited['artifacts'][name]
    assert tested['copiedData'] == inherited['copiedData']
    counts = markers(STAGE / 'birth-native-checks.log')
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert sum(counts.values()) == 105 and ' ERR]' not in log and ': failed' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert 'pass across3boots/2reloads' in (STAGE / 'full-run-6.log').read_text()
    sys.path.insert(0, str(ROOT / 'tests'))
    from check_fire_bolt import configuration
    config = configuration(True)
    for family in config['extra_families']:
        assert counts[family + '-check'] == 1 and counts[family + '-reload-check'] == 2
    assert counts['fire-bolt-reload-result'] == 2
    for mode in ('right', 'quick'):
        assert counts['fire-bolt-' + mode + '-check'] == counts['fire-bolt-' + mode + '-result'] == 3
        assert log.count('fire-bolt-' + mode + ': windup') == 3
    saved = json.loads((STAGE / 'fire-bolt-fixtures.json').read_text())
    assert saved['Generation'] == 2 and saved['Flight']['Owner'] == saved['Owner']
    assert saved['Flight']['Frames'] == 50 and saved['Flight']['VelocityFp8'] == 3840
    for kind in ('content', 'integration'):
        build = (STAGE / (kind + '-build-6.log')).read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
    audit_inputs = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    assert audit_inputs['artifacts'] == tested['artifacts'] and audit_inputs['copiedData'] == now['copiedData']
    assert markers(STAGE / 'audit-native-checks.log') == Counter({
        'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    audit_log = (STAGE / 'audit-native-checks.log').read_text()
    assert ' ERR]' not in audit_log and ': failed' not in audit_log
    sizes = []
    for name in ('named-item-runtime-audit.json', 'first-loop-affix-audit.json'):
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        before = json.loads(subprocess.check_output(['git', 'show', 'b05b356:tools/reference/' + name], cwd=ROOT))
        # The explicit candidate's assembly path/hash and input fingerprints
        # identify this build; every remaining decision payload is unchanged.
        identity = ('InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath')
        if 'ContentAssemblyPath' in audit:
            assert (ROOT / audit['ContentAssemblyPath']).resolve() == (STAGE / 'content/Sanctuary.Content.dll').resolve()
        assert {k: v for k, v in audit.items() if k not in identity} == {
            k: v for k, v in before.items() if k not in identity}
        sizes.append(len(audit['InputHashes']))
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    source = ROOT / 'tools/reference/bone-spear-source-evidence.json'
    inherited_source = json.loads(source.read_text())
    launch = '.local/class-skills/d2moo-pinned-source/source/D2Game/src/SKILLS/Skills.cpp'
    assert sha(ROOT / launch) == inherited_source['inputs'][launch]
    files = [Path(__file__).resolve(), source, ROOT / launch, ROOT / 'tests/check_fire_bolt.py',
        ROOT / 'tools/build_skill_roadmap.py',
        ROOT / 'docs/sorceress-fire-bolt-checkpoint.md', ROOT / 'docs/class-skills-resume-checkpoint.md',
        ROOT / 'docs/class-skill-roadmap.md', ROOT / 'docs/skill-effects.md', ROOT / 'docs/skill-icons.md',
        ROOT / 'tools/reference/named-item-runtime-audit.json', ROOT / 'tools/reference/first-loop-affix-audit.json']
    files += [STAGE / name for name in ('pre-build-6.json', 'birth-inputs.json', 'birth-native-checks.log',
        'full-run-6.log', 'fire-bolt-fixtures.json', 'audit-inputs.json', 'audit-native-checks.log',
        'audit-run-8.log', 'python-tests.log', 'checks-6.log', 'content-build-6.log', 'integration-build-6.log')]
    files += [p for p in (STAGE / 'pre-annotation').iterdir() if p.is_file()]
    result = dict(referenceVersion='3.3.93847', component='Player Fire Bolt source-flight increment',
        installed=False, graphicalClientVerified=False, completedClasses=[],
        verified=dict(nativeMarkers=105, boots=3, actualReloads=2, saves=3,
            actualRightQuickActions=6, actualPostLoadFlights=2, fixtureFamilies=12, sharedChecks=5,
            auditMarkers=4, auditFingerprints=sizes, pythonTests=71, unchangedInstalledArtifacts=106,
            annotationOnlyFieldsChanged=['sorceress.firebolt.verification', 'sorceress.firebolt.remaining']),
        markers=dict(sorted(counts.items())), artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'Fire Bolt uses pinned source36/firebolt,50frames,Vel20/default flags33,shared Q12/source-step/do1/contact,FP256 max-exclusive damage and first-hit removal. Actual targets beyond the old12tile cutoff take damage.',
            'Rank/item mastery and Slow Missiles capture at release;T29 potion eligibility remains at impact. Actual gear removal,new-cast comparison,fire resistance/immunity,separate potion lanes,wall/miss/lost-mana and owner lifecycle controls pass.',
            'Three real right and three quick casts observe windup before payment,then actual damage beyond12tiles. Two reloads retain exact original payload/deadline/binding/mana and launch independent copies through actual timer-driven travel.',
            'All existing Sorceress baseline families/shared primitives remain in the same native run. Current audit decisions,gameplay data,kernel and installed artifacts are unchanged. Only Fire Bolt verification/remaining annotations advance after the run;fresh audits use that explicitly compared metadata and identical tested assemblies.'],
        remaining=[
            'Modern executable parity,initial factory dispatch and zero-vector aim,global creator RNG,native room/terrain/ordered-candidate adapters and unmapped native creature footprints remain unverified.',
            'Offline/restart flights age harmlessly and owner death/respec/class/facet changes cancel under existing Sanctuary persistent-spell policy;this is not a modern D2R owner-removal trace. Existing pre-upgrade private timer flights have no saved migration.',
            'ReturnFire and named avoidance reuse existing consumers;PvP is outside current hostile targeting,so those player-defense interactions are not established by these PvE input checks. Final effects/audio/icons and graphical acceptance remain pending.',
            'Other basic Sorceress projectiles,nova/static/passive acceptance and Inferno modern held-input/payment/release remain separate unfinished work. No skill or class certificate is inferred.'])
    output = ROOT / 'tools/reference/fire-bolt-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Player Fire Bolt:105markers/3boots/2reloads,six real inputs,four audits/71Python;no class certificate or deployment.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
