#!/usr/bin/env python3
"""Bind ordinary thrown-missile flags,damage events and return ordering."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE,function

def record(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';records={};bodies={}
    files={'source/D2Game/src/MISSILES/MissMode.cpp':['MISSMODE_SetDamageFlags'],'source/D2Game/src/MISSILES/Missiles.cpp':['MISSILES_CreateMissileFromParams'],'source/D2Common/src/Units/Missile.cpp':['MISSILE_CalculateDamageData','MISSILE_SetDamageStats'],'source/D2Game/src/UNIT/SUnitDmg.cpp':['SUNITDMG_ExecuteEvents','SUNITDMG_ExecuteMissileDamage']}
    for path,names in files.items():
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==path)
        for name in names:
            body=function(raw.decode(),name);bodies[name]=body;records[name]=dict(path=path,sha256=hashlib.sha256(body.encode()).hexdigest())
    events=bodies['SUNITDMG_ExecuteEvents'];assert events.index('UNITEVENT_DOMISSILEDMG')<events.index('UNITEVENT_DAMAGEDBYMISSILE')<events.index('pDamage->dwPhysDamage = std::min')
    assert bodies['MISSMODE_SetDamageFlags'].index('SUNITDMG_ExecuteEvents(')<bodies['MISSMODE_SetDamageFlags'].index('SUNITDMG_ExecuteMissileDamage(')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],functions=records,scope='Complete older weapon-missile flags/damage/return bodies and pinned ordinary throwing rows;not modern executable parity.',established=['Source weapon contribution sets missile flag1 and packet HitFlags32. Creation suppression and damage metadata can set missile flag2 and packet HitFlags128.','Successful unblocked impact dispatches domissiledamage before recipient damagedbymissile,physical clipping,drain,HP,status,kill and return. HitFlags128 suppresses only the attacker damage event.','Source DoMissileDamage has no melee attempt,weapon wear or Thorns phase. No missile-attack caller was established in this pinned source;no launch/per-impact attack event is invented.','Five ordinary throwing GetHit1/HitClass rows come from the archived current tables. Source damage,poison,drain and hit class retain the existing released flight;item providers are read at impact through the existing live graph.','Reuse the approved persistent player/world RNG and noninterrupting item-cast mapping;actual Whirlwind state retains its existing execution refusal.'],remaining=['Only ordinary Double Throw is connected in this checkpoint;other player missiles and Mirrored/NPC bodies remain separate.','Modern missile attack timing,selected-hand item graph timing,RNG/owner/geometry,exceptional/PvP recipients and full named admission remain unverified.'])
    p=ROOT/'tools/reference/thrown-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Thrown missile complete flags/damage/return bodies bound;missile-attack caller unresolved.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
