#!/usr/bin/env python3
"""Resolve the owner-loss notification's immutable functor and capture its Invoke."""
import json,re,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups

def capture():
 image=Image();parentpath=STAGE/'callees-pet-missing-owner-20261109.json';parent=json.loads(parentpath.read_text())
 for name,h in parent['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
 text='\n'.join((ROOT/n).read_text()for n in parent['artifacts']if n.endswith('.asm'))
 assert '# 0x141722cd0'in text and 'call   0x140333e10'in text
 linkpath=STAGE/'callees-pet-owner-loss-links-20261109.json';link=json.loads(linkpath.read_text())
 for name,h in link['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
 text='\n'.join((ROOT/n).read_text()for n in link['artifacts']if '-333e10-'in n and n.endswith('.asm'))
 assert 'call   QWORD PTR [rax+0x8]'in text
 target=STAGE/'pet-death-functor-20261109.json';assert not target.exists(),'Preserve earlier captures'
 reader=CodeReader(image);artifacts=[parentpath,linkpath,Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py'];records=[]
 try:
  reader.anchors();raw=image.bytes(0x1722cd0,16);assert image.section(0x1722cd0,16)['name']=='.rdata'
  p=STAGE/'pet-death-functor-20261109-table.bin';p.write_bytes(raw);artifacts.append(p)
  destructor,invoke=[x-image.base for x in struct.unpack('<2Q',raw)];unwind=UnwindGroups(image)
  row=dict(rva=hex(invoke),bytes=0,fragments=[],semanticIdentityVerified=False)
  try:parts=unwind.fragments(invoke)
  except StopIteration:
   raw=reader.stable(invoke,32,'code');sample=STAGE/'pet-death-functor-20261109-leaf-sample.bin';sample.write_bytes(raw);asm,bad=disassemble(sample,invoke,image.base);artifacts.extend([sample,asm]);assert bad==0
   instructions=[]
   for line in asm.read_text().splitlines():
    match=re.match(r'\s*([0-9a-f]+):\s*((?:[0-9a-f]{2}\s+)+)\s*(\S+)\s*(.*)',line)
    if not match:continue
    address,encoded,mnemonic,operands=match.groups();instructions.append((int(address,16),len(encoded.split()),mnemonic,operands))
    if mnemonic=='jmp':break
    assert not mnemonic.startswith('j')and mnemonic not in ['call','ret','retq'],'Leaf boundary is not a straight-line tail thunk'
   address,size,mnemonic,operands=instructions[-1];assert mnemonic=='jmp'and operands.strip()=='0x140392be0'
   end=address-image.base+size;assert end-invoke<=32
   row['boundary']='No unwind entry;straight-line bounded leaf ending in qualified owner-loss tail jump'
   parts=[(invoke,end,None)]
  for i,(begin,end,u)in enumerate(parts):
   size=end-begin;row['bytes']+=size;entry=next((m for m in reader.maps if m[0]<=image.base+begin and image.base+end<=m[1]),None)
   f=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u)if u is not None else None,permissions=entry[2]if entry else 'cross-map',captured=False)
   if entry and 'r'in entry[2]and 'x'in entry[2]and size<=4096 and reader.total+size*2<=8192:
    raw=reader.stable(begin,size,'code');p=STAGE/f'pet-death-functor-20261109-{invoke:x}-part{i}.bin';p.write_bytes(raw);asm,bad=disassemble(p,begin,image.base);artifacts.extend([p,asm]);f.update(captured=True,sha256=digest(raw),invalidInstructionMarkers=bad)
   row['fragments'].append(f)
  row['allUnwindFragmentsCaptured']=all(f['captured']for f in row['fragments']);records.append(row)
 finally:reader.close()
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],parent=str(parentpath.relative_to(ROOT)),notification=str(linkpath.relative_to(ROOT)),immutableVtableRva='0x1722cd0',destructorRva=hex(destructor),invokeRva=hex(invoke),immutableTableSource='pinned installed image;not sampled at runtime',indirectInvokeLinkVerified=True,process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,records=records,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(invokeRva=hex(invoke),bytesRead=reader.total,records=records)))
if __name__=='__main__':capture()
