#!/usr/bin/env python3
"""Execute complete current movement helpers offline,never in the game process."""
import argparse,itertools,json,struct
from collections import Counter
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE,UC_HOOK_MEM_READ,UC_HOOK_MEM_INVALID
from unicorn.x86_const import *
from probe_generic_pet_decisions import Emulator,BASE,STACK,MEM,STOP,GAME,PET,CFG,PATH,MON,CTRL,EVENTS
from probe_generic_pet_actions import OPAQUE,pointer,target,signed
from probe_warlock_runtime import ROOT,STAGE,Image,digest
OUT=ROOT/'.local/class-skills/generic-pet-movement-source-probe.json'
TABLE=MEM+0x1a000;MOVE=MEM+0x1b000;AUX=MEM+0x1c000;GLOBAL=BASE+0x1e03ac0;PREP=0x4b1220;THINK=0x4b12d0;APPROACH=0x4b1600;CORE=0x4b1550;EXEC=0x4257a0;DISP=0x4b2350
FNS={k:BASE+v for k,v in dict(init=0x3de170,mode=0x3dda90,remove=0x32e790,schedule=0x32fe20,wander=0x425e80,idle=0x425550,fatal=0x7b6af0,distance=0x40bfd0,collision=0x27cee0,monster=0x6bd00,collision4=0x27c710,move=0x425bb0).items()}
class Movement(Emulator):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for name,rvas in [('callees-live-approach-core-20261029.json',[CORE]),('callees-live-action-approach-20261029.json',[APPROACH]),('callees-live-movement-core-20261029.json',[THINK,EXEC]),('callees-live-action-disposition-20261029.json',[DISP])]:
   pp=STAGE/name;proof=json.loads(pp.read_text());self.artifacts.append(pp)
   for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for rva in rvas:
    row=next(r for r in proof['records'] if int(r['rva'],16)==rva);assert row['allUnwindFragmentsCaptured']
    for f in row['fragments']:
     assert f['captured'] and not f['invalidInstructionMarkers'];a=int(f['rva'],16);size=int(f['endRva'],16)-a
     paths=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(paths)==1
     p=paths[0];body=p.read_bytes();assert len(body)==size;self.map(BASE+a,size);self.uc.mem_write(BASE+a,body);self.ranges.append((BASE+a,BASE+a+size));self.artifacts.append(p)
  pp=STAGE/'movement-prepare-leaf-complete-20261029.json';proof=json.loads(pp.read_text());self.artifacts.append(pp)
  for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  leaf=STAGE/'movement-prepare-leaf-complete-20261029.bin';body=leaf.read_bytes();assert len(body)==176 and body[-3:]==b'\xc3\xcc\xcc';self.artifacts.append(leaf)
  # All explicit branches remain in the174-byte body; no indirect jumps/calls.
  asm=(STAGE/'movement-prepare-leaf-complete-20261029.asm').read_text();import re
  assert not re.search(r'\bcall\b',asm) and asm.count('\tret')==1
  destinations=[int(x,16) for x in re.findall(r'\bj\w+\s+0x([0-9a-f]+)',asm)]
  assert destinations and all(BASE+PREP<=x<BASE+PREP+174 for x in destinations)
  self.map(BASE+PREP,len(body));self.uc.mem_write(BASE+PREP,body);self.ranges.append((BASE+PREP,BASE+PREP+174))
  self.map(MEM,0x40000);self.map(STACK,0x40000);self.map(STOP,4096);self.map(GLOBAL,64)
  for a in FNS.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook);self.uc.hook_add(UC_HOOK_MEM_READ,self.read,begin=TABLE+0x2d0,end=TABLE+0x2d3);self.uc.hook_add(UC_HOOK_MEM_INVALID,self.invalid)
 def invalid(self,uc,access,address,size,value,user):
  assert address in [0x20,0x28],('Unexpected invalid memory',hex(address));self.trace.append([99]);self.fatal=True;uc.emu_stop();return False
 def read(self,uc,access,address,size,value,user):assert address==TABLE+0x2d0 and size==4;self.trace.append([6,self.p['selector'],self.p['table']])
 def ret(self,value):
  rsp=self.uc.reg_read(UC_X86_REG_RSP);dest=self.q(rsp);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,rsp+8);self.uc.reg_write(UC_X86_REG_RIP,dest)
 def relink(self):
  self.wq(PET+0x148,EVENTS+self.events[0][0]*0x80 if self.events else 0)
  for i,e in enumerate(self.events):self.wq(EVENTS+e[0]*0x80+0x38,EVENTS+self.events[i+1][0]*0x80 if i+1<len(self.events) else 0)
 def hook(self,uc,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((n for n,v in FNS.items() if v==a),None);assert name,('Undeclared helper',hex(a))
  reg=lambda r:uc.reg_read(r);c,d,v,z=[reg(r) for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];rsp=reg(UC_X86_REG_RSP);arg=lambda n:self.q(rsp+8*n);rax=0
  if name=='init':
   assert c==PET;self.trace.append([0,signed(d)]);uc.mem_write(v,struct.pack('<QQQiiQQ',OPAQUE[0],OPAQUE[1],pointer(77),-111,222,OPAQUE[2],OPAQUE[3]))
  elif name=='mode':
   assert c==GAME and v==1;raw=bytes(uc.mem_read(d,48));o0,o1,t,x,y,o2,o3=struct.unpack('<QQQiiQQ',raw);n=self.calls;self.calls+=1;rax=self.p['responses'][min(n,len(self.p['responses'])-1)];self.trace.append([1,target(t),x,y,o0,o1,o2,o3,rax])
  elif name=='remove':
   assert c==GAME and (d-EVENTS)%0x80==0;i=(d-EVENTS)//0x80;self.trace.append([2,i]);self.events=[e for e in self.events if e[0]!=i];self.relink()
  elif name=='schedule':
   assert c==GAME and d==(PET if self.p['kind']>=0 else 0);self.trace.append([3,signed(v),signed(z),signed(arg(5)),signed(arg(6)),signed(arg(7))])
  elif name=='wander':assert c==GAME and d==PET;self.trace.append([4,v&255])
  elif name=='idle':assert c==GAME and d==PET;self.trace.append([5,signed(v)])
  elif name=='distance':assert c==(PET if self.p['kind']>=0 else 0);self.trace.append([7,signed(d),signed(v),self.p['distance']]);rax=self.p['distance']
  elif name=='collision':assert c==(PET if self.p['kind']>=0 else 0) and v==z==0;self.trace.append([8,target(d),0]);rax=0
  elif name=='collision4':assert c==(PET if self.p['kind']>=0 else 0) and v==4;self.trace.append([10,target(d),4,self.p['collision4']]);rax=self.p['collision4']
  elif name=='move':assert c==GAME and d==PET and signed(arg(6))==0 and arg(7)&65535==4;self.trace.append([11,signed(v),signed(z),arg(5)&255,0,4,self.p['move']]);rax=self.p['move']
  elif name=='monster':
   assert c==self.p['selector'] and signed(d)==(742 if self.p['kind']>=0 else -1);self.trace.append([9,int(c),signed(d),self.p['monster']]);rax=MON if self.p['monster']>=0 else 0
  elif name=='fatal':self.trace.append([99]);self.fatal=True;uc.emu_stop();return
  self.ret(rax)
 def execute(self,family,p):
  self.p=p;self.calls=0;self.fatal=False;self.trace=[];self.uc.mem_write(MEM,b'\0'*0x40000);self.uc.mem_write(STACK,b'\0'*0x40000)
  self.events=[[i,*e] for i,e in enumerate(p['events'])];self.wi(PET,max(0,p['kind']));self.wi(PET+4,742);self.wq(PET+0x10,MON if p['metadata'] else 0);self.wq(MON+0x40,MOVE if p['movement'] else 0);self.wq(MON+0x38,CTRL if p['control'] else 0)
  self.wi(MOVE+0x20,-11);self.wi(MOVE+0x24,22);self.uc.mem_write(MOVE+0x28,b'\x21');self.uc.mem_write(CTRL+0x38,struct.pack('<H',p['ai']));self.wq(PET+0x28,p['seed']);self.wq(PET+0x38,PATH if p['path'] else 0);self.uc.mem_write(PET+0x1bd,bytes([p['selector']]));self.uc.mem_write(GAME+0x106,bytes([p['selector']]));self.wi(GAME+0x170,p['frame'])
  self.wq(PET+0x88,AUX if p['aux'] else 0);self.wi(AUX+0x1c,p['auxWord']);self.wi(AUX+0xb34,signed(p['auxFlags']));self.uc.mem_write(PATH+0xbf,b'\x2c');self.uc.mem_write(MON+0xf1,bytes([max(0,p['monster'])]))
  for i in [1,2,3]:self.wq(GLOBAL+i*16,TABLE)
  self.wi(TABLE+0x2d0,p['table']);
  for i,t,frame in self.events:self.uc.mem_write(EVENTS+i*0x80,bytes([t]));self.wi(EVENTS+i*0x80+4,frame)
  self.relink();rsp=STACK+0x20008;self.wq(rsp,STOP);unit=PET if p['kind']>=0 else 0;entry={'prepare':PREP,'think':THINK,'execute':EXEC,'approach':APPROACH,'bound':DISP,'bound-range':DISP}[family];args=[GAME,unit,pointer(p['target']),p['hint']]
  if family=='prepare':args=[unit,p['speed']&0xffffffff,p['tuning']&0xffffffff,p['options']&0xffffffff]
  elif family=='execute':
   args=[GAME,unit,pointer(p['target']),p['mode']&0xffffffff]
   for i,value in enumerate([p['x'],p['y'],p['pathByte'],p['flags']],5):self.wq(rsp+8*i,value&0xffffffffffffffff)
  elif family in ['bound','bound-range']:
   self.coords(pointer(70),1,45,55,MEM+0x27000);self.wq(CFG+0x400,pointer(70));self.uc.mem_write(CFG+0x400+36,b'\x05' if family=='bound-range' else b'\x04');self.wi(CFG+0x3c,p['minimum']);self.wi(CFG+0x40,p['maximum']);args=[GAME,unit,CFG,CFG+0x400]
  for r,value in zip([UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9],args):self.uc.reg_write(r,value)
  self.uc.reg_write(UC_X86_REG_RSP,rsp)
  if family=='approach':self.wq(rsp-0x28,p['stack5'])
  try:self.uc.emu_start(BASE+entry,STOP,count=10000)
  except unicorn.UcError:
   if not self.fatal:raise
  assert self.fatal or self.uc.reg_read(UC_X86_REG_RIP)==STOP
  raw=self.uc.reg_read(UC_X86_REG_RAX);ret=-1 if self.fatal else 0 if family in ['prepare','think'] else raw&255 if family=='approach' else signed(raw)
  seed=self.q(PET+0x28);state=[self.i(MOVE+0x20),self.i(MOVE+0x24),int(self.uc.mem_read(MOVE+0x28,1)[0]),int(self.uc.mem_read(PATH+0xbf,1)[0]),struct.unpack('<H',self.uc.mem_read(CTRL+0x38,2))[0],seed&0xffffffff,seed>>32]
  return [ret,state,self.events,self.trace]
def default(**fields):
 p=dict(kind=1,metadata=1,movement=1,control=1,path=1,aux=0,selector=1,table=61,auxWord=0,auxFlags=0,ai=0x1234,seed=0x1234567812345678,frame=100,events=[[2,151],[1,77],[2,170]],responses=[1],target=70,hint=0,mode=2,x=-13,y=27,pathByte=1,flags=4,speed=0,tuning=0,options=0,distance=50,monster=128,stack5=0,minimum=10,maximum=200,collision4=0,move=1);p.update(fields);return p
def build(check=False):
 em=Movement();cases=[]
 def add(family,**fields):
  p=default(**fields);cases.append([family,p,em.execute(family,p)])
 for kind,metadata,movement in [(-1,1,1),(0,1,1),(1,0,1),(1,1,0),(1,1,1),(2,1,1)]:
  for speed,tuning,options in itertools.product([-2147483648,-1,0,1,2147483647],[-256,-1,0,1,76,77,78,100,255,256,257],[-1,0,1,2,15,2147483647]):add('prepare',kind=kind,metadata=metadata,movement=movement,speed=speed,tuning=tuning,options=options)
 for kind,frame,events in itertools.product([-1,1],[-2147483648,-100,-50,-1,0,100,2147483597,2147483598,2147483647],[[],[[2,0]],[[2,-1]],[[1,99],[2,149],[2,150]],[[2,150],[2,151]],[[2,151]],[[2,200],[2,149],[2,149]],[[255,1],[2,2147483647]]]):add('think',kind=kind,frame=frame,events=events)
 # Direct movement includes raw int returns,low-byte arguments and failure flags.
 for mode,kind,flags,response,pathByte in itertools.product([2,15],[-1,0,1,2,3,4,5],[0,1,2,3,4,5,6,7,260],[-1,0,1],[0,1,19,20,256,257]):add('execute',mode=mode,kind=kind,flags=flags,responses=[response],pathByte=pathByte)
 for mode,kind,selector,table,path,word,flag in itertools.product([2,15],[0,1,3],[0,1,2,3,4],[60,61],[0,1],[-1,0],[0,0x10000000]):add('execute',mode=mode,kind=kind,selector=selector,table=table,path=path,auxWord=word,auxFlags=flag)
 for kind,metadata,movement,aux,word,flag,table,path in itertools.product([0,1,3],[0,1],[0,1],[0,1],[-1,0],[0,0x10000000],[60,61],[0,1]):add('execute',mode=15,kind=kind,metadata=metadata,movement=movement,aux=aux,auxWord=word,auxFlags=flag,table=table,path=path)
 for targetId,control,metadata,response in itertools.product([0,70],[0,1],[0,1],[0,1]):add('execute',target=targetId,control=control,metadata=metadata,responses=[response],flags=7,ai=0x8001,seed=0)
 for seed,flags in itertools.product([0,1,2,3,13,87,0xffffffff,0xffffffffffffffff],[2,3,6,7]):add('execute',seed=seed,flags=flags,responses=[0])
 for family,hint,responses,frame,events,selector in itertools.product(['approach','bound'],[-1,0,1,2],[[1],[0,1],[0,0,1],[0,0,0,1],[0,0,0,0]],[-100,0,100,2147483647],[[],[[2,149],[2,151],[1,70]],[[2,200],[2,200]]],[1,3]):
  if family=='bound' and hint not in [0,1]:continue
  add(family,hint=hint,responses=responses,frame=frame,events=events,selector=selector,monster=128 if hint==1 else 0)
 for family,kind,metadata,movement in itertools.product(['approach','bound'],[-1,0,1,2],[0,1],[0,1]):add(family,kind=kind,metadata=metadata,movement=movement)
 for distance,monster,response in itertools.product([0,16,17,36,37],[-1,0,128],[0,1]):add('bound',distance=distance,monster=monster,responses=[response])
 for mode,targetId,response in itertools.product([-2147483648,-1,0,256,257],[0,70],[0,1]):add('execute',mode=mode,target=targetId,responses=[response],pathByte=-1)
 for value,hint,responses in itertools.product([0,0x7fffffffffffffff,0xffffffffffffffff],[0,1],[[1],[0,0,0,1],[0,0,0,0]]):add('approach',stack5=value,hint=hint,responses=responses)
 for distance,collision4,move in itertools.product([0,9,10,200,201],[0,1],[0,1]):add('bound-range',distance=distance,collision4=collision4,move=move)
 # Preserve every earlier source result after separating independent slots.
 previous=ROOT/'.local/class-skills/generic-pet-movement/diagnostics/distinct-slot88/generic-pet-movement-source-probe.json'
 old=json.loads(previous.read_text())
 for row in old:
  row[1]['auxWord']=row[1].pop('pathWord');row[1]['auxFlags']=row[1].pop('pathFlags');row[1]['aux']=0
 key=lambda row:json.dumps(row,sort_keys=True,separators=(',',':'))
 assert len(old)==5760 and not Counter(map(key,old))-Counter(map(key,cases))
 encoded=json.dumps(cases,separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==encoded
 else:OUT.write_text(encoded)
 libraries=list((STAGE/'venv').glob('lib/python*/site-packages/unicorn/lib/libunicorn.so.2'));assert len(libraries)==1
 artifacts=em.artifacts+[previous,OUT,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_generic_pet_actions.py',libraries[0]]
 counts={family:sum(row[0]==family for row in cases) for family in ['prepare','think','execute','approach','bound','bound-range']}
 result=dict(referenceVersion='3.3.93847',component='Current approach,movement preparation/mode wrapper and think-event maintenance',processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,completeAiVerified=False,handlerEnabled=False,
  verified=dict(contexts=len(cases),retainedEarlierSourceResults=len(old),byKind=counts,sourceFunctions=6,machineCodeBytes=sum(b-a for a,b in em.ranges),leafCodeBytes=174,leafPaddingBytes=2,fatalBoundaries=sum(row[-1][0]==-1 and [99] in row[-1][-1] for row in cases),realApproachCompositions=counts['approach'],realDispositionApproachCompositions=counts['bound'],realRangedPreparationThinkCompositions=counts['bound-range']),
  emulatorLibrary=dict(path=str(libraries[0].relative_to(ROOT)),sha256=digest(libraries[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},
  services=['Mode initializer supplies48explicit bytes including four opaque words. Actual movement/pathfinding and mode acceptance remain declared services,not world adapters. Their raw integer results and exact selected request fields are compared.',
   'Current movement preparation is a174-byte leaf with all direct branches inside its body,one return and two trailingINT3bytes before the next known unwind entry. A prior64-byte sample remains explicitly incomplete. Invalid null/nonmonster/missing-data writes are unsupported boundaries,not invented guards.',
   'Approach executes all five real movement bodies together;bound compositions additionally execute the captured disposition body. The unwritten fifth approach-core stack argument is never read by the complete core. Mode request does not depend on its residue.',
   'Queue removal services preserve node identities and next links. Scheduling,mode effects,table records and all native actor-clock/seed ownership/persistence remain separate integration work. Source RNG is the actual inline movement fallback code.'],remaining=['Bind real NPC metadata,path/mode actions,event queue/clock and persistent shared actor seed. Follow executor,target search callback/sorter,ordering,full root scheduling/owner callbacks and learned demon lifecycle remain unfinished. No new talent or class completion.'])
 out=ROOT/'tools/reference/generic-pet-movement-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==text
 else:out.write_text(text)
 print('Current movement oracle:',json.dumps(result['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
