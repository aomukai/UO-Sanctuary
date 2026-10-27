#!/usr/bin/env python3
"""Record the pinned complete damage functions' ordering; this is a source review, not an execution probe."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def function(text, name):
    match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
    assert match, name
    end, depth = text.index('{', match.start()) + 1, 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[match.start():end]


def record(check=False):
    relative = 'source/D2Game/src/UNIT/SUnitDmg.cpp'
    raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    blob = hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest()
    assert blob == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    text = raw.decode()
    execute = function(text, 'SUNITDMG_ExecuteEvents')
    call_at = text.index('SUNITDMG_ExecuteEvents(pGame, pAttacker, pDefender, 0, &damageCopy);')
    # Locate the complete caller by its declaration, not an arbitrary tail slice.
    declarations = list(re.finditer(r'^.*\b(SUNITDMG_\w+)\([^;\n]*\)\s*\n\{', text[:call_at], re.M))
    caller_name = declarations[-1].group(1)
    caller = function(text, caller_name)
    phases = [
        'SUNITEVENT_Trigger(pGame, UNITEVENT_DOMELEEDMG',
        'SUNITEVENT_Trigger(pGame, UNITEVENT_DAMAGEDINMELEE',
        'pDamage->dwPhysDamage = std::min(pDamage->dwPhysDamage, nHp);',
        'if (pDamage->dwLifeLeech || pDamage->dwManaLeech)',
        'if (pDamage->dwAbsLife > 0',
        'if (pDamage->dwDmgTotal > 0)',
        'if (pDamage->dwManaLeech > 0)',
        'SUNITEVENT_Trigger(pGame, UNITEVENT_KILLED',
        'SUNITEVENT_Trigger(pGame, UNITEVENT_KILL,',
    ]
    caller_phases = [
        'SUNITDMG_ExecuteEvents(pGame, pAttacker, pDefender, 0, &damageCopy);',
        'SUNITEVENT_Trigger(pGame, UNITEVENT_DOMELEEATTACK',
        'SUNITEVENT_Trigger(pGame, UNITEVENT_ATTACKEDINMELEE',
        'SKILLS_ApplyThornsDamage(',
        'SUNITDMG_ExecuteMissileDamage(',
    ]
    for body, anchors in [(execute, phases), (caller, caller_phases)]:
        positions = [body.index(anchor) for anchor in anchors]
        assert positions == sorted(positions)
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'],
        sourceFile=relative, sourceSha256=hashlib.sha256(raw).hexdigest(), gitBlob=blob,
        scope='Static ordering review of complete pinned older functions, not execution of the complete pipeline or proof of the current executable.',
        functions={name: hashlib.sha256(body.encode()).hexdigest() for name, body in [('SUNITDMG_ExecuteEvents', execute), (caller_name, caller)]},
        orderedExecuteAnchors=phases, orderedCallerAnchors=caller_phases,
        correction='ExecuteEvents commits incoming total damage to defender HP before the caller dispatches flat ATD and percentage Thorns. ExecuteMissileDamage is later reaction/death dispatch, not the first HP subtraction.',
        candidateScope='PreparedWeaponEvents moves Life Tap, clipped ordinary leech and absorb healing ahead of the existing return bridge. It preserves the pre-absorb physical clip. It does not repair incoming HP commitment, death flags/callbacks, mode dispatch or complete native absorb chronology.',
        remaining=[
            'A shared ordered HP/death/event bridge is required; ordinary Mobile.Damage immediately performs native callbacks/death and is not an interchangeable delayed dispatch.',
            'Do not stage HP with SetBalance256 and then subtract it again through Damage, restore old HP to replay callbacks, or infer complete event parity from isolated arithmetic probes.',
            'Barbs needs one aggregate flat ATD packet shared with compatible Thorns/item stats, independent of percentage return. Learned Barbs remains disabled.',
        ])
    path = ROOT / 'tools/reference/weapon-event-order-review.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Complete pinned source ordering reviewed; HP-before-return correction retained. No complete pipeline execution claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
