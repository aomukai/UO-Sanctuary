#!/usr/bin/env python3
"""Execute cached current search-state preparation; no process access/input."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn.x86_const import *
from probe_generic_pet_preparation import Preparation,BASE,STACK,MEM,STOP,SLOTS,VEC,TABLE,STATES,SKILL,HEAP,ALLOC,VTABLE,FREE,GLOBAL,REGS,encode,decode,signed,row
from probe_warlock_runtime import ROOT,STAGE,digest

ENTRY=BASE+0x4b2cb0;DEST=MEM+0x1a000;GROUPS=MEM+0x1b000
OUT=ROOT/'.local/class-skills/generic-pet-search-groups-source-probe.json'
class SearchGroups(Preparation):
 def __init__(self):
  super().__init__()
  p=STAGE/'callees-live-search-preparation-20261029.json';proof=json.loads(p.read_text());self.artifacts.append(p)
  assert proof['executableSha256']=='e1f5436e3d9687f644ef16938b1b183d1fdef434f18cf66d852cf68f48cc8936'
  for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  record=next(r for r in proof['records'] if int(r['rva'],16)==0x4b2cb0);assert record['allUnwindFragmentsCaptured'] and record['bytes']==846
  for f in record['fragments']:
   assert f['captured'] and not f['invalidInstructionMarkers'];a=BASE+int(f['rva'],16);n=int(f['endRva'],16)-int(f['rva'],16)
   paths=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(paths)==1
   p=paths[0];data=p.read_bytes();assert len(data)==n and digest(data)==f['sha256'];self.map(a,n);self.uc.mem_write(a,data);self.ranges.append((a,a+n));self.artifacts.append(p)
 def hook(self,uc,a,size,user):
  # Observe the initial group-word read once per state. Later identical loads
  # in this immutable raw row are not additional world-service calls.
  if a==BASE+0x4b2ef9:self.rawStateRead=True
  if a==BASE+0x4b2f00:self.rawStateRead=False
  if a==BASE+0xab410:
   vector=uc.reg_read(UC_X86_REG_RCX);capacity=uc.reg_read(UC_X86_REG_RDX);count=self.q(vector+8)
   assert 10<=count<24 and capacity==count+1
   if vector not in self.reserves:self.reserves[vector]=HEAP+len(self.reserves)*1024
   new=self.reserves[vector];uc.mem_write(new,bytes(uc.mem_read(self.q(vector),count*4)))
   self.wq(vector,new);self.wq(vector+16,capacity);self.grows+=1;self.ret();return
  if a==BASE+0x6bf40:
   selector=uc.reg_read(UC_X86_REG_RCX)&255;sid=signed(uc.reg_read(UC_X86_REG_RDX));assert selector==self.p['selector']
   info=self.p['skills'].get(str(sid));self.trace.append([1,selector,sid,info[0] if info is not None else None]);result=0
   if info is not None:
    # Deliberately distinct buff field/flags prove this routine uses A2.
    uc.mem_write(SKILL+0xa0,struct.pack('<hh',info[1],info[0]));uc.mem_write(SKILL+39,bytes([info[2]]));result=SKILL
   self.ret(result);return
  super().hook(uc,a,size,user)
 def execute(self,p,rows,old):
  self.p=p;self.trace=[];self.jobs=[];self.rawStateRead=False;self.fatal=False;self.allocations=0;self.grows=0;self.frees=0;self.reserves={}
  self.uc.mem_write(MEM,b'\0'*0x60000);self.uc.mem_write(STACK,b'\0'*0x40000)
  for n in [1,2,3]:self.wq(GLOBAL+n*16,TABLE)
  self.wq(TABLE+0x2d0,len(p['states']));self.wq(TABLE+0x2c8,STATES)
  for i,group in enumerate(p['states']):self.uc.mem_write(STATES+i*68+30,struct.pack('<h',group or 0))
  self.wq(ALLOC,VTABLE);self.wq(VTABLE+0x20,FREE)
  for i,r in enumerate(rows):self.uc.mem_write(SLOTS+i*40,encode(r))
  self.wq(VEC,SLOTS);self.wq(VEC+8,len(rows));self.wq(VEC+16,32)
  self.wq(DEST,GROUPS);self.wq(DEST+8,len(old));self.wq(DEST+16,(1<<63)|10)
  # Old destination includes both embedded and owned state arrays. Actual
  # captured cleanup runs before every lookup, including empty/fatal cases.
  for i,(values,owned) in enumerate(old):
   a=GROUPS+i*80;data=HEAP+0x7000+i*256 if owned else a+32
   self.wq(a,SLOTS);self.wq(a+8,data);self.wq(a+16,len(values));self.wq(a+24,len(values) if owned else (1<<63)|10)
   for j,v in enumerate(values):self.wi(data+j*4,v)
  rsp=STACK+0x20008;self.wq(rsp,STOP)
  for reg,value in [(UC_X86_REG_RSP,rsp),*zip(REGS,[p['selector'],VEC,DEST,0])]:self.uc.reg_write(reg,value)
  self.uc.emu_start(ENTRY,STOP,count=300000);assert self.fatal or self.uc.reg_read(UC_X86_REG_RIP)==STOP
  groups=[]
  for i in range(self.q(DEST+8)):
   a=GROUPS+i*80;slot=(self.q(a)-SLOTS)//40;assert 0<=slot<len(rows) and self.q(a)==SLOTS+slot*40
   ptr=self.q(a+8);values=[self.i(ptr+j*4) for j in range(self.q(a+16))]
   groups.append([slot,self.i(a+72),values,*self.uc.mem_read(a+76,2)])
  assert self.frees==sum(o[1] for o in old)
  return [int(self.fatal),[decode(bytes(self.uc.mem_read(SLOTS+i*40,40))) for i in range(len(rows))],groups,self.trace]

def sample(**values):
 p=dict(selector=2,states=[0,5,5,0],skills={'41':[1,3,255],'42':[3,-1,255]});p.update(values);return p
def build(check=False):
 em=SearchGroups();cases=[];grows=frees=0
 def case(p,rows,old=()):
  nonlocal grows,frees
  result=em.execute(p,rows,old);grows+=em.grows;frees+=em.frees;cases.append([p,rows,old,result])
 for selector,state,kind,base in itertools.product([0,1,2,3,4,255],[-32768,-1,0,1,3,4,32767],[0,1,2,3,255],[0,1,255]):
  case(sample(selector=selector,skills={'41':[state,3,255]}),[row(kind=kind,base=base)],[[[3,8],False],[[4,9],True]])
 for states in [[],[0],[None],[5],[0]*16,[3]*16,[-1]*16,[-32768]*16,[32767]*16,[0,1,2,1,2,0],[None,1,1,0]]:
  for skills in [{},{'41':None},{'41':[0,2,6],'42':[1,3,4]},{'41':[1,0,2],'42':[1,3,255]},{'41':[0,0,0]}]:
   for selector in [1,2,3]:
    case(sample(selector=selector,states=states,skills=skills),[row(kind=2),row(42,kind=2),row(kind=1),row(kind=2),row(42,kind=2,base=1)])
 # Stable source pointers survive duplicate IDs and different vector slots.
 for n in range(1,10):
  case(sample(states=[7]*24),[row(kind=2,target=i+1) for i in range(n)],[[list(range(12)),True]])
 for selector in [0,1,2,3,255]:
  case(sample(selector=selector),[],[[[0],False],[[1],True]])
  case(sample(selector=selector,skills={}),[row(kind=2)],[[[0],True]])
  case(sample(selector=selector),[row(kind=1)],[[[0],False]])
 encoded=json.dumps(cases,separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==encoded
 else:OUT.write_text(encoded)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 deps=[OUT,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_preparation.py',ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',*em.artifacts]
 result=dict(referenceVersion='3.3.93847',component='Current search-state group preparation, before caller cleanup/callback',processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,handlerEnabled=False,fullAiVerified=False,
  verified=dict(contexts=len(cases),fatalBoundaries=sum(c[-1][0] for c in cases),preparationBytes=846,appendBytes=253,completeFunctionsExecuted=2,stateVectorGrowths=grows,previousOwnedStateFrees=frees,maxOutputGroups=9,maxStateIds=24),
  emulatorLibrary=dict(path=str(libs[0].relative_to(ROOT)),sha256=digest(libs[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(deps))},
  services=['Skills lookup uses signed word A2, deliberately distinct from A0 and flags39; state lookup returns signed group IDs. Immutable tables are supplied with counts0..24. The initial raw group load is observed once per state; repeated reads of that same immutable word are not independent service calls.',
   'Captured source clears the prior destination and frees owned state arrays before scanning candidates. Actual append code runs inside ten embedded group slots. State-ID reserve/allocation/free are declared memory services; allocation semantics, arbitrary metadata mutation and group-vector growth are not certified.',
   'The inherited harness maps cached buff functions for reuse of the same memory helpers, but the entry executed here is the complete846-byte search routine and the253-byte append, not the buff recipient callback. No running process is inspected.'],
  remaining=['Native managed comparison and retained buff regression must pass before acceptance. Current dispatcher linkage, real table/state suppliers and search callback consumption remain separate.',
   'Captured caller prepares groups, then calls cleanup before invoking its still-unreadable search callback. This evidence stops at the preparation return: post-cleanup ownership/lifetime/consumption is unverified. No guessed target selector or learned summon is enabled.'])
 receipt=ROOT/'tools/reference/generic-pet-search-groups-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert receipt.read_text()==text
 else:receipt.write_text(text)
 print('Current search-state preparation:',json.dumps(result['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
