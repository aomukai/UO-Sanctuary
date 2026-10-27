#!/usr/bin/env python3
"""Bind corrected raw beast mode stats and retained saved consumers to one candidate."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as body_contract

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'beast-mode-stream'
FROZEN = '51c063b'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def historical(name):
    return subprocess.check_output(['git', 'show', FROZEN + ':' + name], cwd=ROOT)


def record(check=False):
    body_contract.STAGE = STAGE
    inputs = body_contract.validate_inventory(STAGE / 'stats-inputs.json')
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'mode-run.log', 'python-tests.log', 'grizzly-body-fixtures.json']]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        build = path.read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 236
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    assert log.count('beast-mode-stream-check: passed') == 1
    assert '73728native complete writer comparisons for3species/all16header modes' in log
    for name in ['grizzly-body', 'dire-wolf-body', 'dire-wolf-world', 'dire-wolf-casting', 'spirit-wolf-body', 'spirit-wolf-world', 'spirit-wolf-casting', 'barbs-casting', 'wolverine-casting', 'oak-casting']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    for name in ['dire-wolf', 'spirit-wolf', 'barbs', 'oak', 'wolverine']:
        for kind in ['right', 'quick']:
            assert log.count(name + '-' + kind + '-result: passed') == 3, name
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    source_path = ROOT / 'tools/reference/beast-mode-stream-engine-evidence.json'
    source = json.loads(source_path.read_text())
    assert source['referenceVersion'] == '3.3.93847' and source['legacyCommit'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    assert source['monsterModes']['MONMODE_BLOCK'] == 6 and source['monsterModes']['MONMODE_SEQUENCE'] == 14
    fixture = ROOT / source['fixture']['path']
    generated = BASE / 'beast-mode-stream-source-probe.cpp'
    sanitizer = BASE / 'beast-mode-stream-sanitizer.log'
    assert source['fixture']['cases'] == 76416 and source['fixture']['ordinaryContexts'] == 73728 and source['fixture']['guardContexts'] == 2688
    assert sha(fixture) == source['fixture']['sha256'] and sha(generated) == source['fixture']['generatedSourceSha256'] and sanitizer.read_text() == ''
    for name, digest in source['sourceHashes'].items():
        assert sha(BASE / 'd2moo-pinned-source' / name) == digest, name
    artifacts += [source_path, fixture, generated, sanitizer]
    # Full retained acceptance stays with the unchanged earlier body package.
    frozen_path = 'tools/reference/grizzly-body-native-evidence.json'
    frozen = json.loads(historical(frozen_path))
    assert frozen['verified']['fullClassMarkers'] == 1184 and frozen['verified']['fullClassBoots'] == 3 and frozen['verified']['fullClassReloads'] == 2
    for name, digest in frozen['artifacts'].items():
        raw = (ROOT / name).read_bytes() if name.startswith('.local/') else historical(name)
        assert hashlib.sha256(raw).hexdigest() == digest, name
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for source, digest in audit['InputHashes'].items():
            assert sha(ROOT / source) == digest, source
        audits.append(audit)
        artifacts += [path, ROOT / 'tools/reference' / name]
    baseline = json.loads(historical('tools/reference/named-item-runtime-audit.json'))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 171 and sum(x['characterClass'] == 'Druid' for x in handlers) == 15 and status['completedClasses'] == []
    assert next(x for x in status['skills'] if x['talentId'] == 'druid.summongrizzly')['implementation'] != 'handler-present'
    observation_path = ROOT / 'tools/reference/grizzly-bearsmite-user-observations.json'
    observation = json.loads(observation_path.read_text())
    assert observation['observed']['verbatim'] == "i've only seen a knockback, no stuns"
    artifacts += [observation_path]
    artifacts += [ROOT / name for name in ['tools/record_beast_mode_stream.py', 'tools/record_grizzly_body.py', 'tools/probe_beast_mode_stream_reference.py', 'tests/check_beast_mode_stream.py', 'tests/check_creeper_stats.py']]
    result = dict(referenceVersion='3.3.93847', installed=False, grizzlyHandlerEnabled=False, handlers=171, druidHandlers=15, completedClasses=[], goalComplete=False,
        verified=dict(nativeModeComparisons=73728, sourceOrdinaryContexts=73728, sourceGuardContexts=2688, species=3, sourceModes=16,
            passMarkers=236, boots=3, actualReloads=2, pythonTests=70, namedDefinitions=573, inputFingerprints=[len(x['InputHashes']) for x in audits]),
        frozenFullAcceptance=dict(commit=FROZEN, receipt=frozen_path, sha256=hashlib.sha256(historical(frozen_path)).hexdigest(), package='.local/class-skills/grizzly-body', fullClassMarkers=1184, boots=3, actualReloads=2),
        inputInventory=inputs, artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        established=[
            'Raw flag1mode helper now follows actual header modes:5A2;6block/7cast/8S1useS1;other valid0..15useA1,includingGH3/dead12/sequence14. Unknown modes fail explicitly. This returns stats only and never starts a death/attack/sequence.',
            'All73728native ordinary writer comparisons match complete source getters/noRatio/mode routines at three species,all16modes,ranks0..255,three difficulties and two game contexts.2688source guards remain C++reference coverage;they are not asserted to be native body/service guards.',
            'Previously compared4/5/7/8outputs are unchanged. The unused3-as-block mapping is fixed to actual block6;the old mode12test wording is corrected. Copied birth/passives remain distinct from live owner damage/AR mode lists. This does not yet implement full runtime refresh on every mode transition.',
            'Retained Grizzly body and learned wolf/spirit consumers pass236markers over3boots/2actual reloads,including connected natural combat and right/quick inputs. All70Python tests pass;fresh item fingerprints retain573named decisions. Earlier1184-marker full acceptance remains frozen at51c063bwith its own body assemblies/data;no new full-suite claim is made for this mode-only package.',
            'The user observes Grizzly knockback without stuns in pinned3.3.93847. No stun or guessed250extra damage is introduced by the raw-mode correction.',
        ], remaining=[
            'Connect saved Grizzly body to AI112world movement/targeting and real ordinary physical hit. Compose sequence14clock/start32/do2/BearSmite326/knockback with proper innate NPCrank and Battle Command. Raw mode-list AR proof does not establish skill-action or hit-event ordering.',
            'Full runtime mode-list refresh and action capture,exact modern first-think/busy/geometry/path/event/seed,equipment/nonzero-sunder/item-only and incoming lethal HP/death/return chronology remain acceptance work. The observed absence of stun does not measure hidden extra-damage/packet or knockback-distance semantics.',
            'Learned40mana/cap1/coexistence/rollback/right+quick/reload and preserved rollout remain pending. Final B7effects/audio and separate talent/right/quick icons stay in independent registers. Installed main remains101013b;no class is complete.',
        ])
    path = ROOT / 'tools/reference/beast-mode-stream-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Beast raw mode stream:73728native comparisons,236markers/3boots/2reloads,70Python tests;171handlers/Druid15,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
