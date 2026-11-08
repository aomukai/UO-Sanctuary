#!/usr/bin/env python3
"""Authenticate new owned skill-do captures without reopening game memory."""
import argparse,hashlib,json
from pathlib import Path
from probe_warlock_runtime import ROOT,Image
from record_inferno_held_input import preservation
BASE=ROOT/'.local/class-skills/warlock-runtime-reference'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 image=Image();names=['warlock-new-do-20261108/capture.json','warlock-common-do-20261108/capture.json']+[f'warlock-do-{n}-20261108/capture.json'for n in range(156,168)]+['warlock-new-do-20261108/services-ring-progressive-helpers.json','warlock-new-do-20261108/services-ring-missile-factory.json']
 captures=[];complete={};unavailable={};artifacts={};processes=set()
 for name in names:
  p=BASE/name;r=json.loads(p.read_text());assert r['referenceVersion']=='3.3.93847'and r['executableSha256']==image.ref['executable']['sha256']and r['processMemoryRead']
  assert r['bytesRead']<=8192 and r['bytesRead']==2*sum(v['size']for v in r['reads'])
  assert not any(r[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
  if name.endswith('/capture.json'):assert r['immutableTableMatches']and r['dispatcherIndexLinked']and not r['callbackSemanticsVerified']
  else:assert r['sourceReferencesVerified']
  processes.add((r['process']['pid'],r['process']['startTicks'],r['process']['name']))
  for rel,h in r['artifacts'].items():assert sha(ROOT/rel)==h,rel;artifacts[rel]=h
  artifacts[str(p.relative_to(ROOT))]=sha(p);captures.append(dict(path=str(p.relative_to(ROOT)),bytesRead=r['bytesRead'],sha256=sha(p)))
  for row in r['records']:
   key='do'+str(row['slot'])if'slot'in row else row['rva']
   assert row['allUnwindFragmentsCaptured']==all(f['captured']for f in row['fragments'])
   for f in row['fragments']:
    if f['captured']:assert 'r'in f['permissions']and'x'in f['permissions']and f['invalidInstructionMarkers']==0
   if row['allUnwindFragmentsCaptured']:
    complete[key]=dict(rva=row['rva'],bytes=sum(int(f['endRva'],16)-int(f['rva'],16)for f in row['fragments']),fragments=len(row['fragments']),capture=str(p.relative_to(ROOT)),fragmentSha256=[f['sha256']for f in row['fragments']])
   elif key not in complete:unavailable[key]=dict(rva=row['rva'],permissions=[f['permissions']for f in row['fragments']if not f['captured']],capture=str(p.relative_to(ROOT)))
 assert set(complete)=={'do154','do155','do157','do158','do163','do164','do167','0x400760'}and len(processes)==1
 for key in complete:unavailable.pop(key,None)
 events=[]
 for name in ['event-bodies-40-37.json','event-bodies-35-36-39.json']:
  p=BASE/'current-20261108'/name;r=json.loads(p.read_text());assert r['bytesRead']==1216 and r['immutableCandidateTableMatches']and not r['callbackIdentitiesVerified']
  assert all(not x['allUnwindFragmentsCaptured']for x in r['records']);events.extend(dict(slot=x['slot'],rva=x['rva'],unavailable=True)for x in r['records']);artifacts[str(p.relative_to(ROOT))]=sha(p)
  for rel,h in r['artifacts'].items():assert sha(ROOT/rel)==h,rel;artifacts[rel]=h
 assert {r['slot']for r in events}=={35,36,37,39,40}
 skills=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/skills.json').read_text());mapped=[dict(id=int(k),name=r['skill'],serverDo=r.get('srvdofunc',0),itemEffect=r.get('ItemEffect',0))for k,r in skills.items()if int(r.get('srvdofunc',0))in [154,155,157,158,163,164,167]or int(r.get('ItemEffect',0))in [154,155,157,158,163,164,167]]
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],handlers=210,completedClasses=[],productionChanged=False,newNativeRuns=0,process=[dict(pid=p,startTicks=t,name=n)for p,t,n in processes],captures=captures,complete=complete,unavailable=unavailable,eventCandidatesUnavailable=events,skillTableMapping=mapped,dispatcherIndexLinked=True,callbackSemanticsVerified=False,preservation=preservation(),gameMemoryWritten=False,processInput=False,ptraceAttach=False,securitySettingsChanged=False,artifacts=artifacts,
 established=['Preserved complete current dispatcher3aaf70bounds primary do by190,loads the index from skill field4e (or alternate ItemEffect context),and calls table163a8b0[index]. The freshly read immutable191pointer table equals the pinned executable. This resolves index linkage beyond earlier unique-shape corroboration.','Complete current bodies154/155/157/158/163/164/167and directly referenced missile factory400760are captured with all unwind fragments and no invalid-instruction markers. No runtime heap/player state/input/write/protection change was used. Raw binaries/disassembly stay ignored locally.','The process uses Linux comm Main. Exact process-name pgrep D2R.exe is insufficient to establish closure;use probe_warlock_runtime.processes(),then CodeReader identity/anchors for reference reads.'],
 limits=['Dispatch index linkage and complete capture do not establish the detailed semantics of every callee,formula/damage/event ordering or a playable skill. No handler/class is certified.','Do56/156/159/160/161/162/165/166and Ring progressive helpers remain incomplete in these captures. Some initial omissions were capture-budget limits;separate per-slot observations resolve the final complete set. Protected or incomplete code is never synthesized from partial fragments.','Event candidates35/36/37/39/40remain inaccessible;their callback identities/semantics are still unverified. No unchanged polling window or forced access was used.','Next translate/emulate the complete Ring launch or Sigil/Cleave body using existing factory/vector/skill services;qualify needed helper and missile-damage contexts before enabling learned casts.'])
 p=ROOT/'tools/reference/warlock-skill-do-current-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Current Warlock reference:7complete linked do functions plus full missile factory;5reactive event candidates still inaccessible;no production/native/save changes.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
