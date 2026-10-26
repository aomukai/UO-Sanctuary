#!/usr/bin/env python3
"""Record bounded prepared Fissure acceptance without claiming installation."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
NATIVE = STAGE / 'fissure'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    coverage = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert len(coverage['skills']) == 240 and not coverage['completedClasses']
    present = [r for r in coverage['skills'] if r['implementation'] == 'handler-present']
    assert len(present) == 159 and sum(r['characterClass'] == 'Druid' for r in present) == 3
    assert next(r for r in coverage['skills'] if r['talentId'] == 'druid.fissure')['implementation'] == 'pending'
    log = (NATIVE / 'native-checks.log').read_text()
    expected = {'fissure-check': 1, 'fissure-reload-check': 2, 'fissure-live-result': 3,
                'blizzard-check': 1, 'blizzard-reload-check': 2, 'blizzard-result': 1, 'blizzard-reload-result': 2}
    assert ' ERR]' not in log and ': failed' not in log
    for name, count in expected.items():
        assert log.count(name + ': passed') == count, (name, count)
    assert 'pass3boots/2actual reloads/3connected natural actors' in (STAGE / 'fissure-focused-run.log').read_text()
    inventory = json.loads((NATIVE / 'focused-inputs.json').read_text())
    for name, digest in inventory['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inventory['copiedData'].items():
        assert sha(ROOT / ('data/Sanctuary/' + name.removeprefix('Data/Sanctuary/'))) == digest, name
    for name, path in [('Assemblies/Sanctuary.Content.dll', NATIVE / 'content/Sanctuary.Content.dll'),
                       ('Assemblies/Sanctuary.Integration.dll', NATIVE / 'integration/Sanctuary.Integration.dll')]:
        assert sha(path) == inventory['artifacts'][name]
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
             'src/Sanctuary.Content/GroundMissileScatter.cs', 'src/Sanctuary.Content/DruidCombat.cs',
             'tests/Sanctuary.Integration/FissureChecks.cs', 'tests/check_fissure.py', 'tools/record_fissure_preparation.py',
             'tools/reference/fissure-source-evidence.json', 'tools/reference/fissure-factory-source-evidence.json',
             'tools/reference/fissure-damage-source-evidence.json', 'tools/reference/fissure-damage-evidence.json',
             'tools/reference/fissure-routing-source-evidence.json', 'tools/reference/ground-missile-scatter-evidence.json',
             'tools/reference/named-item-runtime-audit.json', 'tools/reference/first-loop-affix-audit.json']]
    paths += [NATIVE / p for p in ['content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll',
              'focused-inputs.json', 'native-checks.log', 'native-fixtures.json', 'pre-native-named-audit.json', 'audit-refresh-native.log']]
    paths += [STAGE / p for p in ['fissure-focused-run.log', 'fissure-python-tests.log', 'fissure-damage-run.log']]
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False, handlerCount=159, druidHandlers=3,
                    goalComplete=False, completedClasses=[], focusedBoots=3, actualReloads=2, connectedNaturalActors=3,
                    fullClassRegression=False, sourceEndpointComparisons=18360, pythonTests=70,
                    namedAuditFingerprints=len(audit['InputHashes']), firstLoopFingerprints=len(first['InputHashes']), unchangedNamedDefinitions=573,
                    hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
                    established=[
                        'Prepared native source234 center/cracks retain separate80/84-frame lifetimes, emission-before-decrement, seeded square placement and own Size3 missile-mask exclusion. Fresh hard synergies/mastery at retained cast rank reuse the verified fire endpoint supplier.',
                        'Native first eligible single collider, LastCollide suppression/alternation, exclusive fire roll, fire immunity, real tier3 potion nonfire additions and GetHit1 recovery pass. The saved cast/target lease is explicitly provisional modern scope, not a parity certificate.',
                        'Offline/old-facet no-fight lifetime expiry, class transition cleanup, read-only worker serialization, atomic malformed-state rejection and snapshot array isolation pass.',
                        'Three actual boots/two reloads preserve exact center/crack clocks/endpoints/seeds/active lease/last victim/fractional life. Three connected natural timer runs emit, damage, retain cracks after emitter expiry and finish. Retained Blizzard natural damage/lifetime and both reload controls pass.',
                        'New current rows and identified crack/contact placeholders are exported through the existing generator. Fresh native588/21 input fingerprints retain all573named item decisions, and70Python tests pass.'
                    ], fixtureControls=[
                        'Direct prepared Create/Tick calls do not exercise learned SC/payment/input. Explicit actions=true supplies online activity in controlled chronology cases; connected natural cases use actual NetState and production timers.',
                        'SeedLow=1 is scoped to deterministic actor checks. OtherMissileFootprint=true is a recording control for landing rejection, not evidence that other producer registries are connected.',
                        'The held state uses a two-day test epoch only after ordinary natural lifetime checks. The held injured monster receives the existing real prevent-heal state, preventing its D2 life recovery from changing saved-life inspections; ordinary damage/lifetime tests are unchanged.',
                        'Worktree Projects/Distribution dependencies resolve read-only to the matching main checkout; no installed package, save or client configuration is changed by these disposable worlds.'
                    ], remaining=[
                        'Learned SC/release payment/cancellation/local50-frame cooldown/right/quick binding and natural input acceptance remain unimplemented; Fissure stays outside DruidCombat.Implemented.',
                        'Other Collision1 missile producers have not supplied footprints to OtherMissileFootprint. Exact cross-producer allocation/mask timing, native terrain/size projection and room-list/collider order remain acceptance gaps.',
                        'Official2.7revises legacy global hit delay, but precise current Fissure crack/cast/owner grouping and expiry boundaries remain unverified. The cast-target-provisional-2.7 save marker makes the current prepared model explicit.',
                        'Full source mode/owner/events/return-fire/client/seed/current-executable acceptance, complete class regression, final effects/audio and separate talent/right/quick icons remain open. No skill/class completion or installation is claimed.'
                    ], next='Connect shared missile footprints and finish learned Fissure casting/input, then full regression and installation acceptance. Keep modern grouping/source-native limits explicit and continue unaffected skills if individually blocked.')
    path = ROOT / 'tools/reference/fissure-native-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Prepared Fissure:3boots/2reloads/3connected actors, retained Blizzard,70Python tests and573unchanged named decisions verified;159installed handlers remain.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
