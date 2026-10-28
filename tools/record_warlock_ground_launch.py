#!/usr/bin/env python3
"""Bind shared Warlock ground launch while keeping new dispatch gaps explicit."""
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
STAGE = ROOT / '.local/class-skills/warlock-ground-launch'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    _, prior = frozen_receipt('0e5cb36', 'tools/reference/melee-geometry-native-evidence.json')
    assert prior['verified']['passMarkers'] == 200 and prior['verified']['fullClassMarkers'] is None
    artifacts = []
    for kind in ['content', 'integration']:
        p = STAGE / (kind + '-build.log')
        assert all(s in p.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [p, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 201
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    pattern = re.compile(r'\] ([A-Za-z0-9-]+): passed')
    before = Counter(pattern.findall((ROOT / '.local/class-skills/melee-geometry/stats-native-checks.log').read_text()))
    after = Counter(pattern.findall(log))
    assert sum(before.values()) == 200 and not before - after and after - before == Counter({'warlock-ground-launch-check': 1})
    assert log.count('Warlock ground launch:64start14/2304do28-do123/144hit23contexts;exact placement/flags/seed-at-allocation and target-before-dispatch;new do39/do163remain gated') == 1
    p = ROOT / 'tools/reference/warlock-ground-launch-source-evidence.json'; source = json.loads(p.read_text()); artifacts.append(p)
    assert not source['currentExecutableParity'] and source['verified']['caseCounts'] == dict(start=64, launch=2304, hit23=144)
    assert source['verified']['legacySkill163IsNull'] and len(source['compiledUnchangedRoutineHashes']) == 16
    profiles = source['verified']['profiles']
    assert json.loads((ROOT / 'data/Sanctuary/Balance/warlock-ground-launch.json').read_text()) == profiles
    assert [p['Ability'] for p in profiles] == [241, 240] and [p['MissileDo'] for p in profiles] == [39, 1]
    assert [p['HitDispatch'] for p in profiles] == [0, 163] and [p['Lifetime'] for p in profiles] == [50, 50]
    for group in ['inputs', 'artifacts']:
        for name, digest in source[group].items():
            assert sha(ROOT / name) == digest, name
            artifacts.append(ROOT / name)
    assert not (ROOT / '.local/class-skills/warlock-ground-launch-source/sanitizer.log').read_bytes()
    saved = json.loads((STAGE / 'focused-fixtures/monster-death-delay-fixtures.json').read_text())
    assert saved['Next'] == 2 and [len(b) for b in saved['Batches']] == [4, 4]
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        p = STAGE / name; audit = json.loads(p.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name2, digest in audit['InputHashes'].items():
            assert sha(ROOT / name2) == digest, name2
        audits.append(audit); artifacts += [p, ROOT / 'tools/reference' / name]
    old = json.loads(subprocess.check_output(['git', 'show', '0e5cb36:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == old['Definitions']
    assert [len(a['InputHashes']) for a in audits] == [723, 21]
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    main = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(main['installedArtifacts']) == 106
    for name, digest in main['installedArtifacts'].items():
        assert sha(Path(main['runtimeRoot']) / name) == digest, name
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 183 and status['completedClasses'] == []
    blocked = [s for s in status['skills'] if s['talentId'] in ['warlock.abyss', 'warlock.apocalypse']]
    assert len(blocked) == 2 and all(s['implementation'] == 'blocked' for s in blocked)
    artifacts += [STAGE / n for n in ['stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log']]
    artifacts += list((STAGE / 'focused-fixtures').glob('*fixtures.json'))
    artifacts += [ROOT / n for n in ['tools/record_warlock_ground_launch.py', 'tests/check_warlock_ground_launch.py', 'tools/build_skill_roadmap.py', 'data/Sanctuary/Balance/class-skill-status.json', 'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json', 'tools/reference/creeper-frame-installation-evidence.json']]
    result = dict(referenceVersion='3.3.93847', component='Prepared Abyss/Apocalypse ground launch and hit23 dispatch boundary', installed=False, handlerEnabled=False, currentExecutableParity=False, fullActorAccepted=False, goalComplete=False, handlers=183, warlockHandlers=1, completedClasses=[],
        verified=dict(passMarkers=201, retainedMarkers=200, worldBoots=3, actualWorldReloads=2, sourceStart14Contexts=64, sourceLaunchContexts=2304, sourceHit23Contexts=144, compiledRoutineCount=16, targetVersusPath=True, differentDoFlagOrder=True, genericDistanceBoundary=100, casterSeedBeforeAllocation=True, failedAllocationConsumesApocalypseDraw=True, targetBeforeHitDispatch=True, legacySkill163IsNull=True, currentGroundProfiles=profiles, explicitlyBlockedSkills=['warlock.abyss', 'warlock.apocalypse'], fullClassMarkers=None, pythonTests=70, unchangedNamedDefinitions=573, inputFingerprints=[723, 21], unchangedMainInstallationArtifacts=106),
        inputInventory=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))}, frozenPredecessor=dict(commit='0e5cb36', receipt='tools/reference/melee-geometry-native-evidence.json'),
        established=[
            'Native prepared policy matches64complete-source start14,2304do28/do123and144hit23contexts. Selected-target and first-path coordinates are intentionally different in the fixture. Missing room/zero axes/destination room/town,missile-index boundaries,mask5versus0x40,allocation failure and99/100/101distance controls retain source results and requests. Factory requests are explicit services,not actual current missile actors.',
            'Abyss shared do28sets the do flag before placement and passes the separate generic100distance check before allocation. Apocalypse shared do123sets the flag after placement,consumes one caster draw before allocation and retains that draw on failure. Successful allocation stores seed memory before notification. Exact caster seeds at every factory request match the complete source routines.',
            'Complete hit23optionally assigns the victim before forwarding retained original skill/rank to its hit parameter. Current Apocalypse parameter163lands in a null older dispatcher slot;current Abyss center and terminal use new missile do39. Owned3.3.93847skill/missile provenance is bound through the existing whole-table comparisons. Both skills are now explicitly blocked with their missing dependencies rather than left generically pending. Raw Abyss sksrc radius text is retained without inventing its lookup semantics.',
            'All200predecessor markers,including contact/geometry/health/Stun/death and both actual saved deferred-death batches,remain exact. Both builds,70Python tests,573named-item decisions and106installed artifact hashes pass. The earlier16ce299package separately retains its1184full-class checks;no new full suite,learned Warlock cast,installation or class completion is claimed.'
        ], remaining=[
            'Abyss new do39pull path/pulse/terminal burst,hit-delay and LastCollide identity,current Radius sksrc semantics,mastery/pierce and full owner lifecycle remain unsupported. Apocalypse new do163area/filter/LOS,one-roll-versus-per-target order,fire-resistance state/immunity penalty/restoration and expiry remain unverified. Neither current handler is replaced by a legacy Blizzard/Volcano effect.',
            'Modern outer cast/payment/animation clocks and actual room/placement/factory/stat/network services remain separate. This component does not debit mana,emit working current missiles or certify current executable parity. Goatman current AI/proc/extended passives and other previously recorded actor/collision work remain open;continue unaffected mechanics when an individual current handler is unavailable.',
            'Final Abyss open/pulse/pull/close and Apocalypse cast/detonation/debuff effects/audio,plus separate talent/hotkey icons,remain in their independent registers. Existing generic presentations are identified placeholders;no new art is required to prepare mechanical code. No class is complete.'
        ])
    path = ROOT / 'tools/reference/warlock-ground-launch-native-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Warlock ground launch:201focused/3boots/2reloads;64start/2304launch/144hit23controls;70Python;do39/do163remain blocked.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
