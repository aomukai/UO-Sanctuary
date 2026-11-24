#!/usr/bin/env python3
"""Current Assassin martial arts, buffs, traps, held Fury and original Shadows."""
import argparse
from contextlib import contextmanager
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
from fists_claw_input import exercise as claw_input
from check_dragon_flight import configuration as flight
from check_blade_cast import configuration as sentinel
from check_wake_fire_automatic import configuration as sentries
from check_shadow_buffs import configuration as shadow
from check_arctic_held_input import OPEN, drain, keepalive

STAGE = ROOT / '.local/class-skills/assassin-planned-acceptance'
CORE = ('CobraStrike', 'FistsFire', 'FistsClaw', 'ThunderClaw', 'ThunderRelease',
        'IceClaw', 'IceRelease', 'PhoenixBuilder', 'PhoenixRelease', 'ClawMastery',
        'PsychicHammer', 'BurstSpeed', 'Cloak', 'WeaponBlock', 'Fade', 'MindBlast',
        'Venom', 'FireBlast', 'ShockWeb', 'BladeShield')
LEGACY = ('tiger-strike', 'cobra-strike', 'fists-claw', 'thunder-claw', 'ice-claw',
          'phoenix-builder', 'fire-blast', 'shock-web', 'psychic-hammer',
          'burst-speed', 'fade', 'venom', 'mind-blast', 'cloak')
CLAW_INPUTS = ('fists-claw', 'thunder-claw', 'ice-claw', 'phoenix-builder')


@contextmanager
def paced_clients():
    """Pace this disposable runner's sockets; leave native security intact."""
    original = socket.create_connection
    next_connect = 0.0

    def connect(*args, **kwargs):
        nonlocal next_connect
        remaining = next_connect - time.monotonic()
        if remaining > 0:
            time.sleep(remaining)
        next_connect = time.monotonic() + 1.1
        return original(*args, **kwargs)

    socket.create_connection = connect
    try:
        yield
    finally:
        socket.create_connection = original


def legacy_wires(session, port, boot):
    for name in LEGACY:
        with fixture_connection(session, port) as client:
            time.sleep(.15)
            if name in CLAW_INPUTS:
                claw_input(session, client, Path(session.log.name), prefix=name)
                continue
            session.send(name + '-input-check')
            assert session.expect(name + r'-input-check: (passed|failed)').group(1) == 'passed'
            phases = [(name + '-input', 1)]
            if name == 'cobra-strike':
                phases += [('cobra-strike-input-finisher', 2)]
            elif name == 'cloak':
                phases += [('cloak-input: reject-ready', 2), ('cloak-input: unlocked-ready', 3)]
            for label, sequence in phases:
                targeted = name in ('tiger-strike', 'cobra-strike')
                if ': ' in label:
                    pattern = label + r' ability=(\d+) x=(\d+) y=(\d+) z=(-?\d+)'
                else:
                    pattern = label + r': ready ability=(\d+) '
                    pattern += r'target=(\d+) ' if targeted else ''
                    pattern += r'x=(\d+) y=(\d+) z=(-?\d+)'
                values = tuple(map(int, session.expect(pattern).groups()))
                ability, target, x, y, z = values if targeted else (values[0], 0, *values[1:])
                client.sendall(struct.pack('>BHHBBIBIHHb', 0xBF, 21, 0xF2, 7, 3,
                                           sequence, ability, target, x, y, z))
            assert session.expect(name + r'-input-result: (passed|failed)', timeout=15).group(1) == 'passed'
    return 2 * len(LEGACY)


def fury_wires(session, port, boot):
    # Same approved held packets as check_blade_fury_held_input.fury_wires;
    # reuse the established drain/shutdown helper for completed wire scenes.
    for source in (0, 1, 9):
        label = f'fury-held-wire-{source}'
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
    command = 'fury-held-input-save-stage-check'
    session.send(command)
    assert session.expect(command + r': (passed|failed)', timeout=30).group(1) == 'passed'
    threading.Thread(target=keepalive, args=(client,), daemon=True).start()
    return 4


def merge(config, other):
    for key in ('extra_families', 'extra_checks', 'extra_connected_checks',
                'extra_target_input_commands', 'extra_ground_input_commands',
                'extra_instant_input_commands', 'extra_inputs'):
        config[key] = tuple(dict.fromkeys((*config.get(key, ()), *other.get(key, ()))))
    for key in ('extra_files', 'extra_wait_results', 'extra_manifest_names',
                'extra_check_timeouts', 'extra_input_timeouts'):
        config.setdefault(key, {}).update(other.get(key, {}))


def configuration():
    config = flight()
    config['stage'] = STAGE
    omitted = ('cleave', 'mirrored-blades', 'player-teleport')
    config['extra_families'] = tuple(n for n in config['extra_families'] if n not in omitted)
    config['extra_checks'] = tuple(n for n in config['extra_checks'] if n != 'leap-geometry-check')
    config['extra_target_input_commands'] = tuple(n for n in config['extra_target_input_commands'] if not n.startswith('player-teleport-'))
    config['extra_connected_checks'] = ()
    config['extra_ground_input_commands'] = ()
    config['extra_wait_results'] = {}
    config.pop('save_command'); config.pop('save_result')
    merge(config, sentinel())
    merge(config, sentries())
    config['extra_files'].update(shadow()['extra_files'])
    for fixture in CORE:
        path = ROOT / 'tests/Sanctuary.Integration' / (fixture + 'Checks.cs')
        text = path.read_text()
        family = re.search(r'Register\("([a-z0-9-]+)-check"', text).group(1)
        config['extra_families'] += (family,)
        manifest = re.search(r'private static string Manifest=>Path.Join\(Core.BaseDirectory,"([a-z0-9-]+\.json)"', text)
        if manifest:
            config['extra_manifest_names'][family] = manifest.group(1)
        for name in sorted(set(re.findall(r'"([a-z0-9-]+(?:source|probe)[a-z0-9-]*\.json)"', text))):
            if name not in config['extra_files']:
                source = ROOT / '.local/class-skills' / name
                if not source.is_file():
                    source = ROOT / 'tools/reference' / name
                assert source.is_file(), (fixture, name)
                config['extra_files'][name] = source
    config['extra_families'] += ('sentry-placement', 'wake-learned', 'blade-fury-held-input',
                                 'shadow-cast', 'shadow-master-world', 'shadow-fury-cast')
    config['extra_families'] = tuple(dict.fromkeys(config['extra_families']))
    config['extra_checks'] += ('assassin-cast-clock-check', 'wake-learned-mechanics-check')
    config['extra_files'].update({
        'assassin-cast-clock-source-probe.json': ROOT / '.local/class-skills/assassin-cast-clock-source-probe.json',
        'shadow-fury-action-cases.json': ROOT / '.local/class-skills/shadow-fury-action-source/cases.json',
    })
    for family in ('psychic-hammer', 'burst-speed', 'cloak', 'fade', 'venom',
                   'fire-blast', 'shock-web', 'mind-blast'):
        config['extra_wait_results'].update({family + '-check': family + '-result',
                                            family + '-reload-check': family + '-reload-result'})
    config['extra_wait_results']['weapon-block-check'] = 'weapon-block-result'
    config['extra_wait_results'].update({
        'blade-shield-check': 'blade-shield-result', 'blade-shield-reload-check': 'blade-shield-result',
        'wake-learned-check': 'wake-learned-result', 'wake-learned-reload-check': 'wake-learned-reload-result',
    })
    config['extra_connected_checks'] += ('wake-learned-check', 'wake-learned-reload-check',
                                       'blade-fury-held-input-check', 'blade-fury-held-input-reload-check')
    config['extra_target_input_commands'] += ('wake-learned-left', 'wake-learned-right', 'wake-learned-quick',
                                            'assassin-cast-clock-right', 'assassin-cast-clock-quick',
                                            'shadow-cast-right', 'shadow-cast-quick')
    config['extra_ground_input_commands'] += ('wake-learned-left', 'wake-learned-right', 'wake-learned-quick')
    config['extra_instant_input_commands'] += ('wake-learned-left',)
    config['extra_wire_callbacks'] = (legacy_wires, fury_wires)
    config['extra_connection_settle'] = .15
    config['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'tests/check_class_skills.py',
                              ROOT / 'tests/fists_claw_input.py', ROOT / 'tests/check_blade_fury_held_input.py',
                              ROOT / 'tools/reference/item-content-scope-decision.json',
                              ROOT / 'tools/reference/wake-learned-sanctuary-decision.json',
                              ROOT / 'tools/reference/rules/blade-fury-approved-input-rule.md')
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
        print(json.dumps({key: len(config[key]) for key in (
            'extra_families', 'extra_checks', 'extra_target_input_commands', 'extra_files')}, indent=2))
        print(f'{len(LEGACY)} legacy wire sequences and three Fury held sequences per boot; cached references only.')
    else:
        os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
        try:
            with paced_clients():
                check(**config)
        finally:
            for client in OPEN:
                client.close()
