#!/usr/bin/env python3
"""Freeze Sigil Death's owned data and bounded code evidence; --check is offline."""
import argparse
import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def record(check=False):
    inputs = {}

    def pin(path):
        raw = path.read_bytes()
        inputs[str(path.relative_to(ROOT))] = hashlib.sha256(raw).hexdigest()
        return raw

    def capture(path):
        result = json.loads(pin(path))
        assert result['referenceVersion'] == '3.3.93847'
        assert result['bytesRead'] <= 8192
        assert result['bytesRead'] == 2 * sum(r['size'] for r in result['reads'])
        assert all(r['stable'] and 'r' in r['permissions'] and
                   (r['kind'] != 'code' or 'x' in r['permissions']) for r in result['reads'])
        assert not any(result[k] for k in ['processInput', 'ptraceAttach',
                                          'securitySettingsChanged', 'gameMemoryWritten'])
        for name, digest in result['artifacts'].items():
            assert hashlib.sha256(pin(ROOT / name)).hexdigest() == digest, name
        return result

    live = STAGE / 'warlock-runtime-reference'
    events = capture(live / 'sigil-death-events-20261109.json')
    explosion = capture(live / 'sigil-death-explosion-153-20261109/capture.json')
    assert events['process'] == explosion['process']
    assert events['executableSha256'] == explosion['executableSha256']
    assert events['immutableCandidateTableMatches']
    assert not events['dispatcherIndexLinked'] and not events['semanticIdentityVerified']
    trigger, damage = events['records']
    assert (trigger['slot'], trigger['rva'], trigger['bytes']) == (33, '0x3b0430', 950)
    assert trigger['allUnwindFragmentsCaptured'] and len(trigger['fragments']) == 1
    assert trigger['fragments'][0]['invalidInstructionMarkers'] == 0
    assert (damage['slot'], damage['rva'], damage['bytes']) == (34, '0x44a3e0', 759)
    assert len(damage['fragments']) == 3 and not damage['allUnwindFragmentsCaptured']
    assert all(not f['captured'] and f['permissions'] == '---s' for f in damage['fragments'])
    do = explosion['records'][0]
    assert explosion['dispatcherIndexLinked'] and explosion['immutableTableMatches']
    assert do['slot'] == 153 and do['rva'] == '0x44a6e0'
    assert not do['allUnwindFragmentsCaptured']
    assert all(not f['captured'] and f['permissions'] == '---s' for f in do['fragments'])

    source = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads(pin(source / 'sources.json'))
    skills_raw = pin(source / 'json/skills.json')
    assert hashlib.sha256(skills_raw).hexdigest() == manifest['files']['json/skills.json']['sha256']
    skills = json.loads(skills_raw)
    death, hidden = skills['400'], skills['403']
    assert death['skill'] == 'Sigil Death' and death['srvdofunc'] == 157
    assert [death['auraeventfunc' + str(i)] for i in range(1, 5)] == [34, 34, 33, 34]
    assert [death['auraevent' + str(i)] for i in range(1, 5)] == ['damagedinmelee', 'damagedbymissile', 'killed', 'install']
    assert [death['aurastatcalc' + str(i)] for i in range(1, 5)] == [100, 0, 1, 1]
    assert death['sumskill1'] == hidden['skill'] == 'Sigil Death Explosion'
    assert death['sumsk1calc'] == "skill('Sigil Death'.lvl)"
    assert hidden['srvdofunc'] == 153 and hidden['TargetCorpse'] == 1
    assert [hidden['calc' + str(i)] for i in range(1, 4)] == ['par1', 'par2', 'par5']
    assert [hidden['Param' + str(i)] for i in range(1, 6)] == [70, 120, 8, 0, 50]

    # Reuse the existing owned CASC extraction, without touching the process.
    compiled = STAGE / 'defiler-compiled-formula'
    extraction = json.loads(pin(compiled / 'extraction.json'))
    assert extraction['referenceVersion'] == '3.3.93847'
    assert extraction['executableSha256'] == events['executableSha256']
    assert not extraction['processMemoryRead'] and not extraction['gameFilesWritten']
    raw, code = pin(compiled / 'skills.bin'), pin(compiled / 'skillscode.bin')
    for name, data in [('skills.bin', raw), ('skillscode.bin', code)]:
        assert hashlib.sha256(data).hexdigest() == extraction['files'][name]['sha256']
    assert struct.unpack_from('<I', raw)[0] == len(skills) == 429
    assert len(raw) == 4 + 429 * 748
    ids = {r['skill'].casefold(): int(k) for k, r in skills.items() if r.get('skill')}
    slots = formulas = 0
    for key, row in skills.items():
        base = 4 + int(key) * 748
        for index in range(5):
            value = struct.unpack_from('<h', raw, base + 0x118 + 2 * index)[0]
            assert value == ids.get(str(row.get('sumskill' + str(index + 1), '')).casefold(), -1)
            formula = struct.unpack_from('<i', raw, base + 0x124 + 4 * index)[0]
            assert (formula >= 0) == (row.get('sumsk' + str(index + 1) + 'calc') is not None)
            assert formula == -1 or 0 <= formula < len(code)
            slots += 1
            formulas += int(formula >= 0)
    assert slots == 2145 and formulas == 49
    # Aura formula layout was already qualified across 8,580 slots by the
    # Shadow birth receipt. Here check only the four fields used by this body.
    for index in range(4):
        assert 0 <= struct.unpack_from('<i', raw, 4 + 400 * 748 + 0x88 + 4 * index)[0] < len(code)
    pin(ROOT / 'tools/reference/shadow-current-birth-source-evidence.json')
    pin(ROOT / 'src/Sanctuary.Content/SourceUnitEvents.cs')
    pin(ROOT / 'src/Sanctuary.Content/CorpseExplosionPackets.cs')
    pin(Path(__file__).resolve())
    result = dict(
        referenceVersion='3.3.93847', skillId=400, hiddenSkillId=403,
        learnedHandlerEnabled=False, handlers=213, warlockHandlers=4,
        completedClasses=[], process=events['process'],
        ownedSummonSkillSlots=slots, ownedPresentSummonFormulas=formulas,
        compiledOffsets=dict(aurastatcalc1='0x88', sumskill1='0x118', sumsk1calc='0x124'),
        eventCandidate=dict(tableRva='0x163a750', dispatcherLinked=False,
                            slot33Rva=trigger['rva'], completeBytes=950,
                            slot34Rva=damage['rva'], protectedBytes=759),
        explosion=dict(dispatcherLinked=True, do=153, rva=do['rva'], protectedBytes=159),
        capturedCodeBytes=950, processBytesRead=events['bytesRead'] + explosion['bytesRead'],
        primary={k: death[k] for k in ['aurafilter', 'auratargetstate', 'auralencalc',
                                      'aurarangecalc', 'mana', 'lvlmana', 'manashift']},
        hiddenPrimary={k: hidden[k] for k in ['aurarangecalc', 'TargetCorpse', 'EType',
                                             'calc1', 'calc2', 'calc3', 'Param1', 'Param2',
                                             'Param3', 'Param4', 'Param5']},
        observations=[
            'The complete candidate33 body evaluates aura formula fields, resolves/swaps units, guards skill-node recursion, advances a unit RNG, checks chance, then invokes a skill from sumskill1 with sumsk1calc. Its comparison permits roll equality. This is a disassembly observation, not qualified Sigil Death execution.',
            'All 2145 owned compiled summon-skill IDs match case-insensitive primary names and all 49 present formula offsets are in bounds. This qualifies the field labels without running a duplicate Shadow aura matrix.',
            'Reuse candidates are SourceUnitEvents for event traversal and CorpseExplosionPackets for percentages/RNG/split arithmetic. Neither proves modern event33/34 dispatch or explosion153 behavior.'
        ],
        unresolved=[
            'Link candidate event table indices33/34 to the actual current dispatcher; coherent code and matching immutable pointers alone do not establish that identity.',
            'Event34 installation/damage bookkeeping, mark replacement, removal and death-event ownership remain unreadable.',
            'Explosion153 current eligibility, corpse consumption, HP source, roll owner, mitigation/mastery, radius and physical/fire placement remain unreadable. Do not substitute old do55 level scaling, physical inner radius or corpse RNG.',
            'SourceUnitEvents.RegisterFunction deliberately admits only legacy1..31. Do not extend its domain from an unverified table candidate.',
            'No mark, explosion, learned cast or automatic recipient callback is enabled; prepared Sigil markers remain separate.'
        ],
        next='Continue unaffected Warlock work; retain these captures. Retry protected bodies only after a relevant external-state change. Rancor cast request remains optional and pending.',
        installedBuildChanged=False, savesChanged=False, inputs=inputs)
    target = ROOT / 'tools/reference/sigil-death-reference-gap.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Sigil Death:950 candidate bytes retained;2145 compiled skill slots checked;event34/do153 gaps explicit;no runtime admission.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
