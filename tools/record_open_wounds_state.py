#!/usr/bin/env python3
"""Bind complete wound-source proof and shared refresh/arithmetic host fixes."""
import argparse
import json
import re
import subprocess
from collections import Counter
from pathlib import Path

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/open-wounds-state'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {name: inventory.validate_inventory(STAGE / f'{name}-inputs.json') for name in ('birth', 'audit')}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / f'{name}-inputs.json')
        assert value['artifacts'] == inputs['birth']['artifacts']
        assert value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/blood-golem-damage-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed = {'src/Sanctuary.Content/NamedCombat.cs', 'src/Sanctuary.Content/TimedEffects.cs'}
    added = {'tests/Sanctuary.Integration/OpenWoundsStateChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/blood-damage/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    expected['open-wounds-state-check'] += 1
    expected['open-wounds-state-reload-check'] += 2
    expected['open-wounds-state-live-result'] += 3
    assert sum(expected.values()) == 307 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('Open Wounds source: 6840 native new-state arithmetic comparisons') == 1
    assert log.count('Open Wounds state:first creator/rate/fraction retained') == 1
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ('content', 'integration'):
        build_log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in build_log for s in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for name, count in (('named-item-runtime-audit.json', 879), ('first-loop-affix-audit.json', 21)):
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert len(audit['InputHashes']) == count
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for source, digest in audit['InputHashes'].items():
            assert sha(ROOT / source) == digest
        prior_audit = json.loads(subprocess.check_output(['git', 'show', 'a181c0f:tools/reference/' + name], cwd=ROOT))
        strip = lambda x: {k: v for k, v in x.items() if k not in ('ContentAssemblyPath', 'ContentAssemblySha256', 'InputHashes')}
        assert strip(audit) == strip(prior_audit)
    assert markers(STAGE / 'audit-native-checks.log') == Counter({
        'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    log = (STAGE / 'audit-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down') == 1
    log = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in log and '\nOK\n' in log
    assert '573 definitions accounted for' in (STAGE / 'named-readiness.log').read_text()
    assert 'Eight classes, 240 source skills' in (STAGE / 'catalogue-check.log').read_text()
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 186 and sum(x['characterClass'] == 'Necromancer' for x in handlers) == 30
    assert not status['completedClasses']
    # Existing timed-effect persistence fields/shape remain unchanged.
    path = 'src/Sanctuary.Content/TimedEffects.cs'
    original = subprocess.check_output(['git', 'show', 'a181c0f:' + path], cwd=ROOT, text=True)
    pattern = r'public sealed record TimedEffectState\([\s\S]*?\);'
    declarations = re.findall(pattern, original)
    assert declarations and declarations == re.findall(pattern, (ROOT / path).read_text())
    saved = json.loads((STAGE / 'open-wounds-state-fixtures.json').read_text())
    assert saved['Generation'] == 2 and saved['State']['BleedSource'] == saved['Source']
    assert saved['State']['BleedMs'] == 8000 and saved['State']['BleedRate'] == 32 / 8000
    assert abs(saved['State']['BleedFraction'] - .6) < 1e-9
    proof_path = ROOT / 'tools/reference/open-wounds-source-evidence.json'
    proof = json.loads(proof_path.read_text())
    assert proof['callbackContexts'] == 50400 and proof['factoryGateContexts'] == 20
    for section in ('inputs', 'artifacts'):
        for path, digest in proof[section].items():
            assert sha(ROOT / path) == digest, path
    docs = ('open-wounds-state-checkpoint', 'pet-adjustment-review', 'class-skill-reuse-notes',
            'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons')
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_open_wounds_state.py', proof_path,
             ROOT / 'tools/probe_open_wounds.py', ROOT / 'tools/review_blood_pet_events.py',
             ROOT / 'tools/reference/blood-pet-event-reuse-review.json',
             *[ROOT / p for p in changed | added], *[ROOT / f'docs/{name}.md' for name in docs]]
    names = [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log',
             f'run-{run}.log', f'audit-run-{audit_run}.log', 'birth-inputs.json', 'birth-native-checks.log',
             'audit-inputs.json', 'audit-native-checks.log', 'python-tests.log', 'named-readiness.log',
             'catalogue-check.log', 'open-wounds-state-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']
    files += [STAGE / name for name in names]
    result = dict(referenceVersion='3.3.93847', component='Shared Open Wounds first-application refresh and uncapped source-level arithmetic',
        installed=False, goalComplete=False, completedClasses=[], bloodUpgradePackageComplete=False, npcWoundEventEnabled=False,
        build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=307, retainedBloodClayWarriorMagePlayerPetSentryMarkers=301,
            boots=3, actualReloads=2, saves=3, naturalNamedItemRefreshExpirySessions=3,
            completeOlderCallbackFactoryContexts=50400, completeOlderFactoryGateContexts=20,
            nativeArithmeticSourceComparisons=6840, auditMarkers=4, pythonTests=71, namedDefinitions=573,
            auditFingerprints=[879, 21], unchangedInstalledArtifacts=106, handlers=186, necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / f'{n}-inputs.json').relative_to(ROOT)),
            sha256=sha(STAGE / f'{n}-inputs.json')) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)},
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'Complete older wound callback/math/shared factory execute50400contexts and20gates under ASan/UBSan with recorded retained row/unit/stat/event/RNG/storage services. Chance success and state-factory acceptance are distinct. Positive chance draws before factory eligibility;100percent still draws. Noncurse state62ignores curse resistance/Attract.',
            'Native formula agrees with6840new-state source cases,including level127and integer player/ranged/monster-flag reductions. Removed the old upper99clamp while retaining minimum1and checked overflow. Public target classification remains the existing host adapter,not a new full species/PvP proof.',
            'Shared bleed settles elapsed work,then active fixed skill0/rank1 refresh retains its first creator,rate and fractional accumulator and resets only8000msremaining. Expired/new applications acquire the new source/rate. Ordinary named-item AfterStrike uses that same state;no NPC event route is enabled.',
            'Deterministic fractional damage/refresh/expiry/regen release and invalid inputs pass. Saved observation clock/state/source/rate/fraction survives two reloads;controlled refreshes keep the first application. Three connected actual named-item sessions naturally tick,refresh and expire after the renewed8seconds even against physical immunity. Existing bleed scheduler/whole-HP transport remains reused.',
            'All301predecessor scopes,71Python,4audits,573named/eight-class240and clean builds pass. Data,audit decisions,kernel and106installed hashes remain unchanged;879/21fingerprints refresh and timed-effect record shape stays unchanged.'],
        remaining=[
            'Blood hard10OpenWounds still requires NPC stat135/function15registration,live hard-point aggregation and source RNG/eligibility/dispatch. Blood hard20five-percent LifeTap5still needs fixed real-pet-source area curse and complete proc target-cast/subscriber ordering. Neither is marked implemented by these shared prerequisites.',
            'Generic bleed currently settles pending effects and uses whole-HP native Damage. Negative HP-regeneration/source-frame equivalence and safe overdue settlement during pre-health NPC events need composition before enabling that callback;do not insert an unsafe reentrant call or assume a post-HP hook is equivalent.',
            'Complete older source under explicit services is not modern executable/full state-list/world/regen/event/PvP/client proof. Actual target classification,source deletion/credit,cure/lethal cases,old-DLL migration and concurrent/unheld save acceptance retain their limits. Saved observation clock is intentionally held;normal lifetime is proven by the separate live sessions.',
            'Other approved pets,Mage basic spells/triples/Mastery mapping,barriers,safe-area parking,Druid upgrades and all remaining class mechanics/assets stay unfinished. No deployment,class completion or player binding. Existing bleed/contact cues and automatic effects remain placeholders;icons are separate.'])
    output = ROOT / 'tools/reference/open-wounds-state-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Open Wounds shared state:307markers/3boots/2reloads,3natural expiry sessions;50400source/20gates/6840native comparisons;installed unchanged. NPC/Blood event integration remains pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
