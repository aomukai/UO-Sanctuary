#!/usr/bin/env python3
"""Bind recipient-level item readers and the corrected to-hit divisors."""
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
STAGE = ROOT / '.local/class-skills/npc-item-suppliers'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False, full_class=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    _, prior = frozen_receipt('16ea609', 'tools/reference/iron-golem-items-native-evidence.json')
    assert prior['verified']['passMarkers'] == 204 and prior['verified']['fullClassMarkers'] is None
    artifacts = []
    for kind in ['content', 'integration']:
        p = STAGE / (kind + '-build.log')
        assert all(s in p.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [p, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 207
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    pattern = re.compile(r'\] ([A-Za-z0-9-]+): passed')
    before = Counter(pattern.findall((ROOT / '.local/class-skills/iron-golem-items/stats-native-checks.log').read_text()))
    after = Counter(pattern.findall(log))
    assert sum(before.values()) == 204 and not before - after
    assert after - before == Counter({'npc-item-suppliers-check': 1, 'npc-item-suppliers-reload-check': 2})
    assert log.count('NPC item suppliers:27648complete-source op2/op4 contexts/27owned scaled properties;explicit recipient37 versus owner47/item99,half-scaled AR610/live-player AR775,FP256life quarter,original ordinary/named items and op5refusal pass;full NPC stat graph/combat remains gated') == 1
    assert log.count('NPC item suppliers:original magic sword/two named armors retain complete rolls,explicit recipient37,local defense/weapon endpoints,AR610and quarter-life FP256through actual reload') == 2
    source_path = ROOT / 'tools/reference/npc-item-suppliers-source-evidence.json'
    source = json.loads(source_path.read_text()); artifacts.append(source_path)
    assert not source['currentExecutableParity'] and len(source['compiledUnchangedRoutineHashes']) == 2
    assert source['verified'] == dict(sourceContexts=27648, scaledProperties=27, ownedComparedFields=189, halfScaleProperties=['att/lvl', 'att%/lvl', 'att-dem/lvl', 'att-und/lvl'])
    for group in ['inputs', 'artifacts']:
        for name, digest in source[group].items():
            assert sha(ROOT / name) == digest, name
            artifacts.append(ROOT / name)
    assert not (ROOT / '.local/class-skills/npc-item-suppliers-source/sanitizer.log').read_bytes()
    saved = json.loads((STAGE / 'focused-fixtures/npc-item-suppliers-fixtures.json').read_text())
    assert saved['Owner'] > 0 and len(saved['Items']) == 3
    assert len({s['Pet'] for s in saved['Items']}) == len({s['Item'] for s in saved['Items']}) == 3
    for s, code in zip(saved['Items'], ['bsd', 'xrs', 'xng']):
        gear = json.loads(s['Gear'])
        assert s['Level'] == 37 and gear['BaseCode'] == code and gear['Identified'] and gear['Roll']['ItemLevel'] == 99
    assert saved['Items'][0]['Attack'] == 610 and saved['Items'][2]['Life256'] % 256 == 64
    assert json.loads(saved['Items'][0]['Gear'])['Roll']['Affixes'][0]['Definition']['Id'] == 'MagicPrefix:526'
    full = None; named_inputs = None
    if full_class:
        full = inventory.validate_inventory(STAGE / 'class-inputs.json')
        assert full['artifacts'] == inputs['artifacts'] and full['copiedData'] == inputs['copiedData']
        full_log = (STAGE / 'class-native-checks.log').read_text()
        assert ': failed' not in full_log and ' ERR]' not in full_log and full_log.count(': passed') == 1181
        assert full_log.count('Shutting down') == 3 and full_log.count('Writing world save snapshot done') == 35
        assert 'including two save/reload cycles.' in (STAGE / 'full-run.log').read_text()
        named_inputs = json.loads((STAGE / 'named-inputs.json').read_text())
        assert named_inputs['artifacts'] == full['artifacts'] and named_inputs['copiedData'] == full['copiedData']
        named_log = (STAGE / 'named-native-checks.log').read_text()
        assert ': failed' not in named_log and ' ERR]' not in named_log
        assert named_log.count(': passed') == named_log.count('Shutting down') == named_log.count('Writing world save snapshot done') == 3
        _, previous_full = frozen_receipt('16ce299', 'tools/reference/monster-ordered-release-native-evidence.json')
        assert previous_full['verified']['fullClassMarkers'] == 1184
        old_full = Counter(pattern.findall((ROOT / '.local/class-skills/monster-ordered-release/class-native-checks.log').read_text()))
        current_full = Counter(pattern.findall(full_log)); named = Counter(pattern.findall(named_log))
        assert named == Counter({'named-gear-check': 1, 'named-gear-reload-check': 2})
        assert old_full - current_full == named and current_full + named == old_full

    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        p = STAGE / name; audit = json.loads(p.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        if full_class:
            assert audit == json.loads((STAGE / ('full-' + name)).read_text())
            artifacts.append(STAGE / ('full-' + name))
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name2, digest in audit['InputHashes'].items():
            assert sha(ROOT / name2) == digest, name2
        audits.append(audit); artifacts += [p, ROOT / 'tools/reference' / name]
    old = json.loads(subprocess.check_output(['git', 'show', '16ea609:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == old['Definitions']
    assert [len(a['InputHashes']) for a in audits] == [727, 21]
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    main = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(main['installedArtifacts']) == 106
    for name, digest in main['installedArtifacts'].items():
        assert sha(Path(main['runtimeRoot']) / name) == digest, name
    for name in ['class-skill-status.json', 'skill-effects.json', 'skill-icons.json']:
        p = ROOT / 'data/Sanctuary/Balance' / name
        assert sha(p) == hashlib.sha256(subprocess.check_output(['git', 'show', '16ea609:' + str(p.relative_to(ROOT))], cwd=ROOT)).hexdigest()
        artifacts.append(p)
    artifacts += [STAGE / n for n in ['stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log']]
    if full_class:
        artifacts += [STAGE / n for n in ['class-inputs.json', 'class-native-checks.log', 'full-run.log', 'named-inputs.json', 'named-native-checks.log', 'named-run.log']]
        artifacts.append(ROOT / 'tests/check_named_candidate.py')
    artifacts += list((STAGE / 'focused-fixtures').glob('*fixtures.json'))
    artifacts += [p for p in (STAGE / 'diagnostics').rglob('*') if p.is_file()]
    artifacts += [ROOT / n for n in ['tools/record_npc_item_suppliers.py', 'tests/check_npc_item_suppliers.py', 'tools/build_skill_roadmap.py', 'tools/build_property_integration_backlog.py', 'tools/reference/item-property-integration-backlog.json', 'tools/reference/creeper-frame-installation-evidence.json']]
    result = dict(referenceVersion='3.3.93847', component='Recipient-level original equipment numeric suppliers', installed=False, handlerEnabled=False, currentExecutableParity=False, fullNpcStatGraphAccepted=False, sourceNpcLevelMappingAccepted=False, goalComplete=False, handlers=183, warlockHandlers=1, completedClasses=[],
        verified=dict(passMarkers=207, retainedMarkers=204, worldBoots=3, actualWorldReloads=2, completeSourceOperationContexts=27648, compiledRoutineCount=2, registeredScaledProperties=27, ownedComparedFields=189, halfScaledToHitProperties=source['verified']['halfScaleProperties'], explicitRecipientLevel=37, independentOwnerLevel=47, independentItemLevel=99, ordinarySourceAffix='MagicPrefix:526', recipientFlatAttackRating=610, realPlayerFlatAttackRating=775, playerEquipUnequip=True, savedOriginalItems=3, originalGearRollsRetained=True, fractionalLife256Remainder=64, unknownRecipientLevelRefused=True, movedOriginalRefused=True, unresolvedOp5Refused=True, fullClassMarkers=1181 if full_class else None, supplementalNamedMarkers=3 if full_class else None, supplementalNamedBoots=3 if full_class else None, supplementalNamedReloads=2 if full_class else None, combinedRegressionMarkers=1184 if full_class else None, fullClassBoots=3 if full_class else None, fullClassReloads=2 if full_class else None, fullClassSaveSnapshots=35 if full_class else None, pythonTests=70, unchangedNamedDefinitions=573, inputFingerprints=[727, 21], unchangedMainInstallationArtifacts=106),
        inputInventory=inputs, fullInputInventory=full, supplementalNamedInputInventory=named_inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))}, frozenPredecessor=dict(commit='16ea609', receipt='tools/reference/iron-golem-items-native-evidence.json'),
        established=[
            'Explicit recipient-level overloads share existing ordinary/named numeric interpretation and local armor/weapon readers. The original bound-item reader refuses missing, moved, replaced or deleted originals and levels outside1..99. The supplied level37 is a declared fixture context, not proof of real Iron Golem level mapping. No NPC stat graph or combat consumer is fabricated.',
            'Owned properties/itemstatcost corroborate189fields for27scaled properties, including resource value shifts. The four to-hit suppliers use halves, not eighths. Complete unchanged recursive query/percentage bodies match27648op2/op4controls under declared base/full-stat lookups, including own-versus-recipient levels, wrong/missing recipients and FP256 coefficients. Other branches of the compiled query are not certified by that matrix.',
            'Actual ordinary Trump magic sword generation, identification and player equip/unequip put775flat AR into and out of the level47player pool. The same source coefficient yields610at declared NPC level37, versus16at implicit level1. Actual original sword and Duriel/Spirit Forge armors retain full saved gear and local suppliers across both reloads. Resource bonuses retain the quarter-life FP256remainder instead of rounding to whole life first.',
            'The existing dmg%/lvl general damage projection is a discovered unresolved maximum-only op5routing gap. The new NPC context rejects that property, leaving an actual admitted Messerschmidt Reaver intact. Existing named acquisition decisions are unchanged; no unavailable proc item or incomplete skill is newly enabled.',
            ('All204predecessor markers remain exact. The identical package passes1181full-class checks over three boots/two reloads/35save snapshots. The optional named gate was omitted from that run;all3missing named checks pass separately over three boots/two reloads against identical artifacts/data. Their combined marker multiset retains all1184checks of16ce299exactly;the two worlds/scopes are reported separately.' if full_class else 'All204predecessor markers remain exact;no new full-class acceptance is claimed in this focused receipt.') + '70Python checks,573unchanged named eligibility decisions and106unchanged main installation artifacts pass. This does not complete a class or install the candidate.'
        ], remaining=[
            'Correct and verify max-only op5damage routing/order. Complete dynamic stat posting, damageRelated/primary-weapon arbitration, all other operation families and callbacks, actual NPC life/defense/damage/resistance/speed/proc consumers and source level mapping before claiming full equipment inheritance.',
            'Iron Golem still needs its real body/AI, paid cast/cap/placement, owner/facet/logout/death/respec handling and bound-item lifecycle. Item skill procs, charges, oskills and auras remain in the approved deferred return ledger. Skill effects/audio and talent/hotkey icons remain separate identified placeholders.'
        ])
    path = ROOT / 'tools/reference/npc-item-suppliers-native-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('NPC item suppliers:207focused/70Python;full-class acceptance='+str(full_class)+';27648source controls/189owned fields;full NPC inheritance remains unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true'); parser.add_argument('--full-class', action='store_true'); args=parser.parse_args(); record(args.check,args.full_class)
