#!/usr/bin/env python3
"""Approved Fury held input, retained fire/cold channels and independent saves."""
import argparse
import os
import socket
import struct
import threading
import time
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_arctic_held_input import configuration as prior, OPEN, drain, keepalive


def fury_wires(session, port, boot):
    for source in (0, 1, 9):
        label = f'fury-held-wire-{source}'
        with socket.create_connection(('127.0.0.1', port), timeout=5) as client:
            threading.Thread(target=drain, args=(client,), daemon=True).start()
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
                    packet = struct.pack('>BHHBBIBBIHHb', 0xBF, 22, 0xF2, 7, action, sequence,
                                         input_source, ability, 0, x, y, z)
                client.sendall(packet)

            send(10); send(9); send(9, replay=True); send(9)
            session.expect(label + ': pressed')
            for _ in range(4):
                time.sleep(.15)
                send(10)
            session.expect(label + ': burning')
            send(11, input_source=(source + 1) % 14); send(10); send(11); send(10)
            assert session.expect(label + r'-result: (passed|failed)', timeout=10).group(1) == 'passed'
        time.sleep(.15)
    client = socket.create_connection(('127.0.0.1', port), timeout=5)
    OPEN.append(client)
    threading.Thread(target=drain, args=(client,), daemon=True).start()
    command = 'fury-held-input-save-stage-check'
    session.send(command)
    assert session.expect(command + r': (passed|failed)', timeout=30).group(1) == 'passed'
    threading.Thread(target=keepalive, args=(client,), daemon=True).start()
    return 4


def configuration(retained=False, isolate=False, shared=False):
    c = prior(retained, isolate, shared)
    suffix = '-shared' if shared else '-isolate' if isolate else '-retained' if retained else ''
    c['stage'] = ROOT / ('.local/class-skills/blade-fury-held-input' + suffix)
    c['extra_families'] += ('blade-fury-held-input',)
    c['extra_connected_checks'] += ('blade-fury-held-input-check', 'blade-fury-held-input-reload-check')
    c['extra_wire_callbacks'] += (fury_wires,)
    c['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'docs/blade-fury-approved-input-rule.md')
    return c


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for flag in ('retained', 'isolate', 'shared'):
        parser.add_argument('--' + flag, action='store_true')
    args = parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    try:
        check(**configuration(args.retained, args.isolate, args.shared))
    finally:
        for client in OPEN:
            client.close()
