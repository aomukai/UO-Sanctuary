#!/usr/bin/env python3
"""Bind Guided Arrow's no-ToHit route,locked recipient and shared missile impact."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE,function
from review_magic_arrow_contact import record as parent

def record(check=False):
    parent(True);tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());path='source/D2Game/src/MISSILES/MissMode.cpp';raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==path);functions={}
    for name in ('MISSMODE_SrvDmgHitHandler','MISSMODE_SrvHit10_GuidedArrow_BoneSpirit','MISSMODE_SrvDo07_GuidedArrow_BoneSpirit'):
        body=function(raw.decode(),name);functions[name]=dict(path=path,sha256=hashlib.sha256(body.encode()).hexdigest())
        if name=='MISSMODE_SrvDmgHitHandler':assert body.index('if (pMissilesTxtRecord->nToHit)')<body.index('SUNITDMG_IsHitSuccessful(');assert body.count('SUNITDMG_IsHitSuccessful(')==1
    unit=(STAGE/'d2moo-pinned-source/source/D2Game/src/UNIT/SUnitDmg.cpp').read_text();hit=function(unit,'SUNITDMG_IsHitSuccessful');assert 'SUNITDMG_PreventMonsterHeal('in hit
    callers=[]
    for p in (STAGE/'d2moo-pinned-source/source/D2Game/src').rglob('*.cpp'):
        if 'SUNITDMG_PreventMonsterHeal('in p.read_text():callers.append(str(p.relative_to(STAGE/'d2moo-pinned-source')))
    assert callers==['source/D2Game/src/UNIT/SUnitDmg.cpp'];assert unit.count('SUNITDMG_PreventMonsterHeal(')==2
    cached=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((cached/'sources.json').read_text());source=cached/'json/missiles.json';sha=hashlib.sha256(source.read_bytes()).hexdigest();assert sha==manifest['files']['json/missiles.json']['sha256'];row=json.loads(source.read_text())['guidedarrow'];runtime=next(r['fields']for r in json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text())if r['key']=='guidedarrow');fields={k:row.get(k,0)for k in ('GetHit','HitClass','pSrvDoFunc','pSrvHitFunc','pSrvDmgFunc','ToHit','NextHit','NextDelay','Range','Vel')};assert fields==dict(GetHit=1,HitClass=10,pSrvDoFunc=7,pSrvHitFunc=10,pSrvDmgFunc=0,ToHit=0,NextHit=0,NextDelay=0,Range=128,Vel=24);assert all(runtime.get(k,0)==v for k,v in fields.items())
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],functions=functions,sharedImpactReviewSha256=hashlib.sha256((ROOT/'tools/reference/magic-arrow-contact-source-review.json').read_bytes()).hexdigest(),currentMissilesSha256=sha,missileFields=fields,established=['Current Guided Arrow ToHit0 bypasses the older router accuracy call. Its sole PMH writer call is inside successful accuracy. Inference:no accuracy-PMH setup on this path;current live3.3PMH behavior remains unverified.','Retain target-locked recipient filtering,existing mode/steer/search/owner lifecycle and zero pierce. Ordinary source shield blocking still applies.','Reuse captured damage/poison/drain/cold/armor and the qualified pre-HP missile item hit event/return bridge. Separate poison can inhibit regeneration without creating the long PMH state.','No launch/per-impact missile attack event,melee attempts,weapon wear or Thorns is invented.'],remaining=['Current D2R PMH behavior is an explicit inference,not an observed current executable result.','Modern path/target-loss,provider/owner/stat/RNG/world and other missile/exceptional/PvP/full named/class/client/asset acceptance remain separate.'])
    p=ROOT/'tools/reference/guided-arrow-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Guided Arrow ToHit0/locked hit route bound;current PMH inference remains explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
