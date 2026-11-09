#!/usr/bin/env python3
"""Bounded callback/iterator capture from the current removal's exact LEA/CALL."""
import json,struct
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups

def capture():
 parentPath=STAGE/'callees-pet-detach-effects-20261109.json';parent=json.loads(parentPath.read_text())
 for n,h in parent['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
 row=next(r for r in parent['records']if r['rva']=='0x35c720');assert row['allUnwindFragmentsCaptured']
 fragment=next(f for f in row['fragments']if int(f['rva'],16)<=0x35c7ed<int(f['endRva'],16))
 path=next(ROOT/n for n,h in parent['artifacts'].items()if n.endswith('.bin')and h==fragment['sha256'])
 raw=path.read_bytes();offset=0x35c7ed-int(fragment['rva'],16);sequence=raw[offset:offset+21]
 assert sequence==bytes.fromhex('488d15acebffff488bcd f30f7f442444 e89e5a0100'.replace(' ',''))
 callback=0x35c7ed+7+struct.unpack_from('<i',sequence,3)[0];iterator=0x35c7fd+5+struct.unpack_from('<i',sequence,17)[0]
 assert callback==0x35b3a0 and iterator==0x3722a0
 target=STAGE/'pet-detach-broadcast-20261109.json';assert not target.exists(),'Preserve earlier captures'
 image=Image();groups=UnwindGroups(image);reader=CodeReader(image);records=[];artifacts=[parentPath,path,image.reference]
 try:
  reader.anchors()
  for rva in [callback,iterator]:
   parts=groups.fragments(rva);record=dict(rva=hex(rva),bytes=sum(b-a for a,b,u in parts),fragments=[])
   for i,(begin,end,u)in enumerate(parts):
    size=end-begin;entry=next((m for m in reader.maps if m[0]<=image.base+begin and image.base+end<=m[1]),None)
    f=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=entry[2]if entry else 'cross-map',captured=False)
    if entry and 'r'in entry[2]and'x'in entry[2]and size<=4096 and reader.total+size*2<=8192:
     raw=reader.stable(begin,size,'code');p=STAGE/f'pet-detach-broadcast-20261109-{rva:x}-part{i}.bin';p.write_bytes(raw);asm,bad=disassemble(p,begin,image.base);artifacts.extend([p,asm]);f.update(captured=True,sha256=digest(raw),invalidInstructionMarkers=bad)
    record['fragments'].append(f)
   record['allUnwindFragmentsCaptured']=all(f['captured']for f in record['fragments']);records.append(record)
 finally:reader.close()
 artifacts.extend([ROOT/'tools/capture_pet_detach_broadcast.py',ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py'])
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],parent=str(parentPath.relative_to(ROOT)),reference=dict(siteRva='0x35c7ed',callbackRva=hex(callback),iteratorRva=hex(iterator),argumentRegister='RDX',sequenceSha256=digest(sequence)),sourceReferencesVerified=True,process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,records=records,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,records=[{k:r[k]for k in ['rva','bytes','allUnwindFragmentsCaptured']}for r in records])))
if __name__=='__main__':capture()
