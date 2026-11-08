#!/usr/bin/env python3
"""Current monster dispatcher+router replay with explicit returning event handlers."""
import argparse,json,struct
from pathlib import Path
from unicorn.x86_const import *
import unicorn
from probe_pet_timed_events import Events,sample,ptr,ident,PET,EVENTS,POOL,BASE,STACK,OBJECTS,STOP,ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/pet-event-dispatch-source';THUNK=STOP+0x200
class Dispatch(Events):
 def __init__(self):
  super().__init__();self.thunkEnd=THUNK
  for name,rva in [('pet-monster-dispatch-candidate-20261109.json','0x32edd0'),('callees-pet-ai-event-router-20261109.json','0x3de390')]:
   path=STAGE/name;capture=json.loads(path.read_text());self.artifacts.append(path)
   for n,h in capture['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   row=next(r for r in capture['records']if r['rva']==rva);assert row['allUnwindFragmentsCaptured']
   for f in row['fragments']:
    p=next(ROOT/n for n,h in capture['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
  table=STAGE/'pet-ai-event-entry-table-20261109.bin';raw=table.read_bytes();assert len(raw)==120;self.artifacts.append(table);self.map(BASE+0x1723f00,len(raw));self.uc.mem_write(BASE+0x1723f00,raw)
  self.handlers={a:i for i,a in enumerate(struct.unpack('<15Q',raw))if a};assert self.handlers[BASE+0x43b0c0]==2
  for address in self.handlers:self.map(address,1);self.uc.mem_write(address,b'\xc3')
 def hook(self,u,a,size,user):
  if THUNK<=a<self.thunkEnd:return
  if a not in getattr(self,'handlers',{}):return super().hook(u,a,size,user)
  sp=u.reg_read(UC_X86_REG_RSP);c,d,p0,p1=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];p2=self.q(sp+0x28)
  assert c==OBJECTS and d==PET
  active=[i for i in range(1,self.allocated+1)if int.from_bytes(u.mem_read(ptr(i)+2,2),'little')&1];assert len(active)==1
  current=active[0];kind=u.mem_read(ptr(current),1)[0];assert self.handlers[a]==kind
  signed=lambda x:(x+2**31)%2**32-2**31
  self.trace.append(f'route:{current}:{kind}:{signed(p0)}:{signed(p1)}:{signed(p2)}');self.callbacks+=1
  code=bytearray(b'\x48\x83\xec\x48')
  def mov(prefix,value):code.extend(prefix+struct.pack('<Q',value&0xffffffffffffffff))
  for cmd in self.p['actions'].get(str(self.callbacks),[]):
   mov(b'\x48\xb9',OBJECTS);mov(b'\x48\xba',ptr(current if cmd[1]==-1 else cmd[1])if cmd[0]=='remove'else PET)
   if cmd[0]=='remove':target=0x32e790
   elif cmd[0]=='idle':mov(b'\x49\xb8',cmd[1]);target=0x425550
   elif cmd[0]=='add':
    mov(b'\x49\xb8',cmd[1]);mov(b'\x49\xb9',cmd[2]);target=0x32fe20
    for i,v in enumerate(cmd[3:]):mov(b'\x48\xb8',v);code.extend(b'\x48\x89\x44\x24'+bytes([0x20+8*i]))
   else:raise AssertionError(cmd)
   mov(b'\x48\xb8',BASE+target);code.extend(b'\xff\xd0')
  code.extend(b'\x48\x83\xc4\x48\xc3');assert THUNK+len(code)<STOP+0x1000
  u.mem_write(THUNK,bytes(code));u.ctl_remove_cache(THUNK,STOP+0x1000);self.thunkEnd=THUNK+len(code);u.reg_write(UC_X86_REG_RIP,THUNK)
 def snapshot(self):
  out=super().snapshot();out['cursor']=ident(self.q(EVENTS+0x1430));return out
 def run(self,p):
  self.callbacks=0;commands=p['commands'];initial=dict(p,commands=[]);super().run(initial);self.p=p;out=[];self.wi(OBJECTS+0x50000+0xb30,p['rootb30'])
  for cmd in commands:
   if cmd[0]=='add':self.call(0x32fe20,OBJECTS,PET,cmd[1],cmd[2],cmd[3:])
   elif cmd[0]=='dispatch':
    assert cmd[1]==0 or 1<=cmd[1]<=self.allocated and not(int.from_bytes(self.uc.mem_read(ptr(cmd[1])+2,2),'little')&2),'Fixture supplied an unallocated/released chain start'
    self.wi(OBJECTS+0x170,cmd[2]);self.call(0x32edd0,OBJECTS,EVENTS,ptr(cmd[1]),cmd[3])
   else:raise AssertionError(cmd)
   out.append(self.snapshot())
   if self.unsupported:break
  return out

def build(check=False):
 em=Dispatch();cases=[]
 def add(commands,**changes):
  p=sample(**dict(dict(actions={},rootb30=0,commands=commands),**changes));cases.append(dict(input=p,output=em.run(p)))
 one=['add',2,115,17,-9,2147483647];two=['add',2,179,21,22,23];same=['add',2,115,21,22,23];third=['add',2,115,31,32,33]
 add([['dispatch',0,115,1]])
 for frame in [114,115,116]:add([one,['dispatch',1,frame,1]])
 add([one,two,['dispatch',1,115,1]])
 add([one,same,['dispatch',1,115,1]])
 add([one,same,['dispatch',2,115,1]])
 add([one,two,['dispatch',1,116,0],['dispatch',1,117,0]])
 for actions in [{'1':[['remove',-1]]},{'1':[['remove',2]]},{'1':[['remove',3]]},{'1':[['idle',15]]},{'1':[['add',2,179,7,8,9]]}]:add([one,same,third,['dispatch',1,115,1]],actions=actions)
 add([one,same,['dispatch',1,116,0]],actions={'1':[['remove',-1]]})
 for kind in range(15):add([['add',kind,115,17,-9,2147483647],['dispatch',1,115,1]])
 for changes in [dict(hasRoot=True,rootb30=2),dict(hasRoot=True,rootb30=2,mode=0),dict(hasRoot=True,rootb30=2,mode=12),dict(hasRoot=True,rootb30=2,flags=0x10000),dict(hasRoot=True,rootb30=2,stats=1),dict(hasRoot=True,rootb30=2,root1c=0)]:add([one,['dispatch',1,115,1]],rootb34=0,**changes)
 add([['add',1,115,0,0,0],['dispatch',1,115,1]],selector=0)
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';text=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 artifacts=[path,Path(__file__).resolve(),ROOT/'tools/probe_pet_timed_events.py',ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]
 result=dict(referenceVersion='3.3.93847',callbackTrampolineCacheInvalidated=True,newDispatcherBytes=722,newRouterBytes=324,totalLoadedBytes=sum(b-a for a,b in em.ranges),sourceSequences=len(cases),processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,scope='Supplied monster/null-callback chains with actual typed router and returning declared handlers;self/next removal,creation and idle execute actual producer/remover. Global starting-chain selection,host clock,AI entry1096body,custom callbacks and exceptions are not certified.',artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set(artifacts))})
 path=ROOT/'tools/reference/pet-event-dispatch-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==text
 else:path.write_text(text)
 print(f"Event dispatch:{len(cases)}source sequences;722-byte dispatcher+324-byte router with actual producer/remover.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
