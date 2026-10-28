#!/usr/bin/env python3
"""Bind original Iron Golem item storage to its source and actual reload checks."""
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
STAGE = ROOT / '.local/class-skills/iron-golem-items'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    _, prior = frozen_receipt('1412424', 'tools/reference/warlock-ground-launch-native-evidence.json')
    assert prior['verified']['passMarkers'] == 201 and prior['verified']['fullClassMarkers'] is None
    artifacts = []
    for kind in ['content', 'integration']:
        p = STAGE / (kind + '-build.log')
        assert all(s in p.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [p, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 204
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    pattern = re.compile(r'\] ([A-Za-z0-9-]+): passed')
    before = Counter(pattern.findall((ROOT / '.local/class-skills/warlock-ground-launch/stats-native-checks.log').read_text()))
    after = Counter(pattern.findall(log))
    assert sum(before.values()) == 201 and not before - after
    assert after - before == Counter({'iron-golem-items-check': 1, 'iron-golem-items-reload-check': 2})
    assert log.count('Iron Golem items:6144cast/135metadata source contexts,692current bases/316metal;') == 1
    assert log.count('Iron Golem items:actual saved sword/armor retain original serial,parent,owner,visibility,move lock,durability and full gear rolls without clone/recast on reload') == 2
    source_path = ROOT / 'tools/reference/iron-golem-items-source-evidence.json'
    source = json.loads(source_path.read_text()); artifacts.append(source_path)
    assert not source['currentExecutableParity'] and len(source['compiledUnchangedRoutineHashes']) == 4
    assert source['verified'] == dict(castContexts=6144, metadataContexts=135, itemBases=692, metalBases=316, currentSkillId=90)
    for group in ['inputs', 'artifacts']:
        for name, digest in source[group].items():
            assert sha(ROOT / name) == digest, name
            artifacts.append(ROOT / name)
    owned = json.loads((ROOT / 'tools/reference/iron-golem-local-items-evidence.json').read_text())
    assert owned['verified'] == dict(itemBases=692, itemTypes=105, comparedFields=1594, bodyLocationCodes=11, unexplainedDifferences=0)
    for name, digest in owned['inputs'].items():
        assert sha(ROOT / name) == digest, name
    for row in owned['tables']:
        assert sha(ROOT / row['path']) == row['sha256']
    assert not (ROOT / '.local/class-skills/iron-golem-items-source/sanitizer.log').read_bytes()
    saved = json.loads((STAGE / 'focused-fixtures/iron-golem-items-fixtures.json').read_text())
    assert len(saved['Items']) == 2 and saved['Owner'] > 0
    assert len({s['Binding']['Pet'] for s in saved['Items']}) == len({s['Binding']['Item'] for s in saved['Items']}) == 2
    for s, code, slot, hue, hits, maximum, native_type in zip(saved['Items'], ['bsd', 'plt'], [4, 3], [0x489, 0x481], [13, 17], [50, 60], ['Server.Items.Broadsword', 'Server.Items.PlateChest']):
        assert s['Binding']['Owner'] == saved['Owner'] and s['Binding']['BaseCode'] == code and s['Binding']['BodyLocation'] == slot
        assert (s['Hue'], s['Hits'], s['MaximumHits'], s['Type']) == (hue, hits, maximum, native_type)
        gear = json.loads(s['Gear'])
        assert gear['BaseCode'] == code and gear['Identified'] and gear['Roll']['ItemLevel'] == 47 and gear['Roll']['Rarity'] == 2
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        p = STAGE / name; audit = json.loads(p.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name2, digest in audit['InputHashes'].items():
            assert sha(ROOT / name2) == digest, name2
        audits.append(audit); artifacts += [p, ROOT / 'tools/reference' / name]
    old = json.loads(subprocess.check_output(['git', 'show', '1412424:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == old['Definitions']
    assert [len(a['InputHashes']) for a in audits] == [726, 21]
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    main = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(main['installedArtifacts']) == 106
    for name, digest in main['installedArtifacts'].items():
        assert sha(Path(main['runtimeRoot']) / name) == digest, name
    for name in ['class-skill-status.json', 'skill-effects.json', 'skill-icons.json']:
        p = ROOT / 'data/Sanctuary/Balance' / name
        assert sha(p) == hashlib.sha256(subprocess.check_output(['git', 'show', '1412424:' + str(p.relative_to(ROOT))], cwd=ROOT)).hexdigest()
        artifacts.append(p)
    artifacts += [STAGE / n for n in ['stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log']]
    artifacts += list((STAGE / 'focused-fixtures').glob('*fixtures.json'))
    artifacts += [p for p in (STAGE / 'diagnostics').rglob('*') if p.is_file()]
    artifacts += [ROOT / n for n in ['tools/record_iron_golem_items.py', 'tests/check_iron_golem_items.py', 'tools/build_skill_roadmap.py', 'tools/reference/creeper-frame-installation-evidence.json']]
    result = dict(referenceVersion='3.3.93847', component='Iron Golem original item eligibility, transfer and storage', installed=False, handlerEnabled=False, currentExecutableParity=False, fullActorAccepted=False, goalComplete=False, handlers=183, warlockHandlers=1, completedClasses=[],
        verified=dict(passMarkers=204, retainedMarkers=201, worldBoots=3, actualWorldReloads=2, sourceCastContexts=6144, sourceMetadataContexts=135, compiledRoutineCount=4, currentItemBases=692, currentMetalBases=316, ownedComparedFields=1594, currentItemTypes=105, currentBodyLocations=11, originalSavedItems=2, originalItemIdentity=True, fullGearRollsRetained=True, nativeDurabilityRetained=True, privateStorage=True, duplicateItemRefused=True, terminalItemDeletion=True, readOnlyWorkerSnapshot=True, atomicMalformedRestore=True, persistenceId=188, schemaVersion=0, fullClassMarkers=None, pythonTests=70, unchangedNamedDefinitions=573, inputFingerprints=[726, 21], unchangedMainInstallationArtifacts=106),
        inputInventory=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))}, frozenPredecessor=dict(commit='1412424', receipt='tools/reference/warlock-ground-launch-native-evidence.json'),
        established=[
            'Prepared start20/do57 and metal/body-slot helpers match 6279 complete-source contexts with exact lazy target checks, allocation-before-ground-removal and original-item equip-before-stat/client/AI order. Source do57 ignores the equip return; no rollback is attributed to it. Full equip/save routines are reviewed separately, not compiled as complete engine acceptance.',
            'Owned read-only 3.3.93847 tables corroborate 1594 cached fields across 692 bases and 105 item types, plus 11 body-location codes. Metal is base bit2, independent of appearance or native resource. Unsupported native item contexts are rejected explicitly.',
            'The storage adapter attaches the same sword or armor object privately to a controlled existing persistent Skeleton Guardian fixture without a player requirement check, clone or reroll. Both actual reloads retain original serial, parent, owner, move lock, visibility, durability and complete gear state. The holder bypasses ordinary native pet stabling explicitly; no Iron Golem actor is substituted. Duplicate binding, protected/nonmetal/stored items, read-only worker serialization, malformed/duplicate atomic restore and terminal item deletion have native controls.',
            'All 201 predecessor markers remain exact. Both builds and 70 Python tests pass, 573 named eligibility decisions remain unchanged and all 106 main installation artifacts match. No learned Iron Golem cast, full new class suite, installation or completed class is claimed.'
        ], remaining=[
            'Iron Golem remains unavailable until full native item stat/proc inheritance, actor/AI, resource/cap/placement, owner/facet/logout/death/respec and bound-item lifecycle are implemented. This internal storage component is not a playable golem or current executable parity certificate.',
            'Named/runeword/socket/ethereal item combat inheritance and dedicated acceptance remain separate from the two saved rare-item fixtures. Final summon/body/attack/hit/death effects and audio remain in the effects register; talent and hotkey icons remain separate. Existing presentations are identified placeholders. Art does not block mechanical implementation.'
        ])
    path = ROOT / 'tools/reference/iron-golem-items-native-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Iron Golem item storage:204focused/3boots/2reloads;6279source controls/692bases;70Python;full actor/property/proc inheritance remains gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
