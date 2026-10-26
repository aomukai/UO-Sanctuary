#!/usr/bin/env python3
"""Bind Raven release acceptance to tested artifacts; installation stays separate."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'raven'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inventory(path):
    data = json.loads(path.read_text())
    for name, digest in data['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in data['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == data['artifacts']['Assemblies/' + name]
    return data


def record(check=False, full_class=False):
    spec = importlib.util.spec_from_file_location('raven_focused_record', ROOT / 'tools/record_raven_stats.py')
    focused_record = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(focused_record)
    focused_record.record(check=True)
    focused = inventory(STAGE / 'stats-inputs.json')
    markers = None
    if full_class:
        current = inventory(STAGE / 'class-inputs.json')
        assert current['artifacts'] == focused['artifacts']
        assert current['copiedData'] == focused['copiedData']
        log = (STAGE / 'class-native-checks.log').read_text()
        assert ' ERR]' not in log and ': failed' not in log
        assert 'Native class skills and projectile checks passed' in (BASE / 'raven-full-run.log').read_text()
        for name in ['raven-body', 'raven-casting', 'armageddon', 'hurricane', 'tornado', 'volcano',
                     'twister', 'fissure', 'molten-boulder', 'firestorm', 'cyclone-armor',
                     'corpse-cast', 'death-explosion', 'death-control', 'corpse-base',
                     'inferno-sentry', 'lightning-sentry', 'class-skills', 'class-names', 'named-gear']:
            assert log.count(name + '-check: passed') == 1, name
            assert log.count(name + '-reload-check: passed') == 2, name
        for name in ['raven-body-live-result', 'raven-right-result', 'raven-quick-result']:
            assert log.count(name + ': passed') == 3, name
        assert log.count('Shutting down') == 3
        markers = len(re.findall(r': passed', log))
    audits = [json.loads((ROOT / 'tools/reference' / name).read_text())
              for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']]
    baseline = json.loads(subprocess.check_output(
        ['git', 'show', 'cb23dd6:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    for data in audits:
        assert data['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for name, digest in data['InputHashes'].items():
            assert sha(ROOT / name) == digest, name
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    client = json.loads((STAGE / 'client-proof.log').read_text())
    assert client['Result'] == 'passed'
    for key in ['Baseline', 'Candidate']:
        row = client[key]
        assert row['Ability'] == 172 and not row['SelfTarget'] and not row['Left'] and row['Right']
        assert row['Label'] == 'Raven' and row['Class'] == 'Druid'
        assert row['Sha256'] == sha(STAGE / 'client/cuo.dll')
    installed_client = json.loads((ROOT / 'tools/reference/armageddon-installation-evidence.json').read_text())
    assert installed_client['clientAfterSha256'] == sha(STAGE / 'client/cuo.dll')
    # The full existing client already contains Raven's stable identity and
    # flags. Reuse its exact DLL rather than claiming an unnecessary rebuild.
    old_shared = subprocess.check_output(['git', 'show', 'cb23dd6:shared/ClassAbilities.cs'], cwd=ROOT)
    assert hashlib.sha256(old_shared).hexdigest() == sha(ROOT / 'shared/ClassAbilities.cs')
    paths = [ROOT / name for name in [
        'tools/record_raven_candidate.py', 'tools/record_raven_stats.py',
        'tools/reference/raven-stats-native-evidence.json', 'tests/check_class_skills.py',
        'tools/reference/named-item-runtime-audit.json', 'tools/reference/first-loop-affix-audit.json',
        'tools/reference/first-loop-evidence.json', 'tools/reference/item-property-integration-backlog.json',
        'shared/ClassAbilities.cs', 'data/Sanctuary/Balance/class-skill-status.json']]
    paths += [STAGE / name for name in [
        'content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll', 'stats-inputs.json',
        'stats-native-checks.log', 'python-tests.log', 'native-audits.log', 'client/cuo.dll',
        'client-proof.log', 'client-proof/Program.cs', 'client-proof/check.csproj', 'client-proof-build.log']]
    if full_class:
        paths += [STAGE / 'class-inputs.json', STAGE / 'class-native-checks.log', BASE / 'raven-full-run.log']
    output = dict(
        referenceVersion='3.3.93847', handlerEnabled=True, installed=False, handlers=166, druidHandlers=10,
        installedHandlers=165, installedDruidHandlers=9, completedClasses=[], goalComplete=False,
        focusedBoots=3, actualReloads=2, connectedNaturalActors=3, connectedHeldRightCasts=3,
        connectedQuickCasts=3, fullClassRegression=full_class, fullClassPassedMarkers=markers,
        pythonTests=70, unchangedNamedDefinitions=573, namedAuditFingerprints=len(audits[0]['InputHashes']),
        firstLoopFingerprints=len(audits[1]['InputHashes']), clientRebuilt=False,
        clientReusedByteIdentical=True, clientMetadataReflection=True,
        sourceComponentComparisons=37640, timingProfile='legacy-npc-clock-current-hk-metadata-v1',
        scope='Exact candidate release checks supplement the focused evidence. Handler coverage is not a whole-skill/class parity certificate; installation requires a separate preservation receipt.',
        remaining=[
            'Complete full regression before installation.' if not full_class else 'Prepare exact-package installation, preservation rollback and original-world load/listen/shutdown smoke.',
            'Exact modern Raven AI scheduling/attempt-versus-hit/first-think semantics and mid-dive focus interruption remain uncertain; historical2.4changed attack frequency.',
            'Source room/vision/target order/global seed/event/geometry/native tile projection and warp/dead-target fallback remain bounded adapters or unverified.',
            'Moving-unit path repair, PvP/unmapped-size targets and nonzero inherited physical sunder remain unsupported.',
            'Ordinary graphical client comparison, final Raven/emergence/flight/dive/blind/retirement/audio and separate talent/right/quick icons remain open.'
        ], hashes={str(path.relative_to(ROOT)): sha(path) for path in paths})
    path = ROOT / 'tools/reference/raven-native-evidence.json'
    text = json.dumps(output, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print(f'Raven candidate166/Druid10: focused3boots/2reloads/3natural/3held-right/3quick,70Python and current audits; fullClass={full_class}; installation remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
