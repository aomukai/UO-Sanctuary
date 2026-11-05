#!/usr/bin/env python3
"""Inspect pinned target lookup dependencies without claiming modern parity."""
import argparse
import hashlib
import json
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
PIN='0c0bbd1e6bef9181a3da1c046523af622a925416'

def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==PIN
    sources={};bodies={}
    selections={
        'source/D2Game/src/SKILLS/SkillItem.cpp':['SKILLITEM_EventFunc21_SkillOnGetHit','SKILLITEM_CastSkillOnTarget','SKILLITEM_HandleItemEffectSkill'],
        'source/D2Game/src/UNIT/SUnit.cpp':['sub_6FCBC7E0','SUNIT_GetTargetUnit'],
        'source/D2Game/src/SKILLS/Skills.cpp':['sub_6FD10360'],
        'source/D2Game/src/SKILLS/SkillNec.cpp':['SKILLS_SrvDo030_Curse']}
    for relative,names in selections.items():
        path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(row['sha']for row in tree['tree']if row['path']==relative)
        sources[str(path.relative_to(ROOT))]=hashlib.sha256(raw).hexdigest()
        for name in names:bodies[name]=function(raw.decode(),name)
    assert 'pDamage->wResultFlags & 4' in bodies['SKILLITEM_EventFunc21_SkillOnGetHit']
    assert 'SKILLITEM_CastSkillOnTarget(pAttacker, v10, v9, pUnit, 0)' in bodies['SKILLITEM_EventFunc21_SkillOnGetHit']
    assert 'SKILLITEM_HandleItemEffectSkill' in bodies['SKILLITEM_CastSkillOnTarget']
    assert 'UNITS_SetTargetUnitForDynamicUnit(pUnit, pTargetUnit)' in bodies['SKILLITEM_HandleItemEffectSkill']
    assert 'SUNIT_GetServerUnit(pGame, nTargetType, nTargetGUID)' in bodies['sub_6FCBC7E0']
    assert 'pTarget->dwUnitType != UNIT_ITEM' in bodies['sub_6FCBC7E0']
    assert 'sub_6FCBC7E0(pGame, pUnit)' in bodies['SUNIT_GetTargetUnit']
    assert 'CLIENTS_GetUnitX(pTarget)' in bodies['sub_6FD10360']and 'D2COMMON_10175_PathGetFirstPointX' in bodies['sub_6FD10360']
    assert 'return sub_6FD10360' in bodies['SKILLS_SrvDo030_Curse']
    result=dict(referenceVersion='3.3.93847',legacyEnginePin=PIN,inspectionOnly=True,modernDispatcherParityEstablished=False,retainedTargetBridgeImplemented=True,
        scope='Pinned complete dependency inspection,not a new compiled differential or actual D2R reflected-death experiment.',
        findings=['Callback21 uses the supplied other-unit pointer when present and otherwise requests the actor first path point. Its packet/result/chance contract is covered by the existing compiled callback oracle.',
            'Target cast and item-effect services install a temporary target and restore the prior path/target. The existing compiled item-effect oracle covers these wrappers.',
            'Target lookup validates the path pointer against the game type/GUID registry;missing or changed registration clears it. The monster branch here adds no death-mode test.',
            'Do30 delegates center lookup to sub_6FD10360:an available target supplies coordinates,otherwise actor first-path coordinates are used;invalid coordinates or unavailable room reject.',
            'Native UO deletion is not proof of which D2 logical registration/target/path state remains. Do not invent a nearest-enemy fallback or silently replace this with caster-centered casting.'],
        nativeBridgeScope='A synchronous original monster reference is captured before retaliation. Existing SourceCorpseIdentity supplies dead-unit registration and current corpse position through the shared item/common-executor/do30 path. A missing corpse identity,changed registration or facet mismatch is refused before incoming proc RNG;no fabricated path fallback.',
        next='Verify the native reflected-death bridge separately;keep unregistered/missing-target fallback,reentrant recipient removal and modern/source room behavior explicit.',
        inputHashes=sources,functionHashes={name:hashlib.sha256(body.encode()).hexdigest()for name,body in bodies.items()})
    path=ROOT/'tools/reference/npc-retained-target-preflight.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'NPC retained-target inspection stale'
    else:path.write_text(encoded)
    print('NPC retained-target dependency inspection matches pinned source;native bridge is separately verified;modern dispatcher parity remains unestablished.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
