#!/usr/bin/env python3
"""Bind complete NecroPet policy acceptance to unchanged source and the tested native build."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import record_grizzly_body as inventory
from snapshot_skill_build import check as check_snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/necromancer-pet-ai'
sha = inventory.sha


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    check_snapshot(STAGE / 'pre-build-2.json', STAGE / 'audit-inputs.json')
    prior = json.loads((ROOT / 'tools/reference/iron-golem-body-native-evidence.json').read_text())['inputInventory']
    old_cs = {name: digest for name,digest in prior['sourceInputs'].items() if name.endswith('.cs')}
    new_cs = {name: digest for name,digest in inputs['sourceInputs'].items() if name.endswith('.cs')}
    assert set(new_cs) - set(old_cs) == {'src/Sanctuary.Content/NecromancerPetAi.cs', 'tests/Sanctuary.Integration/NecromancerPetAiChecks.cs'}
    assert all(new_cs[name] == digest for name,digest in old_cs.items())
    assert inputs['copiedData'] == prior['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['artifacts'][name] == prior['artifacts'][name]
    source_path = ROOT / 'tools/reference/necromancer-pet-ai-source-evidence.json'
    source = json.loads(source_path.read_text())
    source_file = ROOT / '.local/class-skills/d2moo-pinned-source' / source['sourceFile']
    assert sha(source_file) == source['sourceSha256']
    from review_weapon_event_order import function
    for name,digest in source['unchangedRoutineHashes'].items():
        assert hashlib.sha256(function(source_file.read_text(), name).encode()).hexdigest() == digest
    sentinel = source['sentinelSetupReview']
    sentinel_file = ROOT / '.local/class-skills/d2moo-pinned-source' / sentinel['sourceFile']
    assert sha(sentinel_file) == sentinel['sourceSha256']
    assert hashlib.sha256(function(sentinel_file.read_text(), sentinel['routine']).encode()).hexdigest() == sentinel['routineSha256']
    assert source['referenceVersion'] == '3.3.93847' and source['fixture']['cases'] == 1598
    fixture = ROOT / source['fixture']['path']
    assert sha(fixture) == source['fixture']['sha256'] == inputs['sourceInputs'][source['fixture']['path']]
    generated = ROOT / '.local/class-skills/necromancer-pet-ai-source-probe.cpp'
    assert sha(generated) == source['generatedSourceSha256']
    assert sha(ROOT / 'tools/reference/d2r-named-equipment/json/monstats.json') == source['monsterTableSha256']
    assert not (ROOT / '.local/class-skills/necromancer-pet-ai-sanitizer.log').read_text()
    for kind in ['content', 'integration']:
        build = (STAGE / (kind + '-build-2.log')).read_text()
        assert all(value in build for value in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    log = (STAGE / 'audit-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log and log.count('Shutting down') == 1
    assert Counter(re.findall(r'\b([a-z0-9-]+): passed\b', log)) == Counter({'necromancer-pet-ai-check':1, 'named-gear-check':1, 'first-loop-catalogue-check':1})
    assert '1598 complete source ordered-service/state/draw comparisons and8 malformed-service guards pass' in log
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        audit = json.loads((STAGE / name).read_text())
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in audit['InputHashes'].items(): assert sha(ROOT / path) == digest, path
        audits.append(audit)
    assert len(audits[0]['Definitions']) == 573
    assert audits[0]['Definitions'] == json.loads((ROOT / 'tools/reference/named-item-runtime-audit.json').read_text())['Definitions']
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for name,digest in installed['installedArtifacts'].items(): assert sha(Path(installed['runtimeRoot']) / name) == digest, name
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [row for row in status['skills'] if row['implementation'] == 'handler-present']
    assert len(handlers) == 185 and sum(row['characterClass'] == 'Necromancer' for row in handlers) == 29 and not status['completedClasses']
    paths = [Path(__file__).resolve(), source_path, generated, fixture]
    paths += [ROOT / path for path in ['tools/probe_necromancer_pet_ai.py', 'tests/check_necromancer_pet_ai.py',
        'docs/necromancer-pet-ai-checkpoint.md', 'docs/class-skills-resume-checkpoint.md', 'docs/class-skill-roadmap.md', 'docs/skill-mechanical-reference.md']]
    paths += [STAGE / name for name in ['pre-build-2.json', 'audit-inputs.json', 'audit-native-checks.log', 'run-2.log',
        'content-build-2.log', 'integration-build-2.log', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']]
    result = dict(referenceVersion='3.3.93847', component='Complete prepared Necromancer pet AI67 decision policy',
        learnedHandlerEnabled=False, movingActorEnabled=False, installed=False, completedClasses=[], goalComplete=False,
        verified=dict(sourceContexts=1598, nativePolicyComparisons=1598, malformedServiceGuards=8, nativeMarkers=3,
            boots=1, actualReloads=0, namedDefinitions=573, auditFingerprints=[len(audit['InputHashes']) for audit in audits],
            handlers=185, necromancerHandlers=29, unchangedInstalledArtifacts=106), inputInventory=inputs,
        artifacts={str(path.relative_to(ROOT)): sha(path) for path in paths},
        established=[
            'Both complete unchanged older NecroPet routines match native C# policy query/action order,owner memory and percentage-draw count across1598cases. Melee/ranged fallback,visibility,validity,follow,town,attack/skill/chase/reposition/idle decisions remain distinct.',
            'No existing runtime C# or data/native kernel changed. The same isolated boot passes existing named-item and first-loop catalogue checks;573named decisions and106installed artifacts remain unchanged.'
        ], remaining=[
            'No moving actor,automatic recovery,attack or learned Iron Golem is enabled. Default parameter initialization,target/distance/visibility implementations,world movement/path/geometry,event/mode timing,shared item/AI seed,IAS,weapon packets and complete lifecycle remain separate acceptance work.',
            'Source-policy execution does not prove current3.3.93847executable parity. Skill123/mode8is a supplied nonzero-parameter control,not an invented Iron Golem ability. No save/reload,full-class regression,old-content migration,deployment,class completion or asset generation is claimed.'
        ])
    target = ROOT / 'tools/reference/necromancer-pet-ai-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check: assert target.read_text() == rendered
    else: target.write_text(rendered)
    print('NecroPet AI67:1598 complete source/native policy comparisons,8guards,3native markers;106installed artifacts unchanged;live actor/cast remain unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
