#!/usr/bin/env python3
"""Execute complete area search/filter with Tornado's exact caller arguments."""
import argparse
import ast
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    rel = 'source/D2Game/src/SKILLS/Skills.cpp'
    raw = (STAGE / 'd2moo-pinned-source' / rel).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == rel)
    source = raw.decode()
    # Reuse only the declared room/metadata/hostility services, without running
    # unrelated Fury creator probes or depending on their generated caches.
    module = ast.parse((ROOT / 'tools/probe_lightning_fury_reference.py').read_text())
    assignment = next(node for node in module.body if isinstance(node, ast.Assign)
                      and any(isinstance(target, ast.Name) and target.id == 'cpp' for target in node.targets))
    value = assignment.value
    while isinstance(value, ast.BinOp):
        value = value.left
    assert isinstance(value, ast.Constant) and isinstance(value.value, str)
    base = value.value
    blocks = {}
    for name in ['sub_6FD0FA00', 'sub_6FD0FE80']:
        match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', source, re.M)
        assert match, name
        end, depth = source.index('{', match.start()) + 1, 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}')
            end += 1
        body = source[match.start():end]
        base += body + '\n'
        blocks[name] = hashlib.sha256(body.encode()).hexdigest()
    before = 'D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return &caster;}'
    assert base.count(before) == 1
    base = base.replace(before, 'bool hasOwner=true;int ownerQueries=0,losQueries=0;D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){ownerQueries++;return hasOwner?&caster:nullptr;}', 1)
    before = 'bool COLLISION_RayTrace(D2ActiveRoomStrc*,D2CoordStrc*,D2CoordStrc*,unsigned){return wall;}'
    assert base.count(before) == 1
    base = base.replace(before, 'bool COLLISION_RayTrace(D2ActiveRoomStrc*,D2CoordStrc*,D2CoordStrc*,unsigned){losQueries++;return wall;}', 1)
    base += r'''
int callbacks=0,callbackOwner=0;
int Record(D2AuraCallbackStrc* a,D2UnitStrc* target){callbacks++;callbackOwner=a->pOwner==&parent&&target==&targets[0];return 1;}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int dx=-4;dx<=4;dx++)for(int dy=-4;dy<=4;dy++)for(int variant:{0,1,2,3,4,8,16,32,64})for(int check:{0,1})for(int barrier:{0,1})for(int own:{0,1})for(int aim:{0,1,2}){
caster={};caster.dwUnitType=0;caster.x=10;caster.y=10;caster.room=&rooms[0];parent={};parent.dwUnitType=3;parent.x=50;parent.y=70;parent.room=&rooms[0];rooms[0]={};rooms[1]={};adjacent[0]=&rooms[0];adjacent[1]=&rooms[1];for(auto& m:monsters)m={};
targets[0]={};targets[0].x=50+dx;targets[0].y=70+dy;targets[0].room=&rooms[0];rooms[0].pUnitFirst=&targets[0];
monsters[0].dwMonStatsFlags=variant&3;if(variant&4)targets[0].hostile=false;if(variant&8)targets[0].dwAnimMode=MONMODE_DEAD;if(variant&16)targets[0].dwFlags&=~UNITFLAG_CANBEATTACKED;if(variant&32)targets[0].dwFlags&=~UNITFLAG_ISVALIDTARGET;if(variant&64)rooms[0].town=true;
hasOwner=own;wall=barrier;callbacks=callbackOwner=ownerQueries=losQueries=0;
int x=aim?52:0,y=aim==1?71:0;
sub_6FD0FE80(&game,&parent,x,y,3,42371,Record,nullptr,check,__FILE__,__LINE__);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",dx,dy,variant,check,barrier,own,aim,callbacks,callbackOwner,ownerQueries,losQueries);first=false;
}printf("]\n");}
'''
    path = STAGE / 'tornado-area-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'tornado-area-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    rows = json.loads(run.stdout)
    assert len(rows) == 17496
    for dx, dy, variant, check_aura, wall, owner, aim, callbacks, callback_owner, queries, los in rows:
        sx, sy = (2, 1) if aim == 1 else (0, 0)
        in_radius = (dx - sx)**2 + (dy - sy)**2 <= 9
        filtered = bool(variant & (8 | 16 | 32 | 64))
        reached_owner = in_radius and not filtered
        eligible = reached_owner and owner and not (variant & 4) and not (check_aura and variant & 1)
        assert (callbacks, callback_owner, queries, los) == (int(eligible), int(eligible), int(reached_owner), 0), (dx, dy, variant, check_aura, wall, owner, aim)
    fixture = STAGE / 'tornado-area-source-probe.json'
    fixture.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
                    sourceHashes={rel: hashlib.sha256(raw).hexdigest()}, compiledUnchangedRoutines=blocks,
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(rows)),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'Complete shared search and filter execute with exact Tornado filter42371/radius3 and current bCheckMonAuraFlag0. Either missing coordinate resets both to the missile position; a fully explicit control uses its supplied center. Euclidean center distance is inclusive, with no target footprint expansion.',
                        'Dead, friendly, unattackable, invalid-target, town-room and missing missile-owner controls refuse. Source hostility resolves the missile owner before querying the target. Candidate room/list and monster flags remain declared services.',
                        'Current bCheck0 deliberately permits a hostile/attackable/valid NOAURA monster; bCheck1 control rejects it. NPC metadata alone is not an extra gate in this filter when the declared hostility service permits the actor. Reusing SourceAuraTargets.Eligible would incorrectly add a NOAURA restriction.',
                        'Wall controls never call the optional ray trace because0xA583 has no0x200 bit. The periodic pulse does not require caster LOS, generic NextHit or LastCollide eligibility.'
                    ], remaining=[
                        'Declared room adjacency/list/coordinates, monster metadata, hostility/owner and geometric scalar services do not certify native sector order or current room/geometry behavior.',
                        'This source search proof does not establish native periodic/direct damage, path movement, current overlap or learned input. No Tornado handler or final assets are enabled.'
                    ], next='Use an explicit attackable/hostile/radius pulse predicate without optional NOAURA/LOS/history gates; keep direct contact eligibility separate.')
    out = ROOT / 'tools/reference/tornado-area-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Tornado:17496complete area/filter cases pass; current NOAURA/LOS gates remain absent; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
