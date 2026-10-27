#!/usr/bin/env python3
"""Bind native A1stat capture/impact/reload acceptance; preserve the earlier numeric package."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/wolf-mode'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False, full_class=False):
    inputs = json.loads((STAGE / 'stats-inputs.json').read_text())
    expected = {str(p.relative_to(ROOT)) for folder in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / folder).glob('*.cs')}
    assert {name for name in inputs['sourceInputs'] if name.endswith('.cs')} == expected
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == inputs['artifacts']['Assemblies/' + name]
        build = (STAGE / (kind + '-build.log')).read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 199
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    for family in ['spirit-wolf-mode', 'spirit-wolf-body', 'spirit-wolf-casting', 'barbs-casting', 'barbs-aura', 'barbs-world', 'wolverine-casting', 'oak-casting']:
        assert log.count(family + '-check: passed') == 1 and log.count(family + '-reload-check: passed') == 2, family
    for name in ['spirit-wolf-hit', 'spirit-wolf-rate', 'spirit-wolf-stats', 'dire-wolf-stats', 'gem-activation', 'prepared-weapon-event']:
        assert log.count(name + '-check: passed') == 1, name
    for family in ['spirit-wolf-right', 'spirit-wolf-quick', 'barbs-right', 'barbs-quick', 'oak-right', 'oak-quick', 'wolverine-right', 'wolverine-quick']:
        assert log.count(family + '-check: passed') == log.count(family + '-result: passed') == 3, family
    for family in ['spirit-wolf-world-live', 'spirit-wolf-hit-live', 'spirit-wolf-rate-live', 'spirit-wolf-slow-live', 'barbs-world-live', 'oak-world-live', 'wolverine-world-live']:
        assert log.count(family + '-result: passed') == 3, family
    assert 'Wolf A1mode AR capture/impact and pending-bite save/reload cancellation verified' in (STAGE / 'stats-run.log').read_text()
    saved = json.loads((STAGE / 'spirit-wolf-mode-fixtures.json').read_text())
    controller = saved['State']['Controller']
    assert controller['Profile'] == 'source-ai-native-ground-fit-mode-attack-v3' and controller['Mode'] == 4
    assert controller['AttackPacket'] == dict(Rank=2, Minimum=0, Maximum=0, Attack=145)
    assert controller['ActionAt'] > controller['Frame'] and saved['State']['Snapshot']['Birth']['Rank'] == 1
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
    # The prepared numeric receipt belongs to its frozen source revision.
    # Do not rewrite its binaries, logs or old tracked-source fingerprints.
    revision = subprocess.check_output(['git', 'rev-parse', '88cc65c'], cwd=ROOT, text=True).strip()
    old = json.loads((ROOT / 'tools/reference/dire-wolf-stats-native-evidence.json').read_text())
    assert old['verified']['passMarkers'] == 151 and old['verified']['nativeBirthComparisons'] == 55152
    for name, digest in old['artifacts'].items():
        actual = sha(ROOT / name) if name.startswith('.local/') else hashlib.sha256(subprocess.check_output(['git', 'show', revision + ':' + name], cwd=ROOT)).hexdigest()
        assert actual == digest, name
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(ROOT / '.local/class-skills/dire-wolf' / kind / name) == old['testedAssemblies']['Assemblies/' + name]
    baseline = json.loads(subprocess.check_output(['git', 'show', revision + ':tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 170 and sum(x['characterClass'] == 'Druid' for x in handlers) == 14 and status['completedClasses'] == []
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content-build.log', 'integration-build.log', 'python-tests.log', 'spirit-wolf-mode-fixtures.json', 'spirit-wolf-body-fixtures.json', 'spirit-wolf-casting-fixtures.json', 'wolf-world-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']]
    artifacts += [ROOT / name for name in ['tests/check_spirit_wolf_mode.py', 'tests/check_creeper_stats.py', 'tools/record_spirit_wolf_mode.py', 'tools/reference/dire-wolf-stats-native-evidence.json', 'tools/reference/druid-beast-attack-engine-evidence.json']]
    full_markers = None
    if full_class:
        full = json.loads((STAGE / 'class-inputs.json').read_text())
        assert full['artifacts'] == inputs['artifacts'] and full['copiedData'] == inputs['copiedData']
        for name, digest in full['sourceInputs'].items():
            assert sha(ROOT / name) == digest, name
        full_log = (STAGE / 'class-native-checks.log').read_text()
        assert ': failed' not in full_log and ' ERR]' not in full_log and full_log.count('Shutting down') == 3
        full_markers = full_log.count(': passed')
        assert full_markers >= 1184
        assert 'Native class skills and projectile checks passed, including two save/reload cycles.' in (STAGE / 'full-run.log').read_text()
        for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
            assert json.loads((STAGE / ('full-' + name)).read_text()) == json.loads((STAGE / name).read_text())
            artifacts.append(STAGE / ('full-' + name))
        artifacts += [STAGE / name for name in ['class-inputs.json', 'class-native-checks.log', 'full-run.log']]
    result = dict(referenceVersion='3.3.93847', handlerEnabled=True, newHandlerAdded=False, installed=False, handlers=170, druidHandlers=14, completedClasses=[], goalComplete=False,
        verified=dict(passMarkers=199, boots=3, actualReloads=2, naturalWolfFollowRuns=3, naturalWolfHitRuns=3, naturalWolfHasteRuns=3, naturalWolfDecrepifyRuns=3,
            wolfRightCasts=3, wolfQuickCasts=3, retainedBarbsRightCasts=3, retainedBarbsQuickCasts=3, pythonTests=70,
            namedDecisions=573, namedInputFingerprints=len(audits[0]['InputHashes']), firstLoopInputFingerprints=len(audits[1]['InputHashes']), fullClassMarkers=full_markers),
        frozenNumericRevision=revision, testedAssemblies=inputs['artifacts'], artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
        established=[
            'Actual Spirit Wolf A1entry captures live owner rank and source mode AR,125at rank1/145at rank2,independently of copied birth/cold/passive values. Shared creature AR and ordinary-hit chance add birth base/passive/mode values once before live actor percentages. Actual Tarnhelm during windup does not rewrite the packet; next A1refreshes it. Impact records entry rank,not current owner rank.',
            'Actual Enchant AR percent applies to the aggregate flat pool. The high-defense fixture supplies a native Defiance-stat context to avoid the chance cap; it is a component control,not a new player balance rule. Existing cold damage/ordinary block/chill/immunity/critical/reach and no-owner-Dex behavior remain verified.',
            'Saved v3pending packets are validated against source calculations. Corrupt or absent v3packets fail atomically. Exact old v2JSON without an attack packet cancels the old pending bite without fabricated owner context,RNG or damage. Actual world loads retain live actors/birth and cancel offline pending actions under the existing persistence policy; next real A1after legal placement/approach reads current owner rank. Two reloads stage rank2packets while owner rank is1.',
            'Retained normal/haste/repeated Decrepify natural bites,follow,right/quick input,three spirits,resources,items and prepared Dire numeric consumers pass199markers across3boots/2actual reloads. All70Python checks pass with0warning/error builds; fresh item fingerprints preserve573named decisions. Earlier151-marker Dire numeric acceptance remains frozen at88cc65c.',
        ], limits=[
            'This connects the Spirit Wolf A1mode AR consumer. It does not certify the full modern nonattack/GH/teleport mode-stat stream,global event ordering,physical-floor behavior,sunder/equipment,geometry/path/RNG or graphical equivalence. Existing inferred Decrepify busy continuation remains explicitly provisional.',
            'Dire Wolf creation/attack statistics are prepared; distinct Fenris AI,saved body,corpse ownership/consumption/rage,physical combat and learned casting remain unfinished. No new handler or completed class is claimed.',
            'The new package is isolated. Installed main69f3336and existing saves/config/client remain unchanged; installation requires its own accepted package/original-world smoke and preservation receipt. Final effects/audio and separate talent/right/quick icons remain placeholders.',
        ])
    path = ROOT / 'tools/reference/spirit-wolf-mode-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'Wolf A1mode:199markers/3boots/2reloads,natural normal/haste/slow bites and right/quick casts; fullClass={full_markers};170/Druid14unchanged,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
