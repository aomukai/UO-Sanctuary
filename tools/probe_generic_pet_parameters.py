#!/usr/bin/env python3
"""Execute captured current initializer offline with explicit helper services.

No live process access. Unicorn has only synthetic memory and pinned code/data.
"""
import argparse,csv,hashlib,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_warlock_runtime import ROOT,STAGE,Image,digest
BASE=0x140000000;ENTRY=BASE+0x4b0b90;STACK=0x20000000;OBJECTS=0x30000000;STOP=0x40000000
GAME=OBJECTS;PET=OBJECTS+0x1000;OWNER=OBJECTS+0x2000;TICK=OBJECTS+0x3000;CTRL=OBJECTS+0x4000;MON=OBJECTS+0x5000;MONDATA=OBJECTS+0x6000;CFG=OBJECTS+0x7000;TARGET=OBJECTS+0x8000
COPY=BASE+0x4b0ed0;TARGET_FN=BASE+0x4b06a0;UNIT_FN=BASE+0x27acb0;ITERATE=BASE+0x35e6d0;COOKIE=BASE+0x155eeb0
class Initializer:
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.inputs={};self.hooks=[]
  proof=json.loads((STAGE/'generic-helpers-actions.json').read_text());assert proof['directCallTargetsVerified'];record=next(r for r in proof['records'] if r['rva']=='0x4b0b90');assert record['allUnwindFragmentsCaptured']
  for name,h in proof['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
  self.code=[]
  for i,f in enumerate(record['fragments']):
   a=BASE+int(f['rva'],16);p=STAGE/f'generic-actions-4b0b90-part{i}.bin';b=p.read_bytes();assert digest(b)==f['sha256'];self.map(a,len(b));self.uc.mem_write(a,b);self.ranges.append((a,a+len(b)));self.code.append(p)
  for rva in [0x1813840,0x1813460,0x18141c0]:self.map(BASE+rva,16);self.uc.mem_write(BASE+rva,self.image.bytes(rva,16))
  self.map(BASE+0x197a178,8);self.uc.mem_write(BASE+0x197a178,b'\0'*8)
  self.map(STACK,0x40000);self.map(OBJECTS,0x10000);self.map(STOP,0x1000)
  for address in [COPY,TARGET_FN,UNIT_FN,ITERATE,COOKIE]:self.map(address,1);self.uc.mem_write(address,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200)
  self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def map(self,address,size):
  for p in range(address&~0xfff,(address+size+0xfff)&~0xfff,0x1000):
   if p not in self.pages:self.uc.mem_map(p,0x1000);self.pages.add(p)
 def q(self,address):return struct.unpack('<Q',self.uc.mem_read(address,8))[0]
 def putq(self,address,value):self.uc.mem_write(address,struct.pack('<Q',value))
 def hook(self,uc,address,size,user):
  if any(a<=address<b for a,b in self.ranges):return
  rcx=uc.reg_read(UC_X86_REG_RCX);rdx=uc.reg_read(UC_X86_REG_RDX);rax=0
  if address==COPY:
   assert rcx==CFG;uc.mem_write(CFG,bytes(uc.mem_read(rdx,0x70)));rax=CFG;self.hooks.append('copy-defaults')
  elif address==TARGET_FN:
   assert rcx==GAME and rdx==PET;rax=TARGET;self.hooks.append('target-service')
  elif address==UNIT_FN:
   assert rcx in [PET,OWNER];rax=11 if rcx==PET else 12;self.hooks.append('unit-service-'+str(rcx==OWNER))
  elif address==ITERATE:
   assert rcx==GAME and rdx==OWNER and uc.reg_read(UC_X86_REG_R8)==BASE+0x4b08b0;arg=uc.reg_read(UC_X86_REG_R9);assert self.q(arg)==PET and self.q(arg+8)==CFG;self.hooks.append('owner-iterator-noop')
  elif address==COOKIE:self.hooks.append('cookie-service')
  else:raise AssertionError(('Undeclared code executed',hex(address)))
  rsp=uc.reg_read(UC_X86_REG_RSP);ret=self.q(rsp);uc.reg_write(UC_X86_REG_RAX,rax);uc.reg_write(UC_X86_REG_RSP,rsp+8);uc.reg_write(UC_X86_REG_RIP,ret)
 def run(self,params,present=True,owner=-1):
  assert len(params)==24 and all(-32768<=v<=32767 for v in params);uc=self.uc;uc.mem_write(OBJECTS,b'\0'*0x10000);uc.mem_write(STACK,b'\0'*0x40000);self.hooks=[]
  uc.mem_write(PET,struct.pack('<I',1));self.putq(PET+0x10,MONDATA);self.putq(MONDATA+0x48,0x12345678);self.putq(TICK,CTRL);self.putq(TICK+0x28,MON if present else 0);uc.mem_write(MON+0x90,struct.pack('<24h',*params));uc.mem_write(CTRL+0x44,struct.pack('<3i',7,8,9))
  if owner>=0:uc.mem_write(OWNER,struct.pack('<I',owner))
  rsp=STACK+0x20008;self.putq(rsp,STOP);self.putq(rsp+0x28,CFG)
  uc.reg_write(UC_X86_REG_RSP,rsp);uc.reg_write(UC_X86_REG_RCX,GAME);uc.reg_write(UC_X86_REG_RDX,PET);uc.reg_write(UC_X86_REG_R8,OWNER if owner>=0 else 0);uc.reg_write(UC_X86_REG_R9,TICK)
  uc.emu_start(ENTRY,STOP,count=5000);assert uc.reg_read(UC_X86_REG_RIP)==STOP,'Initializer did not return';raw=bytes(uc.mem_read(CFG,0x70));assert self.q(MONDATA+0x48)==0 and self.q(CFG+0x50)==TARGET
  assert struct.unpack_from('<3i',raw,0x58)==(7,8,9) and struct.unpack_from('<i',raw,0x64)[0]==11
  assert ('owner-iterator-noop' in self.hooks)==(owner==0)
  values=[struct.unpack_from('<i',raw,o)[0] for o in [0,4,8,12,16]]+[struct.unpack_from('<H',raw,0x14)[0],struct.unpack_from('<i',raw,0x18)[0],raw[0x1c],struct.unpack_from('<i',raw,0x20)[0],struct.unpack_from('<i',raw,0x24)[0],raw[0x28]]+[struct.unpack_from('<i',raw,o)[0] for o in [0x2c,0x30,0x34,0x38,0x3c,0x40,0x44,0x48,0x4c]]
  return values,self.hooks.copy()
def expected(p,present):
 if not present:return [784,2500,60,0,0,0,0,0,0,16,0,0,0,225,0,25,625,0,0,0]
 return [p[0]*p[0],p[1]*p[1],p[2],p[3],p[4],p[5]&65535,max(0,min(2,p[6])),int(p[7]!=0),max(0,min(1,p[8])),p[9],max(0,min(13,p[10])),p[11],p[12]*p[12],p[13]*p[13],p[14],p[15]*p[15],p[16]*p[16],p[17],p[18],p[19]]
def build(check=False):
 em=Initializer();cases=[];values=[-32768,-101,-1,0,1,2,3,4,5,12,13,14,255,32767]
 for present in [False,True]:
  for owner in [-1,0,1]:
   for field in range(24):
    for value in values:
     p=[0]*24;p[field]=value;out,hooks=em.run(p,present,owner);assert out==expected(p,present),(present,owner,field,value,out,expected(p,present));cases.append([int(present),owner,field,value,out])
 assert len(cases)==2016
 mono=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/monstats.json').read_text());profiles=[]
 receipt=json.loads((STAGE/'monstats-extraction.json').read_text());assert receipt['sha256']==digest((STAGE/'monstats.txt').read_bytes()) and receipt['executableSha256']==em.image.ref['executable']['sha256']
 with (STAGE/'monstats.txt').open(newline='',encoding='utf-8-sig') as f:owned={r['Id']:r for r in csv.DictReader(f,delimiter='\t') if r.get('AI')=='GenericPet'}
 assert set(owned)=={k for k,r in mono.items() if r.get('AI')=='GenericPet'}
 for key,row in mono.items():
  if row.get('AI')!='GenericPet':continue
  p=[int(owned[key].get('aip'+str(i)+s,'') or 0) for i in range(1,9) for s in ['','(N)','(H)']];assert p==[int(row.get('aip'+str(i)+s,0)) for i in range(1,9) for s in ['','(N)','(H)']];out,hooks=em.run(p,True,0);assert out==expected(p,True);profiles.append(dict(key=key,parameters=p,values=out))
 output=STAGE/'generic-parameter-emulation.json';body=json.dumps(dict(cases=cases,profiles=profiles),separators=(',',':'))+'\n'
 if check:assert output.read_text()==body
 else:output.write_text(body)
 libraries=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libraries)==1
 data=ROOT/'data/Sanctuary/Balance/generic-pet-parameters.json';projection=json.dumps([dict(Key=p['key'],Parameters=p['parameters']) for p in profiles],indent=2)+'\n'
 if check:assert data.read_text()==projection
 else:data.write_text(projection)
 probe=ROOT/'.local/class-skills/generic-pet-parameters-source-probe.json'
 numeric=output.read_bytes()
 if check:assert probe.read_bytes()==numeric
 else:probe.write_bytes(numeric)
 result=dict(referenceVersion='3.3.93847',unicornVersion=unicorn.__version__,emulatorLibrary=dict(path=str(libraries[0].relative_to(ROOT)),sha256=digest(libraries[0].read_bytes())),currentCapturedInitializerRva='0x4b0b90',machineCodeBytes=sum(b-a for a,b in em.ranges),matrixCases=len(cases),profiles=[p['key'] for p in profiles],processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,
  services=['Copy/reset the initial parameter prefix from the supplied default temporary;dynamic container ownership is not emulated.','Target lookup returns a supplied synthetic pointer;one-unit property returns11/12.','Owner callback iteration is a declared no-op;actual owner/buff changes remain unverified.','Stack cookie checking is a declared emulator service.'],
  established='Captured current instructions decode all3monstats columns together,square signed16leash/warp and melee/ranged endpoints,retain raw close/wander/swarm/flags/ratings,clamp gather0..2and choose0..1and AItype0..13,and ignore slots20..23. This proves the raw parameter prefix before unspecified owner-iterator effects,not complete GenericPet AI.',
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [output,probe,data,Path(__file__).resolve(),STAGE/'generic-helpers-actions.json',STAGE/'monstats.txt',STAGE/'monstats-extraction.json',*em.code]})
 target=ROOT/'tools/reference/generic-pet-parameter-emulation.json';body=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==body
 else:target.write_text(body)
 print('Current GenericPet initializer:2016offline x64emulation contexts;profiles='+','.join(p['key'] for p in profiles)+';owner callbacks and full AI remain separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
