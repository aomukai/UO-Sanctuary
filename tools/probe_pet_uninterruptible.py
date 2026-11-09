#!/usr/bin/env python3
"""Replay current state54 completion with existing event producer/remover code."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn.x86_const import *
from probe_pet_timed_events import Events,sample as event_sample,PET,ROOTSTAT,TABLE,ptr,ident
from probe_generic_pet_parameters import BASE,OBJECTS,STACK,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/pet-uninterruptible-source';ATTACKER=OBJECTS+0x70000
F={k:BASE+v for k,v in dict(state=0x296a00,resolve=0x370680,fresh=0x393240,loss=0x392be0,fatal=0x7b6af0,cookie=0x155eeb0).items()}
def signed(n):return (n+2**31)%2**32-2**31
class Completion(Events):
 def __init__(self):
  super().__init__();self.recording=False
  path=STAGE/'callees-pet-scheduling-state54-20261109.json';capture=json.loads(path.read_text());self.artifacts.append(path)
  for n,h in capture['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  row=capture['records'][0];assert row['rva']=='0x373690'and row['allUnwindFragmentsCaptured']and row['bytes']==649
  for f in row['fragments']:
   p=next(ROOT/n for n,h in capture['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
  self.map(BASE+0x197a178,8)
  for a in F.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200)
 def hook(self,u,a,size,user):
  if not getattr(self,'recording',False):return super().hook(u,a,size,user)
  if a==BASE+0x32e790:self.trace.append('remove:'+str(ident(u.reg_read(UC_X86_REG_RDX))))
  if any(lo<=a<hi for lo,hi in self.ranges):return
  c,d,v,z=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];p=self.p
  name=next((n for n,x in F.items()if x==a),None)
  if name=='cookie':self.ret(u.reg_read(UC_X86_REG_RAX));return
  if name=='state':
   assert c==(PET if p['actor']else 0)and d in [54,92];self.trace.append(f'state:{d}:{signed(v)}')
   if d==54:
    word=self.si(ROOTSTAT+0xb34)&~0x400000;self.wi(ROOTSTAT+0xb34,word|(0x400000 if signed(v)!=0 else 0))
    if p['after54Game']is not None:self.putq(PET+0xd8,OBJECTS if p['after54Game']else 0)
    if p['after54Kind']is not None:self.wi(PET,p['after54Kind'])
   else:self.wi(ROOTSTAT+0xb38,self.si(ROOTSTAT+0xb38)&~0x10000000)
   self.ret();return
  if name=='resolve':
   assert c==OBJECTS and signed(d)==p['lastKind']and signed(v)==p['lastId'];self.trace.append(f'resolve:{signed(d)}:{signed(v)}');self.ret(ATTACKER if p['attacker']else 0);return
  if name=='fresh':
   assert c==OBJECTS and d==ATTACKER and v==PET
   raw=bytes(u.mem_read(z,64));expected=bytearray(64);expected[4]=2;assert raw==bytes(expected),raw.hex()
   self.trace.append('fresh:2')
   if p['afterReturnKind']is not None:self.wi(PET,p['afterReturnKind'])
   self.ret();return
  if name=='loss':
   assert c==OBJECTS and d==PET and v==0 and z==1;self.trace.append('loss:0:1')
   if p['afterLossKind']is not None:self.wi(PET,p['afterLossKind'])
   self.ret();return
  if name=='fatal':self.unsupported=True;u.emu_stop();return
  return super().hook(u,a,size,user)
 def si(self,a):return struct.unpack('<i',self.uc.mem_read(a,4))[0]
 def run_case(self,p):
  self.recording=False;super().run(event_sample(commands=[]));self.p=p
  for kind,frame in [(2,110),(1,112),(2,115)]:self.call(0x32fe20,OBJECTS,PET,kind,frame)
  if p['processing']:self.uc.mem_write(ptr(3)+2,struct.pack('<H',1))
  self.wi(PET,p['kind']);self.putq(PET+0xd8,OBJECTS if p['game']else 0);self.uc.mem_write(PET+0x1bd,bytes([p['selector']]))
  self.putq(PET+0x88,ROOTSTAT if p['root']else 0);self.wi(ROOTSTAT+0x1c,p['root1c']);self.wi(ROOTSTAT+0xb38,0x10000000 if p['delayed']else 0)
  self.wi(ROOTSTAT+0xb34,0x400000);self.wi(TABLE+0x2d0,p['count']);self.wi(PET+0x128,p['flags128']);self.wi(PET+0xf0,p['lastKind']);self.wi(PET+0xf4,p['lastId'])
  self.trace=[];self.recording=True;self.call(0x373690,PET if p['actor']else 0,p['enabled']);self.recording=False
  snap=self.snapshot();return dict(trace=snap['trace'],unsupported=snap['unsupported'],kind=self.si(PET),unit=snap['unit'],nodes=snap['nodes'],state54=bool(self.si(ROOTSTAT+0xb34)&0x400000),state92=bool(self.si(ROOTSTAT+0xb38)&0x10000000))
def sample(**changes):
 p=dict(actor=True,enabled=0,kind=1,game=True,selector=1,count=232,root=True,root1c=-1,delayed=False,flags128=0x20000,lastKind=0,lastId=77,attacker=True,processing=False,after54Game=None,after54Kind=None,afterReturnKind=None,afterLossKind=None)
 p.update(changes);return p
def build(check=False):
 em=Completion();cases=[]
 def add(**changes):p=sample(**changes);cases.append(dict(input=p,output=em.run_case(p)))
 for kind in [-1,0,1,2,3,4,5]:
  for enabled in [0,1]:add(kind=kind,enabled=enabled,delayed=True)
 for enabled in [-1,256]:add(enabled=enabled,delayed=True)
 for changes in [dict(actor=False),dict(game=False),dict(selector=0),dict(selector=4),dict(selector=2),dict(selector=3),dict(count=92),dict(count=93),dict(root=False),dict(root1c=0),dict(root1c=-2),dict(delayed=False),dict(flags128=0),dict(attacker=False),dict(lastKind=1,lastId=-9),dict(processing=True),dict(after54Game=False),dict(game=False,after54Game=True),dict(after54Kind=0),dict(afterReturnKind=0),dict(attacker=False,afterLossKind=0)]:add(delayed=True,**changes)if 'delayed'not in changes else add(**changes)
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';encoded=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 result=dict(referenceVersion='3.3.93847',sourceContexts=len(cases),newRoutineBytes=649,composedMachineCodeBytes=sum(b-a for a,b in em.ranges),processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,
  scope='Complete373690 guards/state writes/death dispatch and AI-event removal,composed with already-qualified timed producer/node/remover. State-toggle,registry,fresh-return and owner-loss callbacks are declared services. Fresh result retains empty inline callback/storage;opaque callback mutation/destruction and allocator branch are outside this scope.',
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set([path,Path(__file__).resolve(),ROOT/'tools/probe_pet_timed_events.py',ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]))})
 path=ROOT/'tools/reference/pet-uninterruptible-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 print(f"Current uninterruptible completion:{len(cases)}contexts/649new bytes;existing event-store code reused.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
