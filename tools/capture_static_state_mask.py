#!/usr/bin/env python3
"""Bounded read-only static state-mask reference;never inspect actors or inventories."""
import json,os,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,identity,digest
OUT=ROOT/'.local/class-skills/generic-pet-state-mask/static-mask-capture.json'

def capture():
 assert not OUT.exists(),'Preserve captures'
 paths=[ROOT/'tools/probe_warlock_runtime.py',Path(__file__).resolve(),ROOT/'.local/class-skills/generic-pet-metadata/states.bin',ROOT/'.local/class-skills/generic-pet-metadata/states.txt']
 for name,needle in [('callees-live-actions-execute-20261029.json','4b2810'),('callees-live-search-services-idle-20261030.json','27ddb0')]:
  path=STAGE/name;r=json.loads(path.read_text());paths.append(path)
  selected=[rec for rec in r['records'] if int(rec['rva'],16)==int(needle,16)]
  assert len(selected)==1 and selected[0]['allUnwindFragmentsCaptured']
  assert all(f['captured'] and not f['invalidInstructionMarkers'] for f in selected[0]['fragments'])
  for n,h in r['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  code=[ROOT/n for n in r['artifacts'] if n.endswith('.asm') and needle in n];assert code
  text='\n'.join(p.read_text() for p in code)
  assert ('0x2c8' in text if needle=='4b2810' else '0x410' in text and '0x141e03ac0' in text)
  paths += code
 binary=(ROOT/'.local/class-skills/generic-pet-metadata/states.bin').read_bytes();assert struct.unpack_from('<I',binary)[0]==232 and len(binary)==4+232*68
 image=Image();reader=CodeReader(image);reads=[];views=[];failure=None
 def data(address,size,meaning):
  assert identity(reader.identity['pid'])==reader.identity and 0<size<=1024 and reader.total+2*size<=8192
  assert 0x10000<=address<(1<<47)
  mapping=next((m for m in reader.maps if m[0]<=address and address+size<=m[1]),None);assert mapping is not None and 'r' in mapping[2],meaning+':unreadable'
  a=os.pread(reader.fd,size,address);b=os.pread(reader.fd,size,address);reader.total+=2*size
  assert len(a)==size and a==b and identity(reader.identity['pid'])==reader.identity,meaning+':unstable'
  reads.append(dict(meaning=meaning,address=hex(address),size=size,permissions=mapping[2],sha256=digest(a),stable=True));return a
 try:
  reader.anchors()
  for selector in [1,2,3]:
   slot=image.base+0x1e03ac0+selector*16;table=struct.unpack('<Q',data(slot,8,'fixed source table slot '+str(selector)))[0]
   view=dict(selector=selector,table=hex(table),compatible=False);views.append(view)
   if table==0:view['reason']='Null table slot';continue
   rows,count=struct.unpack('<QQ',data(table+0x2c8,16,'state rows/count '+str(selector)));view.update(rows=hex(rows),count=count)
   if count!=232:view['reason']='Different current table extent;no following read';continue
   samples=[]
   for i in [0,2,9,30,105,139,140,231]:
    actual=data(rows+i*68,68,'owned compiled state row '+str(selector)+':'+str(i));expected=binary[4+i*68:4+(i+1)*68]
    assert actual==expected,('Runtime state row differs from owned compiled row',selector,i)
    samples.append(dict(id=i,sha256=digest(actual)))
   mask=struct.unpack('<Q',data(table+0x410,8,'source mask410 pointer '+str(selector)))[0];assert mask!=0 and mask%4==0
   raw=data(mask,32,'static mask410 words '+str(selector));words=list(struct.unpack('<8I',raw));assert words[-1]&0xffffff00==0,'Mask bits exceed232states'
   view.update(compatible=True,rowSamples=samples,maskPointer=hex(mask),words=words,stateIds=[i for i in range(232) if words[i//32]&(1<<(i&31))])
 except Exception as e:failure=dict(type=type(e).__name__,message=str(e))
 finally:reader.close()
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],process=reader.identity,processMemoryRead=True,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,actorDataRead=False,inventoryDataRead=False,
 purpose='Only fixed source table slots,state rows/count,eight known compiled row samples per compatible view,and the32-byte mask at the verified0x410field are read. No actor lookup or arbitrary scanning.',bytesRead=reader.total,codeAnchorReads=reader.reads,metadataReads=reads,views=views,failure=failure,
 artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(paths))})
 OUT.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,views=views,failure=failure)))
if __name__=='__main__':capture()
