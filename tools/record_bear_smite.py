#!/usr/bin/env python3
"""Bind NPC326 preparation and shared mitigation to an isolated native package."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'bear-smite'
FROZEN = '3ebc298'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def historical(name):
    return subprocess.check_output(['git', 'show', FROZEN + ':' + name], cwd=ROOT)


def record(check=False, full_class=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'smite-run.log',
        'python-tests.log', 'grizzly-body-fixtures.json', 'damage-rounding-checks.json', 'first-attempt-startup.log']]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        build = path.read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 240
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    assert log.count('bear-smite-check: passed') == log.count('damage-rounding-check: passed') == 1
    assert log.count('damage-rounding-reload-check: passed') == 2
    assert '1606complete source/native packets,1248complete resistance/absorb cases,255NPC ranks' in log
    assert log.count('beast-mode-stream-check: passed') == 1
    for name in ['grizzly-body', 'dire-wolf-body', 'dire-wolf-world', 'dire-wolf-casting', 'spirit-wolf-body',
                 'spirit-wolf-world', 'spirit-wolf-casting', 'barbs-casting', 'wolverine-casting', 'oak-casting']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    for name in ['dire-wolf', 'spirit-wolf', 'barbs', 'oak', 'wolverine']:
        for kind in ['right', 'quick']:
            assert log.count(name + '-' + kind + '-result: passed') == 3, name
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    source_path = ROOT / 'tools/reference/bear-smite-engine-evidence.json'
    source = json.loads(source_path.read_text())
    assert source['referenceVersion'] == '3.3.93847' and source['legacyCommit'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    fixture = ROOT / source['fixture']['path']
    generated = BASE / 'bear-smite-source-probe.cpp'
    sanitizer = BASE / 'bear-smite-sanitizer.log'
    assert source['fixture']['packetCases'] == 1636 and source['fixture']['reductionCases'] == 1248
    assert source['fixture']['largeValueRationalDifferences'] == 272
    assert sha(fixture) == source['fixture']['sha256'] and sha(generated) == source['fixture']['generatedSourceSha256']
    assert sanitizer.read_text() == ''
    for name, digest in source['sourceHashes'].items():
        assert sha(BASE / 'd2moo-pinned-source' / name) == digest, name
    artifacts += [source_path, fixture, generated, sanitizer, BASE / 'bear-smite-compiler.log']
    frozen_path = 'tools/reference/beast-mode-stream-native-evidence.json'
    frozen = json.loads(historical(frozen_path))
    assert frozen['verified']['nativeModeComparisons'] == 73728 and frozen['verified']['passMarkers'] == 236
    for name, digest in frozen['artifacts'].items():
        raw = (ROOT / name).read_bytes() if name.startswith('.local/') else historical(name)
        assert hashlib.sha256(raw).hexdigest() == digest, name
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for name_input, digest in audit['InputHashes'].items():
            assert sha(ROOT / name_input) == digest, name_input
        audits.append(audit)
        artifacts += [path, ROOT / 'tools/reference' / name]
    baseline = json.loads(historical('tools/reference/named-item-runtime-audit.json'))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    full_markers, full_saves = None, None
    if full_class:
        complete = inventory.validate_inventory(STAGE / 'class-inputs.json')
        assert complete['artifacts'] == inputs['artifacts'] and complete['copiedData'] == inputs['copiedData']
        full_log = (STAGE / 'class-native-checks.log').read_text()
        assert ' ERR]' not in full_log and ': failed' not in full_log and full_log.count('Shutting down') == 3
        assert 'including two save/reload cycles.' in (STAGE / 'full-run.log').read_text()
        assert full_log.count('named-gear-check: passed') == 1 and full_log.count('named-gear-reload-check: passed') == 2
        assert full_log.count('first-loop-catalogue-check: passed') == 1
        full_markers, full_saves = full_log.count(': passed'), full_log.count('Writing world save snapshot done')
        assert full_markers == 1184 and full_saves >= 3
        for name, expected in [('full-named-item-runtime-audit.json', audits[0]), ('full-first-loop-affix-audit.json', audits[1])]:
            path = STAGE / name
            assert json.loads(path.read_text()) == expected, name
            artifacts.append(path)
        artifacts += [STAGE / name for name in ['class-inputs.json', 'class-native-checks.log', 'full-run.log']]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 171 and sum(x['characterClass'] == 'Druid' for x in handlers) == 15 and status['completedClasses'] == []
    assert next(x for x in status['skills'] if x['talentId'] == 'druid.summongrizzly')['implementation'] != 'handler-present'
    npc = json.loads((ROOT / 'data/Sanctuary/Balance/druid-npc-skills.json').read_text())
    assert [x['sourceId'] for x in npc] == [314, 326] and npc[1]['sourceFields']['calc2'] == '"max(250,ln12)"'
    observation_path = ROOT / 'tools/reference/grizzly-bearsmite-user-observations.json'
    assert json.loads(observation_path.read_text())['observed']['verbatim'] == "i've only seen a knockback, no stuns"
    artifacts += [observation_path]
    artifacts += [ROOT / name for name in ['tools/record_bear_smite.py', 'tools/record_grizzly_body.py',
        'tools/probe_bear_smite_reference.py', 'tools/build_class_skills.py', 'tests/check_bear_smite.py', 'tests/check_creeper_stats.py']]
    result = dict(referenceVersion='3.3.93847', installed=False, grizzlyHandlerEnabled=False, handlers=171,
        druidHandlers=15, completedClasses=[], goalComplete=False,
        verified=dict(nativePreparedPackets=1606, sourcePacketContexts=1636, nativeReductionContexts=1248,
            sourceRationalDifferences=272, npcRanks=255, passMarkers=240, boots=3, actualReloads=2,
            pythonTests=70, namedDefinitions=573, inputFingerprints=[len(x['InputHashes']) for x in audits],
            fullClassMarkers=full_markers, fullClassBoots=3 if full_class else None,
            fullClassReloads=2 if full_class else None, fullClassSaveSnapshots=full_saves),
        frozenModeAcceptance=dict(commit=FROZEN, receipt=frozen_path,
            sha256=hashlib.sha256(historical(frozen_path)).hexdigest(), package='.local/class-skills/beast-mode-stream'),
        inputInventory=inputs, artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        established=[
            'NPC326prepared packet separates cached health total from the later physical-field addition. Native packets match complete start32/allocation/fill/mitigation source composition,including flags,cold,critical/hit-class and exact supplied RNG requests. No stun is invented;the user observes knockback only.',
            'Shared nonzero-resistance and percent-absorption calculations now follow source early division above1048576FP. Zero resistance bypasses that helper.1248complete source cases,including272differences from always-multiply-first math,match native mitigation. Existing native zero floor and wide ceiling remain explicit limits outside the supplementary legacy domain.',
            'Two current NPC rows remain separate from player talents:314Fenris rage and326BearSmite.255NPC ranks and unchanged314formulas pass. Initial startup failed because calc2retains quoted source text;validation now accepts those source quotes without altering the pinned row or formula evaluator.',
            '240focused markers across3boots/2actual reloads retain damage rounding,saved Grizzly,wolf/spirit combat and right/quick inputs. Fresh runtime fingerprints retain573named item decisions. Full acceptance,when present,is independently bound to the identical candidate assemblies and copied data.',
        ], remaining=[
            'Prepared helper has no world caller. Connect sequence14clock/start32/action/do2release with range3recheck,stored mitigation/deferred absorb,actual events,knockback,cancellation and saved state. Source final mode/Thorns/cold/global callbacks are recording services,not modern geometry or executable equivalence.',
            'Connect AI112movement,ordinary physical hit,full mode-stat refresh and learned40mana/cap1/coexistence/rollback/right+quick/reload. First-think/busy/path/event/seed,equipment/nonzero-sunder/item-only and lethal HP/death/return need acceptance. Legacy negative-MDR/signed-overflow results remain outside the tested numeric domain.',
            'Final B7effects/audio and separate talent/right/quick icons remain registered placeholders. Main stays101013bwith original build/saves/config/client preserved;no completed class or installed Grizzly is claimed.',
        ])
    path = ROOT / 'tools/reference/bear-smite-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'BearSmite preparation:1606packets/1248reductions,240markers/3boots/2reloads,70Python tests,fullClass={full_markers};171handlers/Druid15,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
