#!/usr/bin/env python3
"""Bind NPC item/Blood wound events and combined source regeneration."""
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
STAGE = ROOT / '.local/class-skills/npc-open-wounds'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {name: inventory.validate_inventory(STAGE / f'{name}-inputs.json') for name in ('birth', 'audit')}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / f'{name}-inputs.json')
        assert value['artifacts'] == inputs['birth']['artifacts']
        assert value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/open-wounds-state-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed = {'src/Sanctuary.Content/' + name + '.cs' for name in (
        'IronGolemContact', 'NpcCombatEvents', 'PoisonStates', 'PreparedMonsterContact', 'TimedEffects')} | {
        'tests/Sanctuary.Integration/NpcEquipmentRegenerationChecks.cs'}
    added = {'src/Sanctuary.Content/NpcOpenWounds.cs', 'tests/Sanctuary.Integration/NpcOpenWoundsChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    baseline = ROOT / '.local/class-skills/open-wounds-state/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    expected['npc-open-wounds-check'] += 1
    expected['npc-open-wounds-reload-check'] += 2
    expected['npc-open-wounds-live-result'] += 3
    assert sum(expected.values()) == 313 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('NPC wounds:2160complete regen source comparisons;') == 1
    assert log.count('actual item60+skill100single subscription') == 1
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ('content', 'integration'):
        build_log = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in build_log for s in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for name, count in (('named-item-runtime-audit.json', 880), ('first-loop-affix-audit.json', 21)):
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert len(audit['InputHashes']) == count
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for source, digest in audit['InputHashes'].items():
            assert sha(ROOT / source) == digest
        prior_audit = json.loads(subprocess.check_output(['git', 'show', '6fc3c68:tools/reference/' + name], cwd=ROOT))
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
    # Optional JSON clock extends the existing record; no mobile fields change.
    path = 'src/Sanctuary.Content/TimedEffects.cs'
    original = subprocess.check_output(['git', 'show', '6fc3c68:' + path], cwd=ROOT, text=True)
    pattern = r'public sealed record TimedEffectState\([\s\S]*?\);'
    declaration = re.search(pattern, original).group(0)
    expected_declaration = declaration.replace('int? SlowAttackPercent=null);',
        'int? SlowAttackPercent=null,\n    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] SourceOpenWoundsClock OpenWounds=null);')
    assert expected_declaration != declaration and re.search(pattern, (ROOT / path).read_text()).group(0) == expected_declaration
    assert 'writer.Write(4)' in original and 'writer.Write(4)' in (ROOT / path).read_text()
    saved = json.loads((STAGE / 'npc-open-wounds-fixtures.json').read_text())
    assert saved['Generation'] == 2 and saved['State']['BleedSource'] == saved['Source']
    clock = saved['State']['OpenWounds']
    assert clock == dict(Rate256=41, ElapsedMs=39, CreditOwner=saved['Owner'])
    assert saved['State']['BleedMs'] == 7961 and saved['State']['BleedFraction'] == 0
    proof_path = ROOT / 'tools/reference/open-wounds-regeneration-source-evidence.json'
    proof = json.loads(proof_path.read_text())
    assert proof['contexts'] == 2160
    for section in ('inputs', 'artifacts'):
        for path, digest in proof[section].items():
            assert sha(ROOT / path) == digest, path
    docs = ('npc-open-wounds-checkpoint', 'pet-adjustment-review', 'class-skill-reuse-notes',
            'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons')
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_npc_open_wounds.py', proof_path,
             ROOT / 'tools/probe_open_wounds_regeneration.py', ROOT / 'tools/probe_open_wounds.py',
             ROOT / 'tools/review_blood_pet_events.py', ROOT / 'tools/reference/blood-pet-event-reuse-review.json',
             ROOT / 'tools/reference/open-wounds-source-evidence.json',
             *[ROOT / p for p in changed | added], *[ROOT / f'docs/{name}.md' for name in docs]]
    names = [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log',
             f'run-{run}.log', f'audit-run-{audit_run}.log', 'birth-inputs.json', 'birth-native-checks.log',
             'audit-inputs.json', 'audit-native-checks.log', 'python-tests.log', 'named-readiness.log',
             'catalogue-check.log', 'npc-open-wounds-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json',
             'pre-build-1.json', 'content-build-1.log', 'integration-build-1.log', 'run-1.log', 'audit-run-1.log',
             'attempt-1-birth-inputs.json', 'attempt-1-birth-native-checks.log', 'attempt-1-audit-inputs.json',
             'attempt-1-audit-native-checks.log', 'attempt-1-named-item-runtime-audit.json', 'attempt-1-first-loop-affix-audit.json']
    files += [STAGE / name for name in names]
    result = dict(referenceVersion='3.3.93847', component='NPC registered Open Wounds/Blood hard10 and exact combined regeneration clock',
        installed=False, goalComplete=False, completedClasses=[], bloodUpgradePackageComplete=False, npcWoundEventEnabled=True,
        build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=313, retainedSharedWoundBloodClayWarriorMagePlayerPetSentryMarkers=307,
            boots=3, actualReloads=2, saves=3, naturalLearnedBloodWoundDeathSessions=3,
            fullOlderRegenerationContexts=2160, nativeSignedRegenerationComparisons=2160,
            actualPoisonWoundReplenishPreventHealControls=12, auditMarkers=4, pythonTests=71, namedDefinitions=573,
            auditFingerprints=[880, 21], unchangedInstalledArtifacts=106, handlers=186, necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / f'{n}-inputs.json').relative_to(ROOT)),
            sha256=sha(STAGE / f'{n}-inputs.json')) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)},
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'NpcCombatEvents now validates/consumes stat135/function15in the real pre-health source melee-damage phase. Blood potential subscription observes live hard10/respec during windup;item chance and inherent100aggregate into one draw,including chances above100. Other unsupported registrations still refuse before result/damage RNG.',
            'Target effects settle before prepared hit entry. If overdue damage removes the recipient,the captured packet cancels without wound RNG/HP replay. The callback itself creates/refreshes state without settling or HP subtraction. Its known NPC factory projection ignores curse/noAura/walk-only gates;unmapped rows warn/skip instead of inventing eligibility.',
            'Optional OpenWounds JSON clock preserves real creator,immutable integer FP rate,0..39phase and durable owner credit. Old absent clocks retain the existing generic bleed representation. Exact wounds share poison HP-regeneration drain/fractional storage and one signed frame of item replenish minus poison minus wounds,with natural regeneration masked. Poison wins combined death credit while active.',
            'Full unchanged older monster regeneration executes2160signed-rate/town/prevent-heal/credit/death-delay contexts under sanitizers;native helper comparisons match. Real live hard9+soft1 refusal,windup hard10unlock/respec,miss/block/successful-zero/full-absorption,one event draw and actual Goreshovel60plus skill100single subscription pass.',
            'Twelve actual item replenish/poison/wound/prevent-heal controls plus a fractional survival edge and poison-priority lethal edge pass. Source39/40msFP tick avoids native Damage/recovery. A saved39msclock survives2reloads after creator removal;controlled copies retain durable owner kills without Damage calls. Three natural learned Blood contacts proc wounds on physical immunity and continue to owner-credited death after summon deletion.',
            'All307predecessor scopes,71Python,4audits,573named/eight-class240and clean builds pass. Runtime data,audit decisions,kernel and106installed hashes remain unchanged;880/21fingerprints refresh. Timed persistence remains version4with an optional omitted-null JSON field;no mobile layout changes/deployment.'],
        remaining=[
            'Blood hard20five-percent LifeTap5still requires the complete proc target-cast/context/area/subscriber-order integration. It is not marked complete by the hard10event. Other NPC conditional item properties/skill procs remain explicitly unsupported.',
            'The exact clock is used for new NPC registered wounds. Existing generic/player bleed states retain their legacy receiver and first-application identity on refresh;full rollout/player-replenish/protection/PvP and mixed old-state transport need separate acceptance.',
            'Unmapped/native-fallback recipients,unsupported material replenish,uninterruptible changes/death-delay/event/client/global frame equivalence and non-NPC target delivery retain existing gaps. Old callback/full-regeneration service probes are not modern executable/full world proof. Poison life-floor/death/credit rules are reused with their prior limits.',
            'Optional JSON/default and candidate saved clocks do not establish installed old-DLL upgrade or concurrent/unheld save acceptance. Held observation clocks are distinct from natural live sessions. Other pets,Mage mapping,barriers,parking,Druid/classes and final effects/icons stay unfinished;no class completion or new player binding.'])
    output = ROOT / 'tools/reference/npc-open-wounds-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('NPC Open Wounds:313markers/3boots/2reloads,3natural learned Blood kills;2160source/native regen comparisons/12signed item controls;installed unchanged. Blood Life Tap remains pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
