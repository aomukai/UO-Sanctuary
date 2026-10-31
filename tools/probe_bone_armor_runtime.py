#!/usr/bin/env python3
"""Inspect the pinned event22 candidate without forcing inaccessible pages."""
import argparse
import json
import struct
from pathlib import Path

from pe_unwind_groups import UnwindGroups
from probe_warlock_event_reference import immutable_table
from probe_warlock_runtime import ROOT, Image, CodeReader, digest, disassemble

STAGE = ROOT / '.local/class-skills/bone-armor-current-reference'
RECEIPT = ROOT / 'tools/reference/bone-armor-current-reference.json'


def capture():
    assert not RECEIPT.exists(), 'Preserve prior observations'
    STAGE.mkdir(parents=True, exist_ok=True)
    image = Image()
    reader = CodeReader(image)
    artifacts = []
    records = []
    try:
        reader.anchors()
        table = immutable_table(reader)
        assert table == image.bytes(0x163a750, 41 * 8)
        rva = struct.unpack('<41Q', table)[22] - image.base
        for begin, end, unwind in UnwindGroups(image).fragments(rva):
            size = end - begin
            entry = next((m for m in reader.maps
                          if m[0] <= image.base + begin and image.base + end <= m[1]), None)
            row = dict(rva=hex(begin), endRva=hex(end), unwindRva=hex(unwind),
                       permissions=entry[2] if entry else 'cross-map', captured=False)
            if entry and 'r' in entry[2] and 'x' in entry[2] and size <= 4096 and reader.total + 2 * size <= 8192:
                data = reader.stable(begin, size, 'code')
                binary = STAGE / f'event22-{begin:x}.bin'
                binary.write_bytes(data)
                assembly, bad = disassemble(binary, begin, image.base)
                artifacts += [binary, assembly]
                row.update(captured=True, sha256=digest(data), invalidInstructionMarkers=bad)
            else:
                row['reason'] = 'Not readable executable within the bounded capture; no forced read'
            records.append(row)
    finally:
        reader.close()
    artifacts += [Path(__file__).resolve(), ROOT / 'tools/probe_warlock_runtime.py',
                  ROOT / 'tools/probe_warlock_event_reference.py', ROOT / 'tools/pe_unwind_groups.py',
                  image.reference]
    result = dict(referenceVersion='3.3.93847', executableSha256=image.ref['executable']['sha256'],
                  process=reader.identity, processMemoryRead=True, bytesRead=reader.total, reads=reader.reads,
                  immutableCandidateTableRva='0x163a750', immutableCandidateTableMatches=True,
                  candidateSlot=22, candidateRva=hex(rva), fragments=records,
                  allUnwindFragmentsCaptured=all(r['captured'] for r in records),
                  semanticCallbackIdentityVerified=False, modernOverflowVerified=False,
                  processInput=False, ptraceAttach=False, securitySettingsChanged=False, gameMemoryWritten=False,
                  artifacts={str(p.relative_to(ROOT)): digest(p.read_bytes()) for p in artifacts},
                  remaining=['Slot22 is a candidate, not a dispatch/semantic identity proof. Even a complete readable body requires identity and control-flow review.',
                             'Unavailable code leaves modern breaking-hit behavior unresolved. Retain the explicit source discrepancy and continue unaffected mechanics.'])
    RECEIPT.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({k: result[k] for k in ['candidateRva', 'bytesRead', 'allUnwindFragmentsCaptured', 'modernOverflowVerified']}))


def check():
    result = json.loads(RECEIPT.read_text())
    image = Image()
    assert result['referenceVersion'] == '3.3.93847'
    assert result['executableSha256'] == image.ref['executable']['sha256']
    assert result['candidateSlot'] == 22 and result['immutableCandidateTableMatches']
    pointers = struct.unpack('<41Q', image.bytes(0x163a750, 41 * 8))
    assert int(result['candidateRva'], 16) == pointers[22] - image.base
    fragments = UnwindGroups(image).fragments(int(result['candidateRva'], 16))
    assert [(int(r['rva'], 16), int(r['endRva'], 16), int(r['unwindRva'], 16))
            for r in result['fragments']] == fragments
    assert result['allUnwindFragmentsCaptured'] == all(r['captured'] for r in result['fragments'])
    assert result['bytesRead'] == sum(2 * r['size'] for r in result['reads']) <= 8192
    assert all(r['stable'] and 'r' in r['permissions'] for r in result['reads'])
    assert all(not r['captured'] or 'r' in r['permissions'] and 'x' in r['permissions'] for r in result['fragments'])
    assert not any(result[k] for k in ['semanticCallbackIdentityVerified', 'modernOverflowVerified',
                                     'processInput', 'ptraceAttach', 'securitySettingsChanged', 'gameMemoryWritten'])
    for path, sha in result['artifacts'].items():
        assert digest((ROOT / path).read_bytes()) == sha, path
    print('Bone Armor current-reference observation verified offline; modern overflow remains unverified.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--capture', action='store_true')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    assert args.capture != args.check
    capture() if args.capture else check()
