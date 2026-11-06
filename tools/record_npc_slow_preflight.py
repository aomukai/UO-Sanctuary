#!/usr/bin/env python3
"""Pin the contradictory slow callback and one maps-only current candidate check."""
import argparse,json,struct,hashlib
from pathlib import Path
from review_weapon_event_order import function
from probe_warlock_runtime import ROOT,Image,digest,processes,identity
from pe_unwind_groups import UnwindGroups
OUT=ROOT/'tools/reference/npc-item-slow-reference-preflight.json'
def record(check=False):
 image=Image();words=struct.unpack('<41Q',image.bytes(0x163a750,41*8));entry=words[19]-image.base;fragments=UnwindGroups(image).fragments(entry)
 source=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/SKILLS/SkillItem.cpp';raw=source.read_bytes();tree=ROOT/'.local/class-skills/d2moo-source-tree.json';pin=json.loads(tree.read_text())
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in pin['tree']if r['path']=='source/D2Game/src/SKILLS/SkillItem.cpp')
 body=function(raw.decode(),'SKILLITEM_EventFunc19_Slow');assert 'if (nSlowValue)'in body and 'pCurse.nDuration = 750;'in body
 candidate=dict(slot=19,rva=hex(entry),fragments=[dict(rva=hex(a),endRva=hex(b))for a,b,_ in fragments])
 if check:
  result=json.loads(OUT.read_text());assert result['candidate']==candidate and result['legacyBodySha256']==digest(body.encode())
  for p,h in result['inputs'].items():assert digest((ROOT/p).read_bytes())==h,p
 else:
  assert not OUT.exists(),'Keep prior observations frozen;do not retry unchanged candidates'
  ps=processes();assert len(ps)==1;process=ps[0];maps=[]
  for line in Path('/proc',str(process['pid']),'maps').read_text().splitlines():
   p=line.split();a,b=(int(x,16)for x in p[0].split('-'));maps.append((a,b,p[1]))
  permissions=[]
  for a,b,_ in fragments:
   regions=[dict(rva=hex(max(x,image.base+a)-image.base),endRva=hex(min(y,image.base+b)-image.base),permissions=p)for x,y,p in maps if x<image.base+b and y>image.base+a]
   permissions.append(dict(rva=hex(a),endRva=hex(b),regions=regions,readableExecutable=sum(int(p['endRva'],16)-int(p['rva'],16)for p in regions)==b-a and all('r'in p['permissions']and'x'in p['permissions']for p in regions)))
  assert identity(process['pid'])==process
  paths=[Path(__file__).resolve(),image.reference,source,tree,ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py',ROOT/'docs/approved-npc-slow-rule.md']
  result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],legacyCommit=pin['sha'],legacyBodySha256=digest(body.encode()),legacyContradiction='The unchanged positive-value guard returns0;zero falls through to a750frame zero-valued slowed state.',candidate=candidate,process=process,observedPermissions=permissions,bytesRead=0,processMemoryRead=False,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,callbackIdentityVerified=False,currentSemanticsVerified=False,approvedAdaptation='Approve this shared slow rule',inputs={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in paths},limits=['File/owned-process identity/maps only;no protected code read or forced. Candidate table linkage and in-memory identity remain unverified. Do not retry without an external state change.','User-approved pet item/outgoing-Clay slow deliberately overrides this unresolved callback. Incoming Clay27,ranged timing and unrelated player-item behavior remain separate.'])
  OUT.write_text(json.dumps(result,indent=2)+'\n')
 assert not any(result[k]for k in ('processMemoryRead','processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten','callbackIdentityVerified','currentSemanticsVerified'))and result['bytesRead']==0
 print('Item slow reference:contradictory older guard/current candidate pinned;zero game-memory bytes;approved adaptation is separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
