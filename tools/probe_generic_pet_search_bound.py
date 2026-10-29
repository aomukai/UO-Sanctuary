#!/usr/bin/env python3
"""Compose actual current search with actual refresh/registry/room traversal."""
import argparse,itertools,json,struct
from pathlib import Path
from unicorn.x86_const import *
from probe_generic_pet_search_wrapper import Wrapper,wrapper_sample,WRAPPER
from probe_generic_pet_search_callback import BASE,MEM,REGS,pointer,identity,ROOMDATA,LEVEL
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/generic-pet-search-bound-source-probe.json'
GRAPH=MEM+0x40000;BUCKETS=MEM+0x56000
class Bound(Wrapper):
 def __init__(self):
  super().__init__();self.boundActive=False;self.freshScene=False
  for name,selected in [('callees-live-search-traversal-20261030.json',[0x371b10,0x40d9f0]),('traversal-services-20261030.json',[0x370680])]:
   p=STAGE/name;c=json.loads(p.read_text());self.artifacts.append(p)
   for n,h in c['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for rva in selected:
    r=next(r for r in c['records'] if int(r['rva'],16)==rva);assert r['allUnwindFragmentsCaptured']
    for f in r['fragments']:
     assert f['captured'] and not f['invalidInstructionMarkers'];paths=[ROOT/n for n,h in c['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(paths)==1
     data=paths[0].read_bytes();a=BASE+int(f['rva'],16);assert len(data)==int(f['endRva'],16)-int(f['rva'],16);self.map(a,len(data));self.uc.mem_write(a,data);self.ranges.append((a,a+len(data)));self.artifacts.append(paths[0])
  self.dispatch=self.image.bytes(0x17243d0,16);assert self.dispatch==b'\0'*16;self.map(BASE+0x17243d0,16);self.uc.mem_write(BASE+0x17243d0,self.dispatch)
  self.map(BASE+0x370610,1);self.uc.mem_write(BASE+0x370610,b'\xc3')
 def graph_room(self,n):return GRAPH+n*0x600 if n else 0
 def setup_graph(self):
  p=self.world;self.bucketSlots={}
  for key,u in p['units'].items():
   n=int(key);self.wi(pointer(n)+8,p['unitIds'][key])
  for key,(typ,ident) in p['pathRefs'].items():self.wi(pointer(int(key))+0x278,typ);self.wi(pointer(int(key))+0x27c,ident)
  for key,nxt in p['registryNext'].items():self.wq(pointer(int(key))+0x158,pointer(nxt))
  for key,nxt in p['next160'].items():self.wq(pointer(int(key))+0x160,pointer(nxt))
  self.wq(MEM+0x3630,pointer(p['tileFirst']))
  for key,r in p['graphRooms'].items():
   a=self.graph_room(int(key));typ,flags,level,first,adj=r;self.wq(a,a+0x200);self.wi(a+0x40,len(adj));self.wq(a+0xa8,pointer(first))
   self.wq(a+0x18,a+0x100);self.wi(a+0x174,typ);self.wi(a+0x150,flags);self.wq(a+0x190,a+0x400);self.wi(a+0x5f8,level)
   for i,n in enumerate(adj):self.wq(a+0x200+i*8,self.graph_room(n))
 def hook(self,uc,a,size,user):
  if not self.boundActive:super().hook(uc,a,size,user);return
  if a==WRAPPER and not self.freshScene:self.setup_graph();self.freshScene=True
  if a in [BASE+0x371b10,BASE+0x40d9f0]:return
  if a==BASE+0x370610:
   c,d,v=[uc.reg_read(r) for r in REGS[:3]];assert d==MEM;typ=(c+2**31)%2**32-2**31;ident=(v+2**31)%2**32-2**31;key=f'{typ}:{ident}';first=self.world['bucketHeads'].get(key,0)
   self.trace.append([21,1,typ,ident,first])
   if key not in self.bucketSlots:self.bucketSlots[key]=BUCKETS+16*len(self.bucketSlots)
   slot=self.bucketSlots[key];self.wq(slot,pointer(first));self.ret(slot);return
  if a==BASE+0x278450:
   which=identity(uc.reg_read(UC_X86_REG_RCX));rid=self.world['unitRoom'].get(str(which),0);to=self.q(uc.reg_read(UC_X86_REG_RSP))
   if BASE+0x40d9f0<=to<BASE+0x40dcbb:self.trace.append([20,which,rid])
   else:self.trace.append([2,which,self.world['graphRooms'][str(rid)][:3] if rid else None])
   self.ret(self.graph_room(rid));return
  if a==BASE+0x372ec0:
   target=identity(uc.reg_read(UC_X86_REG_R8));key=str(target)
   if key in self.world['change160']:self.wq(pointer(target)+0x160,pointer(self.world['change160'][key]))
  if a==BASE+0x7b6af0:
   self.unsupported=True;self.trace.append([99]);uc.emu_stop();return
  super().hook(uc,a,size,user)
 def execute_bound(self,p):
  self.boundActive=True;self.freshScene=False
  result=super().execute_wrapper(p)
  result.insert(4,[identity(self.q(pointer(int(k))+0x270)) for k in p['units']])
  return result
def sample(**values):
 p=wrapper_sample(unitIds={str(i):100+i for i in range(1,10)},pathRefs={'2':[1,103]},registryNext={'3':4},bucketHeads={'1:103':3,'1:104':3},tileFirst=3,
 graphRooms={'1':[0,0,1,0,[2,3]],'2':[0,0,2,3,[]],'3':[0,0,2,5,[]]},unitRoom={'1':1},next160={'3':4,'4':0,'5':0},change160={});p.update(values);return p
def build(check=False):
 em=Bound();cases=[]
 def case(p):cases.append([p,em.execute_bound(p)])
 for mask,owner,explicit,adj,chain in itertools.product([0,1,2,4,8,16,32,63,-1],[0,2],[0,3,4],[[],[2],[2,3],[3,2],[2,2],[0]],[{'3':4,'4':0,'5':0},{'3':5,'5':0},{'3':0,'5':0}]):
  p=sample(mask=mask,owner=owner,explicit=explicit,next160=chain);p['graphRooms']['1'][4]=adj;case(p)
 for cached,typ,ident,first,kind,mode in itertools.product([0,3,4],[1,5],[0,103,104],[0,3,4],[1,4],[0,1,2,3]):
  p=sample(pathTargets={'2':cached},pathRefs={'2':[typ,ident]},bucketHeads={f'{typ}:{ident}':first},tileFirst=first);p['units']['3'][0:1]=[kind];p['units']['3'][4]=mode;case(p)
 for changed,mask,owner,explicit in itertools.product([0,5],[0,4,32,63],[0,2],[0,3]):case(sample(change160={'3':changed},mask=mask,owner=owner,explicit=explicit))
 for owner,pet,path,roomid,valid in itertools.product([0,2],[0,1],[0,1],[0,1],[-1,0,1]):
  p=sample(owner=owner,pet=pet,unitRoom={'1':roomid},valid=valid);p['units']['2'][1]=path;case(p)
 encoded=json.dumps(cases,separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==encoded
 else:OUT.write_text(encoded)
 artifacts=[OUT,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_search_wrapper.py',ROOT/'tools/probe_generic_pet_search_callback.py',ROOT/'tools/probe_generic_pet_search_groups.py',ROOT/'tools/probe_generic_pet_preparation.py',ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',*em.artifacts]
 import unicorn
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 proof=dict(referenceVersion='3.3.93847',component='Full search composed with actual refresh,registry and room traversal',processMemoryRead=False,sourceCodeExecutedInOfflineEmulator=True,handlerEnabled=False,fullAiVerified=False,
 verified=dict(contexts=len(cases),unsupportedBoundaries=sum(c[-1][0] for c in cases),addedCompleteSourceBytes=1049,addedFunctions=3,searchIndex=1),
 emulatorLibrary=dict(path=str(libs[0].relative_to(ROOT)),sha256=digest(libs[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},
 services=['The existing full wrapper/callback/preparation/cleanup/grid bodies execute with actual230-byte refresh,104-byte registry and715-byte traversal. Only registry bucket selection,room metadata and the retained callback predicates remain supplied world services. No synthetic preselected visit list replaces traversal.',
 'Actual index1descriptor is immutable kind0/no override. Adjacent room arrays and linked unit/registry nodes are supplied raw memory;duplicates,missing rooms,stale pointers,item modes,explicit targets and live next-link changes remain observable.',
 'Mutable path targets are checked after the full pipeline. This acceptance requires the managed binder to read the traversal path store after refresh rather than reusing an earlier wrapper path snapshot.'],remaining=['Native complete-bound search comparison and retained integration checks required. Actual Britannia room/registry data,callback helper bodies,ordering and full actor lifecycle remain separate;no learned demon enabled.'])
 p=ROOT/'tools/reference/generic-pet-search-bound-source-evidence.json';text=json.dumps(proof,indent=2)+'\n'
 if check:assert p.read_text()==text
 else:p.write_text(text)
 print('Bound search:',json.dumps(proof['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
