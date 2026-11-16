#!/usr/bin/env python3
"""Bind pure-lightning reset to ordered weapon hit events and zero-ToHit PMH bypass."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE,function
from review_magic_arrow_contact import record as parent

def record(check=False):
    parent(True);tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());records={};bodies={}
    for path,names in {'source/D2Game/src/MISSILES/MissMode.cpp':['MISSMODE_FillDamageParams','MISSMODE_ResetDamageParams','MISSMODE_AddDamageValue','MISSMODE_SrvDmg12_LightningJavelin','MISSMODE_SetDamageFlags','MISSMODE_SrvDmgHitHandler'],'source/D2Game/src/SKILLS/SkillItem.cpp':['SKILLITEM_EventFunc14_Freeze']}.items():
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==path)
        for name in names:body=function(raw.decode(),name);bodies[name]=body;records[name]=dict(path=path,sha256=hashlib.sha256(body.encode()).hexdigest())
    body=bodies['MISSMODE_SrvDmg12_LightningJavelin'];assert body.index('MISSMODE_GetDamageValue(')<body.index('MISSMODE_ResetDamageParams(')<body.index('MISSMODE_AddDamageValue(')
    cache=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((cache/'sources.json').read_text());hashes={}
    for name in ('skills','missiles'):
        p=cache/'json'/(name+'.json');hashes[name]=hashlib.sha256(p.read_bytes()).hexdigest();assert hashes[name]==manifest['files']['json/'+name+'.json']['sha256']
    row=json.loads((cache/'json/missiles.json').read_text())['lightningjavelin'];runtime=next(r['fields']for r in json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text())if r['key']=='lightningjavelin');fields=dict(pSrvDoFunc=1,pSrvHitFunc=0,pSrvDmgFunc=12,ToHit=0,HitClass=64,GetHit=1,NextHit=0,NextDelay=0,Range=25,Size=2,dParam1=100);assert all(row.get(k,0)==v and runtime.get(k,0)==v for k,v in fields.items())
    p=ROOT/'tools/reference/lightning-bolt-engine-evidence.json';legacy=json.loads(p.read_text());assert legacy['pinnedSkill']['SrcDam']==96;probe=ROOT/legacy['probe']['path'];assert hashlib.sha256(probe.read_bytes()).hexdigest()==legacy['probe']['sha256']
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],functions=records,sourceHashes=hashes,missileFields=fields,skillSourceDamage=96,retainedSourceEvidence={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest()},established=['The existing complete creator/impact differentials retain SrcDam96,enhanced/critical physical conversion,second lightning roll and full damage/length/drain reset. The shared actor allocator explicitly receives zero poison/length and drain for this route.','SetDamageFlags adds source weapon HitFlags32 after damage reset:successful item hit callbacks remain,including independent attacker item-freeze;cleared missile cold does not clear owner equipment. No launch or missile attack event is invented.','Current ToHit0 bypasses the older complete accuracy routine,including its PMH setup,as with Guided Arrow. Native AR and physical shield delegates must remain unused for the pure-lightning packet;modern live PMH remains unverified.','Reuse existing source-step collision,Pierce/LastCollide,25-frame lifetime,TH/IAS input,mana/quantity,packet/stat capture and saves. Captured armor still belongs to the separate post-handler stat effect.'],remaining=['Archived current rows and complete older functions establish the scoped adapter,not modern seed/provider/owner/stat/geometry/global scheduler parity.','Actual current zero-ToHit PMH,exceptional/uninterruptible/death-delay/hireling/PvP and depleted-provider semantics remain explicitly unverified.','Fury/poison-cloud children,other missile consumers,whole named acquisition,full class/client and separate effect/audio/icon assets remain open.'])
    out=ROOT/'tools/reference/lightning-bolt-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Lightning Bolt reset/weapon-event bodies bound;zero-ToHit bypass and independent item freeze retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
