#!/usr/bin/env python3
"""Bind Iron's approved captured Clay damage curve and retained item semantics."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/iron-golem-damage'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / f'{n}-inputs.json') for n in ('birth', 'audit')}
    for n, v in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / f'{n}-inputs.json')
        assert v['artifacts'] == inputs['birth']['artifacts'] and v['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/blood-life-tap-native-evidence.json'
    prior = json.loads(parent.read_text())
    ref = prior['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed = {'src/Sanctuary.Content/' + n + '.cs' for n in ('GolemPetDamage', 'IronGolemBody', 'NecromancerSummons')}
    changed.add('tests/Sanctuary.Integration/IronGolemWorldChecks.cs')
    added = {'tests/Sanctuary.Integration/IronGolemDamageChecks.cs'}
    assert before.keys() <= after.keys() and after.keys() - before.keys() == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for n in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert sha(STAGE / 'native/Distribution' / n) == inputs['birth']['artifacts'][n] == old['artifacts'][n]
    # Prepared Iron changes only its damage supplier, not saved records/fields.
    path = 'src/Sanctuary.Content/IronGolemBody.cs'
    original = subprocess.check_output(['git', 'show', '29cf4ad:' + path], cwd=ROOT, text=True)
    first = original.index('    internal override (int Min,int Max,int Percent) SourceWeaponBase=>')
    last = original.index('    public override bool UsesNativeSummonTimer', first)
    expected_body = original[:first] + '    internal override (int Min,int Max,int Percent) SourceWeaponBase=>GolemPetDamage.Weapon(BirthValues,SummonDifficulty);\n' + original[last:]
    assert expected_body == (ROOT / path).read_text()
    baseline = ROOT / '.local/class-skills/blood-life-tap/birth-native-checks.log'
    assert sha(baseline) == prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline)
    expected['iron-golem-damage-check'] += 1
    expected['iron-golem-damage-reload-check'] += 2
    expected['iron-golem-damage-live-result'] += 3
    assert sum(expected.values()) == 364 and markers(STAGE / 'birth-native-checks.log') == expected
    log = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('Iron damage:three independent curve goldens;') == 1
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
        previous = json.loads(subprocess.check_output(['git', 'show', '29cf4ad:tools/reference/' + n], cwd=ROOT))
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
    saved = json.loads((STAGE / 'iron-golem-damage-fixtures.json').read_text())
    assert saved['Generation'] == 2 and saved['Rank'] == 10 and saved['Mastery'] == 4
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [s for s in status['skills'] if s['implementation'] == 'handler-present']
    assert len(handlers) == 186 and sum(s['characterClass'] == 'Necromancer' for s in handlers) == 30 and not status['completedClasses']
    docs = ('iron-golem-damage-checkpoint', 'pet-adjustment-review', 'class-skill-reuse-notes',
            'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons')
    files = [parent, baseline, Path(__file__).resolve(), ROOT / 'tests/check_iron_golem_damage.py',
             ROOT / 'tools/review_iron_affix_reuse.py', ROOT / 'tools/reference/iron-affix-reuse-review.json',
             *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs],
             *[ROOT / 'src/Sanctuary.Content' / (n + '.cs') for n in ('SkeletonPets', 'NpcEquipmentDamage', 'EncounterCreature', 'EncounterProfiles', 'EliteCombat', 'NpcEquipmentImpact')]]
    names = ('birth-inputs.json', 'birth-native-checks.log', 'audit-inputs.json', 'audit-native-checks.log',
             'iron-golem-damage-fixtures.json', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json',
             'python-tests.log', 'named-readiness.log', 'catalogue-check.log')
    files += [STAGE / n for n in names]
    files += [p for p in STAGE.iterdir() if p.is_file() and (p.name.startswith(('pre-build-', 'content-build-', 'integration-build-', 'run-', 'audit-run-', 'attempt-1-')))]
    result = dict(referenceVersion='3.3.93847', component='Approved Iron captured Clay curve with Golem Mastery and unchanged original-item composition',
        installed=False, goalComplete=False, completedClasses=[], ironUpgradePackageComplete=False, ironAffixesEnabled=False,
        build=build, run=run, auditRun=audit_run,
        verified=dict(nativeMarkers=364, retainedBloodCursePetPlayerSentryMarkers=358, boots=3, actualReloads=2, saves=3,
            independentNormalCurveGoldens=3, actualOriginalItemControls=3, actualFacetTransitions=3, savedActorTypes=2,
            naturalLearnedIronDamageSessions=3, auditMarkers=4, pythonTests=71, namedDefinitions=573,
            auditFingerprints=[881, 21], unchangedInstalledArtifacts=106, handlers=186, necromancerHandlers=30),
        inputInventories={n: dict(path=str((STAGE / f'{n}-inputs.json').relative_to(ROOT)), sha256=sha(STAGE / f'{n}-inputs.json')) for n in inputs},
        changedSourceHashes={p: after[p] for p in sorted(changed | added)},
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
        established=[
            'Prepared Iron uses the same Skeleton/Clay curve and captured Golem Mastery through its existing original-item recipe. The original plain NecromancerMinion Iron type uses its own captured snapshot with ratio100. Former innate/Fire-synergy damage is replaced. No formula duplicate,new birth resources or live recapture is introduced.',
            'Real learned Broadsword,The Battlebranch and Boneflesh originals retain existing one-hand/armor additive endpoints,two-hand replacement,item-local ED and original identity. Three independent Normal curve goldens pass. Owner training in Iron/Golem Mastery/Fire does not recapture existing damage,wounds,recipe,maximum,order or seed;Skeleton Mastery is distinct.',
            'Three actual owner facet transitions use current configured difficulty and preserve fractional life ratio. Prepared/original historical types,original item,recipe,rank,damage packet,wounds,order and stream survive2reloads. No serialized mobile/JSON record layout changes.',
            'Three connected learned Iron attacks deliver the new physical curve with normal clocks. Iron retains release-time allocation;the test predicts a no-absorption packet during windup,cancels it and restores the exact actor stream,then compares the real later HP loss. This is distinct from immutable unarmed-pet start allocation.',
            'Both clean builds,364markers retaining358scopes,4audits,71Python,573named/eight-class240pass. Audit decisions/data/kernel/106installed hashes unchanged with881/21fingerprints. No deployment or class completion.'],
        remaining=[
            'Iron hard10Extra Strong and hard20Spectral Hit remain pending. Direct/inherited Extra Strong damage/AR percentages must retain difficulty/grade composition. Spectral Hit needs its existing amount/type RNG,all5channels,resistance cap/cold duration and post-direct-hit poison behavior,not resistance-only activation.',
            'The current wild Spectral poison path applies after direct damage and only to survivors;ordinary prepared item poison contributes its rate to initial HP. Wild critical timing/independent raw amount also differs from blindly adding a channel before intrinsic critical. Reuse requires an explicit typed/poison/order adapter;do not silently substitute item-poison semantics.',
            'Prepared Iron retains existing supported original-property/event gates and release-time allocation. Legacy plain actor controller/native fallback,PvP/source world/modern seed/event/protection/client and old-DLL/concurrent/unheld-save acceptance retain prior limits.',
            'Mage basic spell/Mastery mapping,Fire upgrades,barriers,parking,Druid/remaining classes and final effects/icons remain open. No class is complete.'])
    output = ROOT / 'tools/reference/iron-golem-damage-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Iron damage:364markers/3boots/2reloads,three real originals/facet transitions,natural learned attacks;installed unchanged. Affix unlocks remain pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
