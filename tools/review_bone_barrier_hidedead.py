#!/usr/bin/env python3
"""Owned hidedead metadata and bounded immutable-table reference;no game input."""
import argparse
import csv
import hashlib
import json
import os
import re
import struct
import subprocess
from pathlib import Path
from probe_warlock_runtime import Image, CodeReader, identity, digest, disassemble
from pe_unwind_groups import UnwindGroups

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/bone-barrier-death-phase-source'
INIT = 0x163b270
CALLBACKS = 0x1723650
sha = lambda p: digest(p.read_bytes())


def metadata():
    image = Image()
    installation = image.ref
    extraction = json.loads((STAGE / 'extraction.json').read_text())
    assert extraction['referenceVersion'] == installation['referenceVersion'] == '3.3.93847'
    assert extraction['executableSha256'] == installation['executable']['sha256']
    assert extraction['buildInfoSha256'] == installation['buildInfo']['sha256']
    assert sha(Path(installation['installationPath']) / '.build.info') == extraction['buildInfoSha256']
    assert not extraction['processMemoryRead'] and not extraction['gameFilesWritten']
    reader = ROOT / '.local/class-skills/defiler-compiled-formula/casc-reference-reader'
    assert sha(reader) == extraction['readerSha256'] == '61f958eedae630f2294ad5bb960a824bc541dfcebea7827e9d474bcea417f063'
    tables = {}
    for name, info in extraction['files'].items():
        p = STAGE / name
        assert sha(p) == info['sha256'] and p.stat().st_size == info['bytes']
        tables[name] = list(csv.DictReader(p.read_text().splitlines(), delimiter='\t'))
    mod = next(row for row in tables['monumod.txt'] if row['uniquemod'] == 'hidedead')
    bone = next(row for row in tables['monstats2.txt'] if row['Id'] == 'bonewall')
    assert len(tables['monumod.txt']) == 45 and mod['id'] == '44' and mod['enabled'] == '1' and mod['version'] == '100'
    assert bone['SpawnUniqueMod'] == 'hidedead' and not bone['deadCol'] and bone['mDT'] == '1' and bone['mDD'] == '1'
    sourcepath = ROOT / '.local/class-skills/d2moo-pinned-source/source/D2Game/src/MONSTER/MonsterUnique.cpp'
    source = sourcepath.read_bytes()
    tree = json.loads((ROOT / '.local/class-skills/d2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    assert hashlib.sha1(b'blob ' + str(len(source)).encode() + b'\0' + source).hexdigest() == next(
        row['sha'] for row in tree['tree'] if row['path'] == 'source/D2Game/src/MONSTER/MonsterUnique.cpp')
    text = source.decode()
    block = text.split('constexpr UModInitFunc dword_6FD2F018[] =')[1].split('};')[0]
    names = [n.strip().strip(',') for n in block.splitlines() if n.strip() not in ('', '{')]
    assert len(names) == 44
    raw = image.bytes(INIT, 45 * 8)
    init = struct.unpack('<45Q', raw)
    assert all(bool(value) == (name != 'nullptr') for name, value in zip(names, init))
    assert init[8] == init[27] == init[28] and init[44] - image.base == 0x3d0280
    assert all(not p or image.section(p - image.base)['name'] == '.text' for p in init)
    block = text.split('constexpr D2UnkUModStrc stru_6FD2F0C8[43] =')[1].split('};')[0]
    callbacks = [n.strip() for row in re.findall(r'\{([^{}]+)\}', block) for n in row.split(',')]
    current = struct.unpack('<270Q', image.bytes(CALLBACKS, 45 * 6 * 8))
    assert len(callbacks) == 258
    differences = [i for i, (name, value) in enumerate(zip(callbacks, current)) if bool(value) != (name != 'nullptr')]
    assert differences == [114, 147] and not any(current[44 * 6:45 * 6])
    assert all(not p or image.section(p - image.base)['name'] == '.text' for p in current)
    return image, sourcepath, dict(id=44, enabled=True, version=100, boneWallSpawnModifier=bone['SpawnUniqueMod'],
        oldInitializerRows=44, oldCallbackRows=43, candidateInitializerRva=hex(INIT), candidateCallbackTableRva=hex(CALLBACKS),
        oldInitializerNullnessMatches=44, initializerSharedResistantAliases=3, callbackNullnessDifferences=differences,
        candidateSlot44Rva='0x3d0280', candidateSlot44DeathCallbacksEmpty=True,
        dispatcherLinkVerified=False, callbackSemanticIdentityVerified=False)


def immutable(reader, rva, size, name):
    assert reader.image.section(rva, size)['name'] == '.rdata'
    assert identity(reader.identity['pid']) == reader.identity
    va = reader.image.base + rva
    entry = next(p for p in reader.maps if p[0] <= va and va + size <= p[1])
    assert 'r' in entry[2] and 'w' not in entry[2] and reader.total + size * 2 <= 8192
    first = os.pread(reader.fd, size, va); second = os.pread(reader.fd, size, va); reader.total += size * 2
    assert first == second == reader.image.bytes(rva, size) and identity(reader.identity['pid']) == reader.identity
    reader.reads.append(dict(kind=name, rva=hex(rva), size=size, permissions=entry[2], stable=True, sha256=digest(first)))
    return first


def capture():
    target = STAGE / 'hidedead-capture.json'
    assert not target.exists(), 'Preserve prior capture;no repeated reads without an external-state change'
    image, _, _ = metadata(); reader = CodeReader(image); groups = UnwindGroups(image); artifacts = []; records = []
    try:
        reader.anchors()
        for rva, size, name in ((INIT, 45 * 8, 'candidate-initializers'), (CALLBACKS, 45 * 6 * 8, 'candidate-callbacks')):
            data = immutable(reader, rva, size, name); path = STAGE / (name + '.bin'); path.write_bytes(data); artifacts.append(path)
        for index, (begin, end, unwind) in enumerate(groups.fragments(0x3d0280)):
            va = image.base + begin; size = end - begin
            entry = next((m for m in reader.maps if m[0] <= va and va + size <= m[1]), None)
            record = dict(rva=hex(begin), endRva=hex(end), unwindRva=hex(unwind), permissions=entry[2] if entry else 'cross-map', captured=False)
            if entry and 'r' in entry[2] and 'x' in entry[2] and size <= 4096 and reader.total + size * 2 <= 8192:
                body = reader.stable(begin, size, 'code'); path = STAGE / ('initializer44-part' + str(index) + '.bin'); path.write_bytes(body)
                asm, bad = disassemble(path, begin, image.base); artifacts += [path, asm]
                record.update(captured=True, sha256=digest(body), invalidInstructionMarkers=bad)
            else:
                record['reason'] = 'Not readable executable code or outside the bounded budget;no forced read'
            records.append(record)
    finally:
        reader.close()
    result = dict(referenceVersion='3.3.93847', executableSha256=image.ref['executable']['sha256'], process=reader.identity,
        processMemoryRead=True, bytesRead=reader.total, reads=reader.reads, records=records,
        processInput=False, ptraceAttach=False, gameMemoryWritten=False, securitySettingsChanged=False,
        callbackSemanticIdentityVerified=False, artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts})
    target.write_text(json.dumps(result, indent=2) + '\n')
    print('Bounded hidedead candidate reference:', reader.total, 'bytes;captured code fragments:', sum(r['captured'] for r in records))


def record(check=False):
    image, sourcepath, values = metadata()
    capturepath = STAGE / 'hidedead-capture.json'; saved = json.loads(capturepath.read_text())
    assert saved['executableSha256'] == image.ref['executable']['sha256'] and saved['bytesRead'] <= 8192
    assert not any(saved[k] for k in ('processInput', 'ptraceAttach', 'gameMemoryWritten', 'securitySettingsChanged', 'callbackSemanticIdentityVerified'))
    for name, digest in saved['artifacts'].items():
        assert sha(ROOT / name) == digest, name
    assert all(not row['captured'] for row in saved['records']) and all(row['permissions'] == '---s' for row in saved['records'])
    assert (STAGE / 'candidate-initializers.bin').read_bytes() == image.bytes(INIT, 45 * 8)
    assert (STAGE / 'candidate-callbacks.bin').read_bytes() == image.bytes(CALLBACKS, 45 * 6 * 8)
    assert all(row['stable'] and 'r' in row['permissions'] for row in saved['reads'])
    installedpath = ROOT / 'tools/reference/creeper-frame-installation-evidence.json'
    installed = json.loads(installedpath.read_text()); assert len(installed['installedArtifacts']) == 106
    for name, value in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / name) == value, name
    protectedpath = ROOT / '.local/class-skills/creeper-frame-installation.json'
    protected = json.loads(protectedpath.read_text())['protected']
    for key, path, count in (('saves', '.local/server/Saves', 38), ('configuration', '.local/server/Configuration', 20)):
        folder = Path(installed['runtimeRoot']) / path
        assert len(protected[key]) == count and {str(p.relative_to(folder)): sha(p) for p in folder.rglob('*') if p.is_file()} == protected[key]
    parent = json.loads((ROOT / 'tools/reference/golem-mastery-native-evidence.json').read_text())
    assert parent['verified']['nativeMarkers'] == 423
    for name, value in parent['tested']['sourceInputs'].items():
        assert sha(ROOT / name) == value, name
    for name, value in parent['tested']['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == value, name
    accepted = ROOT / '.local/class-skills/golem-mastery'
    for name, value in parent['tested']['artifacts'].items():
        path = accepted / ('content' if name == 'Assemblies/Sanctuary.Content.dll' else 'integration') / Path(name).name if name.startswith('Assemblies/Sanctuary.') else accepted / 'native/Distribution' / name
        assert sha(path) == value, name
    files = [Path(__file__).resolve(), ROOT / 'tools/probe_warlock_runtime.py', ROOT / 'tools/pe_unwind_groups.py', sourcepath,
        ROOT / 'tools/reference/d2r-local-installation-evidence.json', installedpath, protectedpath, capturepath,
        ROOT / 'tools/reference/golem-mastery-native-evidence.json',
        *(ROOT / 'docs' / name for name in ('bone-barrier-hidedead-reference-checkpoint.md', 'bone-barrier-death-source-review.md',
                                           'class-skill-roadmap.md', 'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md')),
        *(STAGE / name for name in ('extraction.json', 'monumod.txt', 'monstats2.txt', 'monumod.txt.extract.log', 'monstats2.txt.extract.log',
                                   'umod-table-candidates.json', 'umod-table-near-candidates.json', 'umod-init-candidates.json'))]
    result = dict(referenceVersion='3.3.93847', component='Bone barrier hidedead44 reference boundary', reference=values,
        productionChanged=False, nativeBaselineMarkers=423, goalComplete=False, completedClasses=[],
        established=['Owned CASC monumod45rows assigns enabled/version100hidedead to ID44;bonewall SpawnUniqueMod names it.',
            'The older death proof omits this modern modifier:its dispatch table has43rows and its initializer has44rows.',
            'A pinned-file initializer candidate matches all44classic nullness positions and the three shared Resistant aliases. Slot44 points to0x3d0280. Its immutable runtime bytes match the pinned executable. Dispatcher and semantic identity are still unverified.',
            'The near callback candidate differs in two classic nullness positions and its slot44 is empty. This does not establish that hidedead has no effect;it may act through initialization or another path.',
            'All candidate code fragments are on observed ---s pages. No code read was attempted;no input,attach,permission change or game write occurred.'],
        next=['Keep final barrier death/removal and initial owner/minion graph locally deferred. Do not delete at ENDANIM or infer a hidden-corpse lifetime from the modifier name.',
            'A future convenient Bone Wall or Hydra summon may activate this initializer;that is a possible external change,not guaranteed reference data. Do not repeat the inaccessible-page probe until state changes.',
            'Continue unaffected Necromancer/class work. Existing423check baseline,production C#/runtime data,installed106artifacts/38saves/20configuration files remain unchanged.'],
        evidence={str(p.relative_to(ROOT)): sha(p) for p in files})
    target = ROOT / 'tools/reference/bone-barrier-hidedead-reference-evidence.json'; text = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == text
    else:
        target.write_text(text)
    print('Owned hidedead44/current initializer candidate established;code inaccessible;death/removal remains deferred;423check baseline preserved.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--capture', action='store_true'); parser.add_argument('--check', action='store_true'); args = parser.parse_args()
    assert not (args.capture and args.check)
    if args.capture:
        capture()
    else:
        record(args.check)
