#!/usr/bin/env python3
"""Run the full cached search wrapper,callback,grid heap and bounded zero leaf."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn.x86_const import *
from probe_generic_pet_search_callback import SearchCallback,sample,pointer,identity,encode,decode,BASE,MEM,STACK,STOP,REGS,SLOTS,VEC,CFG,GRID,CALLBACK,PREP,STATES,TABLE,GLOBAL
from probe_warlock_runtime import ROOT,STAGE,digest
WRAPPER=BASE+0x4b3df0;ROWINIT=BASE+0x4b42a0;HEAP=BASE+0x4b6dd0;LEAF=BASE+0xaeea0
OUTPUT=MEM+0xf000;INDICES=MEM+0x7000;TRAMP=STOP+0x2800
OUT=ROOT/'.local/class-skills/generic-pet-search-wrapper-source-probe.json'
class Wrapper(SearchCallback):
 def __init__(self):
  super().__init__()
  for name,selected in [('callees-live-actions-targets-20261029.json',[0x4b3df0]),('callees-live-search-vector-idle-20261030.json',[0x4b42a0,0x4b6dd0])]:
   p=STAGE/name;c=json.loads(p.read_text());self.artifacts.append(p)
   for n,h in c['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for rva in selected:
    r=next(r for r in c['records'] if int(r['rva'],16)==rva);assert r['allUnwindFragmentsCaptured']
    for f in r['fragments']:
     assert f['captured'] and not f['invalidInstructionMarkers'];ps=[ROOT/n for n,h in c['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(ps)==1
     data=ps[0].read_bytes();a=BASE+int(f['rva'],16);assert len(data)==int(f['endRva'],16)-int(f['rva'],16);self.map(a,len(data));self.uc.mem_write(a,data);self.ranges.append((a,a+len(data)));self.artifacts.append(ps[0])
  p=STAGE/'grid-cell-leaf-idle-20261030.json';c=json.loads(p.read_text());r=c['record'];assert r['boundedLeafReference'] and not r['allUnwindFragmentsCaptured'] and r['rva']=='0xaeea0' and r['nextKnownUnwindStart']=='0xaeeb0' and r['bytes']==16 and r['captured'] and r['invalidInstructionMarkers']==0
  assert c['bytesRead']==592 and not any(c[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten']);self.artifacts.append(p)
  for n,h in c['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  p=STAGE/'grid-cell-leaf-idle-20261030.bin';data=p.read_bytes();assert data==bytes.fromhex('33c048890148894108488bc1c3cccccc');assert not any(a<=0xaeea0<b for a,b,_ in self.image.functions);assert min(a for a,b,_ in self.image.functions if a>0xaeea0)==0xaeeb0
  self.map(LEAF,16);self.uc.mem_write(LEAF,data);self.ranges.append((LEAF,LEAF+16));self.artifacts += [p,*[ROOT/n for n in c['artifacts'] if n.endswith('.asm')]]
  for a in [BASE+n for n in [0x371b10,0x40d9f0,0x1562240,0x155eeb0,0x7b6af0]]:self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.skills=json.loads((ROOT/'data/Sanctuary/Balance/generic-pet-skills.json').read_text());self.states=json.loads((ROOT/'data/Sanctuary/Balance/generic-pet-states.json').read_text());self.wrapperActive=False
 def hook(self,uc,a,size,user):
  if not self.wrapperActive:super().hook(uc,a,size,user);return
  if self.prepReturn==a:self.mode='callback';self.p=self.world;self.prepReturn=None
  if a==PREP:
   self.prepReturn=self.q(uc.reg_read(UC_X86_REG_RSP));self.mode='prepare';self.savedTrace=self.trace;self.trace=[]
   self.p=dict(selector=self.world['selector'],states=[r['group'] for r in self.states],skills={str(r['id']):[r['auraTargetState'],r['auraState'],r['flags39']] for r in self.skills})
  if self.mode=='prepare' and self.prepReturn is None:
   raise AssertionError('Unclosed source preparation')
  if self.restoreTrace==a:
   self.trace=self.savedTrace;self.restoreTrace=None
  if a==PREP:self.restoreTrace=self.prepReturn
  if a==TRAMP:self.step();return
  if a==BASE+0x371b10:
   c,d=[uc.reg_read(r) for r in REGS[:2]];assert c==MEM and identity(d)==self.world['owner'];self.trace.append([10,identity(d)])
   after=self.world.get('refreshPathTarget')
   if after is not None:self.wq(d+0x270,pointer(after))
   self.ret();return
  if a==BASE+0x40d9f0:
   c,d,v,z=[uc.reg_read(r) for r in REGS];rsp=uc.reg_read(UC_X86_REG_RSP);assert c==MEM and identity(d)==self.world['pet'] and z==CALLBACK and self.q(rsp+0x28)&255==1
   self.trace.append([11,identity(d),1]);self.job=dict(rsp=rsp,context=v,pet=d,index=0);self.step();return
  if a==BASE+0x1562240:
   c,d,v=[uc.reg_read(r) for r in REGS[:3]];assert d==0 and v==1024;uc.mem_write(c,b'\0'*v);self.ret(c);return
  if a==BASE+0x155eeb0:self.ret(uc.reg_read(UC_X86_REG_RAX));return
  if a==BASE+0x7b6af0:
   self.trace.append([99,5]);self.unsupported=True;uc.emu_stop();return
  # Callback hooks handle all world services and actual distance returns.
  super().hook(uc,a,size,user)
 def step(self):
  job=self.job;visits=self.world['visits']
  if job['index']==len(visits):self.uc.reg_write(UC_X86_REG_RSP,job['rsp']);self.ret();return
  target=visits[job['index']];job['index']+=1;rsp=job['rsp']-0x180;self.wq(rsp,TRAMP)
  for reg,value in [(UC_X86_REG_RSP,rsp),*zip(REGS,[MEM,job['pet'],pointer(target),job['context']])]:self.uc.reg_write(reg,value)
  self.uc.reg_write(UC_X86_REG_RIP,CALLBACK)
 def execute_wrapper(self,p):
  self.wrapperActive=False;self.prepReturn=self.restoreTrace=None
  shell=dict(p,visits=[],prepare=None,groups=None);super().execute(shell);self.world=p;self.p=p;self.trace=[];self.wrapperActive=True
  self.uc.mem_write(STACK,bytes([p['stackFill']])*0x40000)
  for key,value in p['pathTargets'].items():self.wq(pointer(int(key))+0x270,pointer(value))
  for key,value in p['flags128'].items():self.wi(pointer(int(key))+0x128,value)
  self.wq(VEC,SLOTS);self.wq(VEC+8,len(p['rows']));self.wq(VEC+16,(1<<63)|32)
  for selector in [1,2,3]:self.wq(GLOBAL+selector*16,TABLE)
  self.wq(TABLE+0x2d0,len(self.states));self.wq(TABLE+0x2c8,STATES)
  for i,state in enumerate(self.states):self.uc.mem_write(STATES+i*68+30,struct.pack('<h',state['group']))
  rsp=STACK+0x20008;self.wq(rsp,STOP)
  for i,value in enumerate([CFG if p['config'] else 0,VEC,p['mask']&0xffffffff,p['explicit'] and pointer(p['explicit'])]):self.wq(rsp+0x28+i*8,value)
  for reg,value in [(UC_X86_REG_RSP,rsp),*zip(REGS,[OUTPUT,MEM,pointer(p['pet']),pointer(p['owner'])])]:self.uc.reg_write(reg,value)
  from unicorn import UcError
  try:self.uc.emu_start(WRAPPER,STOP,count=1000000)
  except UcError as error:
   if not self.unsupported:raise AssertionError(dict(error=str(error),rip=hex(self.uc.reg_read(UC_X86_REG_RIP)),rsp=hex(self.uc.reg_read(UC_X86_REG_RSP)),mode=self.mode,trace=self.trace[-12:],mask=p['mask'],owner=p['owner'],explicit=p['explicit'])) from error
  assert self.unsupported or self.uc.reg_read(UC_X86_REG_RIP)==STOP and self.uc.reg_read(UC_X86_REG_RAX)==OUTPUT
  return [int(self.unsupported),[identity(self.q(OUTPUT+i*8)) for i in range(5)],[self.q(OUTPUT+40+i*8) for i in range(2)],[decode(bytes(self.uc.mem_read(SLOTS+i*40,40))) for i in range(len(p['rows']))],self.trace]
 def execute_heap(self,cells,indices):
  self.wrapperActive=False;self.mode='callback';self.pending=None;self.unsupported=False;self.trace=[]
  for i,(count,distance,target) in enumerate(cells):self.wi(GRID+i*16,count);self.wi(GRID+i*16+4,distance);self.wq(GRID+i*16+8,pointer(target))
  for i,idx in enumerate(indices):self.wq(INDICES+i*8,GRID+idx*16)
  self.run_entry(HEAP,[INDICES,INDICES+len(indices)*8,255,0])
  return [(self.q(INDICES+i*8)-GRID)//16 for i in range(len(indices))]
def wrapper_sample(**values):
 p=sample(groups=None,pathTargets={'2':3},flags128={},refreshPathTarget=None,explicit=0,stackFill=0);p.update(values);return p
def build(check=False):
 em=Wrapper();cases=[];heaps=[]
 def case(p):cases.append([p,em.execute_wrapper(p)])
 for mask,owner,explicit,visits in itertools.product([0,1,2,4,8,16,32,63,-1],[0,2],[0,3,4],[[3],[3,4,5],[5,4,3],[2,3,4],[]]):case(wrapper_sample(mask=mask,owner=owner,explicit=explicit,visits=visits))
 for kind,path,mode,flags,extra,target,valid in itertools.product([0,1,2,5],[0,1],[0,1,12,17],[0,4,0x10000],[0,0x4000000],[0,2,3],[-1,0,1]):
  if kind==2 and not path:continue
  p=wrapper_sample(pathTargets={'2':target},flags128={'3':extra},valid=valid,explicit=3);p['units']['2'][0:2]=[kind,path];p['units']['3'][4:6]=[mode,flags];case(p)
 for kind,mode,extra,flags in itertools.product([0,1,2,4,5],[0,1],[0,0x4000000],[4,0x10000]):
  p=wrapper_sample(flags128={'3':extra},visits=[2,4]);p['units']['3'][0]=kind;p['units']['3'][4:6]=[mode,flags];case(p)
 for after,rows,mask,stackFill in itertools.product([None,0,2,3,4],[[],[sample()['rows'][0]],sample()['rows']],[0,4,32,63],[0,165,255]):case(wrapper_sample(refreshPathTarget=after,rows=rows,mask=mask,stackFill=stackFill,visits=[3,4,5]))
 for pet,config,explicit,selector in itertools.product([0,1],[False,True],[0,3],[1,2,3]):case(wrapper_sample(pet=pet,config=config,explicit=explicit,selector=selector))
 # Comparator/address ties and full Floyd heap permutations,not just root.
 for n in range(7):
  for permutation in itertools.permutations(range(n)):
   for shape in range(3):
    cells=[[((i%3)-1 if shape==1 else (i%2)),(0 if shape==0 else (i%3)-1),3] for i in range(n)]
    indices=list(permutation);heaps.append([cells,indices,em.execute_heap(cells,indices)])
 for n in [8,16,32,63,64]:
  cells=[[2147483647 if i%2 else -2147483648,(-2147483648 if i%3 else 2147483647),3] for i in range(n)]
  for indices in [list(range(n)),list(reversed(range(n))),list(range(0,n,2))+list(range(1,n,2))]:heaps.append([cells,indices,em.execute_heap(cells,indices)])
 encoded=json.dumps(dict(cases=cases,heaps=heaps),separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==encoded
 else:OUT.write_text(encoded)
 deps=[OUT,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_search_callback.py',ROOT/'tools/probe_generic_pet_search_groups.py',ROOT/'tools/probe_generic_pet_preparation.py',ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',*em.artifacts]
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 proof=dict(referenceVersion='3.3.93847',component='Full current search wrapper with real callback/preparation/cleanup/grid heap',processMemoryRead=False,sourceCodeExecutedInOfflineEmulator=True,handlerEnabled=False,fullAiVerified=False,
  verified=dict(wrapperContexts=len(cases),unsupportedBoundaries=sum(r[-1][0] for r in cases),heapPermutations=len(heaps),wrapperBytes=1187,rowInitializerBytes=69,heapBytes=300,boundedLeafBytes=16,leafInstructionBytes=13,leafPaddingBytes=3),
  emulatorLibrary=dict(path=str(libs[0].relative_to(ROOT)),sha256=digest(libs[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(deps))},
  services=['The full wrapper1187bytes,callback3027,distance138,preparation846,append253,cleanup152,row initializer69 and grid heap300 execute. The bounded16-byte leaf has13instruction bytes/one return plus3INT3padding bytes before the next known unwind start;no own unwind completeness is claimed.',
   'Owner refresh371b10 and traversal40d9f0 are supplied world services. Refresh effects are applied before owner/path-target reads. Traversal calls the actual callback in supplied order with raw byte1. Actual validity/room/level/collision/special/state/priority services retain the callback acceptance boundary.',
   'Group/pointer vectors remain embedded within10groups/state IDs and64positive grid cells. Memory zeroing and cookie validation are declared services;cookie preserves return RAX. Heap helper ignores its source uninitialized R8byte;controlled stack fills retain that distinction.'],
  remaining=['Native wrapper/payload/heap and retained integration comparison required. Real world traversal/refresh and callback helper bindings,heap-backed caller behavior,ordering and actor lifecycle remain unfinished. No learned demon enabled.'])
 p=ROOT/'tools/reference/generic-pet-search-wrapper-source-evidence.json';encoded=json.dumps(proof,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Current search wrapper:',json.dumps(proof['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
