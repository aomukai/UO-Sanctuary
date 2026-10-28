#!/usr/bin/env python3
"""Inspect one known immutable event-candidate pointer table read-only."""
import argparse,json,os,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,identity,digest,disassemble
TABLE=0x163a750;COUNT=41

def immutable_table(reader):
 assert identity(reader.identity['pid'])==reader.identity;image=reader.image;size=COUNT*8;assert image.section(TABLE,size)['name']=='.rdata';va=image.base+TABLE
 entry=next(x for x in reader.maps if x[0]<=va and va+size<=x[1]);assert 'r' in entry[2] and 'w' not in entry[2]
 assert reader.total+2*size<=8192;first=os.pread(reader.fd,size,va);second=os.pread(reader.fd,size,va);reader.total+=2*size
 assert first==second and len(first)==size and identity(reader.identity['pid'])==reader.identity
 reader.reads.append(dict(kind='immutable-event-candidate-table',rva=hex(TABLE),size=size,permissions=entry[2],stable=True,sha256=digest(first)));return first

def capture():
 target=STAGE/'event-candidates.json';assert not target.exists(),'Preserve accepted captures'
 image=Image();reader=CodeReader(image);artifacts=[];candidates=[]
 try:
  reader.anchors();raw=immutable_table(reader);file=image.bytes(TABLE,COUNT*8);words=struct.unpack('<41Q',raw)
  assert words[0]==0 and all(image.section(v-image.base)['name']=='.text' for v in words[1:]);p=STAGE/'event-candidate-table.bin';p.write_bytes(raw);artifacts.append(p)
  for slot in [17,28,36,37,39,40]:
   rva=words[slot]-image.base;begin,end,unwind=next(x for x in image.functions if x[0]==rva);size=end-begin;va=image.base+rva
   entry=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None);item=dict(slot=slot,rva=hex(rva),size=size,permissions=entry[2] if entry else 'cross-map',identityVerified=False)
   if not entry or 'r' not in entry[2] or 'x' not in entry[2]:item['sampled']=False;item['reason']='Not a readable executable range;no forced read'
   else:
    body=reader.stable(rva,size,'code');p=STAGE/f'event-candidate-{slot}.bin';p.write_bytes(body);asm,bad=disassemble(p,rva,image.base);artifacts += [p,asm];item.update(sampled=True,invalidInstructionMarkers=bad,changedBytes=sum(a!=b for a,b in zip(body,image.bytes(rva,size))))
   candidates.append(item)
 finally:reader.close()
 artifacts += [Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py']
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,tableRva=hex(TABLE),tableMatchesPinnedFile=raw==file,candidates=candidates,callbackIdentitiesVerified=False,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in artifacts})
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,tableMatchesPinnedFile=raw==file,candidates=candidates)))
def check():
 r=json.loads((STAGE/'event-candidates.json').read_text());assert r['executableSha256']==Image().ref['executable']['sha256']
 for name,h in r['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
 assert not any(r[k] for k in ['callbackIdentitiesVerified','processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten']);assert all(not c['sampled'] or 'r' in c['permissions'] and 'x' in c['permissions'] for c in r['candidates']);print('Event-candidate reference checked offline;identities remain unverified.')
if __name__=='__main__':
 p=argparse.ArgumentParser();g=p.add_mutually_exclusive_group(required=True);g.add_argument('--capture',action='store_true');g.add_argument('--check',action='store_true');a=p.parse_args();capture() if a.capture else check()
