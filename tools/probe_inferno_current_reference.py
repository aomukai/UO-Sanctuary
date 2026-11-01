#!/usr/bin/env python3
"""Record Inferno's corroborated start-table candidate and page permissions only.

No process memory is opened. This does not establish dispatcher linkage or
callback semantics, and never changes page protection or sends game input.
"""
import argparse
import json
from pathlib import Path
import struct

from pe_unwind_groups import UnwindGroups
from probe_barbarian_callback_tables import OUT as TABLE_EVIDENCE, record as table_check
from probe_warlock_runtime import ROOT, Image, digest, identity, processes

OUT = ROOT / 'tools/reference/inferno-current-reference-preflight.json'


def static(image):
    table = json.loads(TABLE_EVIDENCE.read_text())
    assert table['executableSha256'] == image.ref['executable']['sha256']
    assert not table['dispatcherIndexLinkVerified']
    address = int(table['staticCorroboration']['tableCandidateRva'], 16)
    skills = json.loads((ROOT / 'data/Sanctuary/Balance/class-skills.json').read_text())
    row = next(r for r in skills if r['sourceId'] == 41)
    assert row['sourceFields']['srvstfunc'] == 11 and row['sourceFields']['srvdofunc'] == 19
    pointer, = struct.unpack('<Q', image.bytes(address + 11 * 8, 8))
    fragments = UnwindGroups(image).fragments(pointer - image.base)
    return dict(tableCandidateRva=hex(address), slot=11, skillId=41,
                candidateRva=hex(pointer - image.base),
                fragments=[dict(rva=hex(a), endRva=hex(b), unwindRva=hex(u)) for a, b, u in fragments])


def record(check=False):
    table_check(check=True)
    image = Image()
    candidate = static(image)
    if check:
        result = json.loads(OUT.read_text())
        assert result['candidate'] == candidate
        assert result['executableSha256'] == image.ref['executable']['sha256']
        assert result['processMemoryRead'] is False and result['bytesRead'] == 0 and not result['processImageVerified']
        assert result['completeCandidateReadable']==all(p['readableExecutable'] for p in result['observedPermissions'])
        assert all(result[k] is False for k in ('processInput', 'ptraceAttach', 'securitySettingsChanged',
                                               'gameMemoryWritten', 'dispatcherLinkVerified', 'callbackSemanticsVerified'))
        for name, sha in result['artifacts'].items():
            assert digest((ROOT / name).read_bytes()) == sha, name
    else:
        assert not OUT.exists(), 'Preserve the existing observation'
        matches = processes()
        assert len(matches) == 1, 'One owned D2R process is required for map metadata'
        process = matches[0]
        mappings = []
        for line in Path('/proc', str(process['pid']), 'maps').read_text().splitlines():
            parts = line.split()
            a, b = (int(v, 16) for v in parts[0].split('-'))
            mappings.append((a, b, parts[1]))
        observed = []
        for fragment in candidate['fragments']:
            lo = image.base + int(fragment['rva'], 16)
            hi = image.base + int(fragment['endRva'], 16)
            regions = [(max(a, lo), min(b, hi), perms) for a, b, perms in mappings if a < hi and b > lo]
            covered = sum(b - a for a, b, _ in regions) == hi - lo
            observed.append(dict(rva=fragment['rva'], mappingCoverageComplete=covered,
                readableExecutable=covered and all('r' in p and 'x' in p for _, _, p in regions),
                permissions=[p for _, _, p in regions]))
        assert identity(process['pid']) == process, 'Process identity changed during metadata inspection'
        files = [Path(__file__).resolve(), TABLE_EVIDENCE, image.reference,
                 ROOT / 'tools/probe_warlock_runtime.py', ROOT / 'tools/pe_unwind_groups.py',
                 ROOT / 'tools/probe_barbarian_callback_tables.py',
                 ROOT / 'data/Sanctuary/Balance/class-skills.json']
        result = dict(referenceVersion='3.3.93847', executableSha256=image.ref['executable']['sha256'],
            candidate=candidate, process=process, observedPermissions=observed,
            completeCandidateReadable=all(p['readableExecutable'] for p in observed),
            processMemoryRead=False, bytesRead=0, processImageVerified=False, processInput=False, ptraceAttach=False,
            securitySettingsChanged=False, gameMemoryWritten=False, dispatcherLinkVerified=False,
            callbackSemanticsVerified=False, handlerEnabled=False,
            artifacts={str(p.relative_to(ROOT)): digest(p.read_bytes()) for p in files},
            remaining=[
                'Slot11 is from the corroborated candidate table; current dispatcher/index linkage and callback identity remain unverified.',
                'The process is selected by owned D2R.exe command identity. No in-memory PE identity or code bytes are verified;addresses come from the separately hashed pinned executable file.',
                'This observation records map permissions only. No protected code is read or forced. Readability can change with the process context.',
                'Modern accepted start/reentry/payment frequency and release composition remain unresolved. Existing checked channel/sequence and stream components do not establish that cadence.'])
        OUT.write_text(json.dumps(result, indent=2) + '\n')
    print('Inferno current-reference metadata verified; no process memory read or callback semantics established.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
