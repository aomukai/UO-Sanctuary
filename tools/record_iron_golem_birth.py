#!/usr/bin/env python3
"""Bind the Iron Golem prepared birth scope to exact tested inputs and logs."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import record_grizzly_body as inventory
from snapshot_skill_build import check as check_snapshot
import build_skill_roadmap

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/iron-golem-birth'
sha = inventory.sha


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    check_snapshot(STAGE / 'pre-build-3.json', STAGE / 'birth-inputs.json')
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['artifacts'][name]
    sourcepath = ROOT / 'tools/reference/iron-golem-birth-source-evidence.json'
    source = json.loads(sourcepath.read_text())
    assert source['referenceVersion'] == '3.3.93847' and source['birthCases'] == 36720 and source['resistanceCases'] == 81
    for name, digest in (source['inputs'] | source['artifacts']).items():
        assert sha(ROOT / name) == digest, name
    assert inputs['sourceInputs']['.local/class-skills/iron-golem-birth-source/cases.json'] == source['artifacts']['.local/class-skills/iron-golem-birth-source/cases.json']
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    markers = Counter(re.findall(r'\b([a-z0-9-]+): passed\b', log))
    assert markers == Counter({'iron-golem-birth-check': 3, 'iron-golem-items-check': 1, 'iron-golem-items-reload-check': 2,
        'npc-resource-callbacks-check': 1, 'npc-resource-callbacks-reload-check': 2})
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    for kind in ['content', 'integration']:
        text = (STAGE / (kind + '-build-3.log')).read_text()
        assert all(x in text for x in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert status == build_skill_roadmap.build()
    present = [row for row in status['skills'] if row['implementation'] == 'handler-present']
    assert len(present) == 185 and sum(row['characterClass'] == 'Necromancer' for row in present) == 29 and not status['completedClasses']
    iron = next(row for row in status['skills'] if row['talentId'] == 'necromancer.irongolem')
    assert iron['implementation'] == 'blocked' and not iron['complete'] and 'post-equip' in iron['remaining']
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for name, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / name) == digest, name
    paths = [Path(__file__).resolve(), sourcepath, ROOT / 'tools/probe_iron_golem_birth.py', ROOT / 'tests/check_iron_golem_birth.py',
        ROOT / 'tools/build_skill_roadmap.py', ROOT / 'docs/iron-golem-birth-checkpoint.md', ROOT / 'docs/class-skill-roadmap.md',
        ROOT / 'docs/class-skills-resume-checkpoint.md', ROOT / 'docs/skill-mechanical-reference.md', ROOT / 'docs/skill-effects.md', ROOT / 'docs/skill-icons.md']
    paths += [STAGE / name for name in ['pre-build-3.json', 'birth-inputs.json', 'birth-native-checks.log', 'run-4.log',
        'content-build-3.log', 'integration-build-3.log', 'iron-golem-items-fixtures.json', 'npc-resource-callbacks-fixtures.json']]
    result = dict(referenceVersion='3.3.93847', component='Iron Golem post-equipment numeric birth preparation',
        learnedHandlerEnabled=False, actorEnabled=False, installed=False, completedClasses=[], goalComplete=False,
        verified=dict(birthContextsPerBoot=36720, resistanceContextsPerBoot=81, nativeMarkers=9, boots=3, actualReloads=2, saves=3,
            handlers=185, necromancerHandlers=29, unchangedInstalledArtifacts=106),
        inputInventory=inputs, artifacts={str(path.relative_to(ROOT)): sha(path) for path in paths},
        established=['Native preparation matches numeric outputs of four complete unchanged source routines with declared services; all ten current passive fields are retained, hard synergies exclude effective item ranks and zero resistance avoids absorption reads.',
            'Existing original-item storage and resource callback fixtures survive two actual reloads against the exact source/data/assemblies. No persistent schema or existing runtime consumer is changed.',
            'Clean builds have zero warnings/errors. The machine roadmap agrees with its generator and still leaves Iron Golem blocked. All106 installed artifacts are unchanged.'],
        remaining=['Prepared MAXHP is a base write, not final accrued HP. Applying it through the real item graph/callbacks, full spawn/birth, thorns/event/proc delivery, modern sunder inheritance, cast and actor/AI/lifecycle remain unfinished.',
            'No current executable equivalence, new learned handler, class completion, full-class regression, fresh named-item audit or installation is claimed. Missing effects/audio and icons remain separately tracked placeholders.',
            'The first disposable attempt passed its initial mechanics but failed native shutdown with PingServer disabled; the corrected harness passes all three boots. The full Markdown roadmap generator already had historical milestone drift before this increment; only its machine output and the updated skill row are certified here.'])
    target = ROOT / 'tools/reference/iron-golem-birth-native-evidence.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == text
    else:
        target.write_text(text)
    print('Iron Golem prepared birth:36,720numeric/81resistance controls per boot,9native checks/3boots/2reloads;185handlers and106installed artifacts preserved.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
