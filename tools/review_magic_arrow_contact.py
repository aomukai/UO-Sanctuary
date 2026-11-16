#!/usr/bin/env python3
"""Bind Magic Arrow conversion,PMH,captured armor and missile event ordering."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE,function

def record(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';records={};bodies={}
    files={'source/D2Game/src/MISSILES/MissMode.cpp':['MISSMODE_SetDamageFlags','MISSMODE_SrvDmgHitHandler','MISSMODE_SrvDmg01_FireArrow_MagicArrow_ColdArrow'],'source/D2Game/src/MISSILES/Missiles.cpp':['MISSILES_CreateMissileFromParams'],'source/D2Common/src/Units/Missile.cpp':['MISSILE_CalculateDamageData','MISSILE_SetDamageStats'],'source/D2Game/src/UNIT/SUnitDmg.cpp':['SUNITDMG_ExecuteEvents','SUNITDMG_ExecuteMissileDamage','SUNITDMG_IsHitSuccessful','SUNITDMG_PreventMonsterHeal']}
    for path,names in files.items():
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==path)
        for name in names:
            body=function(raw.decode(),name);bodies[name]=body;records[name]=dict(path=path,sha256=hashlib.sha256(body.encode()).hexdigest())
    events=bodies['SUNITDMG_ExecuteEvents'];assert events.index('UNITEVENT_DOMISSILEDMG')<events.index('UNITEVENT_DAMAGEDBYMISSILE')<events.index('pDamage->dwPhysDamage = std::min')
    assert bodies['MISSMODE_SetDamageFlags'].index('SUNITDMG_ExecuteEvents(')<bodies['MISSMODE_SetDamageFlags'].index('SUNITDMG_ExecuteMissileDamage(')
    hit=bodies['SUNITDMG_IsHitSuccessful'];assert hit.index('if (nRand >= nChanceToHit)')<hit.index('SUNITDMG_PreventMonsterHeal(')
    cached=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((cached/'sources.json').read_text());source_hashes={}
    for name in ('skills','missiles'):
        path=cached/'json'/(name+'.json');raw=path.read_bytes();source_hashes[name]=hashlib.sha256(raw).hexdigest();assert source_hashes[name]==manifest['files']['json/'+name+'.json']['sha256']
    row=json.loads((cached/'json/missiles.json').read_text())['magicarrow'];current=next(r['fields']for r in json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text())if r['key']=='magicarrow')
    fields={f:row.get(f,0)for f in ('GetHit','HitClass','pSrvDoFunc','pSrvDmgFunc','ToHit')};assert fields==dict(GetHit=1,HitClass=10,pSrvDoFunc=1,pSrvDmgFunc=1,ToHit=1)
    assert all(current.get(f,0)==v for f,v in fields.items())
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],functions=records,sourceHashes=source_hashes,missileFields=fields,scope='Complete older Magic Arrow conversion/PMH/missile event/return bodies and existing pinned current row;not modern executable parity.',established=['Successful accuracy applies PMH before shield blocking,including outer event-suppressed arrows;the old PMH-as-hit-event fixture expectation was incomplete.','Source weapon contribution sets missile flag1 and packet HitFlags32. Creation suppression and damage metadata can set missile flag2 and packet HitFlags128.','Successful unblocked impact dispatches domissiledamage before recipient damagedbymissile,physical clipping,drain,HP,status,kill and return. HitFlags128 suppresses only the attacker damage event.','Source DoMissileDamage has no melee attempt,weapon wear or Thorns phase. No missile-attack caller was established in this pinned source;no launch/per-impact attack event is invented.','Magic Arrow reuses current GetHit1/HitClass10 and damage-function1. Physical conversion remains in its existing released packet. Target armor delta is captured at release and applied after the hit handler,including block and suppressed attacker events.','Reuse the approved persistent player/world RNG and noninterrupting item-cast mapping;actual Whirlwind state retains its existing execution refusal.'],remaining=['Magic Arrow and ordinary Double Throw share one packet allocator;other player missiles and Mirrored/NPC bodies remain separate.','Modern missile attack timing,selected-hand item graph timing,RNG/owner/geometry,exceptional/PvP recipients and full named admission remain unverified.'])
    p=ROOT/'tools/reference/magic-arrow-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Magic Arrow complete impact/conversion/PMH bodies bound;missile-attack caller unresolved.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
