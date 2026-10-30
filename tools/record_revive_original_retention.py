#!/usr/bin/env python3
"""Bind original native actor retention and isolated patched runtime to actual world saves."""
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

STAGE = ROOT / '.local/class-skills/revive-original-retention'
PREDECESSOR = 'b04355c'


def sha(path):
    return digest(path.read_bytes())


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    check_snapshot(STAGE / 'pre-build-inputs.json', STAGE / 'stats-inputs.json')
    _, prior = frozen_receipt(PREDECESSOR, 'tools/reference/source-ai-eligibility-native-evidence.json')
    assert prior['verified']['passMarkers'] == 305
    artifacts = [Path(__file__).resolve()]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 309
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    pattern = r'\] ([A-Za-z0-9-]+): passed'
    before = Counter(re.findall(pattern, (ROOT / '.local/class-skills/revive-ai-eligibility/stats-native-checks.log').read_text()))
    after = Counter(re.findall(pattern, log))
    assert sum(before.values()) == 305 and not before - after
    assert after - before == Counter({'revive-original-retention-check':1,'revive-original-retention-result':1,'revive-original-retention-reload-check':2})
    assert log.count('Retained originals:9actual deaths/5native types/3difficulties preserve original serials,native fields,ability definitions and corpse loot;dead hidden internal actors,queued breath refusal,corpse/actor deletion cleanup,7atomic invalid restores and actual reloads pass;pre-death state,equipment,full ability timers,activation and learned Revive remain unfinished') == 3
    from prepare_revive_retention_native import inputs as native_inputs
    native = STAGE / 'native'
    manifest = json.loads((STAGE / 'native-pre-build-inputs.json').read_text())
    assert manifest == native_inputs(native)
    assert manifest['upstream'] == 'eff9de028136b11e544fd81375d1e0e710e22dd6' and len(manifest['patches']) == 66
    native_log = (STAGE / 'native-build.log').read_text()
    assert all(s in native_log for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        path = native / 'Distribution' / name
        assert sha(path) == inputs['artifacts'][name], name
        artifacts.append(path)
    for name, hash_ in manifest['patches'].items():
        assert sha(ROOT / name) == hash_, name
        artifacts.append(ROOT / name)
    saved = json.loads((STAGE / 'revive-original-retention-fixtures.json').read_text())
    assert len(saved) == 9 and len({r['Actor'] for r in saved}) == len({r['Corpse'] for r in saved}) == 9
    assert len({r['Type'] for r in saved}) == 5 and all(r['Loot'] for r in saved)
    artifacts += [STAGE/'native-pre-build-inputs.json', STAGE/'native-build.log',ROOT/'tools/prepare_revive_retention_native.py']
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
    assert fingerprints == [770, 21]
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
    diagnostic = STAGE/'diagnostics'
    assert 'parent worktree' in (diagnostic/'native-replay1/reason.txt').read_text()
    assert 'CS1729' in (diagnostic/'compile1/integration-build.log').read_text()
    artifacts += [diagnostic/'native-replay1/reason.txt',diagnostic/'native-replay1/native-build.log']
    artifacts += [p for p in (diagnostic/'compile1').iterdir() if p.is_file()]
    for name in ['pre-build-inputs.json', 'stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log', 'revive-original-retention-fixtures.json']:
        artifacts.append(STAGE / name)
    artifacts += [main_path, ROOT / 'tests/check_revive_original_retention.py', ROOT / 'tools/build_skill_roadmap.py', ROOT / 'tools/build_property_integration_backlog.py', ROOT / 'tools/reference/item-property-integration-backlog.json', ROOT / 'tools/reference/uo-monster-strength-classification-policy.json']
    result = dict(referenceVersion='3.3.93847', component='Actual original native NPC retention as dead internal actors until unrevived corpse loss', installed=False, handlerEnabled=False, retainedNativeActorImplemented=True, activeReviveActorImplemented=False, automaticRetentionEnabled=False, goalComplete=False, handlers=184, completedClasses=[], processMemoryRead=False,
        verified=dict(passMarkers=309, retainedMarkers=305, worldBoots=3, actualWorldReloads=2, persistedRetainedDeaths=9, cleanupRetainedDeaths=2, defaultDeathControl=1, nativeTypes=5, difficulties=3, originalSerialsRetained=True, nativeFieldsAndAbilityDefinitionsRetained=True, queuedDragonBreathRefused=True, corpseAndActorDeletionCleanup=True, atomicMalformedRestoreChecksPerBoot=7, nativePatchCount=66, isolatedNativeCleanBuild=True, preBuildAndNativeSourceDataMatch=True, pythonTests=71, unchangedNamedDefinitions=573, inputFingerprints=fingerprints, unchangedMainInstallationArtifacts=106, newFullClassRun=False),
        frozenPredecessor=dict(commit=PREDECESSOR, receipt='tools/reference/source-ai-eligibility-native-evidence.json'), inputInventory=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))},
        established=[
            'The native death hook can retain the actual NPC after corpse creation rather than deleting it. The retained actor remains registered under its original serial and concrete type, has Alive=false, is blessed/hidden/frozen/internal with AI stopped, and retains its native persistence. Original state is not reconstructed through a generic summon constructor.',
            'Nine actual deaths across five native types and three difficulties retain checked raw/damage/loyalty/control-slot fields, native ability definitions, original serials and all corpse loot through two real reloads. Source historical identity and the existing registry resolve those same original serials. A queued dragon breath delivers no damage after death. These comparisons cover post-death native state and ability definitions, not every dynamic ability timer or full pre-death state.',
            'Corpse deletion deletes its retained original. External actor deletion removes stale storage without deleting corpse loot. Seven malformed/unknown-version metadata cases per boot refuse atomically, and metadata roundtrips pass. The new persistence ID222 is independent of existing v0 corpse records. Unknown derived death overrides, owned/summoned actors and unmapped/unsupported species are refused.',
            'Automatic gameplay retention is gated by the unfinished learned Revive route. Native tests explicitly select the prepared capture hook and restore the default afterward; a default ordinary death still deletes its NPC. Retention is distinct from activation, ownership, source passives, stats, AI and replacement. Approved no-expiration Revive policy is unchanged.',
            'A separate Git-root archive replays all66 patches on pinned ModernUO eff9de028136b11e544fd81375d1e0e710e22dd6. Native source/patch inventories recorded before the clean build match afterward, and the three native binaries exactly match those used by the disposable server. Installed sources, binaries and saves are unchanged.',
            'The first native replay incorrectly discovered the parent worktree, skipped early patch paths and failed at0019; a subsequent shell command wrongly built that unpatched tree. That build is rejected and preserved. The corrected preparer initializes a separate Git root and requires a complete manifest before building. A fixture-only six-argument constructor compile error was separately preserved and corrected before clean content/integration rebuilds. No native comparison failed.'],
        remaining=[
            'Provide full pre-death state and original equipment handling, verify dynamic native special-ability/timer persistence, and compose actual activation with source start/release, AI permission, current passives/sunder/events and ownership/cap. The registered serial reservation applies to these retained originals; legacy ownerless historical corpses remain unsupported for full restoration.',
            'Native dynamic source-state production, other derived death paths, all owner/death/replacement/facet/logout/respec/save lifecycle, learned input/casting, modern executable equivalence and full-class acceptance remain unfinished. No active Revive actor or learned handler is enabled. No class complete; final effects/audio and talent/hotkey icons remain separate placeholders.'])
    output = ROOT / 'tools/reference/revive-original-retention-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Original retention:309 native/71 Python;9 actual NPCs,2 reloads,queued breath and cleanup pass;activation and learned Revive unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
