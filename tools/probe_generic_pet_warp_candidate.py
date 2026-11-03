#!/usr/bin/env python3
"""Execute current validation-only candidate body,alone and inside real warp search."""
import argparse,itertools,json,struct
from pathlib import Path
from unicorn.x86_const import *
import probe_generic_pet_warp_primitives as core
from probe_generic_pet_warp_primitives import Primitives,defaults,BASE,MEM,GAME,ROOM,AREA,OUTPUT,FRESH
from probe_warlock_runtime import ROOT,digest
OUT=ROOT/'.local/class-skills/generic-pet-warp-candidate-source-probe.json'
PRIMARY=MEM+0xb000;SECONDARY=MEM+0xc000;REQUEST=MEM+0xd000
FUNCS={n:BASE+r for n,r in dict(secondary=0x6bc90,primary=0x6bd00,special=0x429e20,allowed=0x48c120,point=0x2a4920,two=0x2a5060,box=0x2a4b30,cookie=0x155eeb0).items()}
core.ENTRY['candidate']=0x428b20
signed=lambda v:struct.unpack('<i',struct.pack('<I',v&0xffffffff))[0]
class Candidate(Primitives):
 def __init__(self):
  super().__init__();pp=FRESH/'callees-follow-warp-candidate-20261103.json';proof=json.loads(pp.read_text());self.artifacts.append(pp)
  for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  row=proof['records'][0];assert row['allUnwindFragmentsCaptured'] and row['rva']=='0x428b20'
  for f in row['fragments']:
   assert f['captured'] and f['invalidInstructionMarkers']==0
   paths=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(paths)==1
   path=paths[0];body=path.read_bytes();start=BASE+int(f['rva'],16);self.map(start,len(body));self.uc.mem_write(start,body);self.ranges.append((start,start+len(body)));self.artifacts.append(path)
  self.map(BASE+0x197a178,8);self.uc.mem_write(BASE+0x197a178,b'\0'*8)
  for a in FUNCS.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
 def hook(self,uc,a,size,user):
  if not self.ready:
   self.ready=True;p=self.p;self.uc.mem_write(GAME+0x106,bytes([p['selector']]));self.uc.mem_write(PRIMARY+8,bytes([p['size']&255]));self.uc.mem_write(PRIMARY+10,bytes([p['walk']]))
   if self.family=='candidate':
    self.wq(REQUEST,GAME);self.wq(REQUEST+8,ROOM if p['candidateRoom'] else 0);self.wq(REQUEST+16,AREA if p['area'] else 0)
    for off,value in [(24,p['classId']),(28,1),(32,0),(36,p['candidate'][0]),(40,p['candidate'][1]),(44,-1)]:self.wi(REQUEST+off,value)
    self.uc.mem_write(REQUEST+48,b'\1\0');self.wq(REQUEST+56,0);uc.reg_write(UC_X86_REG_RCX,REQUEST)
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((n for n,v in FUNCS.items() if v==a),None)
  if name is None:return super().hook(uc,a,size,user)
  reg=uc.reg_read;c,d,v,z=[reg(r) for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];rsp=reg(UC_X86_REG_RSP);arg=lambda n:self.q(rsp+n*8);p=self.p;result=0
  # The captured epilogue sets RAX before this call and returns it unchanged.
  # The normal-return cookie service must preserve that value,not replace it.
  if name=='cookie':self.ret(uc.reg_read(UC_X86_REG_RAX));return
  if name=='secondary':assert c==p['selector'] and signed(d)==p['classId'];self.trace.append([4,int(c),signed(d),p['secondary']]);result=SECONDARY if p['secondary'] else 0
  elif name=='primary':assert c==p['selector'] and signed(d)==p['classId'];self.trace.append([5,int(c),signed(d),p['primary']]);result=PRIMARY if p['primary'] else 0
  elif name=='special':assert c==0 and d==ROOM;self.trace.append([6,0,1,*p['special']]);self.wi(v,p['special'][0]);self.wi(z,p['special'][1])
  elif name=='allowed':assert c==p['selector'] and signed(d)==p['classId'] and v==ROOM and arg(6)==1;self.trace.append([7,int(c),signed(d),1,signed(z),signed(arg(5)),1,p['allowed']]);result=p['allowed']
  elif name in ['point','two']:
   assert c==ROOM;self.trace.append([8 if name=='point' else 9,1,signed(d),signed(v),signed(z),p['collision']]);result=p['collision']
  elif name=='box':assert c==ROOM;rect=[self.i(d+i*4) for i in range(4)];self.trace.append([10,1,*rect,signed(v),p['collision']]);result=p['collision']
  self.ret(result)
 def run(self,family,p):self.family=family;self.ready=False;return super().run(family,p)
def params(**values):
 p=defaults();p.update(dict(selector=3,walk=0,size=2,secondary=1,primary=1,special=[45,55],allowed=1,collision=0,candidate=[45,55],candidateRoom=True));p.update(values);return p
def cases():
 for walk,size,classId,secondary,primary,allowed,collision in itertools.product([0,1,2,3,5,255],[-1,0,1,2,3,4],[742,153,258,263,264,-1],[0,1],[0,1],[-7,0,1],[0,1,65536]):
  yield 'candidate',params(walk=walk,size=size,classId=classId,secondary=secondary,primary=primary,allowed=allowed,collision=collision)
 for area,point,rooms,ids,seed in itertools.product([False,True],[[0,0],[40,50],[64,74],[65,75],[-1,-5]],[[0],[1]],[[None],[0],[-1],[7]],[0,1,2]):
  yield 'candidate',params(area=area,candidate=point,rooms=rooms,regionStates=ids,seed=seed)
 for point,room in itertools.product([[0,0],[0,55],[45,0],[45,55],[-1,-5]],[False,True]):
  yield 'candidate',params(walk=1,special=point,candidateRoom=room)
 for area,walk,size,seed,collision,allowed in itertools.product([False,True],[0,1,2,3],range(4),[0,1,2,0xffffffffffffffff],[0,1],[0,1]):
  yield 'search',params(area=area,walk=walk,size=size,seed=seed,collision=collision,allowed=allowed)
 for point in [[0,0],[0,1],[1,0],[-1,-5]]:yield 'candidate',params(candidate=point,bounds=[-100,-100,200,200])
 for collision in [-7,65537]:yield 'candidate',params(collision=collision)
 for bounds,point in itertools.product([[-3,-2,0,1],[2147483647,-2147483648,-2147483648,2147483647],[0,0,0,0]],[[-1,-5],[1,2],[2147483647,-2147483648]]):
  yield 'candidate',params(area=True,areaBounds=bounds,candidate=point)
def main(check=False):
 em=Candidate();rows=[[f,p,em.run(f,p)] for f,p in cases()];payload=json.dumps(rows,separators=(',',':'))+'\n'
 proof=dict(referenceVersion='3.3.93847',component='Current warp validation-only candidate with original search composition',cases=len(rows),byFamily={k:sum(r[0]==k for r in rows) for k in ['candidate','search']},offlineMachineCodeExecuted=True,processMemoryReadDuringProbe=False,targetProcessCodeExecuted=False,worldMovementVerified=False,handlerEnabled=False,
  limits=['The actual immutable request word44=-1/word48=1selects radius0 and validation-only behavior. Other generic spawning flags/radii are outside this interface.',
   'Metadata,raw geometry/collision and special-point helper remain explicit services. Complete candidate and search bytes execute together;actual native placement/controller/world mapping remain unfinished.'],
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_warp_primitives.py',ROOT/'tools/probe_generic_pet_decisions.py',*em.artifacts,em.image.reference]},fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
 out=ROOT/'tools/reference/generic-pet-warp-candidate-source-evidence.json'
 if check:assert OUT.read_text()==payload and json.loads(out.read_text())==proof
 else:OUT.write_text(payload);out.write_text(json.dumps(proof,indent=2)+'\n')
 print('Current warp candidate:',len(rows),'captured-code cases;',proof['byFamily'],';no world/learned claim.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
