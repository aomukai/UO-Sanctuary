#!/usr/bin/env python3
"""Bind learned Barbs to exact focused/full packages and its frozen component source revision."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from record_barbs_core import sha

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/barbs-casting'


def record(check=False, full_class=False):
    inputs = json.loads((STAGE / 'stats-inputs.json').read_text())
    expected = {str(p.relative_to(ROOT)) for folder in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / folder).glob('*.cs')}
    assert {p for p in inputs['sourceInputs'] if p.endswith('.cs')} == expected
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == inputs['artifacts']['Assemblies/' + name]
        build = (STAGE / (kind + '-build.log')).read_text()
        assert '0 Warning(s)' in build and '0 Error(s)' in build
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log and log.count(': passed') == 150
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    for name in ['barbs-casting', 'barbs-aura', 'barbs-world', 'gem-activation', 'prepared-weapon-event', 'wolverine-casting', 'wolverine-aura', 'wolverine-world', 'oak-casting', 'oak-world']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    for family in ['barbs-right', 'barbs-quick', 'oak-right', 'oak-quick', 'wolverine-right', 'wolverine-quick']:
        assert log.count(family + '-check: passed') == log.count(family + '-result: passed') == 3, family
    assert log.count('barbs-world-live-result: passed') == 3
    assert 'Learned Barbs:170handlers/Druid14' in (STAGE / 'stats-run.log').read_text()
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        audits.append(audit)
    assert len(audits[0]['Definitions']) == 573
    saved = json.loads((STAGE / 'barbs-casting-fixtures.json').read_text())
    assert saved['State']['Birth']['Rank'] == 1 and saved['State']['Birth']['Life256'] == 54528
    assert saved['State']['Controller']['Fault'] is None and saved['Body'] != saved['Wolf'] != saved['Raven']
    assert saved['PendingMana'] > 0 and saved['State']['Profile'] == 'source-birth-and-totem-motion-v2'
    client = json.loads((STAGE / 'client-proof.log').read_text())
    assert client['Result'] == 'passed'
    for name in ['Baseline', 'Candidate']:
        row = client[name]
        assert row['Ability'] == 180 and row['Label'] == 'Spirit of Barbs' and row['Class'] == 'Druid'
        assert not row['SelfTarget'] and not row['Left'] and row['Right'] and row['Sha256'] == sha(STAGE / 'client/cuo.dll')
    # Core proof is historical. Validate tracked files at its actual Git
    # revision, not against the later cast source, and preserve its local bytes.
    revision = subprocess.check_output(['git', 'rev-parse', 'a3d02de'], cwd=ROOT, text=True).strip()
    core_path = ROOT / 'tools/reference/barbs-core-native-evidence.json'
    core = json.loads(core_path.read_text())
    assert core['verified']['passMarkers'] == 135 and not core['handlerEnabled']
    for name, digest in core['artifacts'].items():
        if name.startswith('.local/'):
            actual = sha(ROOT / name)
        else:
            actual = hashlib.sha256(subprocess.check_output(['git', 'show', revision + ':' + name], cwd=ROOT)).hexdigest()
        assert actual == digest, name
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(ROOT / '.local/class-skills/barbs' / kind / name) == core['testedAssemblies']['Assemblies/' + name]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert len([x for x in status['skills'] if x['implementation'] == 'handler-present']) == 170
    assert len([x for x in status['skills'] if x['implementation'] == 'handler-present' and x['characterClass'] == 'Druid']) == 14
    assert status['completedClasses'] == []
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content-build.log', 'integration-build.log', 'python-tests.log', 'barbs-casting-fixtures.json', 'barbs-aura-fixtures.json', 'barbs-world-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json', 'client-proof.log', 'client-proof-build.log', 'client-proof/Program.cs', 'client-proof/check.csproj', 'client/cuo.dll']]
    artifacts += [ROOT / name for name in ['tests/check_barbs_casting.py', 'tests/check_creeper_stats.py', 'tools/record_barbs_casting.py', 'tools/reference/barbs-core-native-evidence.json', 'tools/reference/weapon-event-order-review.json']]
    full_markers = None
    if full_class:
        full = json.loads((STAGE / 'class-inputs.json').read_text())
        assert full['artifacts'] == inputs['artifacts'] and full['copiedData'] == inputs['copiedData']
        for name, digest in full['sourceInputs'].items():
            assert sha(ROOT / name) == digest, name
        full_log = (STAGE / 'class-native-checks.log').read_text()
        assert ' ERR]' not in full_log and ': failed' not in full_log and full_log.count('Shutting down') == 3
        assert 'Native class skills and projectile checks passed, including two save/reload cycles.' in (STAGE / 'full-run.log').read_text()
        assert full_log.count('named-gear-check: passed') == 1 and full_log.count('named-gear-reload-check: passed') == 2
        for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
            assert json.loads((STAGE / ('full-' + name)).read_text()) == json.loads((STAGE / name).read_text())
            artifacts.append(STAGE / ('full-' + name))
        full_markers = full_log.count(': passed')
        assert full_markers >= 1184
        artifacts += [STAGE / name for name in ['class-inputs.json', 'class-native-checks.log', 'full-run.log']]
    result = dict(referenceVersion='3.3.93847', handlerEnabled=True, installed=False, handlers=170, druidHandlers=14, completedClasses=[], currentExecutableParity=False,
        verified=dict(boots=3, actualReloads=2, passMarkers=150, connectedRightCasts=3, connectedQuickCasts=3, naturalBarbsFollowExpiryRuns=3, pythonTests=70, namedDecisions=573, namedInputFingerprints=len(audits[0]['InputHashes']), firstLoopInputFingerprints=len(audits[1]['InputHashes'])),
        fullClassRegression=full_class, fullClassBoots=3 if full_class else 0, fullClassPassMarkers=full_markers,
        frozenCoreRevision=revision, clientReusedByteIdentical=True, graphicalClientAcceptance=False,
        sourceInputs=inputs['sourceInputs'], copiedData=inputs['copiedData'], testedAssemblies=inputs['artifacts'], artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
        established=[
            'Learned SC/right/quick Barbs uses the shared spirit transaction:24+effective rank mana after allocation and before category replacement. Fractional/changed mana,cancel/stop/move/map/recovery/freeze,early/duplicate release,failed/wrong-identity allocation and post-allocation payment controls pass.',
            'Actual paid Oak/Heart/Barbs replacements preserve wolves/Ravens. Live/deleted target placement,town,real soft gear ranks and three real right/quick packet sessions pass. Saved paid Barbs,bindings,coexisting summons and unpaid-windup discard pass both real reloads.',
            'The exact candidate retains the saved vulnerable Barbs/shared aura/aggregate ATD core checks,including NPC Command,old Heart JSON and gem fractional leech/retaliation. Existing client bytes already contain correct180/Druid/right-only metadata; no client rebuild or graphical playtest is inferred.',
        ], remaining=[
            'Full source incoming HP/death/event chronology remains unfinished: current return bridge precedes original incoming HP subtraction. Iron Maiden/other callback ordering,native absorb placement and complete current executable event/mode behavior are not certified.',
            'Item-only/staffmod permission,exceptional NPC/PvP/unmapped producers,exact modern first-think/room/vision/path/geometry/global RNG/AStar and graphical acceptance remain explicit individual limits. No class is complete.',
            'Final X4 body/emergence/walk/hurt/death,Barbs control/recipient overlays,retaliation contact/audio and talent/right/quick icons remain pending. Silent wisp/violet body,SC pose/sound and retaliation flash are labelled placeholders.',
        ])
    path = ROOT / 'tools/reference/barbs-casting-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'Learned Barbs:170/Druid14,150focused markers/3boots/2reloads/3right+quick; fullClass={full_class}; not installed or class-complete.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
