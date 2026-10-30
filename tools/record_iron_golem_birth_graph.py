#!/usr/bin/env python3
"""Bind equipped Iron Golem graph birth to source controls and retained originals."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import record_grizzly_body as inventory
from snapshot_skill_build import check as check_snapshot
import build_skill_roadmap

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/iron-golem-birth-graph'
sha = inventory.sha


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    check_snapshot(STAGE / 'pre-build-2.json', STAGE / 'birth-inputs.json')
    prior = json.loads((ROOT / 'tools/reference/iron-golem-birth-native-evidence.json').read_text())
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['artifacts'][name] == prior['inputInventory']['artifacts'][name]
    for name, digest in prior['inputInventory']['sourceInputs'].items():
        if name.endswith('.json'):
            assert inputs['sourceInputs'][name] == digest, name
    sourcepath = ROOT / 'tools/reference/iron-golem-birth-graph-source-evidence.json'
    source = json.loads(sourcepath.read_text())
    assert source['referenceVersion'] == '3.3.93847' and source['cases'] == 2016
    for name, digest in (source['inputs'] | source['artifacts']).items():
        assert sha(ROOT / name) == digest, name
    casepath = '.local/class-skills/iron-golem-birth-graph-source/cases.json'
    assert inputs['sourceInputs'][casepath] == source['artifacts'][casepath]
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    markers = Counter(re.findall(r'\b([a-z0-9-]+): passed\b', log))
    assert markers == Counter({'iron-golem-birth-check': 3, 'iron-golem-items-check': 1, 'iron-golem-items-reload-check': 2,
        'npc-resource-callbacks-check': 1, 'npc-resource-callbacks-reload-check': 2,
        'iron-golem-birth-graph-check': 1, 'iron-golem-birth-graph-reload-check': 2})
    assert log.count('864 post-level life changes;2016 accrued maxima differ from base writes') == 3
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    for kind in ['content', 'integration']:
        text = (STAGE / (kind + '-build-2.log')).read_text()
        assert all(value in text for value in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert status == build_skill_roadmap.build()
    present = [row for row in status['skills'] if row['implementation'] == 'handler-present']
    assert len(present) == 185 and sum(row['characterClass'] == 'Necromancer' for row in present) == 29 and not status['completedClasses']
    iron = next(row for row in status['skills'] if row['talentId'] == 'necromancer.irongolem')
    assert iron['implementation'] == 'blocked' and iron['verification'] == 'partial-native-coverage' and not iron['complete']
    saved = json.loads((STAGE / 'iron-golem-birth-graph-fixtures.json').read_text())
    assert len(saved) == 3 and len({row['Item'] for row in saved}) == len({row['Pet'] for row in saved}) == 3
    assert [json.loads(row['Gear'])['NamedItemId'] for row in saved] == ['unique/206', 'unique/215', 'unique/213']
    assert [row['LifeBeforeWrite'] for row in saved] == [91136, 93696, 93376]
    assert [row['LifeBaseWrite'] for row in saved] == [546816, 562176, 560256]
    assert [row['Maximum'] for row in saved] == [559616, 577536, 575296]
    assert [row['Current'] for row in saved] == [row['LifeBaseWrite'] for row in saved]
    assert [row['Omissions'] for row in saved] == [[], [], ['sock:Numeric writer is not integrated']]
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for name, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / name) == digest, name
    paths = [Path(__file__).resolve(), sourcepath]
    paths += [ROOT / name for name in ['tools/probe_iron_golem_birth_graph.py', 'tests/check_iron_golem_birth.py', 'tests/check_iron_golem_birth_graph.py',
        'tools/build_skill_roadmap.py', 'docs/iron-golem-birth-graph-checkpoint.md', 'docs/class-skill-roadmap.md', 'docs/class-skills-resume-checkpoint.md',
        'docs/skill-mechanical-reference.md', 'docs/skill-effects.md', 'docs/skill-icons.md']]
    paths += [STAGE / name for name in ['pre-build-2.json', 'birth-inputs.json', 'birth-native-checks.log', 'run-2.log',
        'content-build-2.log', 'integration-build-2.log', 'iron-golem-items-fixtures.json', 'npc-resource-callbacks-fixtures.json', 'iron-golem-birth-graph-fixtures.json']]
    result = dict(referenceVersion='3.3.93847', component='Equipped numeric Iron Golem birth graph', installed=False, learnedHandlerEnabled=False,
        liveActorEnabled=False, completedClasses=[], goalComplete=False,
        verified=dict(sourceGraphContextsPerBoot=2016, postLevelLifeChanges=864, baseVsAccruedDifferences=2016,
            retainedBirthContextsPerBoot=36720, retainedResistanceContextsPerBoot=81, nativeMarkers=12, boots=3, actualReloads=2, saves=3,
            retainedOriginals=3, handlers=185, necromancerHandlers=29, unchangedInstalledArtifacts=106),
        inputInventory=inputs, artifacts={str(path.relative_to(ROOT)): sha(path) for path in paths},
        established=['Actual numeric graph setters/list attachment and callbacks preserve exact source before/birth/detach arrays and callback chronology. Level-driven item life recalculates before the passive life getter; base and accrued values remain distinct.',
            'Crown of Thieves,Shaftstop and Spiritforge rebuild identical projections from the same retained originals/full rolls on both reloads. Crown/Shaftstop have no numeric omissions;Spiritforge socket metadata is explicitly omitted.',
            'Unsupported graph ownership/damage inclusion is refused before mutation. A birth is single-use,including refusal to retry a partially written graph. Existing original-item/resource checks pass again. Machine roadmap records partial native coverage but leaves the talent blocked.'],
        remaining=['Live Mobile stat/resource synchronization and avoiding duplicate item contributions, complete source item/spawn birth, item-event registration/retaliation/procs, modern sunder, actual AI and learned cast/lifecycle remain unfinished.',
            'Raw source item/spawn arrays,formula services,successful allocation and event/state/equipment recording remain explicit. The numeric callback uses existing verified ratio/recovery helpers;this is not an accepted live Iron Golem body or a modern executable equivalence certificate.',
            'No new handler,class completion,full-class regression,fresh named-item audit or installation. No persistent schema is added;graph state is derived. Effects/audio and icons remain separate placeholders. Pre-existing Markdown roadmap history drift remains outside this component;machine output is checked.'])
    target = ROOT / 'tools/reference/iron-golem-birth-graph-native-evidence.json'
    text = json.dumps(result, indent=2) + '\n'
    if check: assert target.read_text() == text
    else: target.write_text(text)
    print('Iron Golem equipped graph:2016 source/native contexts per boot,12markers/3boots/2reloads,3retained originals;live body/cast remains unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
