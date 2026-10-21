#!/usr/bin/env python3
"""Compare original v0-v2 character/v0 binding bytes with a copied-world migration snapshot.

Read-only: use the matching stopped pre-T39 backup; no live saves are rewritten.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct


class Reader:
    def __init__(self, path):
        self.path = path
        self.data = path.read_bytes()
        self.at = 0

    def read(self, format):
        result, = struct.unpack_from('<' + format, self.data, self.at)
        self.at += struct.calcsize('<' + format)
        return result

    def string(self):
        if not self.read('B'):
            return None
        length = shift = 0
        for _ in range(5):
            value = self.read('B')
            length |= (value & 127) << shift
            if value < 128:
                break
            shift += 7
        else:
            raise ValueError('Invalid encoded string length')
        end = self.at + length
        if end > len(self.data):
            raise ValueError('Truncated string')
        value = self.data[self.at:end].decode('utf-8')
        self.at = end
        return value

    def finish(self):
        assert self.at == len(self.data), f'Unconsumed bytes: {self.path}'


def check(saves, snapshot, output):
    r = Reader(saves / 'SanctuaryCharacters/SanctuaryCharacters.bin')
    version, count = r.read('i'), r.read('i')
    assert version in (0, 1, 2) and 0 <= count <= 1_000_000, 'Expected original pre-T39 character data'
    histories = {}
    for _ in range(count):
        serial = r.read('I')
        profile = dict(Class=r.read('B'), Level=r.read('i'), Experience=r.read('q'))
        allocation = {name: r.read('i') if version >= 2 else 0 for name in ('Strength', 'Dexterity', 'Energy', 'Vitality')}
        ranks = {}
        for _ in range(r.read('i') if version >= 1 else 0):
            name = r.string()
            ranks[name] = r.read('B')
        assert serial not in histories
        histories[serial] = dict(Profile=profile, Allocation=allocation, Ranks=ranks)
    r.finish()
    binding_path = saves / 'SanctuaryBindings/SanctuaryBindings.bin'
    b = Reader(binding_path)
    assert b.read('i') == 0, 'Expected original version0 mouse bindings'
    bindings = {b.read('I'): (b.read('B'), b.read('B')) for _ in range(b.read('i'))}
    b.finish()
    players = {p['Serial']: p for p in json.loads(snapshot.read_text())['Players']}
    for serial, old in histories.items():
        p = players[serial]
        assert p['Profile'] == old['Profile']
        assert len(p['ClassHistories']) == 1
        migrated = p['ClassHistories'][0]
        assert migrated['Profile'] == old['Profile'] and migrated['Ranks'] == old['Ranks']
        assert {k: migrated['Allocation'][k] for k in old['Allocation']} == old['Allocation']
        assert len(p['ClassBindings']) == 1
        bound = p['ClassBindings'][0]
        default = (1, 2 if old['Profile']['Class'] == 103 else 0)
        assert bound['Class'] == old['Profile']['Class'] and (bound['Left'], bound['Right']) == bindings.get(serial, default)
        assert bound['Quick'] == [0] * 12
    report = dict(characters=len(histories), character_version=version, bindings_version=0,
                  original_character_sha256=hashlib.sha256(r.data).hexdigest(),
                  original_bindings_sha256=hashlib.sha256(b.data).hexdigest(),
                  original_histories=histories, original_bindings=bindings,
                  migrated_snapshot=str(snapshot), result='all_original_values_preserved')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + '\n')
    print(f'Raw legacy migration: {len(histories)} class character(s); IDs, level, XP, allocations, ranks and mouse bindings preserved; quick bars empty.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--legacy-saves', type=Path, required=True)
    parser.add_argument('--snapshot', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    check(args.legacy_saves, args.snapshot, args.output)
