#!/usr/bin/env python3
"""Bind Multiple Shot's existing fan partition and shared weapon missile impact."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE,function
from review_magic_arrow_contact import record as parent

def record(check=False):
    parent(True);tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());path='source/D2Game/src/SKILLS/SkillAma.cpp';raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==path)
    body=function(raw.decode(),'SKILLS_SrvDo008_MultipleShot_Teeth_ShockWave');assert body.index('missileParams.dwFlags |= 0x10000')<body.index('missileParams.dwFlags &= 0xFFFEFFFF')<body.rindex('missileParams.dwFlags |= 0x10000')
    cached=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((cached/'sources.json').read_text());source=cached/'json/missiles.json';sha=hashlib.sha256(source.read_bytes()).hexdigest();assert sha==manifest['files']['json/missiles.json']['sha256'];rows=json.loads(source.read_text());runtime={r['key']:r['fields']for r in json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text())};fields={}
    for name in ('multipleshotarrow','multipleshotbolt'):
        f={k:rows[name].get(k,0)for k in ('GetHit','HitClass','pSrvDoFunc','pSrvDmgFunc','ToHit','NextHit','NextDelay','Range','Vel','SrcDamage')};assert f==dict(GetHit=1,HitClass=10,pSrvDoFunc=1,pSrvDmgFunc=0,ToHit=1,NextHit=1,NextDelay=4,Range=50,Vel=24,SrcDamage=96);assert all(runtime[name].get(k,0)==v for k,v in f.items());fields[name]=f
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],fan=dict(path=path,sha256=hashlib.sha256(body.encode()).hexdigest()),sharedImpactReviewSha256=hashlib.sha256((ROOT/'tools/reference/magic-arrow-contact-source-review.json').read_bytes()).hexdigest(),currentMissilesSha256=sha,missileFields=fields,established=['Existing do8/Teeth fan emits central flags without0x10000 and outer flags with0x10000. No fan geometry is rewritten.','Creation flags feed the shared missile damage event suppression. Weapon damage and source poison/drain remain;outer flags suppress attacker hit events,not recipient events or accuracy PMH.','Use current GetHit1/HitClass10,pSrvDmgFunc0 and SrcDamage96. Reuse captured target armor and the approved persistent world/actor proc stream.','Existing per-cast/recipient four-frame lease and pierce helper remain unchanged;separate casts retain independent identities. No missile attack event is invented.'],remaining=['Exact modern fan seed correlation,NHD owner identity,quiver/stat/provider/owner/world and attack timing remain unverified.','This qualifies existing Sanctuary lease behavior,not a newly recovered modern global hit-delay rule. Other missile families,exceptional/PvP/full named/class/client/asset acceptance remain separate.'])
    p=ROOT/'tools/reference/multiple-shot-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Multiple Shot shared do8 partition and current arrow/bolt impact rows bound;modern NHD identity remains explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
