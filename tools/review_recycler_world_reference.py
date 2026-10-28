#!/usr/bin/env python3
"""Pin recycler mode-entry and walk-tactic review; not compiled route acceptance."""
import argparse
import hashlib
import json
from pathlib import Path
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]


def build(check=False):
    base = ROOT / '.local/class-skills'
    tree = json.loads((base / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    groups = {
        'source/D2Game/src/AI/AiTactics.cpp': ['AITACTICS_UseSkill', 'AITACTICS_WalkToTargetUnitWithFlags', 'AITACTICS_MoveToTarget', 'AITACTICS_WalkCloseToUnit'],
        'source/D2Game/src/MONSTER/MonsterMode.cpp': ['sub_6FC645E0'],
        'source/D2Game/src/SKILLS/Skills.cpp': ['sub_6FD12FD0', 'sub_6FD12950'],
        'source/D2Game/src/SKILLS/SkillDruid.cpp': ['SKILLS_SrvSt63_Corpse_VineCycler', 'SKILLS_FindUseableCorpse'],
        'source/D2Common/src/Units/Units.cpp': ['UNITS_IsInMeleeRange']}
    hashes, routines = {}, {}
    for relative, names in groups.items():
        raw = (base / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        for name in names:
            routines[name] = hashlib.sha256(function(raw.decode(), name).encode()).hexdigest()
    preflight = json.loads((ROOT / 'tools/reference/recycler-vines-preflight.json').read_text())
    for key in ['cycleoflife', 'vinecreature']:
        assert preflight['currentRows']['monstats'][key]['Code'].upper() == 'K9'
        body = preflight['currentRows']['monstats2'][key]
        assert body['SizeX'] == body['SizeY'] == '0' and body['MeleeRng'] == '1' and body.get('S1mv', '0') == '0'
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], compiled=False, currentExecutableParity=False,
        completeRoutines=len(routines), routineHashes=routines, sourceHashes=hashes,
        reviewed=[
            'AITACTICS_UseSkill selects the innate skill and requests mode8. The mode8 entry callback sets combat mode and calls the skill start wrapper immediately; it returns1 independently of the start result. Current recycler bodies have no S1-moving flag, so this callback does not require a live path.',
            'The start wrappers validate target flags/corpse state, skill eligibility, town and declared LOS before start63. Start63 reserves/refreshes the corpse before allocating a delay owned by the original player; no later action-frame dofunc creates it.',
            'WalkToTargetUnitWithFlags passes7 as failure flags, not as a path type. On movement failure it deletes think, sets AI control0x40 and uses a70percent walk-close/30percent idle fallback. Successful approach uses the ordinary movement path. The native controller must not substitute path type7 for this argument.',
            'Corpse finder takes the first usable target in source room/unit order, not the nearest corpse. Native sector/item order and access/LOS/loot safeguards are explicit adapters. Size0 vines use ordinary melee range1 plus1 against mapped corpse sizes; special tentacle reach remains outside this bounded route.'],
        remaining=[
            'This is complete-source review, not compilation/execution of all composed outer dispatch. Current executable timing, skill prerequisites/exceptional grants, global event/list ordering, room geometry and underground exposure remain separate.',
            'Native walk failure flags now dispatch through a shared ordered policy. The source AI-control0x40consumer is projected to a fresh native hostility scan; exact source vision/room/lookup composition remains unverified rather than certified.',
            'Missing historical corpse-owner/size metadata is unsupported. Battle Command innate transport and modern Solar calc2 behavior remain separately gated.'])
    path = ROOT / 'tools/reference/recycler-world-source-review.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Recycler world:10 complete routines reviewed; mode-entry start and walk failure flags distinguished; composed modern parity unclaimed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
