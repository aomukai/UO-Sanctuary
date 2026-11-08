#!/usr/bin/env python3
"""Authenticate reused Ice helpers and the progressive-state ordering source."""
import hashlib,json
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';routines={};inputs={}
 for name,key in [('fists-fire-source-evidence.json','compiledRoutines'),('martial-fire-release-source-evidence.json','compiledRoutines'),('ice-damage-source-evidence.json','blockHashes')]:
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
 # These full bodies are source-reviewed, not claimed as new compiled controls.
 reviewed={}
 for rel,name in [('source/D2Common/src/DataTbls/DataTbls.cpp','DATATBLS_LoadStatesTxt'),('source/D2Game/src/SKILLS/SkillAss.cpp','sub_6FCF5870')]:
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(t['sha']for t in tree['tree']if t['path']==rel);reviewed[name]=hashlib.sha256(function(raw.decode(),name).encode()).hexdigest()
 p=ROOT/'tools/reference/ice-impact-source-evidence.json';impact=json.loads(p.read_text());rel='source/D2Game/src/MISSILES/MissMode.cpp';raw=(BASE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha256(raw).hexdigest()==impact['sourceHashes'][rel]
 body=function(raw.decode(),'MISSMODE_SrvDmg10_BladesOfIceCubes');assert hashlib.sha256(body.encode()).hexdigest()==impact['cubeDamage10FunctionHash'];routines['MISSMODE_SrvDmg10_BladesOfIceCubes']=impact['cubeDamage10FunctionHash'];inputs[str(p.relative_to(ROOT))]=sha(p)
 root=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((root/'sources.json').read_text());p=root/'json/missiles.json';assert sha(p)==manifest['files']['json/missiles.json']['sha256'];r=next(r for r in json.loads(p.read_text()).values()if r['Missile']=='bladesoficecubes');assert r['pSrvDmgFunc']==10 and r['Range']==30 and r['ELen']==100 and r['EMin']==r['EMax']==1 and not r.get('GetHit',0);inputs[str(p.relative_to(ROOT))]=sha(p)
 states=json.loads((ROOT/'data/Sanctuary/Balance/generic-pet-states.json').read_text());assert [states[i]['name']for i in [124,125,126,127]]==['progressive_other','progressive_fire','progressive_cold','progressive_lightning']
 e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],reusedRoutineHashes=routines,additionalReviewedRoutineHashes=reviewed,inputs=inputs,newCompiledControls=0,scope='Existing complete inline/conversion and38/39/dispatcher proof plus complete elemental writer/endpoints reused. Writer replaces weapon cold length;count3 adds freeze from that final skill length. Copied area chills only. Cube damage10 keeps cold100 and copies it to freeze;constant1..1before mastery/no synergy. Ascending state loader and full dispatcher reviewed for mixed Phoenix124/Fire125/Ice126/Thunder127 order;first38drains parent before later callbacks. Current numeric rows authenticated. Native own source/status/cube/world/combined-family/save composition requires separate receipt;modern callback/capture/removal/geometry/PvP remain scoped.')
 (ROOT/'tools/reference/shadow-ice-source-review.json').write_text(json.dumps(e,indent=2)+'\n');print('Authenticated',len(routines),'reused helpers;',len(reviewed),'additional whole-body reviews;no duplicate compiler matrix.')
if __name__=='__main__':main()
