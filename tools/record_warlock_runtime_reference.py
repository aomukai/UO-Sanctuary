#!/usr/bin/env python3
"""Verify saved code-reference captures offline; never open process memory."""
import argparse,csv,hashlib,json,re,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,digest
from pe_unwind_groups import UnwindGroups

def record(check=False):
 image=Image();artifacts={};snapshots={}
 def include(p):
  p=Path(p);artifacts[str(p.relative_to(ROOT))]=digest(p.read_bytes())
 def saved(name):
  p=STAGE/(name+'.json');j=json.loads(p.read_text());include(p)
  assert j['referenceVersion']=='3.3.93847'
  assert j['executableSha256']==image.ref['executable']['sha256']
  assert not any(j.get(k,False) for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
  assert j['bytesRead']<=8192
  for n,h in j.get('artifacts',{}).items():assert digest((ROOT/n).read_bytes())==h,n;include(ROOT/n)
  snapshots[name]=dict(bytesRead=j['bytesRead'],process=j['process']);return j
 sample=saved('code-sample');assert sample['sample']['runtimeInvalidInstructionMarkers']==0
 events=saved('event-candidates');bodies=saved('event-bodies-17-28-36-37-40')
 saved('inactive-known-code-sample');cold=saved('known-cold-code-sample');assert cold['changedBytes']==0 and cold['invalidInstructionMarkers']>0
 saved('event36-window');ai=saved('generic-pet-candidate');helpers=saved('generic-helpers-actions')
 treepath=ROOT/'.local/class-skills/d2moo-source-tree.json';tree=json.loads(treepath.read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 rel='source/D2Game/src/AI/AiThink.cpp';source=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=source.read_bytes()
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
 block=raw.decode().split('static const D2AiTableStrc gpAiTable_6FD3F990[] =',1)[1].split('};',1)[0]
 rows=[list(m) for m in re.findall(r'\{\s*(\d+)\s*,\s*(\w+)\s*,\s*(\w+)\s*,\s*(\w+)\s*\}',block)];assert len(rows)==148
 assert rows==json.loads((STAGE/'ai-table-candidates.json').read_text())['classicRows'];include(source);include(treepath)
 changed=[];values=[]
 for i,row in enumerate(rows):
  v=struct.unpack('<QQQQ',image.bytes(0x1d18810+i*32,32));values.append(v)
  assert all((p==0)==(n=='nullptr') for p,n in zip(v[1:],row[1:]))
  if v[0]!=int(row[0]):changed.append(dict(index=i,legacy=int(row[0]),current=v[0]))
 assert changed==ai['changedClassicFlags'] and len(changed)==3
 for a,b in [(1,100),(12,19),(80,92),(106,143)]:assert values[a][2]==values[b][2]
 with (STAGE/'monai.txt').open(newline='',encoding='utf-8-sig') as f:monai=list(csv.DictReader(f,delimiter='\t'))
 assert monai[148]['AI']=='GenericPet' and ai['genericPetParameters']==monai[148]
 groups=UnwindGroups(image);assert ai['genericPetRva']=='0x4b60c0' and ai['allUnwindFragmentsCaptured']
 assert [(int(x['rva'],16),int(x['endRva'],16),int(x['unwindRva'],16)) for x in ai['fragments']]==groups.fragments(0x4b60c0)
 assert sum(int(x['endRva'],16)-int(x['rva'],16) for x in ai['fragments'])==781
 assert helpers['directCallTargetsVerified']
 for r in helpers['records']:
  assert r['allUnwindFragmentsCaptured']
  assert [(int(x['rva'],16),int(x['endRva'],16),int(x['unwindRva'],16)) for x in r['fragments']]==groups.fragments(int(r['rva'],16))
 em=ROOT/'tools/reference/generic-pet-parameter-emulation.json';e=json.loads(em.read_text());include(em)
 assert e['matrixCases']==2016 and e['machineCodeBytes']==818 and e['sourceCodeExecutedInOfflineEmulator'] and not e['targetProcessCodeExecuted']
 for n,h in e['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n;include(ROOT/n)
 lib=ROOT/e['emulatorLibrary']['path'];assert digest(lib.read_bytes())==e['emulatorLibrary']['sha256'];include(lib)
 for n in ['tools/record_warlock_runtime_reference.py','tools/probe_warlock_runtime.py','tools/pe_unwind_groups.py']:include(ROOT/n)
 result=dict(referenceVersion='3.3.93847',component='Bounded current-code reference and GenericPet raw initializer',executableSha256=image.ref['executable']['sha256'],processMemoryRead=True,readOnly=True,targetProcessCodeExecuted=False,offlineInitializerCodeExecuted=True,dispatcherLinkVerified=False,completeAiVerified=False,ownerCallbacksVerified=False,handlerEnabled=False,goalComplete=False,
  verified=dict(classicNullnessPatterns=148,classicAliasPairs=4,changedClassicFlags=changed,genericPetIndex=148,genericPetRootBytes=781,currentInitializerBytes=818,offlineInitializerContexts=2016,ownedDemonProfiles=3),snapshots=snapshots,artifacts=dict(sorted(artifacts.items())),
  established=['Existing OS permissions allowed bounded double-stable reads of pinned headers, immutable tables and known executable ranges. No attach, target input, memory writes or security changes. Full argv, heap, saves and authentication contents were not sampled.',
   'Current AI table layout is corroborated by 148 classic callback-nullness patterns, four aliases and eight runtime descriptor matches. Three flag differences remain explicit. Owned monai names index148 GenericPet; a current dispatcher link is still unverified.',
   'All known chained-unwind fragments of the GenericPet root and its initializer/follow/combat candidates were captured. Chained unwind grouping is not a certificate of every exception or failure path. Full AI and owner callbacks remain unverified.',
   'Actual captured initializer instructions run only offline against synthetic memory and declared services. 2016 boundary contexts and three owned demon profiles establish raw decoding before unspecified owner-iterator effects. No current-game code is executed in the target by these tools.',
   'A separate 32-byte cold-page experiment through an existing read-only procfs descriptor matched encrypted disk bytes and is unusable as behavioral evidence. No protection was changed and no forced decryption was attempted. A 45-second natural readability watch captured no new callback body.'],
  remaining=['Verify current dispatcher/index linkage, owner callback adjustments, target/skill choice, movement/event/seed and NPC combat dependencies before enabling a summon.', 'Modern passive writer, duplicated sunder fields, ordinary-hit events and unlocked NPC skill callbacks remain independent gaps.', 'Proprietary captured binaries/disassembly remain ignored local files; the committed receipt records hashes and scope only. Historical zero-memory receipts describe their earlier snapshots.'])
 p=ROOT/'tools/reference/warlock-runtime-reference-evidence.json';out=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==out
 else:p.write_text(out)
 print('Current reference verified offline:148 classic patterns,781-byte root,818-byte initializer,2016 contexts;full AI remains gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
