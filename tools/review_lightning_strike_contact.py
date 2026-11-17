#!/usr/bin/env python3
"""Bind source0 Lightning Strike chain events and the approved opening adaptation."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE,function
from review_magic_arrow_contact import record as parent

def record(check=False):
    parent(True);tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());records={}
    for path,names in {'source/D2Game/src/MISSILES/MissMode.cpp':['MISSMODE_SrvHit12_ChainLightning_LightningStrike','MISSMODE_SetDamageFlags','MISSMODE_SrvDmgHitHandler'],'source/D2Game/src/UNIT/SUnitDmg.cpp':['SUNITDMG_ExecuteEvents','SUNITDMG_ExecuteMissileDamage'],'source/D2Game/src/SKILLS/SkillAma.cpp':['SKILLS_SrvDo014_LightningStrike']}.items():
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==path)
        for name in names:body=function(raw.decode(),name);records[name]=dict(path=path,sha256=hashlib.sha256(body.encode()).hexdigest())
    cache=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((cache/'sources.json').read_text());p=cache/'json/missiles.json';sha=hashlib.sha256(p.read_bytes()).hexdigest();assert sha==manifest['files']['json/missiles.json']['sha256'];row=json.loads(p.read_text())['lightningstrike'];runtime=next(r['fields']for r in json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text())if r['key']=='lightningstrike')
    fields=dict(pSrvDoFunc=1,pSrvHitFunc=12,pSrvDmgFunc=0,ToHit=0,GetHit=1,ResultFlags=4,HitFlags=0,HitClass=64,SrcDamage=0,Range=25,Size=2,CollideKill=1,LastCollide=1,NextHit=1,NextDelay=4,ReturnFire=1)
    assert all(row.get(k,0)==v and runtime.get(k,0)==v for k,v in fields.items())
    evidence={}
    for name in ('lightning-strike-engine-evidence','lightning-strike-damage-source-evidence','lightning-strike-sanctuary-decision','amazon-thrust-contact-source-review'):
        p=ROOT/f'tools/reference/{name}.json';evidence[str(p.relative_to(ROOT))]=hashlib.sha256(p.read_bytes()).hexdigest()
    source=json.loads((ROOT/'tools/reference/lightning-strike-engine-evidence.json').read_text());p=ROOT/source['probe']['fixture'];evidence[str(p.relative_to(ROOT))]=hashlib.sha256(p.read_bytes()).hexdigest()
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],functions=records,currentMissilesSha256=sha,missileFields=fields,retainedSourceEvidence=evidence,established=['Source0 chain receives Successful1/GetHit4/HitClass64/HitFlags0. Elemental HP/absorb/credit/kill/native return apply without weapon damage/attack callbacks,proc allocation,Crushing Blow,drain,poison/cold status,slow or armor.','The user-approved opening remains:melee lightning then a different neighbor;no outgoing chain when alone and no invented hidden opening primary re-hit. Existing identity search,four-frame target leases,finite budget,source path and per-hop recapture are retained.','Existing impact-time spell-potion components remain pending a nonblocking scope question. Immediate added poison uses zero status frames and must not replace ongoing poison;added cold has no chill. This is a Sanctuary potion rule,not a D2R source claim.','The shared Sorceress/Mage/NPC/legacy compatibility branches stay unchanged;Lightning Strike records missing required player-packet metadata remain refused.'],remaining=['Complete older bodies and archived current rows do not establish exact modern events/stat/RNG/owner/room/grid/NHD/scheduler or current executable parity.','Potion classification is a nonblocking user question;current behavior is preserved pending reply. Modern literal calc1ED20 overlap,exceptional/uninterruptible/death-delay/hireling/PvP and full named/class/client/asset acceptance remain open.'])
    out=ROOT/'tools/reference/lightning-strike-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Lightning Strike source0 chain flags and approved opening bound;other chain producers unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
