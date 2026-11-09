#!/usr/bin/env python3
"""Complete current missing-owner death orchestration with explicit native leaves."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/pet-owner-loss-source'
ACTOR=OBJECTS+0x1000;DATA=OBJECTS+0x2000;CTRL=OBJECTS+0x3000;OWNER=OBJECTS+0x4000;KILLER=OBJECTS+0x5000;PATH=OBJECTS+0x6000;KPATH=OBJECTS+0x7000;A=OBJECTS+0x8000;B=OBJECTS+0x9000
F={k:BASE+v for k,v in dict(primary=0x6bc90,resolve=0x370680,detach=0x35c970,notify=0x333e10,clear=0x333c20,credit=0x3969f0,initialize=0x3de170,direction=0x27aa90,execute=0x3dda90,after=0x35f7d0,secondary=0x6bd00,transform=0x392aa0,fatal=0x7b6af0).items()}
class Loss(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  path=STAGE/'callees-pet-missing-owner-20261109.json';capture=json.loads(path.read_text());self.artifacts.append(path)
  for n,h in capture['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  row=capture['records'][0];assert row['rva']=='0x392be0'and row['allUnwindFragmentsCaptured']and row['bytes']==706
  for f in row['fragments']:
   p=next(ROOT/n for n,h in capture['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
  path=STAGE/'pet-ai-overrides-20261109.json';capture=json.loads(path.read_text());self.artifacts.append(path)
  for n,h in capture['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  row=next(r for r in capture['records']if r['rva']=='0x4b66e0');assert row['allUnwindFragmentsCaptured']and row['bytes']==933
  for f in row['fragments']:
   p=next(ROOT/n for n,h in capture['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
  self.map(OBJECTS,0x10000);self.map(STACK,0x40000);self.map(STOP,0x1000);self.map(BASE+0x197a178,8)
  self.map(BASE+0x155eeb0,1);self.uc.mem_write(BASE+0x155eeb0,b'\xc3')
  for a in F.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200);self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def wb(self,a,v):self.uc.mem_write(a,bytes([v]))
 def si(self,a):return struct.unpack('<i',self.uc.mem_read(a,4))[0]
 def ret(self,value=0):
  sp=self.uc.reg_read(UC_X86_REG_RSP);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,sp+8);self.uc.reg_write(UC_X86_REG_RIP,self.q(sp))
 def hook(self,u,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  if a==BASE+0x155eeb0:self.ret(u.reg_read(UC_X86_REG_RAX));return
  c,d,v,z=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];p=self.p
  name=next((n for n,x in F.items()if x==a),None);signed=lambda x:(x+2**31)%2**32-2**31
  if name=='primary':
   self.trace.append(f'primary:{c}:{signed(d)}')
   if p['afterPrimaryKind']is not None:self.wi(ACTOR,p['afterPrimaryKind'])
   self.ret(A if p['primary']else 0);return
  if name=='resolve':assert c==OBJECTS and d==0 and v==77;self.trace.append('resolve:0:77');self.ret(OWNER if p['owner']else 0);return
  if name=='detach':
   assert c==OBJECTS and d==OWNER and v==ACTOR;self.trace.append('detach')
   if p['afterDetachFlags']is not None:self.wi(ACTOR+0x124,p['afterDetachFlags'])
   if p['afterDetachAi']is not None:self.wi(CTRL+0x90,p['afterDetachAi'])
   self.ret();return
  if name=='notify':assert c==OBJECTS and d==ACTOR and self.q(v)==BASE+0x1722cd0 and self.q(v+8)==OBJECTS;self.trace.append('notify:24259792');self.ret();return
  if name=='clear':assert c==OBJECTS and d==ACTOR;self.trace.append('clear');self.ret();return
  if name=='credit':assert c==OBJECTS and d==KILLER and v==ACTOR;self.trace.append('credit');self.ret();return
  if name=='initialize':
   assert c==ACTOR and d==0;self.mode=v;self.trace.append('initialize:0');u.mem_write(v,b'\x55'*48)
   if p['afterInitKind']is not None:self.wi(ACTOR,p['afterInitKind'])
   self.ret();return
  if name=='direction':assert c==ACTOR;self.trace.append(f'direction:{signed(d)}:{signed(v)}');self.ret(p['direction']);return
  if name=='execute':
   assert c==OBJECTS and d==self.mode and v==1 and self.q(d+0x10)==(KILLER if p['killer']else 0)
   self.trace.append(f'execute:{u.mem_read(d+0x20,1)[0]}:1')
   # Unmodified bytes belong to the shared mode initializer,not this caller.
   assert bytes(u.mem_read(d,16))==b'\x55'*16 and bytes(u.mem_read(d+0x18,8))==b'\x55'*8
   if p['afterExecuteFlags']is not None:self.wi(ACTOR+0x124,p['afterExecuteFlags'])
   self.ret();return
  if name=='after':assert c==OBJECTS and d==ACTOR and v==(KILLER if p['killer']else 0);self.trace.append('after');self.ret();return
  if name=='secondary':
   self.trace.append(f'secondary:{c}:{signed(d)}')
   if p['afterSecondaryClass']is not None:self.wi(ACTOR+4,p['afterSecondaryClass'])
   self.ret(B if p['secondary']else 0);return
  if name=='transform':assert c==ACTOR;self.trace.append(f'transform:{signed(d)}');self.ret();return
  if name=='fatal':self.failed=True;u.emu_stop();return
  raise AssertionError(('Undeclared instruction',hex(a)))
 def run(self,p):
  self.p=p;self.trace=[];self.failed=False;self.mode=0;u=self.uc;u.mem_write(OBJECTS,b'\0'*0x10000);u.mem_write(STACK,b'\0'*0x40000)
  self.wb(OBJECTS+0x106,1);self.wi(ACTOR,p['kind']);self.wi(ACTOR+4,p['classId']);self.wi(ACTOR+12,p['mode']);self.wi(ACTOR+0x124,p['flags']);self.putq(ACTOR+0x10,DATA if p['data']else 0);self.putq(DATA+0x38,CTRL if p['ai']else 0);self.putq(CTRL+0x60,OBJECTS if p['ownerGame']else 0);self.wi(CTRL+0x68,77);self.wi(CTRL+0x6c,0);self.wi(CTRL+0x90,p['ai90']);self.wi(OWNER,p['ownerKind']);self.wi(OWNER+12,p['ownerMode']);self.wi(CTRL,0);self.wi(CTRL+0x10,1);self.putq(OBJECTS+0xa000,CTRL);self.wb(A+0x3d,128 if p['killable']else 0);self.wb(B+6,p['flags6'])
  self.putq(ACTOR+0x38,PATH if p['path']else 0);u.mem_write(PATH+0x80,struct.pack('<I',p['facingBits']));self.wi(KILLER,p['killerKind']);self.putq(KILLER+0x38,KPATH if p['killerPath']else 0);u.mem_write(KPATH+2,struct.pack('<H',p['x']&65535));u.mem_write(KPATH+6,struct.pack('<H',p['y']&65535));self.wi(KPATH+0x10,p['x']);self.wi(KPATH+0x14,p['y'])
  sp=STACK+0x20008;self.putq(sp,STOP)
  for r,value in [(UC_X86_REG_RSP,sp),(UC_X86_REG_RCX,OBJECTS),(UC_X86_REG_RDX,ACTOR if p['actor']else 0),(UC_X86_REG_R8,OBJECTS+0xa000 if p['viaOverride']else KILLER if p['killer']else 0),(UC_X86_REG_R9,0 if p['viaOverride']else p['detach'])]:u.reg_write(r,value)
  try:u.emu_start(BASE+(0x4b66e0 if p['viaOverride']else 0x392be0),STOP,count=10000)
  except unicorn.UcError as error:
   if u.reg_read(UC_X86_REG_RIP)==BASE+0x392d8a:self.failed=True
   else:raise
  if not self.failed:assert u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(unsupported=self.failed,trace=self.trace.copy(),kind=self.si(ACTOR),classId=self.si(ACTOR+4),flags=struct.unpack('<I',u.mem_read(ACTOR+0x124,4))[0],ai90=self.si(CTRL+0x90))
def sample(**changes):
 p=dict(viaOverride=False,ownerMode=1,actor=True,kind=1,classId=742,mode=1,flags=0,primary=True,killable=True,data=True,ai=True,ownerGame=True,owner=True,ownerKind=0,ai90=0,killer=False,killerKind=0,killerPath=True,x=70000,y=-2,path=True,facingBits=0x41d40000,direction=278,detach=1,secondary=True,flags6=0,afterPrimaryKind=None,afterDetachFlags=None,afterDetachAi=None,afterInitKind=None,afterExecuteFlags=None,afterSecondaryClass=None);p.update(changes);return p
def build(check=False):
 em=Loss();cases=[]
 for change in [dict(),dict(actor=False),dict(kind=0),dict(kind=2),dict(mode=0),dict(mode=12),dict(kind=0,mode=0),dict(kind=0,mode=17),dict(primary=False),dict(killable=False),dict(data=False),dict(ai=False),dict(ownerGame=False),dict(owner=False),dict(ownerKind=1),dict(detach=0),dict(detach=-1),dict(ai90=1),dict(afterDetachAi=1),dict(killer=True),dict(killer=True,flags=0x4000000),dict(killer=True,killerKind=2),dict(killer=True,killerPath=False),dict(killer=True,killerKind=2,killerPath=False),dict(path=False),dict(facingBits=0x7fc00000),dict(facingBits=0x7f800000),dict(facingBits=0x4f000000),dict(facingBits=0xc02ccccd),dict(flags=0x80000000),dict(flags=0x80000000,afterDetachFlags=0),dict(afterDetachFlags=0x80000000),dict(afterExecuteFlags=0x80000000),dict(afterExecuteFlags=0),dict(secondary=False),dict(flags6=4),dict(flags6=4,classId=432),dict(flags6=4,classId=433),dict(flags6=4,afterSecondaryClass=432),dict(afterPrimaryKind=0),dict(afterInitKind=5),dict(afterInitKind=5,killer=True)]:
  p=sample(**change);cases.append(dict(input=p,output=em.run(p)))
 for change in [dict(owner=False),dict(ownerGame=False),dict(owner=False,flags=0x10000),dict(owner=False,mode=0),dict(ownerMode=17),dict(owner=False,ai90=1)]:
  p=sample(viaOverride=True,**change);cases.append(dict(input=p,output=em.run(p)))
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';text=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 result=dict(referenceVersion='3.3.93847',sourceBytes=706,callbackBytes=933,composedBytes=1639,sourceContexts=len(cases),composedMissingOwnerContexts=6,processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,scope='Complete death orchestrator plus six actual callback1 missing-owner transitions,with explicit metadata,registry,owner detach,linked notification,credit and mode-execution leaves. Actual callback context0/1 and direct-call identities are known;world death admission remains separate. Packet fields outside target/facing stay owned by initialized mode service.',artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set([path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]))})
 path=ROOT/'tools/reference/pet-owner-loss-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print(f'Owner loss:{len(cases)}current contexts/706bytes;killable/owner/notification/credit/mode/facing order.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
