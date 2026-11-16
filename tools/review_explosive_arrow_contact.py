#!/usr/bin/env python3
"""Bind explosive arrow parent creation and independent elemental area packets."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE,function
from review_magic_arrow_contact import record as parent

def record(check=False):
    parent(True);tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());records={};bodies={}
    files={'source/D2Game/src/MISSILES/MissMode.cpp':['MISSMODE_SrvHit04_ExplodingArrow_FreezingArrow_RoyalStrikeMeteorCenter','MISSMODE_SrvHit01_Fireball_ExplodingArrow_FreezingArrowExplosion','MISSMODE_GetDamageValue'],'source/D2Game/src/SKILLS/Skills.cpp':['sub_6FD10200','sub_6FD10140'],'source/D2Game/src/SKILLS/SkillAss.cpp':['sub_6FCF5DE0']}
    for path,names in files.items():
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==path)
        for name in names:body=function(raw.decode(),name);bodies[name]=body;records[name]=dict(path=path,sha256=hashlib.sha256(body.encode()).hexdigest())
    assert 'memcpy(&damage' in bodies['sub_6FCF5DE0'];assert 'SUNITDMG_ApplyBlockOrDodge(pGame, pAttacker, pUnit, 1, 0)'in bodies['sub_6FD10140'];assert bodies['sub_6FD10140'].index('SUNITDMG_ExecuteEvents(')<bodies['sub_6FD10140'].index('SUNITDMG_ExecuteMissileDamage(')
    cached=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((cached/'sources.json').read_text());source=cached/'json/missiles.json';sha=hashlib.sha256(source.read_bytes()).hexdigest();assert sha==manifest['files']['json/missiles.json']['sha256'];rows=json.loads(source.read_text());runtime={r['key']:r['fields']for r in json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text())};fields={}
    for name in ('explodingarrow','freezingarrow'):
        f={k:rows[name].get(k,0)for k in ('GetHit','HitClass','pSrvDoFunc','pSrvHitFunc','pSrvDmgFunc','ToHit','AlwaysExplode','Range','SrcDamage')};assert f==dict(GetHit=1,HitClass=0,pSrvDoFunc=1,pSrvHitFunc=4,pSrvDmgFunc=0,ToHit=1,AlwaysExplode=1,Range=40,SrcDamage=128);assert all(runtime[name].get(k,0)==v for k,v in f.items());fields[name]=f
    for name,element in (('explodingarrowexp2','fire'),('freezingarrowexp3','frze')):
        f={k:rows[name].get(k,0)for k in ('HitFlags','ResultFlags','HitClass','pSrvHitFunc','Range','sHitPar1','SrcDamage')};assert f==dict(HitFlags=2,ResultFlags=5,HitClass=0,pSrvHitFunc=1,Range=1,sHitPar1=5,SrcDamage=0);assert rows[name]['EType']==element and all(runtime[name].get(k,0)==v for k,v in f.items());fields[name]=dict(**f,EType=element)
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],functions=records,sharedImpactReviewSha256=hashlib.sha256((ROOT/'tools/reference/magic-arrow-contact-source-review.json').read_bytes()).hexdigest(),currentMissilesSha256=sha,missileFields=fields,established=['Existing parent hit4 creates independent children before parent damage;AlwaysExplode preserves creation on AR miss,block,terrain and expiry. Existing one-frame scheduling/geometry remain.','Child hit1 rolls one elemental packet;fire getter excludes other lanes,freeze getter writes cold damage and freeze length. Each recipient receives its own copy;area avoidance is requested without shield block.','Child HitFlags2 lacks weapon bit32:neither attacker nor recipient weapon hit callbacks run. ResultFlags5 requests successful hit recovery. Reuse ordered HP,absorb,status,kill,credit and native return without weapon poison,drain,armor or proc tickets.','Parent retains source128 weapon fields,accuracy PMH,captured armor and successful weapon hit tickets. A shared elemental roll cannot be multiplied by per-recipient weapon collection.'],remaining=['Existing co-op hostile/Z/town and source radius/no-LOS projection remain explicit. Modern global catch-up,seed/stat/owner/provider/order contexts remain unverified.','Unsupported native recipient modes are reported and other recipients continue;no replacement mode behavior is invented.','This does not qualify all other spells,other missiles,exceptional/PvP/full named/class/client/asset acceptance.'])
    p=ROOT/'tools/reference/explosive-arrow-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Explosive parent and copied HitFlags2/ResultFlags5 elemental child bodies bound;no weapon callbacks on child.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
