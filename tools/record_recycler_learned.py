#!/usr/bin/env python3
"""Bind learned recycler input, native recovery, original saves and regressions."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import subprocess

from record_npc_kill_procs import expected as previous_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/recycler-learned'
WIDE = ROOT / '.local/class-skills/recycler-learned-broader'
PARENT = '481dc98344d4883cddd129f396d0a39bf63ad6a9'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(path):
    return json.loads(path.read_text())


def expected(broader):
    result = previous_expected(broader)
    for name in ('recycler-learned', 'vine-cast-windup', 'creeper-casting',
                 'recycler-casting', 'recycler-world', 'recycler-recovery', 'recycler-body'):
        result[name + '-check'] += 1
        result[name + '-reload-check'] += 2
    for name in ('recycler-model-check', 'vine-cast-windup-live-check',
                 'vine-cast-windup-live-result', 'recycler-learned-live-result'):
        result[name] += 3
    for name in ('carrion-right', 'carrion-quick', 'solar-right', 'solar-quick'):
        result[name + '-check'] += 3
        result[name + '-result'] += 3
    return result


def build(check=False):
    born, wide, audit = (load(p) for p in (STAGE/'birth-inputs.json', WIDE/'birth-inputs.json', STAGE/'audit-inputs.json'))
    assert born['artifacts'] == wide['artifacts'] == audit['artifacts']
    assert born['copiedData'] == wide['copiedData'] == audit['copiedData']
    for path in (STAGE/'birth-inputs.json', WIDE/'birth-inputs.json', STAGE/'audit-inputs.json'):
        snapshot(STAGE/'pre-build-7.json', path)
        for source, digest in load(path)['sourceInputs'].items():
            assert sha(ROOT/source) == digest, source
    before = load(ROOT/'.local/class-skills/npc-kill-procs/pre-build-2.json')['inputs']
    now = load(STAGE/'pre-build-7.json')['inputs']
    content_inputs = load(STAGE/'pre-build-1.json')['inputs']
    assert content_inputs['projectInputs'] == now['projectInputs']
    assert content_inputs['copiedData'] == now['copiedData']
    assert {p:h for p,h in content_inputs['sourceInputs'].items() if not p.startswith('tests/')} == {p:h for p,h in now['sourceInputs'].items() if not p.startswith('tests/')}
    changed = lambda kind: {p for p in before[kind].keys() | now[kind].keys() if before[kind].get(p) != now[kind].get(p)}
    production = {'ClassCombat', 'DruidCombat', 'DruidVineCasting', 'DruidCreeperBody',
                  'DruidCreeperCasting', 'DruidRecyclerBody', 'DruidRecyclerCasting',
                  'DruidRecyclerRecovery', 'DruidRecyclerWorld'}
    fixtures = {'RecyclerCastingChecks', 'RecyclerVineChecks', 'RecyclerWorldChecks',
                'VineCastWindupChecks', 'RecyclerLearnedChecks'}
    assert changed('sourceInputs') == {f'src/Sanctuary.Content/{n}.cs' for n in production} | {f'tests/Sanctuary.Integration/{n}.cs' for n in fixtures}
    assert changed('copiedData') == {'Data/Sanctuary/Balance/class-skill-status.json'}
    assert before['projectInputs'] == now['projectInputs']
    for stage, broader, count in ((STAGE, False, 197), (WIDE, True, 413)):
        log = (stage/'birth-native-checks.log').read_text()
        assert markers(stage/'birth-native-checks.log') == expected(broader)
        assert sum(markers(stage/'birth-native-checks.log').values()) == count
        assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        assert log.count('Learned recycler vines:252approved Solar whole-point controls') == 1
        assert log.count('Unit events:1488complete source sequence controls/8routines') == 3
        assert log.count('NPC kill procs:actual stat196/function20/source-written carriers') == 1
        saved = load(stage/'recycler-learned-fixtures.json')
        assert saved['Generation'] == 2 and len(saved['Actors']) == 2
        assert len({row['Owner'] for row in saved['Actors']}) == len({row['Body'] for row in saved['Actors']}) == 2
        assert [row['Ability'] for row in saved['Actors']] == [176, 179]
        for row, model in zip(saved['Actors'], (0, 2)):
            effect = row['Recovery']
            assert effect['Owner'] == row['Owner'] and effect['Actor'] == row['Body']
            assert effect['Rank'] == 2 and effect['Model'] == model
            assert effect['Remaining'] == 47 and not effect['Triggered']
            assert row['FinalLife'] is not None and row['FinalMana'] is not None
            assert row['Life'] == 1111
            if model == 0:
                assert row['FinalLife'] > row['OwnerLife'] and row['FinalMana'] == row['Mana']
            else:
                assert row['FinalMana'] > row['Mana'] and row['FinalLife'] == row['OwnerLife']
        assert load(stage/'npc-kill-proc-fixtures.json')['Generation'] == 2
        assert load(stage/'unit-events-fixtures.json')['Generation'] == 2
    for name in ('content-build-1.log', 'integration-build-7.log'):
        assert all(s in (STAGE/name).read_text() for s in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for kind in ('Content', 'Integration'):
        assert sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    decisions = load(ROOT/'tools/reference/vine-sanctuary-decisions.json')
    assert decisions['approvedDeviation'] and decisions['referenceVersion'] == '3.3.93847'
    assert len(decisions['decisions']) == 2 and decisions['decisions'][1]['enumValue'] == 2
    fingerprints = {}
    for name in ('named-item-runtime-audit.json', 'first-loop-affix-audit.json'):
        current = load(STAGE/name)
        assert current == load(ROOT/'tools/reference'/name)
        baseline = json.loads(subprocess.check_output(['git', 'show', PARENT+':tools/reference/'+name], cwd=ROOT))
        omit = {'InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in omit} == {k:v for k,v in baseline.items() if k not in omit}
        assert current['ContentAssemblySha256'] == born['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path, digest in current['InputHashes'].items():
            assert sha(ROOT/path) == digest, path
        fingerprints[name] = len(current['InputHashes'])
    assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions']) == 573
    assert markers(STAGE/'audit-native-checks.log') == Counter({'named-gear-check':1, 'first-loop-catalogue-check':1})
    status = load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert not status['completedClasses']
    assert sum(r['implementation'] == 'handler-present' for r in status['skills']) == 196
    assert sum(r['characterClass'] == 'Druid' and r['implementation'] == 'handler-present' for r in status['skills']) == 28
    verification = load(STAGE/'verification-commands.json')
    assert len(verification) >= 13 and all(c['exitCode'] == 0 for c in verification)
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    installation = load(ROOT/'tools/reference/creeper-frame-installation-evidence.json')
    assert len(installation['installedArtifacts']) == 106
    for path, digest in installation['installedArtifacts'].items():
        assert sha(Path(installation['runtimeRoot'])/path) == digest, path
    protected = load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for kind, relative, count in (('saves','Saves',38), ('configuration','Configuration',20)):
        folder = Path(installation['runtimeRoot'])/'.local/server'/relative
        assert len(protected[kind]) == count
        assert {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()} == protected[kind]
    client = load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json')
    assert client['patches'] == 40
    for kind in ('patchHashes', 'sharedInputs'):
        for path, digest in client[kind].items():
            assert sha(ROOT/path) == digest, path
    files = [Path(__file__).resolve(), ROOT/'tests/check_recycler_learned.py', ROOT/'tools/build_skill_roadmap.py', ROOT/'tools/reference/vine-sanctuary-decisions.json']
    files += [ROOT/p for p in changed('sourceInputs')]
    files += [ROOT/'docs'/name for name in ('recycler-learned-checkpoint.md', 'class-skills-resume-checkpoint.md', 'class-skill-roadmap.md', 'class-skill-reuse-notes.md', 'skill-effects.md', 'skill-icons.md')]
    for stage in (STAGE, WIDE):
        files += [p for p in stage.iterdir() if p.is_file() and p.suffix in ('.json','.log') and p.name != 'execution-state.json']
    for name in ('failed-focused-1', 'failed-focused-2', 'failed-focused-3', 'failed-live-diagnostic-1', 'pre-network-review-focused-4', 'failed-broader-1', 'stale-audit-verification-1'):
        files += [p for p in (STAGE/name).rglob('*') if p.is_file()]
    passed = STAGE/'pre-network-review-focused-4'
    assert sum(markers(passed/'birth-native-checks.log').values()) == 197
    assert sha(passed/'integration/Sanctuary.Integration.dll') == load(passed/'birth-inputs.json')['artifacts']['Assemblies/Sanctuary.Integration.dll']
    failed = STAGE/'failed-broader-1'
    assert 'Sequence contains more than one matching element' in (failed/'birth-native-checks.log').read_text()
    assert sha(failed/'integration/Sanctuary.Integration.dll') == load(failed/'birth-inputs.json')['artifacts']['Assemblies/Sanctuary.Integration.dll']
    receipt = dict(referenceVersion='3.3.93847', parent=PARENT, installed=False,
        handlers=196, druidHandlers=28, completedClasses=[], learned=['druid.carrionvine','druid.solarcreeper'],
        artifacts=born['artifacts'], copiedData=born['copiedData'], contentBuild=1, integrationBuild=7,
        focusedRun=5, broaderRun=2, focusedMarkers=197, broaderMarkers=413,
        bootsPerCohort=3, actualReloadsPerCohort=2, savesPerCohort=3,
        approvedSolarControls=252, originalRecyclerActorsPerCohort=2, originalCapturedRank=2,
        naturalLearnedCastFeedRecoveryPerCohort=6, actualInputCastsPerCohort=12,
        pythonTests=71, namedDecisionsUnchanged=573, auditFingerprints=fingerprints,
        protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        diagnostics=['Obsolete disabled-handler assertions corrected. The Command fixture needed a trained Barbarian supplier rather than a Druid with class-scoped rank lookup; strict rank2 assertion retained.',
            'Passing focused4 preceded broader1 failure from an empty connection left by the existing windup fixture. Immediate native test cleanup closes that connection. Failed and passing input/source/binary cohorts are archived; final tests use Content1/Integration7 and snapshot7. Build6 only had the intentionally used native test-cleanup deprecation warning; build7 locally documents and suppresses it.',
            'Initial Python/readiness checks ran before the fresh native audits were copied to the reference location, so freshness validation rejected the old class-status hash. Archived those logs and old audits; copied the already-passing candidate audits and reran only the two failed checks. No source or test assertion changed.'],
        limits=['Always-vulnerable vines and Solar table/whole-point recovery are approved Sanctuary adaptations, not current executable parity claims. Historical recovery profiles0/1 retain their interpretation; learned Solar uses profile2.',
            'Poison Creeper remains disabled for damage/capture/sunder transport; Arctic Blast remains missing. No class is complete.',
            'Current room/path/vision ordering, animation/global scheduling and extreme inherited skill caps remain uncertain. Source path type1/AStar fallback is explicitly unsupported; learned scenes establish accessible-ground feeding, not complete obstacle navigation. Existing source geometry projections remain explicit.',
            'No deployment or installed-save/client/native/protocol change. Separate final effects/audio and talent/hotkey assets remain placeholders.'],
        files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
    out = ROOT/'tools/reference/recycler-learned-native-evidence.json'
    encoded = json.dumps(receipt, indent=2)+'\n'
    if check:
        assert out.read_text() == encoded, 'Learned recycler receipt stale'
    else:
        out.write_text(encoded)
    print('Learned recycler vines:197focused/413broader markers;actual input/feed/recovery and original actors through2reloads;71Python/audits/protection pass;196handlers,Druid28/30.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
