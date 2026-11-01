#!/usr/bin/env python3
"""Bind Iron's live hard10 direct Extra Strong and unchanged wild composition."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/iron-golem-strong'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / f'{n}-inputs.json') for n in ('birth', 'audit')}
    for n, v in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / f'{n}-inputs.json')
        assert v['artifacts'] == inputs['birth']['artifacts'] and v['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/iron-golem-damage-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed = {'src/Sanctuary.Content/' + n + '.cs' for n in ('GolemPetDamage', 'EncounterProfiles', 'EncounterCreature', 'NpcEquipmentDamage', 'ClassStats')}
    changed.add('tests/Sanctuary.Integration/IronGolemDamageChecks.cs')
    added = {'src/Sanctuary.Content/IronGolemUpgrades.cs', 'tests/Sanctuary.Integration/IronGolemStrongChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for n in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert sha(STAGE / 'native/Distribution' / n) == inputs['birth']['artifacts'][n] == old['artifacts'][n]
    # The encounter actor changes only the existing Strong consumer. Its
    # serialized fields,other affixes and compound grade behavior stay intact.
    path = 'src/Sanctuary.Content/EncounterCreature.cs'
    original = subprocess.check_output(['git', 'show', 'c70fce6:' + path], cwd=ROOT, text=True)
    first = original.index('            damageBonus += (_grade == EncounterGrade.Minion ?')
    last = original.index('        }', first)
    expected_body = original[:first] + '            var strong=EncounterProfiles.ExtraStrong(_difficulty,_grade==EncounterGrade.Minion);\n            damageBonus += strong.DamagePercent;ratingBonus += strong.AttackPercent;\n' + original[last:]
    assert expected_body == (ROOT / path).read_text()
    baseline = ROOT / '.local/class-skills/iron-golem-damage/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    expected['iron-golem-strong-check'] += 1
    expected['iron-golem-strong-reload-check'] += 2
    expected['iron-golem-strong-live-result'] += 3
    assert sum(expected.values()) == 370 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('Iron Extra Strong:12wild direct/inherited pairs,') == 1
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ('content', 'integration'):
        text = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in text for s in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for n, count in (('named-item-runtime-audit.json', 882), ('first-loop-affix-audit.json', 21)):
        audit = json.loads((STAGE / n).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / n).read_text())
        assert len(audit['InputHashes']) == count and audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for p, digest in audit['InputHashes'].items():
            assert sha(ROOT / p) == digest
        previous = json.loads(subprocess.check_output(['git', 'show', 'c70fce6:tools/reference/' + n], cwd=ROOT))
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
    saved = json.loads((STAGE / 'iron-golem-strong-fixtures.json').read_text())
    assert saved['Generation'] == 2 and saved['Rating'] > 0 and saved['LegacyRating'] > 0
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [s for s in status['skills'] if s['implementation'] == 'handler-present']
    assert len(handlers) == 186 and sum(s['characterClass'] == 'Necromancer' for s in handlers) == 30 and not status['completedClasses']
    docs = ('iron-golem-strong-checkpoint', 'pet-adjustment-review', 'class-skill-reuse-notes',
            'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons')
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_iron_golem_strong.py',
             ROOT / 'tools/review_iron_affix_reuse.py', ROOT / 'tools/reference/iron-affix-reuse-review.json',
             *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs],
             *[ROOT / 'src/Sanctuary.Content' / (n + '.cs') for n in ('SkeletonPets', 'NpcEquipmentDamage', 'EncounterCreature', 'EncounterProfiles', 'EliteCombat', 'NpcEquipmentImpact')]]
    names = ('birth-inputs.json', 'birth-native-checks.log', 'audit-inputs.json', 'audit-native-checks.log',
             'iron-golem-strong-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json',
             'python-tests.log', 'named-readiness.log', 'catalogue-check.log')
    files += [STAGE / n for n in names]
    files += [p for p in STAGE.iterdir() if p.is_file() and (p.name.startswith(('pre-build-', 'content-build-', 'integration-build-', 'run-', 'audit-run-', 'attempt-')))]
    result = dict(referenceVersion='3.3.93847', component='Approved Iron live hard10 direct Extra Strong with shared ED/AR consumers',
        installed=False, goalComplete=False, completedClasses=[], ironUpgradePackageComplete=False,
        extraStrongEnabled=True, spectralHitEnabled=False, build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=370, retainedIronCurveBloodCursePetPlayerSentryMarkers=364, boots=3, actualReloads=2, saves=3,
            unchangedWildDifficultyGradePairs=12, actualFacetTransitions=3, savedActorTypes=2,
            naturalWindupHard10UnlockSessions=3, auditMarkers=4, pythonTests=71, namedDefinitions=573,
            auditFingerprints=[882, 21], unchangedInstalledArtifacts=106, handlers=186, necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / f'{n}-inputs.json').relative_to(ROOT)), sha256=sha(STAGE / f'{n}-inputs.json')) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)},
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'Existing direct/inherited Extra Strong percentages are extracted into EncounterProfiles.ExtraStrong. Wild actor replaces only those two array lookups. Twelve actual Ordinary/Unique/SuperUnique/Minion and difficulty pairs retain damage/AR plus all other stats,normal attack frames,life/resistance/reward behavior.',
            'Iron gains the direct self-applied branch at live10hard points:Normal135ED/90AR,Nightmare112/75,Hell99/66. The weaker inherited branch belongs to boss minions,not all summoned actors. ED joins ordinary NPC damage percent over existing original-item capture;AR joins the existing item percentage pool over full flat/item Dexterity basis.',
            'Hard9+actual soft1refuses;train10/respec/relearn update prepared and historical actors without new stat writes,birth recapture,health/maximum change,controller/order/seed change or resistance bonus. The actual Messerschmidt original supplies flat Dexterity and positive ARpercent/local weapon ED;three real facet changes use current difficulty and preserve wounds.',
            'Prepared/historical actor resources,full prepared recipe,original packet/live AR,order and stream survive2reloads. Three connected sessions train10during actual G4windup;the later release delivers the predicted new damage with its exact actor stream restored after a canceled no-absorption prediction.',
            'The retained curve fixture now allows exactly the intended live EDdelta while keeping its captured curve/resource/recipe assertions. Both clean builds,370markers retaining364scopes,4audits,71Python,573named/eight-class240pass;882/21fresh audit fingerprints with decisions/data/kernel/106installed hashes unchanged. No new persistence fields,deployment or class completion.'],
        remaining=[
            'Iron hard20Spectral Hit remains pending:all5offensive channels/resistance rules,amount/type RNG,critical/cold/post-direct poison order and live/saved actors need complete integration. The current wild and ordinary item-poison contracts remain distinct;resistance-only activation is not substituted.',
            'Current source-table/modern executable/stat-list/global seed/event/PvP/native fallback/client/protection limits remain. The direct affix percent policy is an approved existing-affix composition,not proof of every hidden modern monster route. Historical controllers retain their existing transport.',
            'Held saved observation does not establish installed old-DLL/concurrent/unheld-save acceptance. Final affix/weapon feedback/audio and talent/hotkey icons remain separate;no extra player binding.',
            'Mage mapping,Fire upgrades,barriers,parking,Druid/remaining classes and other reference/acceptance gaps remain. No class is complete.'])
    output = ROOT / 'tools/reference/iron-golem-strong-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Iron Extra Strong:370markers/3boots/2reloads,12wild pairs,three facets/natural windup unlocks;installed unchanged. Spectral Hit remains pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
