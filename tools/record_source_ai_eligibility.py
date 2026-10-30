#!/usr/bin/env python3
"""Bind source AI-permission predicates and reused current species to the tested candidate."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess

import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_snapshot
from probe_warlock_runtime import ROOT, digest

STAGE = ROOT / '.local/class-skills/revive-ai-eligibility'
PREDECESSOR = '915f419'


def sha(path):
    return digest(path.read_bytes())


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    check_snapshot(STAGE / 'pre-build-inputs.json', STAGE / 'stats-inputs.json')
    _, prior = frozen_receipt(PREDECESSOR, 'tools/reference/revive-creature-snapshot-native-evidence.json')
    assert prior['verified']['passMarkers'] == 302
    artifacts = [Path(__file__).resolve()]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 305
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    pattern = r'\] ([A-Za-z0-9-]+): passed'
    before = Counter(re.findall(pattern, (ROOT / '.local/class-skills/revive-creature-snapshots/stats-native-checks.log').read_text()))
    after = Counter(re.findall(pattern, log))
    assert sum(before.values()) == 302 and not before - after
    assert after - before == Counter({'source-ai-eligibility-check': 1, 'source-ai-eligibility-reload-check': 2})
    assert log.count('Source AI eligibility:61440complete older contexts/five predicates,96128current Revive contexts,751reused species rows and1152saved-corpse contexts pass;dynamic source state remains explicit input;native producer,actor restoration and lifecycle remain unfinished') == 3
    source_path = ROOT / 'tools/reference/source-ai-eligibility-evidence.json'
    source = json.loads(source_path.read_text())
    assert source['verified'] == dict(completeRoutines=5, syntheticContexts=61440, predicatesPerContext=5, currentSpecies=751, currentRevivedContexts=96128, currentCatalogFieldsChecked=3004, sanitizers='ASan/UBSan exit0,empty stderr')
    assert not source['processMemoryRead'] and not source['handlerEnabled'] and not source['modernCodeEquivalenceClaimed']
    artifacts.append(source_path)
    for name, hash_ in source['artifacts'].items():
        assert sha(ROOT / name) == hash_, name
        artifacts.append(ROOT / name)
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name_, hash_ in audit['InputHashes'].items():
            assert sha(ROOT / name_) == hash_, name_
        audits.append(audit)
        artifacts += [path, ROOT / 'tools/reference' / name]
    old = json.loads(subprocess.check_output(['git', 'show', PREDECESSOR + ':tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert audits[0]['Definitions'] == old['Definitions'] and len(audits[0]['Definitions']) == 573
    fingerprints = [len(a['InputHashes']) for a in audits]
    assert fingerprints == [768, 21]
    tests = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
    main_path = ROOT / 'tools/reference/creeper-frame-installation-evidence.json'
    main = json.loads(main_path.read_text())
    assert len(main['installedArtifacts']) == 106
    for name, hash_ in main['installedArtifacts'].items():
        assert sha(Path(main['runtimeRoot']) / name) == hash_, name
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 184
    # This increment changes source and persistence, not balance, assets or runtime data.
    for path in (ROOT / 'data/Sanctuary').rglob('*'):
        if path.is_file():
            assert path.read_bytes() == subprocess.check_output(['git', 'show', PREDECESSOR + ':' + str(path.relative_to(ROOT))], cwd=ROOT), path
    diagnostic = STAGE / 'diagnostics'
    assert 'ValueError' in (diagnostic / 'source1/source-run.log').read_text()
    assert 'nonmonster and missing monster-data' in (diagnostic / 'source2/reason.txt').read_text()
    artifacts += [p for p in diagnostic.rglob('*') if p.is_file()]
    for name in ['pre-build-inputs.json', 'stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log', 'revive-creature-snapshot-fixtures.json', 'source-run.log', 'source-recheck.log']:
        artifacts.append(STAGE / name)
    artifacts += [main_path, ROOT / 'tests/check_source_ai_eligibility.py', ROOT / 'tools/build_skill_roadmap.py', ROOT / 'tools/build_property_integration_backlog.py', ROOT / 'tools/reference/item-property-integration-backlog.json', ROOT / 'tools/reference/uo-monster-strength-classification-policy.json']
    result = dict(referenceVersion='3.3.93847', component='Revived-AI permission using current existing Conversion species rules and explicit dynamic state', installed=False, handlerEnabled=False, reviveActorImplemented=False, goalComplete=False, handlers=184, completedClasses=[], processMemoryRead=False,
        verified=dict(passMarkers=305, retainedMarkers=302, worldBoots=3, actualWorldReloads=2, syntheticContextsPerBoot=61440, predicatesPerContext=5, currentRevivedContextsPerBoot=96128, currentCatalogSpecies=751, currentTextFieldsChecked=3004, savedCorpseContextsPerBoot=1152, completeOlderRoutines=5, sourceSanitizersClean=True, preBuildAndNativeSourceDataMatch=True, pythonTests=71, unchangedNamedDefinitions=573, inputFingerprints=fingerprints, unchangedMainInstallationArtifacts=106, newFullClassRun=False),
        frozenPredecessor=dict(commit=PREDECESSOR, receipt='tools/reference/revive-creature-snapshot-native-evidence.json'), inputInventory=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))},
        established=[
            'Complete unchanged older skill-wrapper, AIUTIL, AITHINK, UNITS and unique-flag helper bodies execute under ASan/UBSan with exit0 and empty stderr. Native results match61440 synthetic contexts across five predicate entry points and96128 current-species Revived-AI contexts per boot.',
            'Current751species reuse the existing Conversion catalog and existing source-name/ID index; no duplicate balance table is added. Base-ID, walking, boss and switchAI fields match3004 owned current-text fields. Base-ID imp attachment, null/kind/class/state boundaries, attackable-or-dead, uninterruptible and unique/superunique gates retain their distinct behavior. Missing monster data cannot supply unique flags.',
            'Nine saved original corpse fixtures use their historical species with explicit dynamic status across1152 comparisons per boot, including both actual reloads. Source corpse identity remains distinct from native Item identity. Dynamic source flags are not inferred from body, hue, native elite presentation or age.',
            'The initial source receipt path conversion followed a symlink outside the worktree and failed after successful source execution. Original sources/logs are preserved. Review then exposed the unique-service boundary for nonmonsters/missing monster data; the expanded model executes the complete original helper and retains that earlier probe under diagnostics/source2. Neither diagnostic attempt was accepted as native verification.',
            'All302 predecessor markers, all71 Python tests, clean builds, matching source/data inventories,573 named decisions and106 installed artifacts are preserved. Runtime balance and separate asset ledgers are unchanged. No new D2R process access occurred.'],
        remaining=[
            'Actual native producer/persistence of dynamic source AI state is unfinished. The corpse helper requires explicit status inputs and does not itself certify eligibility for an actual learned cast. Complete native actor restoration, original special abilities, passive/sunder/event transfer, historical GUID reservation and full owner/death/replacement/facet/logout/respec/save lifecycle remain unfinished.',
            'The approved Revive lifetime remains unlimited until death/Necromancer death/replacement. No learned handler, native actor, modern executable equivalence, new full-class run or installation is claimed. No class complete; effects/audio and talent/hotkey icons remain separate placeholders.'])
    output = ROOT / 'tools/reference/source-ai-eligibility-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Revive AI:305 native/71 Python;61440 source,96128 current species and1152 corpse contexts per boot;native producer/actor unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
