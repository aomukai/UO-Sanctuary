#!/usr/bin/env python3
"""Reuse retained current401byte reach;qualify its player branch and class7."""
import argparse,csv,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,OBJECTS,STACK,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/player-current-reach-source';CAP=STAGE/'cleave-services-20261109.json'
ACTOR=OBJECTS;ITEM=OBJECTS+0x2000;ROW=OBJECTS+0x3000;INVENTORY=OBJECTS+0x4000
F={k:BASE+v for k,v in dict(inventory=0x276f70,select=0x281d00,row=0x255530).items()}
class Reach(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[CAP]
  cap=json.loads(CAP.read_text());assert cap['sourceReferencesVerified'];row=next(r for r in cap['records']if r['rva']=='0x27c360');assert row['allUnwindFragmentsCaptured']and row['bytes']==401
  for f in row['fragments']:
   path=next(ROOT/n for n,h in cap['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=path.read_bytes();assert digest(raw)==f['sha256'];a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(path)
  self.map(OBJECTS,0x10000);self.map(STACK,0x40000);self.map(STOP,0x1000)
  for a in F.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def hook(self,u,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):
   if a==BASE+0x27c4a9 and u.reg_read(UC_X86_REG_RAX)==0:self.unsupported=True;u.emu_stop()
   return
  p=self.p;rcx,rdx,r8,r9=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];sp=u.reg_read(UC_X86_REG_RSP);result=0
  if a==F['inventory']:assert rcx==ACTOR;self.trace.append('inventory');result=INVENTORY
  elif a==F['select']:
   assert rcx==INVENTORY;self.trace.append('select');self.putq(rdx,ITEM if p['item']else 0);result=int(p['found'])
   if p['mutateClass']:self.wi(ACTOR+4,0)
  elif a==F['row']:assert(rcx,rdx)==(1,17);self.trace.append('row');result=ROW if p['row']else 0
  else:raise AssertionError(hex(a))
  u.reg_write(UC_X86_REG_RAX,result);u.reg_write(UC_X86_REG_RSP,sp+8);u.reg_write(UC_X86_REG_RIP,self.q(sp))
 def run(self,p):
  self.p=p;self.trace=[];self.unsupported=False;u=self.uc;u.mem_write(OBJECTS,bytes(0x10000));u.mem_write(STACK,bytes(0x40000));self.wi(ACTOR,0);self.wi(ACTOR+4,p['class']);self.wi(ITEM,p['kind']);self.wi(ITEM+4,17);u.mem_write(ITEM+0x1bd,b'\x01');u.mem_write(ROW+0x115,bytes([p['range']]))
  sp=STACK+0x20008;self.putq(sp,STOP);u.reg_write(UC_X86_REG_RSP,sp);u.reg_write(UC_X86_REG_RCX,ACTOR);u.emu_start(BASE+0x27c360,STOP,count=1000);assert self.unsupported or u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(result=-1 if self.unsupported else u.reg_read(UC_X86_REG_RAX)&0xffffffff,trace=self.trace.copy())
def build(check=False):
 em=Reach();cases=[]
 def add(**changes):p=dict(found=True,item=True,kind=4,row=True,range=0,mutateClass=False,**{'class':7});p.update(changes);cases.append(dict(input=p,output=em.run(p)))
 for cls in range(8):
  add(**{'class':cls},found=False)
  for radius in [0,1,2,4,255]:add(**{'class':cls},range=radius)
 for cls in [0,7]:
  for kw in [dict(item=False),dict(kind=1),dict(kind=0),dict(row=False),dict(found=False,row=False),dict(mutateClass=True,range=4)]:add(**{'class':cls},**kw)
 charroot=ROOT/'.local/class-skills/warlock-hands-reference';proof=json.loads((ROOT/'tools/reference/warlock-hands-reference-evidence.json').read_text())
 for n in ['charstats.txt','charstats.bin']:
  path=charroot/n;assert digest(path.read_bytes())==proof['inputs'][str(path.relative_to(ROOT))]
 rows=[r for r in csv.DictReader((charroot/'charstats.txt').read_text().splitlines(),delimiter='\t')if r['class']!='Expansion'];raw=(charroot/'charstats.bin').read_bytes();assert struct.unpack_from('<I',raw)[0]==len(rows)==8
 for i,row in enumerate(rows):assert raw[4+i*212+32:4+i*212+48].split(b'\0',1)[0].decode()==row['class']
 assert rows[7]['class']=='Warlock'
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';text=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 result=dict(referenceVersion='3.3.93847',sourceContexts=len(cases),machineCodeBytes=401,branch='player',playerClass7='Warlock',classes=[r['class']for r in rows],processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,
  scope='Current player reach class7bonus2 captured before supplied inventory search;failure0,null/nonitem selected retains bonus,byte item row adds to bonus. All eight classes and range endpoints compared. Native inventory search/broken/unarmed eligibility and other unit branches are not certified.',
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set([path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',ROOT/'tools/reference/warlock-hands-reference-evidence.json',charroot/'charstats.txt',charroot/'charstats.bin',*em.artifacts,*libs]))})
 path=ROOT/'tools/reference/player-current-reach-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print(f"Current player reach:{len(cases)}cases/401bytes;owned compiled class7is Warlock;no process reads or old matrix rerun.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
