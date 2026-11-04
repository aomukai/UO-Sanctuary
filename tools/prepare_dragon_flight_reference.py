#!/usr/bin/env python3
"""Pin Dragon Flight without treating an older finisher as current executable proof."""
import argparse
import csv
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def build(check=False):
    pin = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((pin / 'sources.json').read_text())
    raw = (pin / 'json/skills.json').read_bytes()
    cached = hashlib.sha256(raw).hexdigest()
    assert cached == manifest['files']['json/skills.json']['sha256']
    row = json.loads(raw)['275']
    assert (row['srvstfunc'], row['srvdofunc'], row['seqnum'], row['anim'], row['Kick']) == (12, 52, 21, 'SQ', 1)
    assert (row['finishing'], row['prgchargesconsumed'], row['Param8'], row['mana'], row['Param7']) == (1, 1, 1, 15, 38)
    local = ROOT / '.local/research/d2r-3.3.93847/skills.txt'
    blob = local.read_bytes()
    actual = next(x for x in csv.DictReader(blob.decode('utf-8-sig').splitlines(), delimiter='\t') if x.get('skill') == 'Dragon Flight')
    facts = {}
    for key, value in row.items():
        if key in actual and not key.startswith('*') and key != 'lineNumber':
            assert actual[key] == str(value), (key, actual[key], value)
            facts[key] = value
    stage = ROOT / '.local/class-skills'
    tree = json.loads((stage / 'd2moo-source-tree.json').read_text())
    symbols = []
    for filename, symbol in [('SkillSor.cpp', 'SKILLS_SrvSt12_Telekinesis_DragonFlight'),
                             ('SkillAss.cpp', 'SKILLS_SrvDo052_DragonFlight')]:
        relative = 'source/D2Game/src/SKILLS/' + filename
        raw = (stage / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        text = raw.decode()
        match = re.search(r'^.*\b' + symbol + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match
        end, depth = text.index('{', match.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        symbols.append(dict(path=relative, symbol=symbol, sourceSha256=hashlib.sha256(raw).hexdigest(),
                            routineSha256=hashlib.sha256(text[match.start():end].encode()).hexdigest()))
    e = dict(referenceVersion='3.3.93847', talentId='assassin.dragonflight', sourceId=275,
             status='Prepared source control flow; current SQ21 file data corroborated, learned action remains deferred',
             handlerEnabled=False, installed=False,
             source=dict(cachedSkillsSha256=cached, localSkills=dict(path=str(local.relative_to(ROOT)),
                         sha256=hashlib.sha256(blob).hexdigest(), fieldsCorroborated=len(facts)), skill=facts),
             legacyInspection=dict(commit='0c0bbd1e6bef9181a3da1c046523af622a925416', routines=symbols,
                                   executed=True, scope='Complete start12/do52 plus seven sequence/coordinate/warp helpers execute in dragon-flight-source-evidence.json with declared services; not current executable code parity'),
             sourceExecution='tools/reference/dragon-flight-source-evidence.json',
             nativeSequenceSupplier='src/Sanctuary.Content/PlayerWeaponSequences.cs',
             nativeSequenceEvidence='tools/reference/dragon-flight-preparation-native-evidence.json',
             established=[
                 'Current row275 uses start12/do52, sequence21 with A1 transition, Kick1, target leeway SearchEnemyNear1, both mouse bindings and low-mana ordinary attack fallback. It has no localdelay field in the pinned row.',
                 'Current constants are15mana,38source-unit target range, damage100+35*(rank-1), AR60+25*(rank-1), one charge consumed and charged-only always-hit Param8=1.',
                 'Complete older start12 requires a target and row, checks squared range inclusively and rejects invalid/town player or monster targets through existing target services.',
                 'Current immutable executable SQ21 data has14identical23point profiles with CAST/KICK modes and markers9/14. Complete older scheduler/player callback maps these to ordinal0 teleport then1 kick across supplied advances0..768; attack-rate derivation remains separate.',
                 'Complete older do52 has two distinct sequence-parity branches: even resolves target coordinates and origin-level/collision permission before the complete free-space/path warp helper; odd prepares kick AR/result/percentage, allocates combat, processes progressive effects and drains durability. Do not replace this with an instantaneous teleport plus ordinary weapon attack.',
                 'Do52 returns1 after calling the warp helper even when that helper fails to move the owner. The declared sequence dispatcher still reaches the later kick after failed/refused landing; do52 return alone cannot certify arrival or settle payment.'],
             reuse=['Prepared body/boot kick packets and independent martial charge families; ordinary weapon physical and critical are excluded from the kick lane.',
                    'Existing teleport room/landing/permission and PlayerWeaponSequences/SourceAnimationEvents are candidate native reuse points. Shared current-file reader now accepts SQ21 and verifies all322classic descriptors; no parallel sequence reader is added.',
                    'Existing Teleportation.Landing is a range18point-target Sorceress contract; Flight uses target-range38source units and a collision-selected nearby landing. Reuse lower-level travel/space permissions without silently copying the whole Sorceress contract.'],
             blockers=[
                 'Modern charged guaranteed-hit versus block/dodge and consume-one ordering remain Flight-specific gaps. Older result/progressive bodies do not implement the current charged branch. The approved Dragon Tail policy is not silently extended to Flight.',
                 'Sequence21 markers and older ordinal dispatch are established; current attack-rate context, two-phase cancellation, mana payment on failed start/landing/contact, valid native landing near the target and out-of-range retarget behavior need composed checks.',
                 'No native Dragon Flight handler, accepted player input, damage, teleport or save/reload test is claimed. Continue skills independent of these finisher gates.'],
             assets=dict(effects='Teleport departure/arrival, kick/contact, charge release and audio; identified placeholders permitted.',
                         icons='Separate talent, left/right and hotkey/quick icons in the240-row inventory.'),
             next='Connect current SQ21 to the shared native sequence supplier, then compose explicit landing/contact/payment/cancellation and natural input/save tests. Keep unresolved Flight policy separate and continue unaffected skills. No class completion certificate.')
    out = ROOT / 'tools/reference/dragon-flight-preflight.json'
    encoded = json.dumps(e, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded, 'Dragon Flight preflight stale'
    else:
        out.write_text(encoded)
    print(f'Dragon Flight:{len(facts)} current local fields/current SQ21 and complete older control flow; learned payment/landing/current-code gates explicit.')


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--check', action='store_true')
    build(p.parse_args().check)
