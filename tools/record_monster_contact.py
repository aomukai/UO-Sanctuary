#!/usr/bin/env python3
"""Bind captured NPC contact policy and the unchanged qualified/final consumers."""
import argparse
import hashlib
import json
import re
import subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/monster-contact'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    _, prior = frozen_receipt('10e6fe6', 'tools/reference/monster-death-delay-native-evidence.json')
    assert prior['verified']['passMarkers'] == 198 and prior['verified']['fullClassMarkers'] is None
    artifacts = []
    for kind in ['content', 'integration']:
        p = STAGE / (kind + '-build.log')
        assert all(s in p.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [p, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 199
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    pattern = re.compile(r'\] ([A-Za-z0-9-]+): passed')
    before = Counter(pattern.findall((ROOT / '.local/class-skills/monster-death-delay-native/stats-native-checks.log').read_text()))
    after = Counter(pattern.findall(log))
    assert sum(before.values()) == 198 and not before - after and after - before == Counter({'monster-contact-check': 1})
    assert log.count('NPC contact:3840complete-source releases/1616health commits;missing target/range/death-mode/room/hostility/town/killability/dead guards,exact traces and immutable captured packets pass;real metadata/range mapping remains explicit') == 1
    source_path = ROOT / 'tools/reference/goatman-npc-release-source-evidence.json'
    source = json.loads(source_path.read_text()); artifacts.append(source_path)
    assert source['contexts'] == 3840 and not source['currentExecutableParity']
    for group in ['inputs', 'artifacts']:
        for name, digest in source[group].items():
            assert sha(ROOT / name) == digest, name
            artifacts.append(ROOT / name)
    rows = json.loads((ROOT / '.local/class-skills/goatman-npc-release-source-probe.json').read_text())
    exits = Counter(); commits = 0
    for i, _, *_ in rows:
        gate = i[4]; hit = i[2] & 1
        exits['missingTarget' if gate == 3 else 'outOfRangeFreeRequests' if gate == 1 else 'deathModeRetained' if gate == 2 and hit else 'finished'] += 1
        commits += bool(hit and gate == 0 and i[3] > 0)
    assert dict(exits) == dict(finished=3168, outOfRangeFreeRequests=288, missingTarget=288, deathModeRetained=96) and commits == 1616
    assert not (ROOT / '.local/class-skills/goatman-npc-release/sanitizer.log').read_bytes()
    # Retained persistence tests must still complete both real saved batches.
    saved = json.loads((STAGE / 'focused-fixtures/monster-death-delay-fixtures.json').read_text())
    assert saved['Next'] == 2 and [len(b) for b in saved['Batches']] == [4, 4]
    assert log.count('monster-death-delay-reload-check: passed') == 2
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        p = STAGE / name; audit = json.loads(p.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name2, digest in audit['InputHashes'].items():
            assert sha(ROOT / name2) == digest, name2
        audits.append(audit); artifacts += [p, ROOT / 'tools/reference' / name]
    old = json.loads(subprocess.check_output(['git', 'show', '10e6fe6:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == old['Definitions']
    assert [len(a['InputHashes']) for a in audits] == [719, 21]
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    main = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(main['installedArtifacts']) == 106
    for name, digest in main['installedArtifacts'].items():
        assert sha(Path(main['runtimeRoot']) / name) == digest, name
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 183 and status['completedClasses'] == []
    artifacts += [STAGE / n for n in ['stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log']]
    artifacts += list((STAGE / 'focused-fixtures').glob('*fixtures.json'))
    artifacts += [ROOT / n for n in ['tools/record_monster_contact.py', 'tests/check_monster_contact.py', 'tools/build_skill_roadmap.py', 'data/Sanctuary/Balance/class-skill-status.json', 'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json', 'tools/reference/creeper-frame-installation-evidence.json']]
    result = dict(referenceVersion='3.3.93847', component='Captured NPC melee contact and outer event eligibility', installed=False, handlerEnabled=False, currentExecutableParity=False, fullActorAccepted=False, goalComplete=False, handlers=183, warlockHandlers=1, completedClasses=[],
        verified=dict(passMarkers=199, retainedMarkers=198, worldBoots=3, actualWorldReloads=2, completeSourceReleases=3840, qualifiedHealthCommits=1616, outerOrMissCases=2224, exits=dict(exits), missingPacketShortCircuit=True, lazyMetadataOrder=True, releaseStateBeforeTargetLookup=True, immutableStartPacket=True, copiedContactHitFlags=32, missVersusRejectedSuccessPresentation=True, unchangedQualifiedAndFinalConsumers=True, fullClassMarkers=None, pythonTests=70, unchangedNamedDefinitions=573, inputFingerprints=[719, 21], unchangedMainInstallationArtifacts=106),
        inputInventory=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))}, frozenPredecessor=dict(commit='10e6fe6', receipt='tools/reference/monster-death-delay-native-evidence.json'),
        established=[
            'NPC contact policy composes all3840complete-source releases and exact health/event/return/free traces,including2224outer or miss cases not covered by the prior qualified consumer. There are1616health commits,288missing targets,288out-of-range free requests,96successful death-mode packet retentions and3168finished contacts. Metadata calls short-circuit in source order;NPC contact requests range bonus3.',
            'Goatman do2 refreshes the existing Berserk state before looking up the release target. Missing target returns no action. The copied start packet is reused rather than rebuilt from release rank,weapon or resistance. Successful contact overwrites only the copied hit flags with32. Out-of-range contact calls the mandatory free service;successful death-mode contact returns without freeing. Actual packet-store ownership remains the actor service contract.',
            'Hostility and missing-room rejection clear hit/death result bits;town refusal clears only death. Unkillable or dead recipients skip the health/event phase without silently rewriting other flags. Attack-attempt events,thorns eligibility,return and free retain their source order even after rejected health events. Misses skip hit overlay/base-class presentation;an initially successful hit still presents after later rejection. Separate controls verify original4096hit flags cannot bypass the copied contact flags.',
            'The old qualified release now calls the same extracted CommitQualified and Finish phases as contact. Its once-only FP health,transient Stun,cached native death callback lifetime,typed return and packet-free behavior retain all198predecessor markers,including the two actual saved deferred-death batches. Both builds,70Python tests,573named-item decisions and106installed artifact hashes pass. Earlier full-class acceptance remains separately scoped to16ce299;no new full suite or installation is claimed.'
        ], remaining=[
            'These are NPC-to-NPC policies with mandatory real range/collision,hostility,room/row,killability/dead-mode and packet-store services. The fixtures supply those metadata values;they do not establish actual Goatman world geometry or full action scheduling. Zero-HP recipients that a future real metadata service still qualifies remain outside CommitQualified and must be resolved explicitly,not approximated. Reentrant health/deletion remains unsupported.',
            'Real actor mode acceptance,animation/FHR clocks,block/knockback/network/AI actions,think rearming and full lifecycle/deletion cleanup remain unfinished. Contact health still requires actual proc/cold/damage-credit/drain/absorb/regeneration services. Modern current helper/dispatcher linkage,extended passives/event16/CrushingBlow/BloodOath/sunder and new Frenzy/Cleave still gate a learned Goatman actor/cast/AI;current executable parity is not certified.',
            'Final NPC get-hit/Stun/Berserk/body effects/audio and separate talent/hotkey icons remain pending in independent registers. Existing presentation remains an identified placeholder;this shared contact policy creates no player hotkey identity. No class is complete.'
        ])
    path = ROOT / 'tools/reference/monster-contact-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('NPC contact:199focused/3boots/2reloads;3840source releases/1616health commits;70Python;real metadata and actor remain explicit.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
