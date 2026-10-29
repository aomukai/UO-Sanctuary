#!/usr/bin/env python3
"""Execute nine actual search helpers, then compose them through full search."""
import argparse,itertools,json,struct
from pathlib import Path
from unicorn import UcError
from unicorn.x86_const import *
from probe_generic_pet_search_bound import Bound,sample
from probe_generic_pet_search_callback import FNS,BASE,MEM,REGS,pointer,identity,TABLE,GLOBAL,signed
from probe_generic_pet_search_wrapper import WRAPPER
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/generic-pet-search-services-source-probe.json';DATA=ROOT/'.local/class-skills/generic-pet-search-services'
META=MEM+0x80000;MONS=META;LEVELS=META+0x60000;STAT=META+0x80000;SECOND=META+0xa0000;REL=SECOND+0x100;MASK=SECOND+0x200
SELECTED={k:v for k,v in FNS.items() if k!='distance'}
class Services(Bound):
 def __init__(self):
  super().__init__();self.servicesActive=False;self.map(META,0xc0000)
  p=STAGE/'callees-live-search-services-idle-20261030.json';c=json.loads(p.read_text());self.artifacts.append(p)
  for n,h in c['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  for addr in SELECTED.values():
   r=next(r for r in c['records'] if int(r['rva'],16)==addr-BASE);assert r['allUnwindFragmentsCaptured']
   for f in r['fragments']:
    assert f['captured'] and not f['invalidInstructionMarkers'];ps=[ROOT/n for n,h in c['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(ps)==1
    raw=ps[0].read_bytes();a=BASE+int(f['rva'],16);assert len(raw)==int(f['endRva'],16)-int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(ps[0])
  self.monsters=(DATA/'monstats.bin').read_bytes()[4:];self.levels=(DATA/'levels.bin').read_bytes()[4:];self.artifacts += [DATA/'monstats.bin',DATA/'levels.bin']
  for rva in [0x6bd00,0x296f60,0x377dc0,0x24fd30,0x277db0,0x277e60,0x27c500,0x40c060,0x27c5e0]:self.map(BASE+rva,1);self.uc.mem_write(BASE+rva,b'\xc3')
  bits=self.image.bytes(0x1648ec0,128);assert bits==struct.pack('<32I',*[1<<i for i in range(32)]);self.map(BASE+0x1648ec0,128);self.uc.mem_write(BASE+0x1648ec0,bits)
 def count_hook(self,uc,access,address,size,value,user):
  if getattr(self,'servicesActive',False) and self.mode=='callback':return
  super().count_hook(uc,access,address,size,value,user)
 def setup_graph(self):
  super().setup_graph();self.setup_services()
 def setup_services(self):
  p=self.world;self.pointCalls=0;self.uc.mem_write(META,b'\0'*0xc0000);self.uc.mem_write(MONS,self.monsters);self.uc.mem_write(LEVELS,self.levels)
  for selector in [1,2,3]:self.wq(GLOBAL+16*selector,TABLE)
  self.wq(TABLE+0xf90,MONS);self.wq(TABLE+0xf98,751);self.wq(TABLE+0x1480,LEVELS);self.wi(TABLE+0x1488,138);self.wi(TABLE+0x2d0,p['stateCount']);self.wq(TABLE+0x410,MASK)
  for key,r in p['world'].items():
   n=int(key);u=pointer(n);path=u+0x200;room=self.graph_room(r['room']);self.wq(path+(0 if p['units'][key][0] in [2,4,5] else 0x20),room)
   self.wi(u+0x128,r['flags128']);self.uc.mem_write(u+0x29a,bytes([r['monsterFlags26']]))
   self.wq(u+0x2b8,u+0x300 if p['units'][key][6] else 0)
   if r['owner'] is not None:
    game,typ,ident=r['owner'];self.wq(u+0x360,MEM if game else 0);self.wi(u+0x368,ident);self.wi(u+0x36c,typ)
   self.wi(u+0xe8,r['missile'][0]);self.wi(u+0xec,r['missile'][1])
   self.wq(u+0x88,STAT+n*0xc00 if r['stats'] is not None else 0)
   if r['stats'] is not None:
    flags,words=r['stats'];self.wi(STAT+n*0xc00+0x1c,flags)
    for i,w in enumerate(words):self.wi(STAT+n*0xc00+0xb30+4*i,signed(w))
   for off,v in zip([0x178,0x17c,0x180,0x184],r['cache']):self.wi(u+off,v)
 def invalid(self,uc,access,address,size,value,user):
  if self.servicesActive and address<0x100 and uc.reg_read(UC_X86_REG_RIP) in [BASE+n for n in [0x278488,0x40c504,0x40c55e,0x27c7a1,0x27c83f]]:
   self.unsupported=True;self.trace.append([99]);return False
  return super().invalid(uc,access,address,size,value,user)
 def hook(self,uc,a,size,user):
  if not self.servicesActive:super().hook(uc,a,size,user);return
  if a in SELECTED.values():
   if a==FNS['monster']:self.trace.append([40,uc.reg_read(REGS[0])&255,signed(uc.reg_read(REGS[1]))])
   return
  if a==BASE+0x155eeb0:self.ret(uc.reg_read(UC_X86_REG_RAX));return
  p=self.world;c,d,v,z=[uc.reg_read(r) for r in REGS]
  if a==BASE+0x6bd00:
   row=p['monster2'];self.trace.append([45,c&255,signed(d),row]);result=0
   if row is not None:result=SECOND;self.uc.mem_write(SECOND+4,bytes([row[0],0,row[1]]))
   self.ret(result);return
  if a==BASE+0x296f60:
   unit=identity(c);assert d==MASK;selector=p['units'][str(unit)][9];result=p['corpseMask'];self.trace.append([46,selector,unit,result]);self.ret(result);return
  if a==BASE+0x377dc0:
   value=p['relation'];self.trace.append([47,identity(c),identity(d),value]);self.wi(REL+4,value or 0);self.ret(REL if value is not None else 0);return
  if a==BASE+0x24fd30:
   result=p['allegiance'];self.trace.append([48,identity(c),identity(d),result]);self.ret(result);return
  if a in [BASE+0x277db0,BASE+0x277e60]:
   i=int(a==BASE+0x277e60);unit=identity(c);result=p['world'][str(unit)]['sizes'][i];self.trace.append([51+i,unit,result]);self.ret(result);return
  if a==BASE+0x27c500:
   rsp=uc.reg_read(UC_X86_REG_RSP);args=[signed(c),signed(d),signed(v),signed(z),signed(self.q(rsp+0x28)),signed(self.q(rsp+0x30)),(self.q(rsp+0x38)-(MEM+0x40000))//0x600,signed(self.q(rsp+0x40))];result=p['ray'];self.trace.append([53,*args,result]);self.ret(result);return
  if a==BASE+0x40c060:
   result=p['specialIndex'];self.trace.append([49,identity(c),identity(d),result]);self.ret(result);return
  if a==BASE+0x27c5e0:
   points=p['points'];result=points[min(self.pointCalls,len(points)-1)];self.pointCalls+=1;self.trace.append([50,identity(c),signed(d),signed(v),signed(z),result]);self.ret(result);return
  super().hook(uc,a,size,user)
 def caches(self):return [[signed(self.i(pointer(int(k))+off)) for off in [0x178,0x17c,0x180,0x184]] for k in self.world['units']]
 def execute_service(self,p,name,args):
  self.servicesActive=False;self.boundActive=False;self.wrapperActive=False
  shell=dict(p,visits=[],prepare=None,groups=None);super(Bound,self).execute(shell);self.world=p;self.p=p;self.trace=[];self.pending=None;self.setup_graph();self.servicesActive=True;self.boundActive=True;self.mode='callback'
  raw=[pointer(x) if typ=='u' else MEM if typ=='g' else x for typ,x in args]
  self.run_entry(FNS[name],raw+[0]*(4-len(raw)));result=0
  if not self.unsupported:
   result=self.qreg= self.uc.reg_read(UC_X86_REG_RAX)
   if name=='room':result=(result-(MEM+0x40000))//0x600 if result else 0
   elif name=='monster':result=[(result-MONS)//508,*bytes(self.uc.mem_read(result+60,1)),*bytes(self.uc.mem_read(result+137,1))] if result else -1
   elif name=='level':result=[(result-LEVELS)//404,*bytes(self.uc.mem_read(result+63,1))] if result else -1
   else:result=signed(result)
  return [int(self.unsupported),result,self.caches(),self.trace]
 def execute_pipeline(self,p):
  self.servicesActive=True
  result=super().execute_bound(p);result.insert(-1,self.caches());return result

def fixture(**values):
 p=sample();p.update(stateCount=232,monster2=[128,0],corpseMask=0,relation=8,allegiance=0,ray=0,specialIndex=5,points=[0])
 p['world']={str(i):dict(room=1 if i in [1,2] else 2,flags128=0,monsterFlags26=0,owner=None,missile=[1,102],stats=[-2147483648,[0]*16],cache=[-1,-1,0,0],sizes=[1,2]) for i in range(1,10)}
 p.update(values);return p

def build(check=False):
 em=Services();cases=[];pipeline=[]
 def case(p,name,args):cases.append([p,name,args,em.execute_service(p,name,args)])
 for kind,path,room,subject in itertools.product([-1,0,1,2,3,4,5,6],[0,1],[0,1],[0,1]):
  p=fixture();p['units']['1'][0:2]=[kind,path];p['world']['1']['room']=room;case(p,'room',[['u',subject]])
 for selector in [0,1,2,3,4,255]:
  for name,ids in [('monster',[-1,0,1,750,751,2147483647]),('level',[-1,0,1,137,138,2147483647])]:
   for ident in ids:case(fixture(),name,[['i',selector],['i',ident]])
 for selector in [1,2,3]:
  for i in range(751):case(fixture(),'monster',[['i',selector],['i',i]])
  for i in range(138):case(fixture(),'level',[['i',selector],['i',i]])
 for kind,selector,state,variant,count in itertools.product([-1,0,1,2,3,4,5],[0,1,3,4],[-1,0,1,31,32,231,232,510,511],[0,1,2],[232,512]):
  p=fixture(stateCount=count);p['units']['1'][0]=kind;p['units']['1'][9]=selector;p['world']['1']['stats']=None if variant==0 else [0 if variant==1 else -1,[4294967295]*16];case(p,'has',[['u',1],['i',state]])
 for selector in [1,2,3]:
  for ident in range(751):
   p=fixture();p['units']['1'][8:10]=[ident,selector];case(p,'value',[['u',1]])
 for kind,cls,flags,data in itertools.product([0,1,2,3],[-1,0,229,751],[0,2,4,8,14],[0,1]):
  p=fixture();p['units']['1'][0]=kind;p['units']['1'][6]=data;p['units']['1'][8]=cls;p['world']['1']['monsterFlags26']=flags;case(p,'value',[['u',1]])
 for kind,mode,selector,row,mask in itertools.product([0,1,2],[0,12],[0,1,3,4],[None,[0,0],[128,0],[128,8]],[-1,0,1]):
  p=fixture(monster2=row,corpseMask=mask);p['units']['1'][0]=kind;p['units']['1'][4]=mode;p['units']['1'][9]=selector;case(p,'corpse',[['u',1]])
 for source,target,flag,shape,relation,allegiance in itertools.product([0,1],[0,1,3],[0,1],[0,1,2,3,4,5],[None,0,8,9],[-1,0,1]):
  p=fixture(relation=relation,allegiance=allegiance)
  if shape==1:p['units']['1'][0]=p['units']['3'][0]=0
  if shape in [2,4]:p['world']['1']['owner']=[1,1,102];p['bucketHeads']['1:102']=2
  if shape in [3,4]:
   p['world']['3']['owner']=[1,1,104];p['world']['4']['owner']=[1,1,105];p['bucketHeads'].update({'1:104':4,'1:105':5})
  if shape==5:p['units']['1'][0]=3;p['world']['1']['flags128']=1024;p['bucketHeads']['1:102']=2
  case(p,'valid',[['g',1],['u',source],['u',target],['i',flag]])
 for kind,path,room,hot,result,target in itertools.product([0,1,2,4,5],[0,1],[0,1],[0,1],[-1,0,3],[0,1,3]):
  p=fixture(ray=result);p['units']['1'][0:2]=[kind,path];p['world']['1']['room']=room
  if hot:p['world']['1']['cache']=[kind,p['units']['1'][8],7,8]
  case(p,'collision',[['u',1],['u',target],['i',4]])
 for index,points,dx,dy,kind,path in itertools.product([0,2,3,10,11,24,25,2147483647],[[0],[1,0],[1,1,0],[1,1,1]],[-5,0,5],[-5,0,5],[1,2],[0,1]):
  p=fixture(specialIndex=index,points=points);p['units']['1'][0:2]=[kind,path];p['units']['1'][2:4]=[dx if kind==2 else dx<<16,dy if kind==2 else dy<<16];case(p,'special',[['u',1],['u',3]])
 for mask,owner,explicit,shape in itertools.product([0,1,2,4,8,16,32,63,-1],[0,2],[0,3,4],[0,1,2,3]):
  p=fixture(mask=mask,owner=owner,explicit=explicit)
  if shape==1:p['allegiance']=1
  if shape==2:p['units']['3'][4]=12
  if shape==3:p['world']['3']['stats']=[-1,[4294967295]*16];p['world']['1']['owner']=[1,1,102];p['bucketHeads']['1:102']=2
  pipeline.append([p,em.execute_pipeline(p)])
 encoded=json.dumps(dict(cases=cases,pipeline=pipeline),separators=(',',':'))+'\n'
 if check:assert OUT.read_text()==encoded
 else:OUT.write_text(encoded)
 import unicorn
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 paths=[OUT,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_search_bound.py',ROOT/'tools/probe_generic_pet_search_wrapper.py',ROOT/'tools/probe_generic_pet_search_callback.py',ROOT/'tools/probe_generic_pet_search_groups.py',ROOT/'tools/probe_generic_pet_preparation.py',ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_warlock_runtime.py',*em.artifacts]
 proof=dict(referenceVersion='3.3.93847',component='Nine current search helper bodies and full search composition',processMemoryRead=False,sourceCodeExecutedInOfflineEmulator=True,handlerEnabled=False,fullAiVerified=False,
 verified=dict(helperContexts=len(cases),helperUnsupported=sum(r[-1][0] for r in cases),pipelineContexts=len(pipeline),pipelineUnsupported=sum(r[-1][0] for r in pipeline),completeFunctions=9,completeBytes=2172),
 emulatorLibrary=dict(path=str(libs[0].relative_to(ROOT)),sha256=digest(libs[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(paths))},
 services=['Actual validity,room,collision,level,special,corpse,priority,monster and state helpers execute with the retained real full wrapper/callback/prep/grid/refresh/registry/traversal. Owned compiled monster/level rows and immutable bit masks supply exact getter memory.',
 'Nested player relation,allegiance,secondary monster metadata,corpse state mask,size getters,ray and point collision and special index are declared services. No guessed Britannia geometry or ownership membership is supplied. Source assertions/static null-path dereferences stop as unsupported. Negative special indices and cyclic invalid owner/registry graphs are outside acceptance.'],remaining=['Native helper and composed pipeline comparison required. Actual world providers and nested predicate bodies,ordering and full actor lifecycle remain unfinished;no learned demon enabled.'])
 target=ROOT/'tools/reference/generic-pet-search-services-source-evidence.json';encoded=json.dumps(proof,indent=2)+'\n'
 if check:assert target.read_text()==encoded
 else:target.write_text(encoded)
 print('Search services:',json.dumps(proof['verified']))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
