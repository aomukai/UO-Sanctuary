#!/usr/bin/env python3
"""Current Druid elements, forms, captured summons, vines and held Arctic."""
import argparse
import json
import os
from pathlib import Path
import re
import socket
import struct
import threading
import time

from check_iron_golem_birth import ROOT, check
from check_class_skills import fixture_connection
from check_assassin_planned_acceptance import paced_clients
from check_arctic_held_input import OPEN, drain, keepalive
from check_raven_snapshot import configuration as raven
from check_druid_snapshot import configuration as beasts
from check_creeper_learned import configuration as vines
from check_arctic_potion import configuration as arctic

STAGE = ROOT / '.local/class-skills/druid-planned-acceptance'
CORE = ('Firestorm', 'MoltenBoulder', 'Fissure', 'CycloneArmor', 'Twister',
        'Volcano', 'Tornado', 'Hurricane', 'Armageddon', 'DruidFormWorld',
        'DruidFormCasting', 'ShapeRestriction', 'DruidFormInterruption',
        'DruidFormReaction', 'DruidFormAttack', 'DruidMaul', 'DruidFeral',
        'DruidFireClaws', 'DruidRabies', 'DruidRabiesInfection', 'DruidRabiesLifecycle',
        'DruidHunger', 'DruidFury', 'ShockWave', 'DruidShockWaveCasting',
        'SpiritWolfCasting', 'DireWolfCasting', 'GrizzlyCasting',
        'OakCasting', 'WolverineCasting', 'BarbsCasting')
MODELS = ('DruidHumanCastClock', 'DruidFormModel', 'DruidFormRate',
          'DruidAttackChargeModel', 'DruidRabiesPacket')
ELEMENT_INPUTS = ('firestorm', 'molten-boulder', 'fissure', 'cyclone-armor',
                  'twister', 'volcano', 'tornado', 'hurricane', 'armageddon')
SNAPSHOTS = ('druid-form-world-snapshot-check', 'druid-form-expiry-stage-check',
             'druid-form-interruption-snapshot-check', 'druid-form-reaction-snapshot-check',
             'druid-maul-snapshot-check', 'druid-maul-expiry-stage-check',
             'druid-feral-snapshot-check', 'druid-fire-claws-snapshot-check',
             'druid-rabies-snapshot-check', 'druid-rabies-infection-snapshot-check',
             'druid-rabies-lifecycle-snapshot-check', 'druid-hunger-snapshot-check',
             'shock-wave-casting-snapshot-check', 'shock-wave-expiry-stage-check',
             'druid-fury-snapshot-check', 'arctic-stream-expiry-stage-check')


def merge(config, other):
    for key in ('extra_families', 'extra_checks', 'extra_live_commands',
                'checked_live_commands', 'extra_connected_checks',
                'extra_target_input_commands', 'extra_ground_input_commands', 'extra_inputs'):
        config[key] = tuple(dict.fromkeys((*config.get(key, ()), *other.get(key, ()))))
    for key in ('extra_files', 'extra_wait_results', 'extra_manifest_names',
                'extra_check_timeouts', 'extra_input_timeouts'):
        config.setdefault(key, {}).update(other.get(key, {}))


def source_file(name):
    base = ROOT / '.local/class-skills'
    for path in (base / name, ROOT / 'tools/reference' / name,
                 base / name.removesuffix('-cases.json') / 'cases.json',
                 base / (name.removesuffix('-cases.json') + '-source') / 'cases.json'):
        if path.is_file():
            return path
    raise AssertionError('Missing cached reference: ' + name)


def add_fixture(config, fixture, family=True):
    path = ROOT / 'tests/Sanctuary.Integration' / (fixture + 'Checks.cs')
    text = path.read_text()
    commands = re.findall(r'(?:Register|RegisterCommand)\("([a-z0-9-]+)"', text)
    paired = [n.removesuffix('-check') for n in commands if n.endswith('-check')
              and '"' + n.removesuffix('-check') + '-reload-check"' in text]
    if family:
        assert paired, fixture
        name = paired[0]
        config['extra_families'] += (name,)
        manifest = re.search(r'private static string Manifest\s*=>\s*Path.Join\(Core.BaseDirectory,\s*"([a-z0-9-]+\.json)"', text)
        if manifest:
            config['extra_manifest_names'][name] = manifest.group(1)
        elif fixture == 'ShapeRestriction':
            config['extra_manifest_names'][name] = 'druid-form-world-fixtures.json'
        for suffix, result in (('-check', name + '-result'), ('-reload-check', name + '-reload-result')):
            if '"' + result + '"' in text or '"' + result + ': passed"' in text:
                config['extra_wait_results'][name + suffix] = result
    for name in sorted(set(re.findall(r'"([a-z0-9-]+\.json)"', text))):
        if any(token in name for token in ('source', 'probe', 'cases')):
            config['extra_files'].setdefault(name, source_file(name))


def element_wires(session, port, boot):
    for name in ELEMENT_INPUTS:
        with fixture_connection(session, port) as client:
            time.sleep(.15)
            session.send(name + '-input-check')
            assert session.expect(name + r'-input-check: (passed|failed)').group(1) == 'passed'
            ready = session.expect(name + r'-input: ready ability=(\d+) x=(\d+) y=(\d+) z=(-?\d+)')
            ability, x, y, z = map(int, ready.groups())
            client.sendall(struct.pack('>BHHBBIBIHHb', 0xBF, 21, 0xF2, 7, 3, 1, ability, 0, x, y, z))
            assert session.expect(name + r'-input-result: (passed|failed)', timeout=20).group(1) == 'passed'
    return 2 * len(ELEMENT_INPUTS)


def shock_wave_live(session, port, boot):
    # The existing live check reads the component fixture's saved manifest.
    # Run it after all fixture families have created their original records.
    with fixture_connection(session, port):
        session.send('shock-wave-live-check')
        assert session.expect(r'shock-wave-live-check: (passed|failed)').group(1) == 'passed'
        assert session.expect(r'shock-wave-live-result: (passed|failed)', timeout=15).group(1) == 'passed'
    return 2


def arctic_wires(session, port, boot):
    # Retain the approved Arctic packets from check_arctic_held_input.wires,
    # with its own caster/socket through the actual save boundary.
    for source in (0, 1, 9):
        label = f'arctic-held-wire-{source}'
        with fixture_connection(session, port) as client:
            session.send(label + '-check')
            ready = session.expect(label + r': ready ability=(\d+) x=(\d+) y=(\d+) z=(-?\d+)')
            ability, x, y, z = map(int, ready.groups())
            sequence = 0

            def send(action, input_source=source, replay=False):
                nonlocal sequence
                if not replay:
                    sequence += 1
                if action == 11:
                    packet = struct.pack('>BHHBBIB', 0xBF, 12, 0xF2, 7, action, sequence, input_source)
                else:
                    packet = struct.pack('>BHHBBIBBIHHb', 0xBF, 22, 0xF2, 7, action,
                                         sequence, input_source, ability, 0, x, y, z)
                client.sendall(packet)

            send(10); send(9); send(9, replay=True); send(9)
            session.expect(label + ': pressed')
            for _ in range(4):
                time.sleep(.15)
                send(10)
            session.expect(label + ': burning')
            send(11, input_source=(source + 1) % 14); send(10); send(11); send(10)
            assert session.expect(label + r'-result: (passed|failed)', timeout=10).group(1) == 'passed'
    client = socket.create_connection(('127.0.0.1', port), timeout=5)
    OPEN.append(client)
    threading.Thread(target=drain, args=(client,), daemon=True).start()
    name = 'arctic-held-input-save-stage-check'
    session.send(name)
    assert session.expect(name + r': (passed|failed)').group(1) == 'passed'
    threading.Thread(target=keepalive, args=(client,), daemon=True).start()
    return 4


def fury_save(session, port, boot):
    client = socket.create_connection(('127.0.0.1', port), timeout=5)
    OPEN.append(client)
    threading.Thread(target=drain, args=(client,), daemon=True).start()
    session.send('druid-fury-stage-check')
    seen = set()
    while len(seen) < 2:
        marker = session.expect(r'druid-fury-stage-(check|result): (passed|failed)')
        assert marker.group(2) == 'passed' and marker.group(1) not in seen
        seen.add(marker.group(1))
    threading.Thread(target=keepalive, args=(client,), daemon=True).start()
    return 2


def configuration():
    original = vines()
    families = original['extra_families'][:13]
    config = dict(stage=STAGE, extra_families=families,
                  extra_files=dict(original['extra_files']), extra_inputs=original['extra_inputs'],
                  extra_checks=original['extra_checks'][:5],
                  extra_wait_results={'vine-cast-windup-live-check': 'vine-cast-windup-live-result'},
                  extra_connected_checks=('vine-cast-windup-live-check',),
                  extra_live_commands=('creeper-learned-live', 'recycler-learned-live'),
                  extra_target_input_commands=('carrion-right', 'carrion-quick', 'solar-right', 'solar-quick',
                                               'creeper-learned-right', 'creeper-learned-quick'),
                  extra_ground_input_commands=('carrion-right', 'carrion-quick', 'solar-right', 'solar-quick',
                                               'creeper-learned-right', 'creeper-learned-quick'),
                  extra_manifest_names={})
    merge(config, raven(compact=True))
    captured = beasts()
    config['extra_files'].update(captured['extra_files'])
    config['extra_inputs'] += captured['extra_inputs']
    config['extra_families'] += captured['extra_families'][:7]
    config['extra_checks'] += captured['extra_checks'][:3]
    config['extra_connected_checks'] += captured['extra_checks'][:3]
    config['extra_wait_results'].update({n: captured['extra_wait_results'][n] for n in captured['extra_checks'][:3]})
    merge(config, arctic(isolate=True))
    config['extra_families'] += ('arctic-held-input',)
    config['extra_connected_checks'] += ('arctic-held-input-check', 'arctic-held-input-reload-check')
    for fixture in CORE:
        add_fixture(config, fixture)
    for fixture in MODELS:
        add_fixture(config, fixture, False)
    config['extra_manifest_names']['druid-form-attack'] = 'druid-form-world-fixtures.json'
    # Verify the exact saved Firestorm pool before the clock control makes new
    # legitimate casts and advances its global allocation counter. The normal
    # later wire control records those allocations for the next save.
    config['extra_early_reload_families'] = ('firestorm',)
    config['extra_checks'] += ('druid-human-cast-clock-check', 'druid-form-model-check',
                               'druid-form-rate-check', 'druid-charge-model-check',
                               'druid-rabies-packet-check', 'druid-fire-claws-scalars-check',
                               'druid-form-basic-block-check', 'druid-hunger-model-check',
                               'druid-form-delay-live-check', 'druid-form-live-check',
                               'druid-form-reaction-live-check', 'druid-feral-live-check',
                               'druid-maul-live-check')
    config['extra_wait_results'].update({
        'druid-form-delay-live-check': 'druid-form-delay-result',
        **{n + '-check': n + '-result' for n in ('druid-form-live', 'druid-form-reaction-live',
                                               'druid-feral-live', 'druid-maul-live', 'shock-wave-live')},
    })
    config['extra_target_input_commands'] += tuple(n + '-' + mode for n in (
        'spirit-wolf', 'dire-wolf', 'grizzly', 'oak', 'wolverine', 'barbs',
        'rabies', 'hunger', 'fire-claws', 'feral', 'maul') for mode in ('right', 'quick'))
    config['extra_ground_input_commands'] += tuple(n + '-' + mode for n in (
        'spirit-wolf', 'dire-wolf', 'grizzly', 'oak', 'wolverine', 'barbs') for mode in ('right', 'quick'))
    config['extra_target_input_commands'] += ('form-wolf-right', 'form-bear-quick',
        'form-wolf-unshift-quick', 'form-bear-unshift-right', 'bear-interrupt-quick',
        'fury-left', 'fury-right', 'fury-quick', 'shock-wave-left', 'shock-wave-right', 'shock-wave-quick',
        'druid-human-cast-clock-right', 'druid-human-cast-clock-quick')
    config['extra_ground_input_commands'] += ('form-wolf-right', 'form-bear-quick',
        'form-wolf-unshift-quick', 'form-bear-unshift-right', 'bear-interrupt-quick')
    config['extra_wire_callbacks'] = (shock_wave_live, element_wires, arctic_wires)
    config['extra_pre_save_callbacks'] = (fury_save,)
    config['extra_pre_save_commands'] = SNAPSHOTS
    config['between_boot_delay'] = 5
    config['extra_connection_settle'] = .15
    config['extra_families'] = tuple(dict.fromkeys(config['extra_families']))
    config['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'tests/check_assassin_planned_acceptance.py',
        ROOT / 'tests/check_arctic_held_input.py', ROOT / 'tests/check_druid_fury.py',
        ROOT / 'tools/reference/item-content-scope-decision.json',
        ROOT / 'tools/reference/summon-snapshot-decision.json',
        ROOT / 'tools/reference/rules/arctic-approved-input-rule.md')
    for key in ('extra_families', 'extra_checks', 'extra_target_input_commands'):
        assert len(config[key]) == len(set(config[key])), key
    for path in (*config['extra_files'].values(), *config['extra_inputs']):
        assert path.is_file(), path
    return config


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', action='store_true')
    args = parser.parse_args()
    config = configuration()
    if args.plan:
        print(json.dumps({key: len(config[key]) for key in ('extra_families', 'extra_checks',
            'extra_target_input_commands', 'extra_live_commands', 'extra_files', 'extra_pre_save_commands')}, indent=2))
        print('Nine elemental wire sequences, three held Arctic sequences and a paid Fury save stage per boot; cached references only.')
    else:
        os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
        try:
            with paced_clients():
                check(**config)
        finally:
            for client in OPEN:
                client.close()
