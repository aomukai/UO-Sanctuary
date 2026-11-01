#!/usr/bin/env python3
"""Bind Blood's fixed NPC Life Tap event/casting and affected curse regressions."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/blood-life-tap'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / f'{n}-inputs.json') for n in ('birth', 'audit')}
    for n, v in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / f'{n}-inputs.json')
        assert v['artifacts'] == inputs['birth']['artifacts'] and v['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/npc-open-wounds-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed = {'src/Sanctuary.Content/' + n + '.cs' for n in ('NecromancerCurseCasts', 'NecromancerEffects', 'NpcCombatEvents')}
    changed.add('tests/Sanctuary.Integration/NpcOpenWoundsChecks.cs')
    added = {'src/Sanctuary.Content/NpcHitSkills.cs', 'tests/Sanctuary.Integration/BloodLifeTapChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for n in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert sha(STAGE / 'native/Distribution' / n) == inputs['birth']['artifacts'][n] == old['artifacts'][n]
    # No serialized records/mobile fields change: Effects only widens the
    # presentation's caster type; the existing store/event/validation remains.
    path = 'src/Sanctuary.Content/NecromancerEffects.cs'
    original = subprocess.check_output(['git', 'show', '5af5841:' + path], cwd=ROOT, text=True)
    assert original.replace('CurseApplied(PlayerMobile owner,Mobile target)', 'CurseApplied(Mobile owner,Mobile target)') == (ROOT / path).read_text()
    baseline = ROOT / '.local/class-skills/npc-open-wounds/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    families = ('blood-life-tap', 'life-tap-cast', 'iron-maiden', 'lower-resist', 'decrepify', 'weaken',
                'amplify-damage', 'dim-vision-cast', 'terror-cast', 'confuse-cast')
    for name in families:
        expected[name + '-check'] += 1
        expected[name + '-reload-check'] += 2
    expected['blood-life-tap-live-result'] += 3
    for name in ('life-tap-cast-right', 'life-tap-cast-quick'):
        expected[name + '-check'] += 3
        expected[name + '-result'] += 3
    assert sum(expected.values()) == 358 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('Blood Life Tap:720complete proc comparisons;') == 1
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ('content', 'integration'):
        text = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in text for s in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for n, count in (('named-item-runtime-audit.json', 881), ('first-loop-affix-audit.json', 21)):
        audit = json.loads((STAGE / n).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / n).read_text())
        assert len(audit['InputHashes']) == count and audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for p, digest in audit['InputHashes'].items():
            assert sha(ROOT / p) == digest
        previous = json.loads(subprocess.check_output(['git', 'show', '5af5841:tools/reference/' + n], cwd=ROOT))
        strip = lambda d: {k: v for k, v in d.items() if k not in ('ContentAssemblyPath', 'ContentAssemblySha256', 'InputHashes')}
        assert strip(audit) == strip(previous)
    assert markers(STAGE / 'audit-native-checks.log') == Counter({
        'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    assert ': failed' not in (STAGE / 'audit-native-checks.log').read_text()
    text = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in text and '\nOK\n' in text
    assert '573 definitions accounted for' in (STAGE / 'named-readiness.log').read_text()
    assert 'Eight classes, 240 source skills' in (STAGE / 'catalogue-check.log').read_text()
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for p, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / p) == digest
    saved = json.loads((STAGE / 'blood-life-tap-fixtures.json').read_text())
    assert saved['Generation'] == 2 and saved['Curse']['Rank'] == 5 and saved['Curse']['Source'] == saved['Source']
    proof_path = ROOT / 'tools/reference/npc-hit-skill-source-evidence.json'
    proof = json.loads(proof_path.read_text())
    assert proof['contexts'] == 9360 and proof['notifiedContexts'] == 1088
    for section in ('inputs', 'artifacts'):
        for p, digest in proof[section].items():
            assert sha(ROOT / p) == digest
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [s for s in status['skills'] if s['implementation'] == 'handler-present']
    assert len(handlers) == 186 and sum(s['characterClass'] == 'Necromancer' for s in handlers) == 30 and not status['completedClasses']
    docs = ('blood-life-tap-checkpoint', 'pet-adjustment-review', 'class-skill-reuse-notes',
            'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons')
    files = [parent, baseline, Path(__file__).resolve(), proof_path, ROOT / 'tools/probe_npc_hit_skill.py',
             ROOT / 'tools/review_blood_pet_events.py', ROOT / 'tools/reference/blood-pet-event-reuse-review.json',
             ROOT / 'tools/reference/open-wounds-source-evidence.json', ROOT / 'tests/check_blood_life_tap.py',
             *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs]]
    names = ('birth-inputs.json', 'birth-native-checks.log', 'audit-inputs.json', 'audit-native-checks.log',
             'blood-life-tap-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json',
             'python-tests.log', 'named-readiness.log', 'catalogue-check.log')
    files += [STAGE / n for n in names]
    files += [p for p in STAGE.iterdir() if p.is_file() and (p.name.startswith(('pre-build-', 'content-build-', 'integration-build-', 'run-', 'audit-run-', 'attempt-1-')))]
    result = dict(referenceVersion='3.3.93847', component='Approved Blood hard20 five-percent Life Tap5 in registered source event/shared curse area',
        installed=False, goalComplete=False, completedClasses=[], bloodUpgradePackageComplete=False,
        learnedBloodDamageAndUnlocksImplemented=True, arbitraryItemProcWriterEnabled=False,
        build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=358, retainedNpcWoundPetPlayerSentryMarkers=313, boots=3, actualReloads=2, saves=3,
            affectedPlayerCurseFamilies=9, playerLifeTapInputSessions=6, naturalBloodProcSourceRemovalSessions=3,
            fullProcRoutingContexts=9360, nativeProcChanceComparisons=720, auditMarkers=4, pythonTests=71,
            namedDefinitions=573, auditFingerprints=[881, 21], unchangedInstalledArtifacts=106, handlers=186, necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / f'{n}-inputs.json').relative_to(ROOT)), sha256=sha(STAGE / f'{n}-inputs.json')) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)},
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'Blood potential subscription reads live hard20at the source damage event;soft points do not unlock. Hitflag0x20and positive chance precede one actor-stream draw. The explicit inherent slot follows Open Wounds;miss/block skip,successful zero/full absorption still attempt,uninterruptible cast fails after drawing.',
            'NPC LifeTap uses the real summon caster/origin,owner allegiance,fixed rank5and shared area/eligibility/duration/replacement/presentation. It reads no player LifeTap rank,synergy,gear/mana/cast action and preserves the existing attack controller. Rank5radius8subtiles and640frames pass boundary checks.',
            'Proc runs before defender LifeTap healing and HP clipping;the first successful and lethal swing heals50percent of pre-clip mitigated physical. Stronger/Attract reject,equal rank retains creator while refreshing,weaker replaces. Direct native killer remains the pet with owner loot credit.',
            'Saved curse rank/creator/expiry survive2reloads after caster deletion and still heal through the defender callback. Three connected learned-world attacks proc on successful physical immunity and preserve curse/healing after source deletion. Six player LifeTap input sessions and all9affected curse families pass.',
            'Complete unchanged older proc20/target/position/item-effect helpers pass9360contexts under sanitizers,1088notifications;720native chance controls match. Cast handler/world/path/formula/notification services remain explicit,and no complete modern-engine claim is made.',
            'Both clean builds,358markers retaining313scopes,4audits,71Python,573named/eight-class240pass. Audit decisions/data/kernel/106installed hashes unchanged with881/21fingerprints. No serialized record/mobile field change,deployment or class completion.'],
        remaining=[
            'Original item property writer11,arbitrary proc skills/parameter layers and dynamic source registration priority remain unsupported. The approved fixed inherent wound-then-tap order is tested;older LIFO registration and modern global-seed/event equivalence are not established by this adapter.',
            'Native-fallback/original plain legacy controllers,PvP/unmapped recipients,full curse state-list/geometry/protection/uninterruptible/client equivalence retain prior limits. The item helper no-prior-target temporary identity difference is documented,not silently copied into native Combatant state.',
            'Held curse snapshots do not prove installed old-DLL upgrades or concurrent/unheld saves. No change to generic/player legacy wound rollout. Final Blood/wound/curse effects/audio and talent/hotkey icons remain separate;no extra player binding.',
            'Mage basic spell/Mastery mapping,Iron/Fire upgrades,barriers,parking,Druid and remaining classes still require implementation/acceptance. No class is complete.'])
    output = ROOT / 'tools/reference/blood-life-tap-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Blood Life Tap:358markers/3boots/2reloads;9affected curses,6player inputs,3natural NPC proc/source-removal sessions;installed unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
