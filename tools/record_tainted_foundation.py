#!/usr/bin/env python3
"""Bind Tainted preparation to the tested build, original owners and retained regressions."""
import argparse
import json
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/tainted-foundation'
FOCUS = ROOT / '.local/class-skills/tainted-foundation-focus'
PARENT = 'dc21c97cb24f180a142c2a8863f5a39a185fb66f'
sha = inventory.sha


def record(compare=False):
    inventory.STAGE = STAGE
    native = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    audit = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    focused = inventory.validate_inventory(FOCUS / 'birth-inputs.json')
    for inputs in (STAGE / 'birth-inputs.json', STAGE / 'audit-inputs.json', FOCUS / 'birth-inputs.json'):
        snapshot(STAGE / 'pre-build-2.json', inputs)
    assert native['artifacts'] == audit['artifacts'] == focused['artifacts']
    assert native['copiedData'] == audit['copiedData'] == focused['copiedData']
    prior = json.loads((ROOT / '.local/class-skills/druid-human-cast-clock/birth-inputs.json').read_text())
    changed = {n for n in set(prior['sourceInputs']) | set(native['sourceInputs'])
               if n.endswith('.cs') and prior['sourceInputs'].get(n) != native['sourceInputs'].get(n)}
    assert changed == {'src/Sanctuary.Content/TaintedPrepared.cs', 'src/Sanctuary.Content/SkillMechanics.cs',
                       'src/Sanctuary.Content/GoatmanNpcSkills.cs', 'tests/Sanctuary.Integration/TaintedFoundationChecks.cs'}
    assert {n for n in set(prior['copiedData']) | set(native['copiedData'])
            if prior['copiedData'].get(n) != native['copiedData'].get(n)} == {
        'Data/Sanctuary/Balance/class-skill-status.json', 'Data/Sanctuary/Balance/warlock-npc-skills.json',
        'Data/Sanctuary/Balance/necromancer-summons.json'}
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert native['artifacts'][name] == prior['artifacts'][name] == sha(STAGE / 'native/Distribution' / name)
    source_path = ROOT / 'tools/reference/tainted-foundation-source-evidence.json'
    source = json.loads(source_path.read_text())
    assert source['referenceVersion'] == '3.3.93847' and source['formulaCases'] == 3570 and source['packetCases'] == 6120
    assert source['ownedMonsterFieldComparisons'] == 114 and not source['handlerEnabled'] and not source['currentExecutableParity']
    for group in ('inputs', 'artifacts'):
        for name, digest in source[group].items():
            assert sha(ROOT / name) == digest, name
    baseline = ROOT / '.local/class-skills/druid-human-cast-clock/birth-native-checks.log'
    parent = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/druid-human-cast-clock-native-evidence.json'], cwd=ROOT))
    assert sha(baseline) == parent['evidence'][str(baseline.relative_to(ROOT))]
    expected = markers(baseline); expected['tainted-foundation-check'] += 1; expected['tainted-foundation-reload-check'] += 2
    assert sum(expected.values()) == 347 and markers(STAGE / 'birth-native-checks.log') == expected
    for folder, count in ((STAGE, 347), (FOCUS, 12)):
        log = (folder / 'birth-native-checks.log').read_text()
        assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == count
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        assert log.count('Tainted:3570Fog/6120NPC fire packets/3825shared base controls,30600rank contexts;no actor,aura,learned cast or modern AI claimed') == 3
        saved = json.loads((folder / 'tainted-foundation-fixtures.json').read_text())
        assert len(saved) == 2 and {r['Female'] for r in saved} == {True, False}
        assert {r['Mana'] for r in saved} == {5000, 5001}
        for row in saved:
            capture = row['Snapshot']; v = capture['Birth']['Suppliers']
            assert (capture['MasteryRank'], capture['OathRank'], capture['HardMastery'], capture['HardBloodBoil']) == (11, 3, 10, 5)
            assert (v['Rank'], v['FireMasteryPercent'], v['PetMaximum'], v['BloodBoilRank'], v['PhysicalSunder'], v['FireSunder']) == (2, 110, 3, 5, 0, 0)
            assert row['Packet'] == dict(Minimum256=7526, Maximum256=10752, Radius=4)
    for kind in ('content', 'integration'):
        assert all(t in (STAGE / f'{kind}-build-2.log').read_text() for t in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for name, count in (('named-item-runtime-audit.json', 969), ('first-loop-affix-audit.json', 21)):
        new = json.loads((STAGE / name).read_text()); assert new == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert len(new['InputHashes']) == count and new['ContentAssemblySha256'] == native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path, digest in new['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        old = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/' + name], cwd=ROOT))
        skip = {'InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath'}
        assert {k: v for k, v in new.items() if k not in skip} == {k: v for k, v in old.items() if k not in skip}
    audit_log = (STAGE / 'audit-native-checks.log').read_text()
    assert audit_log.count(': passed') == 2 and ': failed' not in audit_log and ' ERR]' not in audit_log
    assert 'Ran 71 tests' in (STAGE / 'python-tests.log').read_text() and '\nOK\n' in (STAGE / 'python-tests.log').read_text()
    for name, message in (('readiness-check.log', '573 definitions accounted for'), ('catalogue-check.log', 'Eight classes, 240 source skills'),
                          ('roadmap-check.log', 'Class handler coverage, blockers, effects and icons recorded separately.')):
        assert message in (STAGE / name).read_text()
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for name, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / name) == digest, name
    preservation = json.loads((ROOT / '.local/class-skills/creeper-frame-installation.json').read_text())
    for key, relative in (('saves', '.local/server/Saves'), ('configuration', '.local/server/Configuration')):
        folder = Path(installed['runtimeRoot']) / relative
        assert {str(p.relative_to(folder)): sha(p) for p in folder.rglob('*') if p.is_file()} == preservation['protected'][key]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation'] == 'handler-present' for s in status['skills']) == 191
    skill = next(s for s in status['skills'] if s['talentId'] == 'warlock.summontainted')
    assert skill['implementation'] == 'blocked' and skill['verification'] == 'partial-native-coverage' and not skill['complete']
    diagnostic = STAGE / 'diagnostic-1'
    failed = (diagnostic / 'focus/birth-native-checks.log').read_text()
    assert 'Tainted complete-source fire packet/own NPC synergy/mastery order' in failed and 'tainted-foundation-check: failed' in failed
    assert sha(diagnostic / 'content/Sanctuary.Content.dll') != native['artifacts']['Assemblies/Sanctuary.Content.dll']
    assert 'SourcePercentage.Calculate(basis, suppliers.FireMasteryPercent, 100)' in (diagnostic / 'TaintedPrepared.cs').read_text()
    files = [Path(__file__).resolve(), source_path, ROOT / 'tools/probe_tainted_foundation.py',
             ROOT / 'tests/check_tainted_foundation.py', ROOT / 'tests/check_tainted_foundation_focus.py', baseline,
             *(ROOT / 'docs' / n for n in ('tainted-foundation-checkpoint.md', 'class-skill-roadmap.md', 'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md', 'skill-effects.md', 'skill-icons.md')),
             *(STAGE / n for n in ('pre-build-2.json', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'native-run-2.log',
                'audit-native-checks.log', 'audit-run-2.log', 'content-build-2.log', 'integration-build-2.log', 'python-tests.log',
                'readiness-check.log', 'catalogue-check.log', 'roadmap-check.log', 'tainted-foundation-fixtures.json')),
             *(FOCUS / n for n in ('birth-inputs.json', 'birth-native-checks.log', 'native-run-2.log', 'tainted-foundation-fixtures.json')),
             *(p for p in diagnostic.rglob('*') if p.is_file() and not p.is_symlink())]
    result = dict(referenceVersion='3.3.93847', parent=PARENT, component='Summon Tainted captured birth and own NPC Fire Ball/Resist Fire suppliers',
        handlerEnabled=False, goalComplete=False, completedClasses=[],
        verified=dict(nativeMarkers=347, retainedNativeMarkers=344, focusedNativeMarkers=12, boots=3, reloads=2, saves=3,
            formulaCasesPerBoot=3570, npcPacketCasesPerBoot=6120, reusedBaseCasesPerBoot=3825, supplierContextsPerBoot=30600,
            ownedMonsterFields=114, originalOwners=2, originalMana=[5000, 5001], pythonTests=71,
            namedDecisions=573, auditFingerprints=[969, 21], handlers=191, protectedInstalledArtifactsUnchanged=106),
        limitations=[
            'Prepared suppliers and persisted owner/capture DTOs only: no Tainted world actor, automatic GenericPet AI, NPC aura/projectile/Blood Boil release, learned casting/payment or cap replacement.',
            'Shared older base/do56 and damage routines are bounded source evidence with declared services; they do not establish the complete modern writer, source stat-list ownership, mastery capture time, events, movement, room/collision or client behavior.',
            'Separate physical/fire sunder values are captured and round-trip without combining or installing immunity effects. Difficulty recomposition captures ranks/owner level; no Tainted current-life transition is claimed without a body.',
            'Initial failed high-rank packet comparison is preserved. Elemental mastery uses wide ratio arithmetic; the separate monster percentage helper remains for summon life.',
            'Approved Succubus body is an appearance mapping, not a UO stats/spells/AI template. Missing effects/audio and talent/right/quick icons remain separate. No class completion, deployment or installed-save writes.'],
        tested=native, evidence={str(p.relative_to(ROOT)): sha(p) for p in files})
    path = ROOT / 'tools/reference/tainted-foundation-native-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if compare:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Tainted prepared:347 retained/12 focused native checks,2original owners and2reloads,71Python,969/21audits;learned summon remains gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
