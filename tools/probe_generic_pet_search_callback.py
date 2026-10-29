#!/usr/bin/env python3
"""Execute the complete cached target-search callback with explicit world services."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn import UC_HOOK_MEM_READ,UC_HOOK_MEM_INVALID,UcError
from unicorn.x86_const import *
from probe_generic_pet_search_groups import SearchGroups,ENTRY as PREP,DEST,GROUPS
from probe_generic_pet_preparation import BASE,MEM,STACK,STOP,REGS,SLOTS,VEC,TABLE,STATES,GLOBAL,signed
from probe_warlock_runtime import ROOT,STAGE,digest
CTX=MEM+0x2000;CFG=MEM+0x1000;GRID=MEM+0x4000;SWARM=MEM+0x8000;UNITS=MEM+0x30000
ROOM=MEM+0xa000;ROOMDATA=MEM+0xb000;LEVEL=MEM+0xc000;LEVELROW=MEM+0xd000;MONROW=MEM+0xe000
CALLBACK=BASE+0x4b3210;DISTANCE=BASE+0x40bfd0;CLEANUP=BASE+0x4b6c30
FNS={k:BASE+v for k,v in dict(valid=0x372ec0,room=0x278450,collision=0x27c710,level=0x2993a0,special=0x40c450,corpse=0x27ddb0,value=0x4b3000,monster=0x6bc90,has=0x296bc0).items()}
OUT=ROOT/'.local/class-skills/generic-pet-search-callback-source-probe.json'
def pointer(n):return UNITS+n*1024 if n else 0
def identity(p):
 if not p:return 0
 assert UNITS<p<UNITS+0x5000 and (p-UNITS)%1024==0;return (p-UNITS)//1024
def encode(r):return struct.pack('<QQ5i4B',pointer(r[0]),r[1]&0xffffffffffffffff,*r[2:7],*r[7:])
def decode(raw):
 r=list(struct.unpack('<QQ5i4B',raw));r[0]=identity(r[0]);return r
class SearchCallback(SearchGroups):
 def __init__(self):
  super().__init__();self.mode='init'
  for name,selected in [('callees-live-search-idle-20261030.json',[0x4b3210]),('callees-live-search-services-idle-20261030.json',[0x40bfd0])]:
   p=STAGE/name;c=json.loads(p.read_text());self.artifacts.append(p)
   for n,h in c['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for rva in selected:
    record=next(r for r in c['records'] if int(r['rva'],16)==rva);assert record['allUnwindFragmentsCaptured']
    for f in record['fragments']:
     assert f['captured'] and not f['invalidInstructionMarkers'];a=BASE+int(f['rva'],16);n=int(f['endRva'],16)-int(f['rva'],16)
     ps=[ROOT/n for n,h in c['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(ps)==1
     raw=ps[0].read_bytes();assert len(raw)==n;self.map(a,n);self.uc.mem_write(a,raw);self.ranges.append((a,a+n));self.artifacts.append(ps[0])
  self.constant=self.image.bytes(0x18129ec,4);assert struct.unpack('<f',self.constant)==(1/65536,);self.map(BASE+0x18129ec,4);self.uc.mem_write(BASE+0x18129ec,self.constant)
  for a in FNS.values():
   if not any(lo<=a<hi for lo,hi in self.ranges):self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200)
  self.uc.hook_add(UC_HOOK_MEM_READ,self.state_hook,begin=STATES+68*32,end=STATES+68*232-1);self.uc.hook_add(UC_HOOK_MEM_INVALID,self.invalid)
 def invalid(self,uc,access,address,size,value,user):
  if self.mode=='callback' and uc.reg_read(UC_X86_REG_RIP)==BASE+0x4b3489 and address==0x38 and size==1:
   self.trace.append([99]);self.unsupported=True;return False
  raise AssertionError(('Unexpected memory access',hex(uc.reg_read(UC_X86_REG_RIP)),hex(address),size))
 def hook(self,uc,a,size,user):
  if self.mode!='callback':super().hook(uc,a,size,user);return
  if self.pending and a==self.pending[0]:
   self.trace[self.pending[1]].append(signed(uc.reg_read(UC_X86_REG_RAX)));self.pending=None
  if a==DISTANCE:
   c,d,v=[uc.reg_read(r) for r in REGS[:3]];self.trace.append([1,identity(c),signed(d),signed(v)]);self.pending=(self.q(uc.reg_read(UC_X86_REG_RSP)),len(self.trace)-1)
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((k for k,v in FNS.items() if v==a),None);assert name,('Unknown callback helper',hex(a))
  c,d,v,z=[uc.reg_read(r) for r in REGS];p=self.world;result=0
  if name=='valid':assert c==MEM and z&255==1;result=p['valid'];self.trace.append([0,identity(d),identity(v),1,result])
  elif name=='room':
   assert identity(c)==p['pet'];info=p['rooms'][min(self.roomCalls,len(p['rooms'])-1)];self.roomCalls+=1;self.trace.append([2,identity(c),info])
   if info is not None:
    result=ROOM;self.wq(ROOM+0x18,ROOMDATA);self.wi(ROOMDATA+0x74,info[0]);self.wi(ROOMDATA+0x50,signed(info[1]));self.wq(ROOMDATA+0x90,LEVEL);self.wi(LEVEL+0x1f8,info[2])
  elif name=='collision':
   assert v&0xffffffff==4;result=p['collisions'][min(self.collisionCalls,len(p['collisions'])-1)];self.collisionCalls+=1;self.trace.append([3,identity(c),identity(d),4,result])
  elif name=='level':
   assert c&255==p['selector'];value=p['level'];self.trace.append([4,c&255,signed(d),value])
   if value is not None:result=LEVELROW;uc.mem_write(LEVELROW+63,bytes([value]))
  elif name in ['special','corpse','value']:
   if name=='special':result=p['special'];self.trace.append([5,identity(c),identity(d),result])
   elif name=='corpse':result=p['corpse'];self.trace.append([6,identity(c),result])
   else:result=p['values'].get(str(identity(c)),1);self.trace.append([7,identity(c),result])
  elif name=='monster':
   value=p['monsters'].get(str(signed(d)));self.trace.append([9,c&255,signed(d),value])
   if value is not None:result=MONROW;uc.mem_write(MONROW+137,bytes([value]))
  elif name=='has':
   unit=identity(c);state=signed(d);result=int(state in p['has'].get(str(unit),[]));self.trace.append([8,unit,state,result])
  self.ret(result)
 def run_entry(self,entry,args):
  rsp=STACK+0x20008;self.wq(rsp,STOP)
  for reg,value in [(UC_X86_REG_RSP,rsp),*zip(REGS,args)]:self.uc.reg_write(reg,value)
  try:self.uc.emu_start(entry,STOP,count=300000)
  except UcError:
   if not self.unsupported:raise
  assert self.unsupported or self.uc.reg_read(UC_X86_REG_RIP)==STOP
 def execute(self,p):
  self.world=p;self.p=p;self.trace=[];self.pending=None;self.unsupported=False;self.roomCalls=0;self.collisionCalls=0;self.reserves={};self.grows=self.frees=self.allocations=0;self.jobs=[];self.rawStateRead=False
  self.uc.mem_write(MEM,b'\0'*0x60000);self.uc.mem_write(STACK,b'\0'*0x40000);self.uc.mem_write(MEM+0x106,bytes([p['selector']]))
  for key,u in p['units'].items():
   unit=pointer(int(key));kind,path,x,y,mode,flags,hasAi,aiFlag,cls,selector=u;self.wi(unit,kind);self.wi(unit+4,cls);self.wi(unit+12,mode);self.wi(unit+0x124,signed(flags));self.uc.mem_write(unit+445,bytes([selector]));self.wq(unit+0x38,unit+0x200 if path else 0)
   if path:
    off=0x10 if kind in [2,4,5] else 0;self.wi(unit+0x200+off,x);self.wi(unit+0x204+off,y)
   self.wq(unit+0x10,unit+0x280 if hasAi else 0);self.wq(unit+0x2b8,unit+0x300 if hasAi else 0);self.uc.mem_write(unit+0x338,bytes([8 if aiFlag else 0]))
  self.wi(CTX,p['mask']);self.wq(CTX+8,pointer(p['owner']));self.wq(CTX+16,pointer(p['bias']));self.wq(CTX+24,CFG if p['config'] else 0)
  for off,val in zip([0x10,0x14,0x30,0x34,0x3c,0x40],p['params']):
   if off==0x14:self.uc.mem_write(CFG+off,struct.pack('<H',val))
   else:self.wi(CFG+off,val)
  self.wq(CFG+0x78,SWARM);self.wq(CFG+0x80,len(p['swarm']))
  for i,(target,count) in enumerate(p['swarm']):self.wq(SWARM+i*16,pointer(target));self.wi(SWARM+i*16+8,count)
  for i,target in enumerate(p['targets']):self.wq(CTX+0x20+i*8,pointer(target))
  for i,n in enumerate(p['counts']):self.wi(CTX+0x58+i*4,n)
  self.wq(CTX+0x48,GRID if p['grid'] is not None else 0)
  if p['grid'] is not None:
   for i,(count,distance,target) in enumerate(p['grid']):self.wi(GRID+i*16,count);self.wi(GRID+i*16+4,distance);self.wq(GRID+i*16+8,pointer(target))
  for i,r in enumerate(p['rows']):self.uc.mem_write(SLOTS+i*40,encode(r))
  if p.get('prepare'):
   self.mode='prepare';self.p=p['prepare']
   for n in [1,2,3]:self.wq(GLOBAL+n*16,TABLE)
   self.wq(TABLE+0x2d0,len(self.p['states']));self.wq(TABLE+0x2c8,STATES)
   for i,g in enumerate(self.p['states']):self.uc.mem_write(STATES+i*68+30,struct.pack('<h',g or 0))
   self.wq(VEC,SLOTS);self.wq(VEC+8,len(p['rows']));self.wq(VEC+16,32);self.wq(DEST,GROUPS);self.wq(DEST+8,0);self.wq(DEST+16,(1<<63)|10)
   self.run_entry(PREP,[p['selector'],VEC,DEST,0]);assert not self.unsupported
   assert self.q(DEST+8)<=10 and all(self.q(GROUPS+i*80+16)<=10 for i in range(self.q(DEST+8)))
   before=bytes(self.uc.mem_read(DEST,24));groupsBefore=bytes(self.uc.mem_read(GROUPS,self.q(DEST+8)*80));self.mode='cleanup';self.run_entry(CLEANUP,[DEST,0,0,0]);assert before==bytes(self.uc.mem_read(DEST,24)) and groupsBefore==bytes(self.uc.mem_read(GROUPS,self.q(DEST+8)*80))
   self.wq(CTX+0x50,DEST);self.trace=[];self.p=p
  elif p['groups'] is not None:
   self.wq(CTX+0x50,DEST);self.wq(DEST,GROUPS);self.wq(DEST+8,len(p['groups']))
   for i,(slot,states) in enumerate(p['groups']):
    a=GROUPS+i*80;self.wq(a,SLOTS+slot*40);self.wq(a+8,a+32);self.wq(a+16,len(states));self.wq(a+24,(1<<63)|10)
    for j,state in enumerate(states):self.wi(a+32+j*4,state)
  self.mode='callback'
  for target in p['visits']:
   self.run_entry(CALLBACK,[MEM,pointer(p['pet']),pointer(target),CTX])
   if self.unsupported:break
   assert self.uc.reg_read(UC_X86_REG_RAX)==0
  grid=None if p['grid'] is None else [[self.i(GRID+i*16),self.i(GRID+i*16+4),identity(self.q(GRID+i*16+8))] for i in range(64)]
  return [int(self.unsupported),[identity(self.q(CTX+0x20+i*8)) for i in range(5)],[self.i(CTX+0x58+i*4) for i in range(4)],grid,[decode(bytes(self.uc.mem_read(SLOTS+i*40,40))) for i in range(len(p['rows']))],self.trace]

def unit(x=0,y=0,kind=1,mode=1,flags=4,ai=1,aiFlag=0,path=1,cls=101,selector=2):return [kind,path,x,y,mode,flags,ai,aiFlag,cls,selector]
def row(skill=66,target=7):return [target,0,-2147483648,skill,3,16,2,2,0,91,92]
def sample(**values):
 units={'1':unit(),'2':unit(10<<16),'3':unit(9<<16,cls=103),'4':unit(12<<16,cls=104),'5':unit(14<<16,cls=105),'6':unit(20<<16,cls=106),'7':unit(25<<16,mode=12,cls=107),'8':unit(7<<16,cls=108),'9':unit(30<<16,cls=109)}
 p=dict(selector=2,pet=1,owner=2,bias=0,config=True,mask=63,units=units,params=[0,0,0,225,25,625],swarm=[],targets=[0]*5,counts=[0]*4,grid=[[0,0,0] for _ in range(64)],groups=[[0,[9]],[1,[59,193]]],rows=[row(),row(77)],visits=[3],valid=1,rooms=[[0,0,1]],collisions=[0],level=0,special=1,corpse=1,values={},monsters={'103':2,'104':3,'105':4,'108':5},has={});p.update(values);return p
def build(check=False):
 em=SearchCallback();cases=[]
 def case(p):cases.append([p,em.execute(p)])
 for mask,kind,mode,flags,x in itertools.product([0,1,2,4,8,16,32,63,-1],[0,1,2],[0,1,12,17],[0,4,0x10000,0x10004],[0,4,5,9,10,31,32,33]):
  p=sample(mask=mask);p['units']['3']=unit(x<<16,kind=kind,mode=mode,flags=flags,cls=103);case(p)
 for mask,room,ai,flag,collision,level,special in itertools.product([1,2,9,0x2e,63],[None,[1,0,1],[2,0,1],[2,0x80000,1],[3,0,1]],[0,1],[0,1],[-1,0,1],[None,0,8],[0,1]):
  p=sample(mask=mask,rooms=[room],collisions=[collision],level=level,special=special);p['units']['3'][6:8]=[ai,flag];case(p)
 for targets,bias,swarm,flags,visits in itertools.product([[0]*5,[4,5,6,7,8],[3]*5],[0,3,4,8],[[],[[3,2]],[[3,3]],[[4,999],[3,-1]]],[0,1],[[3],[3,4,5],[5,4,3],[3,3]]):
  p=sample(targets=targets,bias=bias,swarm=swarm,visits=visits);p['params']=[2,flags,25,100,100,225];case(p)
 # Counter overflow,wrong/missing actor/config,missing dynamic paths,raw float
 # boundaries and high-bit coordinate arithmetic remain observable.
 for owner,x,path,kind in itertools.product([0,2],[-2147483648,-65536,-1,0,1,524287,524288,2147483647],[0,1],[0,1,2,4,5]):
  if kind in [2,4,5] and not path:continue
  p=sample(owner=owner,counts=[2147483647]*4);p['units']['1']=unit(x,-x if x!=-2147483648 else x,kind=kind,path=path);case(p)
 for pet,visits,config,valid in itertools.product([0,1],[[0],[1],[3]],[False,True],[-1,0,1]):case(sample(pet=pet,visits=visits,config=config,valid=valid))
 # Actual preparation ->actual cleanup ->callback using the owned232states.
 states=json.loads((ROOT/'data/Sanctuary/Balance/generic-pet-states.json').read_text());skills=json.loads((ROOT/'data/Sanctuary/Balance/generic-pet-skills.json').read_text());byId={r['id']:r for r in skills};composed=0
 for ids,visits,present in itertools.product([[66],[66,77,87],[66,66],[361,392]],[[3],[4,3],[3,4]],[[9],[59,193],[]]):
  rows=[row(i) for i in ids];p=sample(rows=rows,visits=visits,has={'3':present},groups=None);p['prepare']=dict(selector=2,states=[r['group'] for r in states],skills={str(i):[byId[i]['auraTargetState'],byId[i]['auraState'],byId[i]['flags39']] for i in ids});case(p);composed+=1
 encoded=json.dumps(cases,separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==encoded
 else:OUT.write_text(encoded)
 artifacts=[OUT,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_search_groups.py',ROOT/'tools/probe_generic_pet_preparation.py',ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',ROOT/'data/Sanctuary/Balance/generic-pet-skills.json',ROOT/'data/Sanctuary/Balance/generic-pet-states.json',*em.artifacts]
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 proof=dict(referenceVersion='3.3.93847',component='Full current target-search callback and embedded preparation/cleanup composition',processMemoryRead=False,sourceCodeExecutedInOfflineEmulator=True,targetProcessCodeExecuted=False,handlerEnabled=False,fullAiVerified=False,
  verified=dict(contexts=len(cases),unsupportedAiNullBoundaries=sum(c[-1][0] for c in cases),callbackBytes=3027,distanceBytes=138,completeCallbackFragments=5,inlinePreparationCleanupCompositions=composed,maxOwnedStateGroupSize=7),
  emulatorLibrary=dict(path=str(libs[0].relative_to(ROOT)),sha256=digest(libs[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},services=['Current squared-distance138-byte leaf executes for every source distance call,with exact observed arguments/results. World validity,room/level/collision,special predicates,priority values/monster row bytes and state membership are supplied explicit services,not certified helper bodies.','Candidate positions preserve unsigned16dynamic versus signed static coordinates,raw float32Q16positions,source int32wrapping and CVTTSS2SIboundaries. Null monster-AI source dereference at4b3489is an explicit unsupported boundary;unknown memory faults fail the oracle. Unit snapshots are immutable during these supplied services;arbitrary mutation/reentrancy is not certified.','Captured846-byte group preparation,253-byte append and152-byte cleanup execute before callback in36owned-metadata compositions. These cases fit10inline groups/IDs;cleanup preserves every header/group byte. Heap-backed caller consumption remains unverified.'],remaining=['Native callback/service-trace and source composition comparison remains required. Real world geometry/validity/membership,caller traversal and downstream ordering/actions/lifecycle remain separate;no learned demon enabled.'])
 p=ROOT/'tools/reference/generic-pet-search-callback-source-evidence.json';encoded=json.dumps(proof,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Current search callback:',json.dumps(proof['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
