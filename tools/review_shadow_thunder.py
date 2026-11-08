#!/usr/bin/env python3
"""Authenticate unchanged Thunder emitters/getters; do not rerun old matrices."""
import hashlib,json
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';routines={};inputs={}
 for name,key in [('martial-lightning-source-evidence.json','compiledRoutines'),('thunder-damage-source-evidence.json','blockHashes')]:
  p=ROOT/'tools/reference'/name;r=json.loads(p.read_text());inputs[str(p.relative_to(ROOT))]=sha(p);texts=[]
  for rel,h in r['sourceHashes'].items():
   f=BASE/'d2moo-pinned-source'/rel;raw=f.read_bytes();assert sha(f)==h and hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(t['sha']for t in tree['tree']if t['path']==rel);texts.append(raw.decode())
  for name,h in r[key].items():
   hashes=[]
   for text in texts:
    try:body=function(text,name)
    except(AssertionError,ValueError):continue
    hashes.append(hashlib.sha256(body.encode()).hexdigest())
   assert h in hashes,name;routines[name]=h
 root=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((root/'sources.json').read_text());p=root/'json/missiles.json';assert sha(p)==manifest['files']['json/missiles.json']['sha256'];rows=json.loads(p.read_text())
 for name in ['clawsofthundernova','clawsofthunderbolt']:
  r=next(r for r in rows.values()if r['Missile']==name);assert r['HitClass']==64 and r['GetHit']==1 and r.get('pSrvDmgFunc',0)==0 and r['NextDelay']==4 and r['NextHit']==1
 inputs[str(p.relative_to(ROOT))]=sha(p)
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],routineHashes=routines,inputs=inputs,newCompiledControls=0,scope='Existing complete stacked dispatcher/36/37/nova emitter/charged initializer and numeric getter proof reused. Nova64/no caster draw precedes bolt16/one caster draw. Each factory gets a world unit seed; bolt initializer then replaces its private seed with spread low and consumes the existing charged-path stream. Shared path/velocity/collision/lease engine retained. Current child HitClass64/GETHIT/no srvdmg validated. Own NPC typed damage/seed/persistence/world/AI composition is separate native verification;modern parity/lifecycle/event/geometry/PvP scopes remain.')
 (ROOT/'tools/reference/shadow-thunder-source-review.json').write_text(json.dumps(evidence,indent=2)+'\n');print('Authenticated',len(routines),'unchanged Thunder helpers/current child routes;no duplicate compiler matrix.')
if __name__=='__main__':main()
