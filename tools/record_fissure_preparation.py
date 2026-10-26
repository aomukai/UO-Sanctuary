#!/usr/bin/env python3
"""Record bounded learned Fissure acceptance without claiming installation."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
NATIVE = STAGE / 'fissure'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False, full_class=False):
    coverage = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert len(coverage['skills']) == 240 and not coverage['completedClasses']
    present = [r for r in coverage['skills'] if r['implementation'] == 'handler-present']
    assert len(present) == 160 and sum(r['characterClass'] == 'Druid' for r in present) == 4
    assert next(r for r in coverage['skills'] if r['talentId'] == 'druid.fissure')['implementation'] == 'handler-present'
    log = (NATIVE / 'native-checks.log').read_text()
    expected = {'fissure-check': 1, 'fissure-reload-check': 2, 'fissure-live-result': 3, 'fissure-input-result': 3,
                'blizzard-check': 1, 'blizzard-reload-check': 2, 'blizzard-result': 1, 'blizzard-reload-result': 2}
    assert ' ERR]' not in log and ': failed' not in log
    for name, count in expected.items():
        assert log.count(name + ': passed') == count, (name, count)
    assert 'pass3boots/2actual reloads/3connected natural actors/3quick casts' in (STAGE / 'fissure-focused-run.log').read_text()
    inventory = json.loads((NATIVE / 'focused-inputs.json').read_text())
    for name, digest in inventory['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inventory['copiedData'].items():
        assert sha(ROOT / ('data/Sanctuary/' + name.removeprefix('Data/Sanctuary/'))) == digest, name
    for name, path in [('Assemblies/Sanctuary.Content.dll', NATIVE / 'content/Sanctuary.Content.dll'),
                       ('Assemblies/Sanctuary.Integration.dll', NATIVE / 'integration/Sanctuary.Integration.dll')]:
        assert sha(path) == inventory['artifacts'][name]
    if full_class:
        full_log = (NATIVE / 'class-native-checks.log').read_text()
        assert ': failed' not in full_log and ' ERR]' not in full_log
        assert 'Native class skills and projectile checks passed' in (STAGE / 'fissure-full-run.log').read_text()
        for name, count in {'fissure-check': 1, 'fissure-reload-check': 2, 'fissure-live-result': 3, 'fissure-input-result': 3}.items():
            assert full_log.count(name + ': passed') == count
        full_inputs = json.loads((NATIVE / 'class-inputs.json').read_text())
        for name, digest in full_inputs['sourceInputs'].items():
            assert sha(ROOT / name) == digest, name
        assert full_inputs['copiedData'] == inventory['copiedData']
        assert full_inputs['artifacts'] == inventory['artifacts']
    fixture = json.loads((NATIVE / 'native-fixtures.json').read_text())
    assert len(fixture['State']['Centers']) == 1 and len(fixture['State']['Cracks']) > 0 and len(fixture['State']['Leases']) == 1
    assert fixture['State']['Cracks'][0]['LastTarget'] == fixture['Target']
    assert fixture['State']['DelayScope'] == 'cast-target-provisional-2.7'
    audit = json.loads((ROOT / 'tools/reference/named-item-runtime-audit.json').read_text())
    old = json.loads((NATIVE / 'pre-native-named-audit.json').read_text())
    assert {r['Id']: r for r in audit['Definitions']} == {r['Id']: r for r in old['Definitions']}
    assert len(audit['Definitions']) == 573
    for name, digest in audit['InputHashes'].items():
        assert sha(ROOT / name) == digest, name
    first = json.loads((ROOT / 'tools/reference/first-loop-affix-audit.json').read_text())
    for name, digest in first['InputHashes'].items():
        assert sha(ROOT / name) == digest, name
    assert audit['ContentAssemblySha256'] == first['ContentAssemblySha256'] == sha(NATIVE / 'content/Sanctuary.Content.dll')
    tests = (STAGE / 'fissure-python-tests.log').read_text()
    assert 'Ran 70 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
    paths = [ROOT / p for p in ['src/Sanctuary.Content/DruidFissure.cs', 'src/Sanctuary.Content/Blizzards.cs',
             'src/Sanctuary.Content/GroundMissileScatter.cs', 'src/Sanctuary.Content/DruidCombat.cs', 'src/Sanctuary.Content/MissileFootprints.cs',
             'src/Sanctuary.Content/NecromancerCombat.cs', 'src/Sanctuary.Content/SkillCombat.cs', 'src/Sanctuary.Content/SkillBindings.cs',
             'tests/check_class_skills.py', 'tools/probe_missile_footprints_reference.py', 'tools/reference/missile-footprints-source-evidence.json',
             'tests/Sanctuary.Integration/FissureChecks.cs', 'tests/check_fissure.py', 'tools/record_fissure_preparation.py',
             'tools/reference/fissure-source-evidence.json', 'tools/reference/fissure-factory-source-evidence.json',
             'tools/reference/fissure-damage-source-evidence.json', 'tools/reference/fissure-damage-evidence.json',
             'tools/reference/fissure-routing-source-evidence.json', 'tools/reference/ground-missile-scatter-evidence.json',
             'tools/reference/named-item-runtime-audit.json', 'tools/reference/first-loop-affix-audit.json']]
    paths += [NATIVE / p for p in ['content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll',
              'focused-inputs.json', 'native-checks.log', 'native-fixtures.json', 'pre-native-named-audit.json', 'audit-refresh-native.log']]
    paths += [STAGE / p for p in ['fissure-focused-run.log', 'fissure-python-tests.log', 'fissure-damage-run.log']]
    if full_class:
        paths += [NATIVE / 'class-native-checks.log', NATIVE / 'class-inputs.json', STAGE / 'fissure-full-run.log']
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=True, installed=False, handlerCount=160, druidHandlers=4,
                    installedHandlerCount=159, installedDruidHandlers=3,
                    goalComplete=False, completedClasses=[], focusedBoots=3, actualReloads=2, connectedNaturalActors=3, connectedQuickCasts=3,
                    fullClassRegression=full_class, sourceEndpointComparisons=18360, footprintCellComparisons=441, pythonTests=70,
                    namedAuditFingerprints=len(audit['InputHashes']), firstLoopFingerprints=len(first['InputHashes']), unchangedNamedDefinitions=573,
                    hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
                    established=[
                        'Learned source234 center/cracks retain separate80/84-frame lifetimes, emission-before-decrement, seeded square placement and shared Collision1 missile-mask projections, including own Size3 crack exclusion. Fresh hard synergies/mastery at retained cast rank reuse the verified fire endpoint supplier.',
                        'Native first eligible single collider, LastCollide suppression/alternation, exclusive fire roll, fire immunity, real tier3 potion nonfire additions and GetHit1 recovery pass. The saved cast/target lease is explicitly provisional modern scope, not a parity certificate.',
                        'Offline/old-facet no-fight lifetime expiry, class transition cleanup, read-only worker serialization, atomic malformed-state rejection and snapshot array isolation pass.',
                        'Three actual boots/two reloads preserve exact center/crack clocks/endpoints/seeds/active lease/last victim/fractional life. Three connected natural timer runs emit, damage, retain cracks after emitter expiry and finish. Three real quick packets preserve unpaid SC windup, once-only15mana release, local50-frame delay, natural damage and expiry. Right/quick bindings survive reload; unpaid pending casts are discarded. Retained Blizzard natural damage/lifetime and both reload controls pass.',
                        'New current rows and identified crack/contact placeholders are exported through the existing generator. Fresh native audits retain all573named item decisions, and70Python tests pass. Fingerprint counts above identify the current candidate.'
                    ], fixtureControls=[
                        'Direct prepared Create/Tick controls are separate from learned SC/payment/input checks. Explicit actions=true supplies online activity in controlled chronology cases; connected natural and quick-packet cases use actual NetState and production timers.',
                        'SeedLow=1 is scoped to deterministic actor checks. OtherMissileFootprint=true is a recording control for landing rejection. Separate restore/projection controls cover six FireGround families, Hydra bolts, ground Shock Web, Plague clouds, Boulder ground and an active legacy poison field; they do not execute each contributing skill cast or certify each producer geometry.',
                        'The connected quick input damage victim is positioned on the first actually accepted crack; it does not assert that a predicted random landing must pass native terrain. Source scatter and placement have separate source matrices and controlled native checks.',
                        'The held state uses a two-day test epoch only after ordinary natural lifetime checks. The held injured monster receives the existing real prevent-heal state, preventing its D2 life recovery from changing saved-life inspections; ordinary damage/lifetime tests are unchanged.',
                        'Worktree Projects/Distribution dependencies resolve read-only to the matching main checkout; no installed package, save or client configuration is changed by these disposable worlds.'
                    ], remaining=[
                        'Full ordinary-client presentation/input acceptance and current-executable correspondence remain open; the connected protocol fixture is not an ordinary-client visual acceptance certificate.',
                        'Cross-producer allocation/mask timing, moving rounding, native terrain/size projection and room-list/collider order remain acceptance gaps. Poison Explosion exposes its existing legacy timer field representative rather than inventing exact source children; it has no new persistence certificate.',
                        'Official2.7revises legacy global hit delay, but precise current Fissure crack/cast/owner grouping and expiry boundaries remain unverified. The cast-target-provisional-2.7 save marker makes the current prepared model explicit.',
                        'Full source mode/owner/events/return-fire/client/seed/current-executable acceptance, final effects/audio and separate talent/right/quick icons remain open. No skill/class completion or installation is claimed.'
                    ], next='Finish full regression if not recorded above, then installation acceptance. Keep modern grouping/source-native limits explicit and continue unaffected skills if individually blocked.')
    path = ROOT / 'tools/reference/fissure-native-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Staged learned Fissure:3boots/2reloads/3natural actors/3quick casts, retained Blizzard,70Python tests and573unchanged named decisions verified;160candidate/159installed handlers.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
