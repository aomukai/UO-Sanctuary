#!/usr/bin/env python3
"""Corroborate the current AI table and sample its GenericPet candidate."""
import csv,hashlib,json,os,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,identity,digest,disassemble
from pe_unwind_groups import UnwindGroups
TABLE=0x1d18810;STRIDE=32

def capture():
 target=STAGE/'generic-pet-candidate.json';assert not target.exists(),'Preserve captures'
 image=Image();old=json.loads((STAGE/'ai-table-candidates.json').read_text())['classicRows'];assert len(old)==148
 flags=[];values=[];differences=[]
 for i,row in enumerate(old):
  v=struct.unpack('<QQQQ',image.bytes(TABLE+i*STRIDE,STRIDE));values.append(v)
  assert all((p==0)==(n=='nullptr') for p,n in zip(v[1:],row[1:]))
  if v[0]!=int(row[0]):differences.append(dict(index=i,legacy=int(row[0]),current=v[0]))
 for a,b in [(1,100),(12,19),(80,92),(106,143)]:assert values[a][2]==values[b][2]
 assert differences==[dict(index=15,legacy=4,current=5),dict(index=23,legacy=1,current=3),dict(index=52,legacy=5,current=6)]
 with (STAGE/'monai.txt').open(encoding='utf-8-sig',newline='') as f:monai=list(csv.DictReader(f,delimiter='\t'))
 assert monai[148]['AI']=='GenericPet';flags,init,pointer,alternate=struct.unpack('<QQQQ',image.bytes(TABLE+148*STRIDE,STRIDE));assert (flags,init,alternate)==(0,0,0);rva=pointer-image.base
 fragments=UnwindGroups(image).fragments(rva);reader=CodeReader(image);records=[];artifacts=[]
 try:
  reader.anchors()
  for index in [0,1,2,12,19,100,147,148]:
   at=TABLE+index*STRIDE;va=image.base+at;entry=next(x for x in reader.maps if x[0]<=va and va+STRIDE<=x[1]);assert 'r' in entry[2];assert identity(reader.identity['pid'])==reader.identity
   first=os.pread(reader.fd,STRIDE,va);second=os.pread(reader.fd,STRIDE,va);reader.total+=STRIDE*2;assert first==second==image.bytes(at,STRIDE)
   reader.reads.append(dict(kind='pinned-static-ai-descriptor',index=index,rva=hex(at),size=STRIDE,stable=True,permissions=entry[2],sha256=digest(first)))
  for i,(begin,end,unwind) in enumerate(fragments):
   size=end-begin;va=image.base+begin;entry=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None);item=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(unwind),permissions=entry[2] if entry else 'cross-map',captured=False)
   if entry and 'r' in entry[2] and 'x' in entry[2] and size<=4096 and reader.total+2*size<=8192:
    body=reader.stable(begin,size,'code');p=STAGE/f'generic-pet-candidate-part{i}.bin';p.write_bytes(body);asm,bad=disassemble(p,begin,image.base);artifacts += [p,asm];item.update(captured=True,sha256=digest(body),invalidInstructionMarkers=bad)
   records.append(item)
  assert identity(reader.identity['pid'])==reader.identity
 finally:reader.close()
 for p in [STAGE/'monai.txt',STAGE/'monai-extraction.json',Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py']:artifacts.append(p)
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,aiTableRva=hex(TABLE),recordStride=STRIDE,classicDescriptorCount=148,classicNullnessMatches=148,classicAliasPairs=4,changedClassicFlags=differences,genericPetIndex=148,genericPetRva=hex(rva),genericPetParameters=monai[148],fragments=records,allUnwindFragmentsCaptured=all(x['captured'] for x in records),dispatcherLinkVerified=False,mechanicsVerified=False,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in artifacts},
  established='Current static AI descriptor layout is corroborated by148classic nullness patterns and all4old function aliases,with3explicit flag changes. Owned monai index148names GenericPet. Eight exact runtime descriptor matches bind the live table. No dispatcher reference or behavioral equivalence is claimed yet.')
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:result[k] for k in ['bytesRead','classicNullnessMatches','changedClassicFlags','genericPetRva','allUnwindFragmentsCaptured']}))
if __name__=='__main__':capture()
