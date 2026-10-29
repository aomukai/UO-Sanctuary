#!/usr/bin/env python3
"""Locate a corroborated start-table candidate in the pinned file;metadata only."""
import argparse,json,re,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,Image,digest,processes,identity
from pe_unwind_groups import UnwindGroups
OUT=ROOT/'tools/reference/barbarian-current-callback-evidence.json'
def record(check=False):
 image=Image();base=image.base;text=image.section(0x1000);lo=base+text['rva'];hi=lo+text['size'];candidates=[]
 source=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/SKILLS/Skills.cpp';raw=source.read_bytes()
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 import hashlib
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']=='source/D2Game/src/SKILLS/Skills.cpp')
 block=raw.decode().split('constexpr SkillStartFunc gpSkillSrvStartFnTable_6FD408B0[] =',1)[1].split('};',1)[0]
 names=re.findall(r'^\s*(nullptr|SKILLS_\w+)\s*,',block,re.M);assert len(names)==91 and [i for i,n in enumerate(names[:66]) if n=='nullptr']==[0,30]
 skillsPath=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';skills=json.loads(skillsPath.read_text());used=sorted({int(r.get('srvstfunc',0)) for r in skills.values()});assert max(used)==70
 for sec in [s for s in image.sections if s['name'] in ['.rdata','.data']]:
  blob=image.bytes(sec['rva'],sec['rawsize']);values=struct.unpack('<'+'Q'*(len(blob)//8),blob[:len(blob)//8*8])
  for i in range(len(values)-91):
   if values[i] or not(lo<=values[i+1]<hi):continue
   row=values[i:i+91]
   if any(row[j] for j in range(71,91)) or any(not(lo<=row[j]<hi) for j in range(1,71)):continue
   differences=[j for j,n in enumerate(names[:66]) if (row[j]==0)!=(n=='nullptr')]
   if differences==[30]:candidates.append((sec['rva']+8*i,row))
 assert len(candidates)==1,[(hex(a),len(v)) for a,v in candidates]
 rva,values=candidates[0];assert rva==0x163aeb0
 groups=UnwindGroups(image);selected=[]
 for slot,label in [(41,'Leap Attack start41'),(67,'Frenzy start67'),(68,'Double Swing start68')]:
  entry=values[slot]-base;parts=groups.fragments(entry)
  selected.append(dict(slot=slot,label=label,candidateRva=hex(entry),unwindBytes=sum(b-a for a,b,_ in parts),fragments=[dict(rva=hex(a),size=b-a) for a,b,_ in parts]))
 static=dict(tableCandidateRva=hex(rva),candidateCount=1,classicPrefixSlots=66,classicNullnessMatches=65,classicNullnessDifferences=[30],classicAlias25_64Retained=values[25]==values[64],currentUsedStartMaximum=70,currentUsedNonzeroStartsNonNull=True,zeroTailSlots=list(range(71,91)),selected=selected)
 if check:
  prior=json.loads(OUT.read_text());assert prior['staticCorroboration']==static
  for n,h in prior['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
 else:
  ps=processes();observed=[]
  if len(ps)==1:
   process=ps[0];maps=[]
   for line in Path('/proc',str(process['pid']),'maps').read_text().splitlines():
    p=line.split();a,b=(int(x,16) for x in p[0].split('-'));maps.append((a,b,p[1]))
   assert identity(process['pid'])==process
   for row in selected:
    permissions=[next((p for a,b,p in maps if a<=base+int(f['rva'],16) and base+int(f['rva'],16)+f['size']<=b),'cross-map') for f in row['fragments']]
    observed.append(dict(slot=row['slot'],permissions=permissions,readableExecutable=all('r' in p and 'x' in p for p in permissions)))
  else:process=None
  artifacts=[source,ROOT/'.local/class-skills/d2moo-source-tree.json',skillsPath,image.reference,Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py']
  result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],staticCorroboration=static,process=process,observedPermissions=observed,processMemoryRead=False,memoryBytesRead=0,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,dispatcherIndexLinkVerified=False,callbackSemanticsVerified=False,handlerEnabled=False,
   artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in artifacts},limits=['This is a corroborated table candidate,not a verified dispatcher/index/semantic identity. Matching nullness,used-start coverage and tail shape cannot certify callback behavior. Old slot30 and25/64alias differences remain explicit.',
    'Only the pinned executable file and owned-process identity/map permissions are inspected. No protected callback bytes are read or forced. The observed entries are unreadable;older null start67/68 and changed modern Leap Attack still block those learned mechanics. Continue unaffected work.'])
  OUT.write_text(json.dumps(result,indent=2)+'\n')
 print('Corroborated current table candidate:65/66classic nullness slots;start41/67/68byte bounds pinned;dispatcher and callback semantics unverified.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
