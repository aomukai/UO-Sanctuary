#!/usr/bin/env python3
"""Current Amazon skills, original Valkyrie, paid casts and saved projectiles."""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import struct
import time

from check_iron_golem_birth import ROOT, check
from check_class_skills import fixture_connection
from check_valkyrie_cast import configuration as valkyrie
from check_charged_strike_velocity import configuration as charged
from check_slow_missiles_cast import configuration as slow

STAGE = ROOT / '.local/class-skills/amazon-planned-acceptance'
SKILL_FIXTURES = (
    'Impale', 'Fend', 'LightningStrike', 'PoisonJavelin', 'PlagueJavelin',
    'LightningBolt', 'LightningFury', 'AmazonCritical', 'AmazonDodge',
    'AmazonAvoid', 'AmazonEvade', 'AmazonPenetrate', 'AmazonPierce',
    'DecoyComponent', 'InnerSight', 'MagicArrow', 'FireArrow', 'ColdArrow',
    'MultipleShot', 'ExplodingArrow', 'IceArrow', 'GuidedArrow',
    'ImmolationArrow', 'Strafe', 'FreezingArrow', 'AmazonCastClock',
)
LEGACY_INPUTS = (
    'magic-arrow', 'multiple-shot', 'fire-arrow', 'cold-arrow', 'ice-arrow',
    'exploding-arrow', 'guided-arrow', 'immolation-arrow', 'strafe',
    'freezing-arrow', 'poison-javelin', 'lightning-bolt', 'plague-javelin',
    'lightning-fury',
)


def old_cast(session, client, name, timeout=15):
    """Retain the existing real Cast packet protocol from check_class_skills."""
    session.send(name + '-input-check')
    assert session.expect(name + r'-input-check: (passed|failed)').group(1) == 'passed'
    ready = session.expect(name + r'-input: ready ability=(\d+) x=(\d+) y=(\d+) z=(-?\d+)')
    ability, x, y, z = map(int, ready.groups())
    client.sendall(struct.pack('>BHHBBIBIHHb', 0xBF, 21, 0xF2, 7, 3, 1, ability, 0, x, y, z))
    assert session.expect(name + r'-input-result: (passed|failed)', timeout=timeout).group(1) == 'passed'
    return 2


def wire_checks(session, port, boot):
    count = 0
    # Respect the native 10-attempt/10-second window while grouping the older
    # Cast controls; this pacing happens before any skill fixture is created.
    next_connect = time.monotonic() + 1.1
    for name in LEGACY_INPUTS:
        remaining = next_connect - time.monotonic()
        if remaining > 0:
            time.sleep(remaining)
        next_connect = time.monotonic() + 1.1
        with fixture_connection(session, port) as client:
            time.sleep(.15)
            count += old_cast(session, client, name)
    count += decoy_checks(session, port, boot)
    session.send('plague-javelin-snapshot-check')
    assert session.expect(r'plague-javelin-snapshot-check: (passed|failed)').group(1) == 'passed'
    return count + 1


def decoy_checks(session, port, boot, stage=STAGE):
    # Decoy's established fixture includes the real Cast and expiry before
    # persisting a replacement; its socket-loss check runs after disconnect.
    command = 'decoy-check' if boot == 0 else 'decoy-reload-check'
    with fixture_connection(session, port) as client:
        time.sleep(.15)
        session.send(command)
        assert session.expect(command + r': (passed|failed)').group(1) == 'passed'
        ready = session.expect(r'decoy-input: ready ability=(\d+) x=(\d+) y=(\d+) z=(-?\d+)')
        ability, x, y, z = map(int, ready.groups())
        client.sendall(struct.pack('>BHHBBIBIHHb', 0xBF, 21, 0xF2, 7, 3, 1, ability, 0, x, y, z))
        assert session.expect(r'decoy-result: (passed|failed)', timeout=30).group(1) == 'passed'
        folder = Path(f'/proc/{session.process.pid}/cwd')
        shutil.copy2(folder / 'decoy-checks.json', stage / f'decoy-boot-{boot}.json')
    session.send('decoy-disconnect-check')
    assert session.expect(r'decoy-disconnect-check: (passed|failed)').group(1) == 'passed'
    return 3


def configuration():
    config = valkyrie(isolate=True)
    config['stage'] = STAGE
    config.setdefault('extra_files', {})
    config.setdefault('extra_checks', ())
    config.setdefault('extra_wait_results', {})
    config.setdefault('extra_manifest_names', {})
    for source in (charged(), slow()):
        config['extra_files'].update(source['extra_files'])
        config['extra_families'] += source['extra_families']
        config['extra_checks'] += source.get('extra_checks', ())
        config['extra_wait_results'].update(source.get('extra_wait_results', {}))
        config['extra_manifest_names'].update(source.get('extra_manifest_names', {}))
    for fixture in SKILL_FIXTURES:
        path = ROOT / 'tests/Sanctuary.Integration' / (fixture + 'Checks.cs')
        text = path.read_text()
        family = re.search(r'Register\("([a-z0-9-]+)-check"', text).group(1)
        config['extra_families'] += (family,)
        manifest = re.search(r'private static string Manifest=>Path.Join\(Core.BaseDirectory,"([a-z0-9-]+\.json)"', text)
        if manifest:
            config['extra_manifest_names'][family] = manifest.group(1)
        for name in sorted(set(re.findall(r'"([a-z0-9-]+(?:source|probe)[a-z0-9-]*\.json)"', text))):
            if name in config['extra_files']:
                continue
            source = ROOT / '.local/class-skills' / name
            if not source.is_file():
                source = ROOT / '.local/class-skills' / name.removesuffix('.json') / 'cases.json'
            assert source.is_file(), (fixture, name, source)
            config['extra_files'][name] = source
    config['extra_families'] += ('slow-missiles-rules', 'slow-missiles-windup', 'slow-missiles-learned')
    families = list(dict.fromkeys(config['extra_families']))
    # Preserve the established check_class_skills order: Fend's exact nearby
    # count runs before Jab/Power Strike/Charged Strike's saved melee scenes.
    # Those older fixtures share the same native-world anchor.
    families.remove('fend')
    families.insert(families.index('jab'), 'fend')
    config['extra_families'] = tuple(families)
    config['extra_checks'] += ('fend-loop-check', 'chain-child-search-check')
    config['extra_wait_results'].update({
        'impale-check': 'impale-slow-expiry', 'fend-loop-check': 'fend-loop-result',
        'inner-sight-check': 'inner-sight-result',
        'amazon-avoid-check': 'amazon-avoid-result',
        'amazon-avoid-reload-check': 'amazon-avoid-result',
        'amazon-evade-check': 'amazon-evade-result',
        'amazon-evade-reload-check': 'amazon-evade-result',
        'cold-arrow-check': 'cold-arrow-state-result',
        'ice-arrow-check': 'ice-arrow-state-result',
        'immolation-arrow-check': 'immolation-arrow-state-result',
        'freezing-arrow-check': 'freezing-arrow-state-result',
        **{f'decoy-component{suffix}': 'decoy-component-result' for suffix in ('-check', '-reload-check')},
        **{f'{family}{suffix}': f'{family}{suffix}-result'
           for family in ('slow-missiles-rules', 'slow-missiles-learned')
           for suffix in ('-check', '-reload-check')},
    })
    config['extra_connected_checks'] = (
        'fend-loop-check', 'amazon-evade-check', 'amazon-evade-reload-check',
        'slow-missiles-rules-check', 'slow-missiles-rules-reload-check',
        'slow-missiles-learned-check', 'slow-missiles-learned-reload-check',
    )
    config['extra_target_input_commands'] += (
        'amazon-cast-clock-right', 'amazon-cast-clock-quick',
        'lightning-strike-right', 'lightning-strike-quick',
        'slow-missiles-learned-right', 'slow-missiles-learned-quick', 'slow-missiles-learned-fcr-quick',
    )
    config['extra_ground_input_commands'] = (
        'slow-missiles-learned-right', 'slow-missiles-learned-quick', 'slow-missiles-learned-fcr-quick',
    )
    config['extra_wire_callbacks'] = (wire_checks,)
    config['extra_connection_settle'] = .15
    config['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'tests/check_class_skills.py',
                              ROOT / 'tools/reference/item-content-scope-decision.json',
                              ROOT / 'tools/reference/lightning-strike-sanctuary-decision.json',
                              ROOT / 'tools/reference/slow-missiles-rule-approval.json')
    for key in ('extra_families', 'extra_checks', 'extra_target_input_commands'):
        assert len(config[key]) == len(set(config[key])), key
    for path in config['extra_files'].values():
        assert path.is_file(), path
    return config


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', action='store_true')
    args = parser.parse_args()
    config = configuration()
    if args.plan:
        print(json.dumps({key: len(config[key]) for key in (
            'extra_families', 'extra_checks', 'extra_target_input_commands', 'extra_files')}, indent=2))
        print(f'{len(LEGACY_INPUTS)} retained Cast packet routes and connected Decoy per boot; cached references only.')
    else:
        os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
        check(**config)
