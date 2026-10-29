#!/usr/bin/env python3
"""Execute current stat/missile producers and complete room trace with retained search."""
import argparse,itertools,json,struct
from pathlib import Path
from unicorn import UcError,UC_HOOK_MEM_READ
from unicorn.x86_const import *
from probe_generic_pet_search_nested import Nested,sample as previous,ENTRIES as NESTED,NODES,STAT,MASK
from probe_generic_pet_search_callback import BASE,MEM,STACK,STOP,REGS,pointer,identity,TABLE,signed
from probe_warlock_runtime import ROOT,STAGE,digest
LOCAL=ROOT/'.local/class-skills/generic-pet-search-producers';OUT=ROOT/'.local/class-skills/generic-pet-search-producers-source-probe.json'
P=MEM+0x300000;COST=P;MISS=P+0x20000;NODE=P+0x80000;OWNER=P+0x90000;VECTOR=P+0xa0000;VALUES=VECTOR+0x100;FROM=P+0xb0000;TO=FROM+0x10;CELLS=P+0xc0000
FNS={k:BASE+v for k,v in dict(stat=0xf6f10,read=0x24acd0,lookup=0x24a9d0,missile=0xdad30,trace=0x2a6c40,resolve=0x2972c0).items()}
class Producers(Nested):
 def __init__(self):
  self.producerActive=False;super().__init__();self.map(P,0x100000)
  self.captures=[]
  for name in ['callees-search-statmeta-idle-20261030.json','services-search-nodevalue-idle-20261030.json','services-search-missilemeta-idle-20261030.json','services-search-roomtrace-idle-20261030.json','services-search-neighbor-room-idle-20261030.json','services-search-statlookup-leaf-idle-20261030.json']:
   path=STAGE/name;c=json.loads(path.read_text());self.artifacts.append(path);self.captures.append(c)
   for n,h in c['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for r in c['records']:
    rva=int(r['rva'],16);assert BASE+rva in FNS.values()
    if rva==0x24a9d0:assert r['boundedLeafReference'] and r['nextKnownUnwindStart']=='0x24aa50' and not r['allUnwindFragmentsCaptured']
    else:assert r['allUnwindFragmentsCaptured']
    for f in r['fragments']:
     assert f['captured'] and not f['invalidInstructionMarkers'];paths=[ROOT/n for n,h in c['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(paths)==1
     raw=paths[0].read_bytes();a=BASE+int(f['rva'],16);assert len(raw)==int(f['endRva'],16)-int(f['rva'],16)
     if rva==0x24a9d0:assert len(raw)==128 and raw[112]==0xc3 and raw[113:]==b'\xcc'*15
     self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(paths[0])
  self.costs=(LOCAL/'itemstatcost.bin').read_bytes()[4:];self.missiles=(LOCAL/'missiles.bin').read_bytes()[4:];self.artifacts += [LOCAL/'itemstatcost.bin',LOCAL/'missiles.bin']
  self.uc.hook_add(UC_HOOK_MEM_READ,self.cell_hook,begin=CELLS,end=CELLS+0x8000-1)
 def node(self,i):return NODE+i*0x400 if i else 0
 def vector(self,header,values,rows):
  self.wq(header,values if rows else 0);self.wq(header+8,len(rows));self.wq(header+16,len(rows))
  for i,(key,value) in enumerate(rows):self.wi(values+i*8,signed(key));self.wi(values+i*8+4,value)
 def setup_services(self):
  super().setup_services()
  if 'traceRooms' not in self.world:return
  p=self.world;self.uc.mem_write(COST,self.costs);self.uc.mem_write(MISS,self.missiles);self.uc.mem_write(NODE,b'\0'*0x20000);self.uc.mem_write(CELLS,b'\0'*0x8000)
  self.wq(TABLE+0x1290,COST);self.wq(TABLE+0x1298,p['statCount']);self.wq(TABLE+0x1158,MISS);self.wq(TABLE+0x1160,p['missileCount'])
  for key,r in p['costOverrides'].items():
   a=COST+int(key)*324;flag=self.uc.mem_read(a+5,1)[0];self.uc.mem_write(a+5,bytes([(flag&~2)|(2 if r[0] else 0)]));self.uc.mem_write(a+20,bytes([r[1]]));self.wi(a+40,r[2])
  for key,r in p['missileOverrides'].items():self.uc.mem_write(MISS+int(key)*460+434,bytes([r]))
  for key,old in p['nodes'].items():
   i=int(key);r=p['nodeReads'].get(key,dict(flags=-2147483648,owner=1,base=[[0xac0000,old[2]]],full=[[0xac0000,old[2]]]))
   a=self.node(i);self.wi(a+0x1c,r['flags']);self.wi(a+0x20,old[0]);self.wq(a+0x68,self.node(old[1]));self.wq(a+0xe0,OWNER+i*0x40 if r['owner'] is not None else 0)
   if r['owner'] is not None:self.wi(OWNER+i*0x40,r['owner'])
   self.vector(a+0x30,a+0x120,r['base']);self.vector(a+0xe8,a+0x220,r['full'])
  for key,r in p['nested'].items():
   i=int(key);stat=STAT+i*0xc00;self.wq(stat+0xd0,self.node(r['primary']));self.wq(stat+0xd8,self.node(r['secondary']))
  self.vector(VECTOR,VALUES,p['lookupValues'])
  for key,r in p['traceRooms'].items():
   i=int(key);a=self.graph_room(i);x,y,width,height,pitch,adj,cells=r
   for off,value in zip([0x80,0x84,0x88,0x8c],[x,y,width,height]):self.wi(a+off,value)
   self.wq(a,a+0x200);self.wi(a+0x40,len(adj))
   for j,n in enumerate(adj):self.wq(a+0x200+8*j,self.graph_room(n))
   self.wq(a+0x38,a+0x300 if cells is not None else 0);self.wi(a+0x308,pitch);self.wq(a+0x320,CELLS+i*0x1000)
   if cells is not None:self.uc.mem_write(CELLS+i*0x1000,struct.pack('<'+str(len(cells))+'H',*cells))
  for a,point in [(FROM,p['from']),(TO,p['to'])]:self.wi(a,point[0]);self.wi(a+4,point[1])
 def cell_hook(self,uc,access,address,size,value,user):
  if not self.producerActive:return
  room=(uc.reg_read(UC_X86_REG_R10)-(MEM+0x40000))//0x600;index=(address-CELLS-room*0x1000)//2
  assert size==2 and (address-CELLS)%2==0
  cells=self.world['traceRooms'][str(room)][6]
  if cells is None or index<0 or index>=len(cells):self.trace.append([99]);self.unsupported=True;uc.emu_stop();return
  self.trace.append([80,room,index,cells[index]])
 def invalid(self,uc,access,address,size,value,user):
  rip=uc.reg_read(UC_X86_REG_RIP)
  if self.producerActive and address<0x100 and (rip in [BASE+0x24acda,BASE+0x2a6c59,BASE+0x2a6c64] or BASE+0x2a6c40<=rip<BASE+0x2a7221):self.trace.append([99]);self.unsupported=True;return False
  return super().invalid(uc,access,address,size,value,user)
 def hook(self,uc,a,size,user):
  if not self.producerActive:super().hook(uc,a,size,user);return
  if a in FNS.values():
   if a in [FNS['stat'],FNS['missile']]:self.trace.append([81 if a==FNS['stat'] else 82,uc.reg_read(REGS[0])&255,signed(uc.reg_read(REGS[1]))])
   return
  super().hook(uc,a,size,user)
 def execute_producer(self,p,name,args):
  self.producerActive=False;self.nestedActive=False;self.servicesActive=False;self.boundActive=False;self.wrapperActive=False
  from probe_generic_pet_search_bound import Bound
  shell=dict(p,visits=[],prepare=None,groups=None);super(Bound,self).execute(shell);self.world=p;self.p=p;self.trace=[];self.pending=None;self.setup_graph();self.producerActive=self.nestedActive=self.servicesActive=self.boundActive=True;self.mode='callback'
  raw=[]
  for typ,value in args:
   raw.append(self.node(value) if typ=='n' else VECTOR if typ=='v' else COST+value*324 if typ=='s' and value>=0 else 0 if typ=='s' else self.graph_room(value) if typ=='r' else FROM if typ=='f' and not p['nullFrom'] else 0 if typ=='f' else TO if typ=='t' and not p['nullTo'] else 0 if typ=='t' else value)
  rsp=STACK+0x20008;self.wq(rsp,STOP)
  for reg,value in [(UC_X86_REG_RSP,rsp),*zip(REGS,raw+[0]*(4-len(raw)))]:self.uc.reg_write(reg,value&0xffffffffffffffff)
  try:self.uc.emu_start(FNS[name],STOP,count=1000000)
  except UcError:
   if not self.unsupported:raise
  assert self.unsupported or self.uc.reg_read(UC_X86_REG_RIP)==STOP
  result=0
  if not self.unsupported:
   result=self.uc.reg_read(UC_X86_REG_RAX)
   if name=='stat':result=[(result-COST)//324,bool(self.uc.mem_read(result+5,1)[0]&2),self.uc.mem_read(result+20,1)[0],self.i(result+40)] if result else -1
   elif name=='missile':result=[(result-MISS)//460,self.uc.mem_read(result+434,1)[0]] if result else -1
   elif name=='lookup':result=(result-VALUES)//8 if result else -1
   elif name=='resolve':result=(result-(MEM+0x40000))//0x600 if result else 0
   else:result=signed(result)
  return [int(self.unsupported),result,[self.i(FROM),self.i(FROM+4)],[self.i(TO),self.i(TO+4)],self.caches(),self.trace]
 def execute_pipeline(self,p):self.producerActive=True;return super().execute_pipeline(p)

def sample(**values):
 p=previous();p.update(statCount=368,missileCount=742,costOverrides={},missileOverrides={},nodeReads={},lookupValues=[],**{'from':[0,0],'to':[3,2]},nullFrom=False,nullTo=False)
 p['traceRooms']={str(i):[-8,-8,48,16,48,p['graphRooms'][str(i)][4][:],[0]*768] for i in [1,2,3]};p.update(values);return p

def grids():return {'1':[0,0,4,4,4,[2,3,4],[0]*256],'2':[4,0,4,4,4,[1,4],[0]*256],'3':[0,4,4,4,4,[1,4],[0]*256],'4':[4,4,4,4,4,[2,3,1],[0]*256]}
def build(check=False):
 em=Producers();cases=[];pipeline=[]
 def case(p,name,args):cases.append([p,name,args,em.execute_producer(p,name,args)])
 for selector,kind in itertools.product([0,1,2,3,4],['stat','missile']):
  count=368 if kind=='stat' else 742
  for i in [-1,0,1,count-1,count,2147483647]:case(sample(),kind,[['i',selector],['i',i]])
 for selector in [1,2,3]:
  for i in range(368):case(sample(),'stat',[['i',selector],['i',i]])
  for i in range(742):case(sample(),'missile',[['i',selector],['i',i]])
 vectors=[[],[[0,0]],[[0xac0000,0]],[[0xac0000,-1],[0xac0000,9]], [[-2147483648,8],[-1,3],[0,1],[0xac0000,2],[2147483647,5]],[[0xac0000,4],[0,1]]]
 for rows,key in itertools.product(vectors,[-2147483648,-1,0,1,0xac0000,0xac0001,2147483647]):case(sample(lookupValues=rows),'lookup',[['v',0],['i',key]])
 for flags,owner,meta,hasMin,shift,minimum,value in itertools.product([0,-2147483648,-1],[None,0,1,4],[-1,172],[False,True],[0,8,31,255],[-1,0,3],[-2147483648,-1,0,2,2147483647]):
  p=sample(costOverrides={'172':[hasMin,shift,minimum]},nodeReads={'1':dict(flags=flags,owner=owner,base=[[0xac0000,value]],full=[[0xac0000,value+0 if value==2147483647 else value+1]])});case(p,'read',[['n',1],['i',0xac0000],['s',meta]])
 for rows,flags,key in itertools.product(vectors,[0,-1],[-1,0,0xac0000,0xac0001]):
  p=sample(nodeReads={'1':dict(flags=flags,owner=0,base=rows,full=rows)});case(p,'read',[['n',1],['i',key],['s',172]])
 case(sample(),'read',[['n',0],['i',0],['s',172]])
 for room,x,y,shape in itertools.product([0,1,2,3,4],[-1,0,3,4,7,8],[-1,0,3,4,7,8],[0,1]):
  rooms=grids()
  if shape:rooms['1'][5]=[0,3,2,4,2]
  case(sample(traceRooms=rooms),'resolve',[['r',room],['i',x],['i',y]])
 points=[[-1,0],[0,0],[1,1],[3,3],[4,0],[4,4],[7,7],[8,7]]
 for a,b,room,mask,shape in itertools.product(points,points,[0,1,4],[0,4,0x805,-1],[0,1,2]):
  rooms=grids()
  if shape==1:rooms['1'][6][5]=4;rooms['4'][6][15]=0x800
  if shape==2:rooms['1'][5]=[];rooms['2'][5]=[];rooms['3'][5]=[];rooms['4'][5]=[]
  case(sample(traceRooms=rooms,**{'from':a,'to':b}),'trace',[['r',room],['f',0],['t',0],['i',mask]])
 for pitch,a,b in itertools.product([2,4,6],[[0,0],[1,1],[3,3]],[[0,3],[3,0],[3,3]]):
  rooms=grids();rooms['1'][4]=pitch;rooms['1'][6][4]=4;case(sample(traceRooms=rooms,**{'from':a,'to':b}),'trace',[['r',1],['f',0],['t',0],['i',4]])
 for a,b in [[[0,0],[2147483647,0]],[[0,0],[-2147483648,0]],[[0,0],[0,-2147483648]],[[0,0],[-2147483648,-2147483648]]]:case(sample(traceRooms=grids(),**{'from':a,'to':b}),'trace',[['r',1],['f',0],['t',0],['i',4]])
 for nullFrom,nullTo in [(True,False),(False,True)]:case(sample(nullFrom=nullFrom,nullTo=nullTo),'trace',[['r',1],['f',0],['t',0],['i',4]])
 for mask,owner,explicit,shape in itertools.product([0,1,2,4,8,16,32,63,-1],[0,2],[0,3,4],[0,1,2,3]):
  p=sample(mask=mask,owner=owner,explicit=explicit)
  if shape==1:p['statCount']=0
  if shape==2:p['traceRooms']['1'][6][(0+8)*48+(5+8)]=4
  if shape==3:p['nodes']['3'][2]=2;p['mask410'][3]=512
  pipeline.append([p,em.execute_pipeline(p)])
 encoded=json.dumps(dict(cases=cases,pipeline=pipeline),separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==encoded
 else:OUT.write_text(encoded)
 import unicorn
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 paths=[OUT,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_search_nested.py',ROOT/'tools/probe_generic_pet_search_services.py',ROOT/'tools/probe_generic_pet_search_bound.py',ROOT/'tools/probe_generic_pet_search_wrapper.py',ROOT/'tools/probe_generic_pet_search_callback.py',ROOT/'tools/probe_generic_pet_search_groups.py',ROOT/'tools/probe_generic_pet_preparation.py',ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',*em.artifacts]
 proof=dict(referenceVersion='3.3.93847',component='Current stat/missile producers and complete room/grid trace',processMemoryRead=False,sourceCodeExecutedInOfflineEmulator=True,handlerEnabled=False,fullAiVerified=False,
 verified=dict(producerContexts=len(cases),producerUnsupported=sum(r[-1][0] for r in cases),pipelineContexts=len(pipeline),pipelineUnsupported=sum(r[-1][0] for r in pipeline),completeFunctions=5,completeFunctionBytes=2025,boundedLeafBytes=128,leafCodeAndNopBytes=113,leafPaddingBytes=15),
 emulatorLibrary=dict(path=str(libs[0].relative_to(ROOT)),sha256=digest(libs[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(paths))},
 services=['The actual current stat/missile getters,node value reader,193-byte adjacent-room resolver and1505-byte terminal raster trace execute. The bounded128-byte sorted lookup has113instruction/NOP bytes and15INT3padding bytes before the next known unwind start;no own unwind completeness is claimed.',
 'Complete stat/missile binary tables and explicit sorted node vectors/grid buffers replace the earlier value/size/trace services. Cell-read order,destination clipping,pitch-versus-width stepping,room changes,nulls,mask selection and retained full search are observable. Buffer bounds are explicit unsupported adapter boundaries;arbitrary corrupt pointers and cyclic graphs are outside acceptance.'],remaining=['Native differential and retained integration required. Actual mask410producer,world grid/room/registry/ownership mapping,ordering and actor lifecycle remain unfinished;no learned demon enabled.'])
 p=ROOT/'tools/reference/generic-pet-search-producers-source-evidence.json';encoded=json.dumps(proof,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Search producers:',json.dumps(proof['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
