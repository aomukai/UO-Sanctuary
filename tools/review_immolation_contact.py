#!/usr/bin/env python3
"""Bind Immolation immediate area,weapon direct and periodic field packet phases."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE,function
from review_explosive_arrow_contact import record as parent

def record(check=False):
    parent(True);tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());records={};bodies={}
    files={'source/D2Game/src/MISSILES/MissMode.cpp':['MISSMODE_SrvHit09_ImmolationArrow','MISSMODE_GetDamageValue','MISSMODE_SetDamageFlags','MISSMODE_SrvDmg03_Blaze_FireWall_ImmolationFire_MeteorFire','MISSMODE_SrvDmgHitHandler','MISSMODE_SrvDo05_FireWall_ImmolationFire_MeteorFire_MoltenBoulderFirePath'],'source/D2Game/src/SKILLS/Skills.cpp':['sub_6FD10200','sub_6FD10140'],'source/D2Game/src/UNIT/SUnitDmg.cpp':['SUNITDMG_IsHitSuccessful','SUNITDMG_ExecuteEvents']}
    for path,names in files.items():
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==path)
        for name in names:body=function(raw.decode(),name);bodies[name]=body;records[name]=dict(path=path,sha256=hashlib.sha256(body.encode()).hexdigest())
    hit=bodies['MISSMODE_SrvHit09_ImmolationArrow'];assert hit.index('STATLIST_SetUnitStat(pMissile, STAT_COLDLENGTH, 0, 0)')<hit.index('MISSMODE_GetDamageValue(')<hit.index('sub_6FD10200(')
    assert 'pDamage->wResultFlags |= 0x4000u' in bodies['MISSMODE_SrvDmg03_Blaze_FireWall_ImmolationFire_MeteorFire'];assert 'SUNITDMG_ApplyBlockOrDodge(pGame, pAttacker, pUnit, 1, 0)'in bodies['sub_6FD10140']
    cache=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((cache/'sources.json').read_text());source=cache/'json/missiles.json';sha=hashlib.sha256(source.read_bytes()).hexdigest();assert sha==manifest['files']['json/missiles.json']['sha256'];rows=json.loads(source.read_text());runtime={r['key']:r['fields']for r in json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text())};fields={}
    for name,wanted in {'immolationarrow':dict(pSrvDoFunc=1,pSrvHitFunc=9,pSrvDmgFunc=0,ToHit=1,AlwaysExplode=1,GetHit=1,HitFlags=2,ResultFlags=5,HitClass=10,Range=40),'immolationfire':dict(pSrvDoFunc=5,pSrvHitFunc=0,pSrvDmgFunc=3,ToHit=0,GetHit=0,HitFlags=0,ResultFlags=0,HitClass=32,DamageRate=41,dParam1=19,Range=100)}.items():
        assert all(rows[name].get(k,0)==v and runtime[name].get(k,0)==v for k,v in wanted.items());fields[name]=wanted
    prior={}
    for name in ('arrow','routing','damage','field','item-event'):
        p=ROOT/f'tools/reference/immolation-{name}-engine-evidence.json';data=json.loads(p.read_text());probe=ROOT/data['probe']['path'];assert hashlib.sha256(probe.read_bytes()).hexdigest()==data['probe']['sha256'];prior[str(p.relative_to(ROOT))]=hashlib.sha256(p.read_bytes()).hexdigest()
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],functions=records,currentMissilesSha256=sha,missileFields=fields,retainedSourceEvidence=prior,established=['hit09 clears missile cold/freezing,creates the disc and copies one captured fire-only HitFlags2/ResultFlags5 area before the separate full weapon hit. Source accuracy PMH precedes hit09;area remains on miss/block/wall/expiry. A dead primary has no later weapon contact.','Weapon source uses full captured physical/element/poison/drain with pre-HP item hit events and post-handler armor. Attacker item freeze is independent of cleared missile cold length.','Field source0 rows have no weapon bit32,ToHit,GetHit or shield check. do3 draws once per collided cell and ORs SOFTHIT for19/128 outcomes;SetDamageFlags adds Successful1 and HitClass32. DamageRate41 affects mitigation,not a second damage multiplier.','Retain source field endpoints,one collider per cell,99updates,terrain/expiry and independent saves. Element-only shared packet delivery uses periodic damage kind for fields,without direct-damage notifications or weapon proc tickets.'],remaining=['Archived current rows plus complete older bodies establish this scoped bridge,not exact modern global catch-up/stat/seed/provider/owner/room-order/geometry.','Existing avoidance adapters and co-op world rules remain;exceptional/uninterruptible/death-delay/hireling/PvP contexts stay explicitly unqualified.','Full named acquisition,other missile callers,complete class/client and separate effect/audio/icon assets remain open.'])
    out=ROOT/'tools/reference/immolation-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Immolation complete area/direct/periodic bodies and archived rows bound;field SOFTHIT differs from GetHit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
