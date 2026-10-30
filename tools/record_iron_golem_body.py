#!/usr/bin/env python3
"""Bind prepared Iron Golem saved-body acceptance to source, native worlds and installed hashes."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import record_grizzly_body as inventory
from snapshot_skill_build import check as check_snapshot
import build_skill_roadmap

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/iron-golem-body'
sha = inventory.sha


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    check_snapshot(STAGE / 'pre-build-7.json', STAGE / 'birth-inputs.json')
    prior = json.loads((ROOT / 'tools/reference/iron-golem-birth-graph-native-evidence.json').read_text())['inputInventory']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['artifacts'][name] == prior['artifacts'][name]
    old_cs = {name: digest for name, digest in prior['sourceInputs'].items() if name.endswith('.cs')}
    new_cs = {name: digest for name, digest in inputs['sourceInputs'].items() if name.endswith('.cs')}
    assert set(new_cs) - set(old_cs) == {'src/Sanctuary.Content/IronGolemBody.cs', 'src/Sanctuary.Content/IronGolemBodyNumbers.cs', 'tests/Sanctuary.Integration/IronGolemBodyChecks.cs'}
    assert {name for name in old_cs if old_cs[name] != new_cs[name]} == {'src/Sanctuary.Content/CreatureProfiles.cs', 'src/Sanctuary.Content/IronGolemItems.cs'}
    assert {name for name in prior['copiedData'] if prior['copiedData'][name] != inputs['copiedData'][name]} == {'Data/Sanctuary/Balance/class-skill-status.json', 'Data/Sanctuary/Balance/skill-effects.json'}
    for name, digest in prior['sourceInputs'].items():
        if name.endswith('.json'): assert inputs['sourceInputs'][name] == digest, name
    sourcepath = ROOT / 'tools/reference/iron-golem-body-source-evidence.json'
    source = json.loads(sourcepath.read_text()); assert source['referenceVersion'] == '3.3.93847' and source['cases'] == 2016
    for name, digest in (source['inputs'] | source['artifacts']).items(): assert sha(ROOT / name) == digest, name
    casepath = '.local/class-skills/iron-golem-body-source/cases.json'
    assert inputs['sourceInputs'][casepath] == source['artifacts'][casepath]
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    markers = Counter(re.findall(r'\b([a-z0-9-]+): passed\b', log))
    expected = Counter({'iron-golem-birth-check': 3})
    for family in ['iron-golem-items', 'npc-resource-callbacks', 'iron-golem-birth-graph', 'iron-golem-body']:
        expected[family + '-check'] = 1; expected[family + '-reload-check'] = 2
    assert markers == expected and sum(markers.values()) == 15
    assert log.count('Iron Golem body recipes:1008 applicable expansion source compositions') == 3
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    for kind in ['content', 'integration']:
        text = (STAGE / (kind + '-build-7.log')).read_text()
        assert all(value in text for value in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text()); assert status == build_skill_roadmap.build()
    present = [row for row in status['skills'] if row['implementation'] == 'handler-present']
    assert len(present) == 185 and sum(row['characterClass'] == 'Necromancer' for row in present) == 29 and not status['completedClasses']
    iron = next(row for row in status['skills'] if row['talentId'] == 'necromancer.irongolem')
    assert iron['implementation'] == 'blocked' and iron['verification'] == 'partial-native-coverage' and not iron['complete']
    saved = json.loads((STAGE / 'iron-golem-body-fixtures.json').read_text()); assert len(saved) == 2
    assert [json.loads(row['Gear'])['NamedItemId'] for row in saved] == ['unique/206', 'unique/215']
    for row in saved:
        state = json.loads(row['State']); recipe = state['Recipe']; numbers = state['Numbers']
        assert recipe['Owner'] == row['Owner'] and recipe['Item'] == row['Item'] and recipe['Rank'] == 1 and recipe['OwnerLevel'] == 99
        assert len(recipe['Effective']) == len(recipe['Hard']) == 30 and numbers['Level'] == 75 and state['Difficulty'] == 0
        assert row['Maximum'] == numbers['BirthMaximum256'] > numbers['LifeBase256'] > row['Life'] > 0
        assert row['Regeneration'] == numbers['Regeneration256']
    effects = json.loads((ROOT / 'data/Sanctuary/Balance/skill-effects.json').read_text())
    assert 'body14/hue0x835' in next(row for row in effects if row['talentId'] == 'necromancer.irongolem')['placeholder']
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text()); assert len(installed['installedArtifacts']) == 106
    for name, digest in installed['installedArtifacts'].items(): assert sha(Path(installed['runtimeRoot']) / name) == digest, name
    paths = [Path(__file__).resolve(), sourcepath]
    paths += [ROOT / name for name in ['tools/probe_iron_golem_body.py', 'tests/check_iron_golem_body.py', 'tools/build_skill_roadmap.py', 'tools/build_class_skills.py',
        'docs/iron-golem-body-checkpoint.md', 'docs/class-skill-roadmap.md', 'docs/class-skills-resume-checkpoint.md', 'docs/skill-mechanical-reference.md', 'docs/skill-effects.md', 'docs/skill-icons.md']]
    paths += [STAGE / name for name in ['pre-build-7.json', 'birth-inputs.json', 'birth-native-checks.log', 'run-4.log', 'content-build-7.log', 'integration-build-7.log',
        'iron-golem-body-fixtures.json', 'iron-golem-items-fixtures.json', 'npc-resource-callbacks-fixtures.json', 'iron-golem-birth-graph-fixtures.json']]
    result = dict(referenceVersion='3.3.93847', component='Prepared saved Iron Golem stat body', learnedHandlerEnabled=False, movingAiEnabled=False,
        automaticRecoveryEnabled=False, installed=False, completedClasses=[], goalComplete=False,
        verified=dict(sourceCases=2016, applicableNativeRecipeCasesPerBoot=1008, nativeMarkers=15, boots=3, actualReloads=2, saves=3, savedBodies=2,
            retainedBirthCasesPerBoot=36720, retainedResistanceCasesPerBoot=81, retainedGraphCasesPerBoot=2016, handlers=185, necromancerHandlers=29, unchangedInstalledArtifacts=106),
        inputInventory=inputs, artifacts={str(path.relative_to(ROOT)): sha(path) for path in paths},
        established=['The new body stores source base life/AR/armor and lets existing NPC item consumers add the original contribution exactly once. Exact maximum/current life, fractional damage/healing, raw resistance pools and seeded regeneration rates match the numeric backend.',
            'Captured rank/item birth recipes rebuild all three difficulties;actual facet changes preserve current-life percentage. Identification changes only the live item layer. Existing recipes ignore later owner gear;new bodies capture effective item ranks.',
            'The same Crown of Thieves and Shaftstop,full rolls,recipes,exact pools and owner identities survive both reloads. Deletion/combat death consumes originals. Injected controller refusal restores the ground original unchanged;unsupported Spiritforge socket projection refuses before transfer.'],
        remaining=['Unverified life multiplication overflow and births outside the current native resource domain are refused before transfer. Full current monster/item initialization and unusual property/clock contexts are not certified. Raw noRatio bases and zero initial AR remain declared table-backed services. The prototype inherits native mana/stamina conventions without a source parity claim.',
            'Moving AI,attack timing/delivery,block/hit-recovery/mode/geometry,automatic recovery,retaliation/item events/skill-dependent procs,modern sunder,unified golem replacement/cap,learned input/payment/targeting/placement,full owner history/logout/death and actual-client acceptance remain unfinished.',
            'No learned handler,class completion,full-class regression,fresh named-item audit,old-content migration or installation is claimed. Two new saved bodies are tested;existing player saves are untouched. Effects/audio and talent/hotkey icons remain separate placeholders.'])
    target = ROOT / 'tools/reference/iron-golem-body-native-evidence.json'; text = json.dumps(result, indent=2) + '\n'
    if check: assert target.read_text() == text
    else: target.write_text(text)
    print('Prepared Iron Golem body:1008 applicable source recipes per boot,15native markers/3boots/2reloads;live stats/facets/rollback/persistence pass;AI/cast remain disabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
