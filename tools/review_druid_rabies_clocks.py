#!/usr/bin/env python3
"""Pin a static review of Rabies age, activation and independent emitter expiry."""
import argparse
import hashlib
import json
from pathlib import Path
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def review(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    pin=json.loads((ROOT/'tools/reference/druid-rabies-preflight.json').read_text())
    assert tree['sha']==pin['legacyCommit']
    bodies,hashes={},{}
    for relative,names in [
        ('source/D2Common/src/Units/Missile.cpp',['MISSILE_SetCurrentFrame','MISSILE_GetCurrentFrame','MISSILE_GetRemainingFrames','MISSILE_GetActivateFrame']),
        ('source/D2Game/src/MISSILES/MissMode.cpp',['MISSMODE_SrvDo30_RabiesPlague','MISSMODE_HandleMissileCollision','MISSMODE_SrvDo01_BasicMissile','MISSMODE_SrvDoHandler']),
        ('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD10E50']),
    ]:
        path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=hashlib.sha256(raw).hexdigest()
        for name in names:bodies[name]=function(raw.decode(),name)
    assert 'nTotalFrames - pMissile->pMissileData->nCurrentFrame' in bodies['MISSILE_GetRemainingFrames']
    assert 'MISSILE_GetCurrentFrame(pMissile) - 1' in bodies['MISSMODE_HandleMissileCollision']
    assert 'MISSILE_GetCurrentFrame(pMissile) > MISSILE_GetActivateFrame(pMissile)' in bodies['MISSMODE_HandleMissileCollision']
    do=bodies['MISSMODE_SrvDo30_RabiesPlague']
    assert 'MISSILE_GetCurrentFrame(pMissile) < 0' in do and 'MISSILE_GetRemainingFrames(pMissile) % nParam' in do
    assert do.index('MISSILES_CreateMissileFromParams')<do.index('MISSMODE_HandleMissileCollision')
    assert do.rstrip().endswith('MISSMODE_HandleMissileCollision(pGame, pMissile);\n\n    return 1;\n}')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],staticReview=True,compiledExecution=False,currentExecutableParity=False,
        sourceHashes=hashes,completeRoutineHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in bodies.items()},
        established=[
            'Despite its exported name, GetRemainingFrames returns total minus current: elapsed age. Factory starts total/current at the supplied lifetime. Do30 tests age modulo4 before invoking generic collision. Its first eligible callback therefore observes age0, followed by4,8,etc. Actual global event scheduling is a separate service.',
            'Generic collision moves a nonzero-velocity missile first, then decrements current, terminates at current<=0, and otherwise scans only when current<=activation frame and path flags permit. Child total/current20 and activation18 allow contact starting after the second ordinary decrement, before the twentieth-ending call.',
            'Do30 ignores the generic collision return and returns1. Its own early exit uses current<0, not current<=0. Thus a generic lifetime decrement alone does not prove that the emitter disappears at the independent state-list deadline. The negative-frame tail, event ordering and whether a replacement marker is visible to that tail need composed source/native acceptance.',
            'The marker remove callback is pinned separately. Source emitter creation is an independent missile owned by the infected host, with original caster identity in metadata. Marker and emitter must be independently representable before claiming complete lifecycle parity; their clocks cannot be silently collapsed.',
        ],nativePreparationLimits=[
            'Historical9e104e8preparation combined an emitter with its marker and stopped both at the marker deadline. The later lifecycle source/native evidence records separate markers/emitters, negative-current retirement, replacement-marker deadlines, zeroed missing-marker metadata and version0 migration. This static review remains distinct from that compiled/native acceptance; casting remains disabled.',
            'Exact modern unit/event tie order, Fog path integration, source factory default deadline when no marker exists, global player/missile seed streams, Slow Missiles damage capture, and original-caster death/logout/facet behavior remain explicit gaps. This is a static source review, not a new compiled or current-executable certificate.',
        ])
    path=ROOT/'tools/reference/druid-rabies-clock-review.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Rabies clock review pins9complete routines: elapsed-age cadence and independent marker/emitter tail; static evidence only.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');review(parser.parse_args().check)
