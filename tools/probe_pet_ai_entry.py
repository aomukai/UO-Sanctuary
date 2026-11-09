#!/usr/bin/env python3
"""Current1096-byte AI entry with explicit game helpers and logical functor ABI."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/pet-ai-entry-source';ACTOR=OBJECTS+0x1000;DATA=OBJECTS+0x4000;ROWA=OBJECTS+0x5000;ROWB=OBJECTS+0x6000;AIROW=OBJECTS+0x7000;TARGET=OBJECTS+0x8000;VTABLE=OBJECTS+0x9000
SPECIAL=BASE+0x1d187f0;PREDICATE=STOP+0x100;DESTROY=STOP+0x180
FUNCTIONS={k:BASE+v for k,v in dict(rowA=0x6bc90,rowB=0x6bd00,preprocess=0x43a6c0,ai=0x439fe0,target1=0x424e60,target2=0x40dcc0,target5=0x425030,post=0x43af30,queued=0x43a140,free=0x5f1b0,move=0x8d7c0,default1=0x4b60c0,default2=0x4b7000,special0=0x4b63d0,special1=0x4b66e0).items()}
def ctrl(i):return OBJECTS+(0x1000+i*0x1000)if i else 0
def queue(i):return OBJECTS+0x10000+i*0x2000
class Entry(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  path=STAGE/'pet-ai-event-entry-20261109.json';capture=json.loads(path.read_text());self.artifacts.append(path);row=capture['records'][0];assert row['rva']=='0x43b0c0' and row['bytes']==1096 and row['allUnwindFragmentsCaptured']
  for n,h in capture['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  for f in row['fragments']:
   p=next(ROOT/n for n,h in capture['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
  self.map(OBJECTS,0x40000);self.map(STACK,0x40000);self.map(STOP,0x1000);self.map(SPECIAL,32)
  for a in FUNCTIONS.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200);self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def si(self,a):return struct.unpack('<i',self.uc.mem_read(a,4))[0]
 def wb(self,a,v):self.uc.mem_write(a,bytes([v]))
 def ret(self,value=0):
  sp=self.uc.reg_read(UC_X86_REG_RSP);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,sp+8);self.uc.reg_write(UC_X86_REG_RIP,self.q(sp))
 def cid(self,address):return next((i for i in [1,2]if address==ctrl(i)),0)
 def tickvalues(self):
  t=self.tick;spec=self.q(t+0x40)
  return dict(control=self.cid(self.q(t)),raw08=self.q(t+8),target=7 if self.q(t+0x10)==TARGET else 0,raw18=self.q(t+0x18),distance=self.si(t+0x20),combat=self.si(t+0x24),rowA=int(self.q(t+0x28)==ROWA),rowB=2 if self.q(t+0x30)==ROWB else 0,snapshot=self.q(t+0x38),special=None if not spec else dict(flags=self.si(spec),function=self.q(spec+8)-BASE if self.q(spec+8)else 0))
 def hook(self,u,a,size,user):
  if any(x<=a<y for x,y in self.ranges):return
  c,d,v,z=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];sp=u.reg_read(UC_X86_REG_RSP);p=self.p;name=next((k for k,x in FUNCTIONS.items()if x==a),None)
  signed=lambda x:(x+2**31)%2**32-2**31
  if name=='rowA':
   self.trace.append(f'rowA:{c}:{signed(d)}')
   if p['mutateLookup']:self.wb(OBJECTS+0x106,2);self.wi(ACTOR+4,743)
   self.ret(ROWA if p['rowA']else 0);return
  if name=='rowB':self.trace.append(f'rowB:{c}:{signed(d)}');self.ret(ROWB if p['rowB']else 0);return
  if name=='preprocess':
   assert c==OBJECTS and d==(ACTOR if p['actor']else 0)and v==self.tick;self.trace.append('preprocess')
   if p['preSwap']:self.putq(self.tick,ctrl(2))
   if p['preSpecial']is not None:self.putq(self.tick+0x40,SPECIAL+16*p['preSpecial'])
   self.ret(int(p['preStop']));return
  if name=='ai':self.trace.append(f'ai:{signed(d)}');self.ret(AIROW);return
  if name in ['target1','target2','target5']:
   assert c==OBJECTS and d==ACTOR
   if name=='target1':assert v==self.q(self.tick) and z==int(p['type']!=3)
   else:assert v==self.q(self.tick)
   self.trace.append(f'target:{p["type"]}:{(self.q(self.tick+0x38)>>32)&255}')
   self.wi(self.tick+0x20,19);self.wi(self.tick+0x24,1);self.ret(TARGET if p['target']else 0);return
  if name=='post':
   assert c==OBJECTS and d==ACTOR and v==self.tick;self.trace.append('post')
   if p['postSwap']:self.putq(self.tick,ctrl(2))
   self.ret(int(p['postStop']));return
  if a==PREDICATE:
   token=self.si(c+16);assert d and self.q(d)==ACTOR;self.trace.append(f'predicate:{token}:{self.si(v)}')
   self.ret(int(token not in p['reject']));return
  if name=='queued':
   assert c==OBJECTS and d==ACTOR and v==self.q(self.tick);command=signed(z);self.trace.append(f'queued:{command}:{self.cid(v)}')
   if p['queueFunction']:self.putq(v+8,FUNCTIONS['default2'])
   if p['append']and command==101:self.addqueue(self.cid(v),2,202)
   self.ret();return
  if a==DESTROY:self.trace.append(f'release:{self.si(c+16)}');self.ret();return
  if name=='free':self.ret();return # Declared inline-functor lifetime;no game effect.
  if name=='move':
   raw=bytes(u.mem_read(d,40));u.mem_write(c,raw);u.mem_write(d,b'\0'*40);self.putq(d,1);self.ret();return # Move leaves a tagged-empty source.
  if name in ['default1','default2','special0','special1']:
   assert c==OBJECTS and d==ACTOR and v==self.tick
   if name.startswith('special'):self.trace.append(f'call:{a-BASE}:{z-BASE if z else 0}')
   else:self.trace.append(f'default:{a-BASE}')
   self.ret();return
  raise AssertionError(('Undeclared instruction',hex(a)))
 def addqueue(self,i,token,command):
  n=self.q(ctrl(i)+0x28);assert n<8;slot=queue(i)+n*48;self.wi(slot,command);self.putq(slot+8,VTABLE);self.wi(slot+24,token);self.putq(ctrl(i)+0x28,n+1)
 def run(self,p):
  self.p=p;self.trace=[];self.currentQueue=2 if p['preSwap']or p['postSwap']else 1;u=self.uc;u.mem_write(OBJECTS,b'\0'*0x40000);u.mem_write(STACK,b'\0'*0x40000)
  for off,value in [(0x101,3),(0x104,2),(0x106,1)]:self.wb(OBJECTS+off,value)
  self.wi(OBJECTS+0x108,0x12345678);self.wi(ACTOR,p['kind']);self.wi(ACTOR+4,742);self.putq(ACTOR+0x10,DATA if p['data']else 0);self.putq(DATA+0x38,ctrl(1)if p['control']else 0);self.wb(DATA+0x30,4 if p['snapshot']else 0);u.mem_write(DATA+0x88,b'ABCD'+struct.pack('<Q',0xbbaa998877665544)+b'WXYZ')
  self.putq(VTABLE,DESTROY);self.putq(VTABLE+8,PREDICATE)
  for i in [1,2]:
   self.wi(ctrl(i),10+i);self.putq(ctrl(i)+8,FUNCTIONS['default'+str(i)] if p['function']else 0);self.wi(ctrl(i)+0x14,p['field14']);self.wb(ctrl(i)+0x18,int(p['executing']));self.putq(ctrl(i)+0x20,queue(i))
  for token,command in p['queue']:self.addqueue(self.currentQueue,token,command)
  self.wi(AIROW,p['type'])
  for i in [0,1]:self.wi(SPECIAL+i*16,p['flags']);self.putq(SPECIAL+i*16+8,FUNCTIONS['special'+str(i)] if p['specialFunction']else 0)
  sp=STACK+0x20008;self.tick=sp-0xc8;self.putq(sp,STOP);self.wb(sp+0x17,p['tail'])
  for reg,value in [(UC_X86_REG_RSP,sp),(UC_X86_REG_RCX,OBJECTS),(UC_X86_REG_RDX,ACTOR if p['actor']else 0)]:u.reg_write(reg,value)
  unsupported=False
  try:u.emu_start(BASE+0x43b0c0,STOP,count=20000)
  except unicorn.UcError as error:
   pc=u.reg_read(UC_X86_REG_RIP)
   if pc==BASE+0x43b1bb and not p['control']or pc==0 and not p['function']:unsupported=True
   else:raise AssertionError((hex(pc),p))from error
  if not unsupported:assert u.reg_read(UC_X86_REG_RIP)==STOP
  controls=[]
  for i in [1,2]:
   n=self.q(ctrl(i)+0x28);assert n<=8;controls.append(dict(function=self.q(ctrl(i)+8)-BASE if self.q(ctrl(i)+8)else 0,executing=bool(u.mem_read(ctrl(i)+0x18,1)[0]),queue=[[self.si(queue(i)+j*48+24),self.si(queue(i)+j*48)]for j in range(n)]))
  return dict(tick=self.tickvalues(),controls=controls,trace=self.trace.copy(),unsupported=unsupported)

def sample(**changes):
 p=dict(actor=True,kind=1,data=True,control=True,rowA=True,rowB=True,snapshot=False,tail=0,mutateLookup=False,field14=2,flags=0,specialFunction=True,function=True,preSwap=False,preSpecial=None,preStop=False,type=0,target=True,postSwap=False,postStop=False,queue=[],reject=[],append=False,queueFunction=False,executing=False);p.update(changes);return p
def build(check=False):
 em=Entry();cases=[]
 def add(**changes):p=sample(**changes);cases.append(dict(input=p,output=em.run(p)))
 add()
 for changes in [dict(rowA=False),dict(rowB=False),dict(actor=False,rowA=False),dict(data=False,rowA=False),dict(control=False),dict(snapshot=True),dict(tail=165),dict(mutateLookup=True),dict(preStop=True),dict(postStop=True),dict(preSwap=True),dict(postSwap=True),dict(executing=True),dict(function=False)]:add(**changes)
 for kind in [-1,0,1,2,3,4,5,6,7]:
  for target in [False,True]:add(type=kind,target=target)
 for field in [0,1,-1,2]:
  add(field14=field);add(field14=field,type=4)
 for flags in [0,2,4,6,8,-1]:add(field14=0,type=4,flags=flags)
 add(field14=0,preSpecial=1,type=4);add(field14=0,preSpecial=1);add(field14=0,specialFunction=False)
 for queueRows,reject in [([[1,101]],[]),([[1,101],[2,202],[3,303]],[]),([[1,101],[2,202]],[1]),([[1,101],[2,202],[3,303]],[2])]:add(queue=queueRows,reject=reject)
 add(queue=[[1,101]],append=True);add(queue=[[1,101]],queueFunction=True);add(queue=[[1,101]],preSwap=True)
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';text=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 result=dict(referenceVersion='3.3.93847',sourceBytes=1096,sourceContexts=len(cases),processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,scope='Complete entry against explicit metadata/preprocessing/target/special/queued services and declared stable inline-functor ABI. Game snapshot stack tail is supplied;raw heap allocator/closure exception behavior and helper/native-world internals are separate.',artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set([path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]))})
 path=ROOT/'tools/reference/pet-ai-entry-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print(f"AI entry:{len(cases)}complete-current contexts/1096bytes;explicit helpers and stable functor ABI.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
