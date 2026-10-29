#!/usr/bin/env python3
"""Execute nested current search/allegiance/geometry/state bodies,including switch data."""
import argparse,itertools,json,struct,re
from pathlib import Path
from unicorn import UcError
from unicorn.x86_const import *
from probe_generic_pet_search_services import Services,fixture,DATA,META,MONS,STAT,MASK
from probe_generic_pet_search_bound import Bound,GRAPH
from probe_generic_pet_search_callback import BASE,MEM,STACK,STOP,REGS,pointer,identity,TABLE,signed
from probe_warlock_runtime import ROOT,STAGE,digest,disassemble
LOCAL=ROOT/'.local/class-skills/generic-pet-search-nested';OUT=ROOT/'.local/class-skills/generic-pet-search-nested-source-probe.json'
N=MEM+0x180000;M2=N;RELN=N+0x40000;NODES=N+0x50000;OBJECT=N+0x60000;STATMETA=N+0x70000;MISSILE=N+0x71000
ENTRIES={k:BASE+v for k,v in dict(allegiance=0x24fd30,relation=0x377dc0,ray=0x27c500,point=0x27c5e0,index=0x40c060,sizeX=0x277db0,sizeY=0x277e60,secondary=0x6bd00,mask=0x296f60,alignment=0x24eb30).items()}
COMPOSED={k:BASE+v for k,v in dict(valid=0x372ec0,collision=0x27c710,special=0x40c450,corpse=0x27ddb0).items()}
class Nested(Services):
 def __init__(self):
  self.nestedActive=False;super().__init__();self.map(N,0x90000);self.annotations=[]
  for label in ['search-allegiance','search-geometry','search-state-mask','search-alignment']:
   p=STAGE/('callees-'+label+'-idle-20261030.json');c=json.loads(p.read_text());self.artifacts.append(p)
   for n,h in c['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for r in c['records']:
    rva=int(r['rva'],16);assert BASE+rva in ENTRIES.values() and r['allUnwindFragmentsCaptured']
    for f in r['fragments']:
     assert f['captured'];ps=[ROOT/n for n,h in c['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(ps)==1
     data=ps[0].read_bytes();a=BASE+int(f['rva'],16);assert len(data)==int(f['endRva'],16)-int(f['rva'],16)
     if rva in [0x277db0,0x277e60]:
      length=152 if rva==0x277db0 else 184;code,table=data[:length],data[length:];assert len(table)==24
      targets=list(struct.unpack('<6I',table));codePath=LOCAL/f'{rva:x}-code.bin';codePath.write_bytes(code);asm,bad=disassemble(codePath,rva,BASE);assert bad==0
      text=asm.read_text();starts={int(v,16)-BASE for v in re.findall(r'^\s+([0-9a-f]+):',text,re.M)}
      assert all(t in starts for t in targets) and f'rax*4+0x{rva+length:x}' in text
      self.annotations.append(dict(rva=hex(rva),codeAndPaddingBytes=length,inlineSwitchBytes=24,tableTargets=targets,rawInvalidMarkers=f['invalidInstructionMarkers'],codeInvalidMarkers=0));self.artifacts += [codePath,asm]
     else:assert not f['invalidInstructionMarkers']
     self.map(a,len(data));self.uc.mem_write(a,data);self.ranges.append((a,a+len(data)));self.artifacts.append(ps[0])
  self.secondary=(LOCAL/'monstats2.bin').read_bytes()[4:];self.artifacts.append(LOCAL/'monstats2.bin')
  for rva in [0xf6f10,0x24acd0,0x2a6c40,0xdad30]:self.map(BASE+rva,1);self.uc.mem_write(BASE+rva,b'\xc3')
 def setup_services(self):
  super().setup_services()
  if 'nested' not in self.world:return
  p=self.world;self.uc.mem_write(N,b'\0'*0x90000);self.uc.mem_write(M2,self.secondary);self.wq(TABLE+0xfd0,M2);self.wq(TABLE+0xfd8,p['secondaryCount']);self.wq(TABLE+0x2d0,p['wideCount']);self.wq(TABLE+0x410,MASK if p['mask410'] is not None else 0)
  if p['mask410'] is not None:
   for i,w in enumerate(p['mask410']):self.wi(MASK+4*i,signed(w))
  for key,value in p['linkOverrides'].items():self.uc.mem_write(MONS+int(key)*508+76,struct.pack('<h',value))
  for key,values in p['dimensionOverrides'].items():self.uc.mem_write(M2+int(key)*296+8,struct.pack('<bb',*values))
  for key,r in p['nested'].items():
   i=int(key);u=pointer(i);data=u+0x280;stat=STAT+i*0xc00
   self.wq(data+0x88,RELN+r['relationHead']*0x40 if r['relationHead'] else 0)
   self.wq(stat+0xd0,NODES+r['primary']*0x80 if r['primary'] else 0);self.wq(stat+0xd8,NODES+r['secondary']*0x80 if r['secondary'] else 0)
   if p['units'][key][0]==2:
    self.wq(data,OBJECT+i*0x100 if r['hasObjectBase'] else 0);self.wi(OBJECT+i*0x100+0x90,r['objectSizes'][0]);self.wi(OBJECT+i*0x100+0x94,r['objectSizes'][1])
  for key,r in p['relations'].items():
   a=RELN+int(key)*0x40;self.wi(a,r[0]);self.wi(a+4,signed(r[1]));self.wq(a+0x18,RELN+r[2]*0x40 if r[2] else 0)
  for key,r in p['nodes'].items():
   a=NODES+int(key)*0x80;self.wi(a+0x20,r[0]);self.wq(a+0x68,NODES+r[1]*0x80 if r[1] else 0)
 def count_hook(self,uc,access,address,size,value,user):
  if self.nestedActive and self.mode=='callback' and uc.reg_read(UC_X86_REG_RIP)==BASE+0x296fb2:
   assert size==8;self.trace.append([70,self.world['units'][str(self.maskUnit)][9],self.world['wideCount']]);return
  super().count_hook(uc,access,address,size,value,user)
 def invalid(self,uc,access,address,size,value,user):
  allowed=[0x377de9,0x277e09,0x277e0c,0x277ebc,0x277ebf,0x40c07a,0x40c0b7,0x40c0b3,0x40c0ea,0x40c11a,0x40c13d,0x296fca]
  if self.nestedActive and address<0x100 and uc.reg_read(UC_X86_REG_RIP) in [BASE+r for r in allowed]:self.unsupported=True;self.trace.append([99]);return False
  return super().invalid(uc,access,address,size,value,user)
 def hook(self,uc,a,size,user):
  if not self.nestedActive:super().hook(uc,a,size,user);return
  if a in ENTRIES.values():
   if a==ENTRIES['mask']:self.maskUnit=identity(uc.reg_read(REGS[0]))
   return
  c,d,v,z=[uc.reg_read(r) for r in REGS];p=self.world
  if a==BASE+0xf6f10:
   assert d==172;value=p['statMetadata'];self.trace.append([71,c&255,172,value]);self.ret(STATMETA if value else 0);return
  if a==BASE+0x24acd0:
   node=(c-NODES)//0x80;assert d==0xac0000 and v==STATMETA;value=p['nodes'][str(node)][2];self.trace.append([72,node,d,value]);self.ret(value);return
  if a==BASE+0x2a6c40:
   room=(c-GRAPH)//0x600 if c else 0;points=[self.i(d),self.i(d+4),self.i(v),self.i(v+4)];value=p['traceResult'];self.trace.append([73,room,*points,signed(z),value]);self.ret(value);return
  if a==BASE+0xdad30:
   value=p['missileSize'];self.trace.append([74,c&255,signed(d),value]);self.uc.mem_write(MISSILE+0x1b2,bytes([value or 0]));self.ret(MISSILE if value is not None else 0);return
  super().hook(uc,a,size,user)
 def execute_nested(self,p,name,args):
  self.nestedActive=False;self.servicesActive=False;self.boundActive=False;self.wrapperActive=False
  shell=dict(p,visits=[],prepare=None,groups=None);super(Bound,self).execute(shell);self.world=p;self.p=p;self.trace=[];self.pending=None;self.setup_graph();self.nestedActive=True;self.servicesActive=True;self.boundActive=True;self.mode='callback'
  raw=[]
  for typ,x in args:raw.append(pointer(x) if typ=='u' else self.graph_room(x) if typ=='r' else (MASK if p['mask410'] is not None else 0) if typ=='m' else MEM if typ=='g' else x)
  rsp=STACK+0x20008;self.wq(rsp,STOP)
  for i,value in enumerate(raw[4:]):self.wq(rsp+0x28+8*i,value&0xffffffffffffffff)
  for reg,value in [(UC_X86_REG_RSP,rsp),*zip(REGS,raw[:4]+[0]*(4-len(raw[:4])))]:self.uc.reg_write(reg,value&0xffffffffffffffff)
  try:self.uc.emu_start((ENTRIES|COMPOSED)[name],STOP,count=300000)
  except UcError:
   if not self.unsupported:raise
  assert self.unsupported or self.uc.reg_read(UC_X86_REG_RIP)==STOP
  value=0
  if not self.unsupported:
   value=self.uc.reg_read(UC_X86_REG_RAX)
   if name=='secondary':value=[(value-M2)//296,*bytes(self.uc.mem_read(value+4,1)),*bytes(self.uc.mem_read(value+6,1)),*struct.unpack('<bb',bytes(self.uc.mem_read(value+8,2)))] if value else -1
   elif name=='relation':value=(value-RELN)//0x40 if value else 0
   else:value=signed(value)
  return [int(self.unsupported),value,self.caches(),self.trace]
 def execute_pipeline(self,p):self.nestedActive=True;return super().execute_pipeline(p)

def sample(**values):
 p=fixture();p.update(secondaryCount=623,linkOverrides={},dimensionOverrides={},wideCount=232,mask410=[0]*16,statMetadata=True,traceResult=0,missileSize=1,
 relations={},nodes={str(i):[105,0,2 if i in [1,2] else 0] for i in range(1,10)},nested={str(i):dict(hasObjectBase=True,objectSizes=[2,3],relationHead=0,primary=i,secondary=0) for i in range(1,10)})
 for r in p['world'].values():r['stats'][1][3]=512
 p.update(values);return p

def build(check=False):
 em=Nested();cases=[];pipeline=[]
 def case(p,name,args):cases.append([p,name,args,em.execute_nested(p,name,args)])
 for selector,cls,count,link in itertools.product([0,1,2,3,4],[-1,0,1,750,751],[0,623],[-1,0,622,623]):case(sample(secondaryCount=count,linkOverrides={'0':link}),'secondary',[['i',selector],['i',cls]])
 for selector in [1,2,3]:
  for cls in range(751):case(sample(),'secondary',[['i',selector],['i',cls]])
  for row in range(623):case(sample(linkOverrides={'0':row}),'secondary',[['i',selector],['i',0]])
 for kind,data,obj,selector,missile in itertools.product([-1,0,1,2,3,4,5,6],[0,1],[False,True],[0,2],[None,0,255]):
  p=sample(missileSize=missile);p['units']['1'][0]=kind;p['units']['1'][6]=data;p['units']['1'][9]=selector;p['nested']['1']['hasObjectBase']=obj
  for name in ['sizeX','sizeY']:case(p,name,[['u',1]])
 for name in ['sizeX','sizeY']:case(sample(),name,[['u',0]])
 for sx,sy in itertools.product([-128,-1,0,2,127],[-128,0,3,127]):
  p=sample(linkOverrides={'101':0},dimensionOverrides={'0':[sx,sy]})
  for name in ['sizeX','sizeY']:case(p,name,[['u',1]])
 for flags,kind,subject,shape,metadata,value in itertools.product([0,-1],[-1,0,1,2,3],[0,1],[0,1,2,3,4],[False,True],[-1,0,1,2,3]):
  p=sample(statMetadata=metadata);p['units']['1'][0]=kind;p['world']['1']['stats'][0]=flags
  p['nodes']={'1':[0,2,99],'2':[105,0,value],'3':[105,0,value+1]};p['nested']['1']['primary']=0 if shape in [0,2] else 1;p['nested']['1']['secondary']=3 if shape in [2,3] else 0
  if shape==4:p['world']['1']['stats']=None
  case(p,'alignment',[['u',subject]])
 for source,target,a,b in itertools.product([0,1],[0,1,3],[-1,0,1,2,3],[-1,0,1,2,3]):
  p=sample();p['nodes']['1'][2]=a;p['nodes']['3'][2]=b;case(p,'allegiance',[['u',source],['u',target]])
 for kind,data,target,head,ids in itertools.product([0,1],[0,1],[0,1,3],[0,1,2],[[103,103],[-1,103],[104,105]]):
  p=sample(relations={'1':[ids[0],8,2],'2':[ids[1],9,0]});p['units']['1'][0]=kind;p['units']['1'][6]=data;p['nested']['1']['relationHead']=head;case(p,'relation',[['u',1],['u',target]])
 case(sample(),'relation',[['u',0],['u',3]])
 for count,selector,flags,shape in itertools.product([0,1,31,32,33,232,511,512,18446744073709551615,137438953472],[0,1,3,4],[0,-1],[0,1,2,3]):
  p=sample(wideCount=count);p['units']['1'][9]=selector;p['world']['1']['stats'][0]=flags;p['world']['1']['stats'][1]=[4294967295]*16
  p['mask410']=None if shape==0 else [0]*16
  if shape==2:p['mask410'][0]=2147483648
  if shape==3:p['mask410'][7]=1
  case(p,'mask',[['u',1],['m',0]])
 for x,y,tx,ty,size,targetSize,result in itertools.product([0,5],[0,5],[-5,0,5],[-5,0,5],[-1,0,1,2,3,127],[-1,0,1,2,3],[-1,0,1]):case(sample(traceResult=result),'ray',[['i',x],['i',y],['i',size],['i',tx],['i',ty],['i',targetSize],['r',1],['i',4]])
 for coords in [[2147483647,0,-1,0],[-2147483648,0,0,0],[2147483647,-2147483648,-1,2147483647]]:
  for size,targetSize in itertools.product([-2147483648,0,2,2147483647],repeat=2):case(sample(),'ray',[['i',coords[0]],['i',coords[1]],['i',size],['i',coords[2]],['i',coords[3]],['i',targetSize],['r',0],['i',-1]])
 for kind,path,room,hot,target in itertools.product([0,1,2,4,5],[0,1],[0,1],[0,1],[0,1,3]):
  p=sample();p['units']['1'][0:2]=[kind,path];p['world']['1']['room']=room
  if hot:p['world']['1']['cache']=[kind,p['units']['1'][8],7,8]
  case(p,'point',[['u',1],['i',9],['i',3],['i',0x805]]);case(p,'index',[['u',1],['u',target]])
 for sourceKind,targetKind,flag,own,relation,a,b in itertools.product([0,1,3],[0,1,3],[0,1],[0,1,2],[0,8],[0,2],[0,2]):
  p=sample(relations={'1':[103,relation,0]});p['units']['1'][0]=sourceKind;p['units']['3'][0]=targetKind;p['nested']['1']['relationHead']=1;p['nodes']['1'][2]=a;p['nodes']['3'][2]=b
  if own==1:p['world']['1']['owner']=[1,1,102];p['bucketHeads']['1:102']=2
  if own==2:p['world']['3']['owner']=[1,1,104];p['bucketHeads']['1:104']=4
  case(p,'valid',[['g',1],['u',1],['u',3],['i',flag]])
 for cls,flag,stats,mask in itertools.product([0,600,720,742,750],[0,-1],[False,True],[False,True]):
  p=sample();p['units']['3'][4]=12;p['units']['3'][8]=cls;p['world']['3']['stats'][0]=flag
  if not stats:p['world']['3']['stats']=None
  if mask:p['mask410'][3]=512
  case(p,'corpse',[['u',3]])
 for kind,hot,result,x in itertools.product([0,1,2,3],[False,True],[0,1],[0,3,20]):
  p=sample(traceResult=result);p['units']['3'][0]=kind;p['units']['3'][2]=x if kind==2 else x<<16
  if hot:p['world']['1']['cache']=[1,p['units']['1'][8],2,3]
  case(p,'collision',[['u',1],['u',3],['i',4]]);case(p,'special',[['u',1],['u',3]])
 for mask,owner,explicit,shape in itertools.product([0,1,2,4,8,16,32,63,-1],[0,2],[0,3,4],[0,1,2,3]):
  p=sample(mask=mask,owner=owner,explicit=explicit)
  if shape==1:p['nodes']['1'][2]=p['nodes']['2'][2]=0
  if shape==2:p['units']['3'][4]=12;p['units']['3'][8]=0
  if shape==3:p['nodes']['3'][2]=2;p['mask410'][3]=512
  pipeline.append([p,em.execute_pipeline(p)])
 encoded=json.dumps(dict(cases=cases,pipeline=pipeline),separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==encoded
 else:OUT.write_text(encoded)
 import unicorn
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 paths=[OUT,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_search_services.py',ROOT/'tools/probe_generic_pet_search_bound.py',ROOT/'tools/probe_generic_pet_search_wrapper.py',ROOT/'tools/probe_generic_pet_search_callback.py',ROOT/'tools/probe_generic_pet_search_groups.py',ROOT/'tools/probe_generic_pet_preparation.py',ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',*em.artifacts]
 proof=dict(referenceVersion='3.3.93847',component='Ten nested current search functions and complete source search composition',processMemoryRead=False,sourceCodeExecutedInOfflineEmulator=True,handlerEnabled=False,fullAiVerified=False,
 verified=dict(nestedContexts=len(cases),nestedUnsupported=sum(r[-1][0] for r in cases),pipelineContexts=len(pipeline),pipelineUnsupported=sum(r[-1][0] for r in pipeline),completeFunctions=10,capturedFunctionBytes=1976,instructionAndPaddingBytes=1928,inlineSwitchBytes=48),switchTables=em.annotations,
 emulatorLibrary=dict(path=str(libs[0].relative_to(ROOT)),sha256=digest(libs[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(paths))},
 services=['Actual nested allegiance/alignment,player relation,secondary metadata,state mask,signed dimensions,ray,point and special-index code execute with the retained full source search stack. Both SizeX and SizeY include six-entry24-byte switch tables;the earlier SizeX-only instruction/data breakdown is superseded,with original captures unchanged.',
 'Stat172metadata and node stat-value decoding,missile size metadata,the raw mask410producer and terminal room trace2a6c40 remain declared services. No source state-mask meaning,world selector,difficulty or Britannia geometry is invented. Null/missing-data assertions are unsupported boundaries;cyclic invalid lists and unbounded mask buffers are outside acceptance.'],remaining=['Native comparison and retained integration required. Actual world membership,remaining stat/mask/collision producers,ordering and actor lifecycle still gate learned demons.'])
 p=ROOT/'tools/reference/generic-pet-search-nested-source-evidence.json';encoded=json.dumps(proof,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Nested search:',json.dumps(proof['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
