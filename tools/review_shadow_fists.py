#!/usr/bin/env python3
"""Authenticate unchanged progressive helpers and exact current Fists field routing."""
import hashlib,json
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';routines={};inputs={};receipts=[]
 for name,key in [('fists-fire-source-evidence.json','compiledRoutines'),('martial-fire-release-source-evidence.json','compiledRoutines'),('shadow-finisher-source-review.json','routines')]:
  p=ROOT/'tools/reference'/name;r=json.loads(p.read_text());receipts.append(str(p.relative_to(ROOT)));inputs[str(p.relative_to(ROOT))]=sha(p)
  for rel,h in r['sourceHashes'].items():
   f=BASE/'d2moo-pinned-source'/rel;raw=f.read_bytes();assert sha(f)==h and hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(t['sha']for t in tree['tree']if t['path']==rel)
  for name,h in r[key].items():
   matches=[]
   for rel in r['sourceHashes']:
    text=(BASE/'d2moo-pinned-source'/rel).read_text()
    try:body=function(text,name)
    except (AssertionError,ValueError):continue
    matches.append(hashlib.sha256(body.encode()).hexdigest())
   assert h in matches,name;routines[name]=h
 # Current pin/catalogue preserves the blank srvdmg slot as zero; the
 # neighboring ImmolationFire row really has srvdmg3. Do not infer inheritance.
 root=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((root/'sources.json').read_text());p=root/'json/missiles.json';assert sha(p)==manifest['files']['json/missiles.json']['sha256'];rows=json.loads(p.read_text());f=next(r for r in rows.values()if r['Missile']=='fistsoffirefirewall');i=next(r for r in rows.values()if r['Missile']=='immolationfire')
 assert f.get('pSrvDmgFunc',0)==0 and i['pSrvDmgFunc']==3 and f['pSrvDoFunc']==1 and f['HitClass']==32 and f['Range']==64 and f['Size']==2 and not f.get('GetHit',0)
 inputs[str(p.relative_to(ROOT))]=sha(p)
 skill=root/'json/skills.json';assert sha(skill)==manifest['files']['json/skills.json']['sha256'];s=json.loads(skill.read_text())['259'];assert [s['srvprgfunc1'],s['srvprgfunc2'],s['srvprgfunc3']]==[143,38,39]and s['prgstack']==1 and s['prgdam']==4 and s.get('srvmissilea',0)==0
 inputs[str(skill.relative_to(ROOT))]=sha(skill)
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],reusedEvidence=receipts,routineHashes=routines,inputs=inputs,newCompiledControls=0,currentField=dict(missile=557,serverDo=1,damageCallback=0,frames=64,size=2,hitClass=32,getHit=False,applyMastery=True),scope='Authenticate existing full prgdam4/conversion/do35/progressive dispatcher/38/39/143/common Attack proofs;no duplicate compiled matrix. Fists143 has no missileA;38 drains parent before copied area;39 takes one caster seed then local scatter. Current field has no Immolation srvdmg3 draw. Own NPC contact/area/field,private RNG and actual save/world delivery require the separate native receipt. Modern callback/lifecycle acceptance remains scoped.')
 (ROOT/'tools/reference/shadow-fists-source-review.json').write_text(json.dumps(evidence,indent=2)+'\n');print('Authenticated',len(routines),'unchanged complete source helpers;current Fists field has no srvdmg3.')
if __name__=='__main__':main()
