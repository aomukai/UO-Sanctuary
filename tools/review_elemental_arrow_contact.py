#!/usr/bin/env python3
"""Bind existing Fire/Cold Arrow conversion and current impact metadata."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT
from review_magic_arrow_contact import record as parent

def record(check=False):
    parent(True);cached=ROOT/'tools/reference/d2r-named-equipment';source=cached/'json/missiles.json';sha=hashlib.sha256(source.read_bytes()).hexdigest();assert sha==json.loads((cached/'sources.json').read_text())['files']['json/missiles.json']['sha256'];rows=json.loads(source.read_text());runtime={r['key']:r['fields']for r in json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text())};fields={}
    for name,frames in (('firearrow',40),('coldarrow',20)):
        f={k:rows[name].get(k,0)for k in ('GetHit','HitClass','pSrvDoFunc','pSrvDmgFunc','ToHit','NextHit','NextDelay','Range','Vel')};assert f==dict(GetHit=1,HitClass=0,pSrvDoFunc=1,pSrvDmgFunc=1,ToHit=1,NextHit=0,NextDelay=0,Range=frames,Vel=24);assert all(runtime[name].get(k,0)==v for k,v in f.items());fields[name]=f
    result=dict(referenceVersion='3.3.93847',sharedConversionImpactReviewSha256=hashlib.sha256((ROOT/'tools/reference/magic-arrow-contact-source-review.json').read_bytes()).hexdigest(),currentMissilesSha256=sha,missileFields=fields,established=['Both arrows reuse bound srvdmg1 conversion after weapon fill and critical;existing skill/item elemental capture and mastery order are preserved.','Current row HitClass0 is passed into the shared packet/return selector;do not reuse Magic Arrow\'s HitClass10.','Reuse accuracy PMH before block,post-handler captured armor,source poison/drain/cold and pre-HP missile item hit events. No melee wear/Thorns or invented missile-attack event.','Existing integer cold resistance,item immunity/half-length,difficulty and owner-retention helpers are reused;ordinary cold corpse-shatter remains unsupported.'],remaining=['Exact modern skill AR,conversion/mastery/stat/provider/owner/RNG/attack timing,exceptional/PvP/full named/class/client/asset parity remains unverified.','This does not enable Ice Arrow freezing,explosive arrows or other missile families.'])
    p=ROOT/'tools/reference/elemental-arrow-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Fire/Cold Arrow shared conversion and HitClass0 rows bound;modern and shatter limits retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
