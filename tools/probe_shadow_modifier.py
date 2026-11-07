#!/usr/bin/env python3
"""Qualify captured Shadow modifier42 registration;no death/AI admission."""
import argparse,itertools,json,os,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_decisions import Emulator,BASE,STACK,MEM,STOP,GAME,PET
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,identity,digest
TABLE=0x163b270;ENTRY=0x3d5fd0;ALIGN=BASE+0x24eb30;DATA=MEM+0x15000;REGION=MEM+0x16000
CAPTURE=STAGE/'shadow-modifier-table-20261107.json';OUT=ROOT/'tools/reference/shadow-modifier-source-evidence.json'

def capture():
 assert not CAPTURE.exists(),'Preserve prior captures'
 parent=STAGE/'services-shadow-umod-20261107.json';proof=json.loads(parent.read_text());r=proof['records'][0]
 assert r['rva']==hex(ENTRY)and r['allUnwindFragmentsCaptured']and r['bytes']==256
 for p,h in proof['artifacts'].items():assert digest((ROOT/p).read_bytes())==h,p
 fragment=next(f for f in r['fragments']if f['rva']=='0x3d6072')
 code=next((ROOT/p).read_bytes()for p,h in proof['artifacts'].items()if p.endswith('.bin')and h==fragment['sha256'])
 assert code[0x29:0x2c]==bytes.fromhex('4c 8d 0d')and 0x3d609b+7+struct.unpack_from('<i',code,0x2c)[0]==TABLE
 image=Image();assert image.ref['executable']['sha256']==proof['executableSha256'];reader=CodeReader(image)
 try:
  reader.anchors();rva=TABLE+41*8;size=32;va=image.base+rva
  assert image.section(rva,size)['name']=='.rdata'
  entry=next(m for m in reader.maps if m[0]<=va and va+size<=m[1]);assert'r'in entry[2]and'w'not in entry[2]
  assert identity(reader.identity['pid'])==reader.identity
  first=os.pread(reader.fd,size,va);second=os.pread(reader.fd,size,va);reader.total+=size*2
  assert first==second==image.bytes(rva,size)and identity(reader.identity['pid'])==reader.identity
  reader.reads.append(dict(kind='immutable-modifier-init-slots41to44',rva=hex(rva),size=size,permissions=entry[2],stable=True,sha256=digest(first)))
 finally:reader.close()
 assert struct.unpack('<4Q',first)[1:3]==(0,0)
 binary=STAGE/'shadow-modifier-table-20261107.bin';binary.write_bytes(first)
 artifacts=[parent,binary,Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',image.reference]
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],process=reader.identity,tableRva=hex(TABLE),firstSlot=41,values=list(struct.unpack('<4Q',first)),immutableTableMatches=True,bytesRead=reader.total,reads=reader.reads,processMemoryRead=True,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
 CAPTURE.write_text(json.dumps(result,indent=2)+'\n')

class Modifier(Emulator):
 def __init__(self):
  assert unicorn.__version__=='2.1.4'
  self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  self.proof=json.loads((STAGE/'services-shadow-umod-20261107.json').read_text());record=self.proof['records'][0]
  assert record['rva']==hex(ENTRY)and record['bytes']==256 and record['allUnwindFragmentsCaptured']
  for n,h in self.proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  for f in record['fragments']:
   assert f['captured']and f['invalidInstructionMarkers']==0
   p=next(ROOT/n for n,h in self.proof['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);b=p.read_bytes();a=BASE+int(f['rva'],16)
   self.map(a,len(b));self.uc.mem_write(a,b);self.ranges.append((a,a+len(b)));self.artifacts.append(p)
  for a,n in [(STACK,0x40000),(MEM,0x20000),(STOP,4096),(BASE+TABLE,512),(ALIGN,1)]:self.map(a,n)
  self.uc.mem_write(ALIGN,b'\xc3');self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def hook(self,uc,address,size,user):
  if any(a<=address<b for a,b in self.ranges):return
  assert address==ALIGN and uc.reg_read(UC_X86_REG_RCX)==PET,('Undeclared external code',hex(address))
  self.calls+=1;sp=uc.reg_read(UC_X86_REG_RSP);uc.reg_write(UC_X86_REG_RAX,self.alignment);uc.reg_write(UC_X86_REG_RSP,sp+8);uc.reg_write(UC_X86_REG_RIP,self.q(sp))
 def execute(self,unit,hasdata,unique,special,already,alignment,region,occupied):
  self.uc.mem_write(MEM,b'\0'*0x20000);self.uc.mem_write(STACK,b'\0'*0x40000)
  self.wi(PET,unit);self.wq(PET+0x10,DATA if hasdata else 0);self.wq(GAME+0x1d0,REGION if region else 0)
  self.uc.mem_write(DATA+0x1a,struct.pack('<H',0x100|(8 if already else 0)));self.uc.mem_write(DATA+0x30,bytes([2 if special else 0]))
  before=[7]*occupied+[0]*(9-occupied);self.uc.mem_write(DATA+0x20,bytes(before));self.wi(REGION+0x2d4,123)
  self.alignment=alignment;self.calls=0;sp=STACK+0x30000;self.wq(sp,STOP)
  for r,v in [(UC_X86_REG_RCX,GAME),(UC_X86_REG_RDX,PET),(UC_X86_REG_R8,42),(UC_X86_REG_R9,unique),(UC_X86_REG_RSP,sp)]:self.uc.reg_write(r,v)
  self.uc.emu_start(BASE+ENTRY,STOP,count=1000)
  active=unit==1 and hasdata;expected=before.copy()
  if active and occupied<9:expected[occupied]=42
  flags=struct.unpack('<H',bytes(self.uc.mem_read(DATA+0x1a,2)))[0];count=struct.unpack('<I',bytes(self.uc.mem_read(REGION+0x2d4,4)))[0]
  calls=int(active and unique and not special and region and not already)
  assert list(self.uc.mem_read(DATA+0x20,9))==expected and flags==(0x100|(8 if already or active and unique else 0))
  assert count==123+int(calls and alignment!=2)and self.calls==calls
  return [unit,hasdata,unique,special,already,alignment,region,occupied,flags,count,self.calls,expected]

def check(write=False):
 c=json.loads(CAPTURE.read_text());assert c['immutableTableMatches']and c['values'][1:3]==[0,0]and c['bytesRead']<=8192
 assert c['bytesRead']==2*sum(r['size']for r in c['reads'])and all(r['stable']and r['size']<=4096 for r in c['reads'])
 assert not any(c[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
 for p,h in c['artifacts'].items():assert digest((ROOT/p).read_bytes())==h,p
 em=Modifier();cases=[em.execute(*a)for a in itertools.product([0,1],[0,1],[0,1],[0,1],[0,1],[0,1,2],[0,1],range(10))];assert len(cases)==1920
 data=json.dumps(cases,separators=(',',':'))+'\n';p=ROOT/'.local/class-skills/shadow-modifier-cases.json'
 if write:p.write_text(data)
 else:assert p.read_text()==data
 artifacts=[CAPTURE,STAGE/'services-shadow-umod-20261107.json',p,*em.artifacts,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_decisions.py']
 result=dict(referenceVersion='3.3.93847',executableSha256=c['executableSha256'],completeCapturedFunctionBytes=256,completeFragments=5,sourceContexts=len(cases),modifier=42,initializerPointer=0,currentModifierRegistrationVerified=True,deathBehaviorVerified=False,handlerEnabled=False,artifacts={str(a.relative_to(ROOT)):digest(a.read_bytes())for a in artifacts},scope=['The actual captured birth helper calls this complete assignment routine. Its code indexes immutable initializer table0x163b270;current slot42 is null. Full-array/empty-slot,unit/data,unique flag,existing unique/special flag,region and alignment controls execute actual captured instructions.','Modifier42 is stored in the first empty one of nine slots;unique request ORs source monster flag8 even if slots are full. Eligible region count increments use the explicit alignment service. No initializer callback runs for42.'],limits=['Alignment service body,source region allocation/count consumers,modifier death callback and Nova/ordinary skill execution remain separate. A source unique flag is not permission to grant a UO elite modifier. No runtime code or learned Shadow route changes.'])
 encoded=json.dumps(result,indent=2)+'\n'
 if write:OUT.write_text(encoded)
 else:assert OUT.read_text()==encoded
 print('Current Shadow modifier42:1920whole-function controls;first of9slots,source unique/region rules,null initializer;death/Nova and live actor remain unverified.')
if __name__=='__main__':
 p=argparse.ArgumentParser();g=p.add_mutually_exclusive_group(required=True);g.add_argument('--capture',action='store_true');g.add_argument('--check',action='store_true');a=p.parse_args()
 if a.capture:capture()
 check(a.capture)
