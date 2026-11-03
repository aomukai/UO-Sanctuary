#!/usr/bin/env python3
"""Complete captured nearby no-field path and shared footprint dispatch offline."""
import argparse,itertools,json
from pathlib import Path
import unicorn
from unicorn.x86_const import *
from probe_generic_pet_footprints import Footprints,param,LOWER
from probe_generic_pet_path_commit import BASE,STACK,MEM,STOP,ROOM,OTHER,ROOT,FRESH,signed
from probe_warlock_runtime import digest
POINT=MEM+0x7000
OUT=ROOT/'.local/class-skills/generic-pet-nearby-source-probe.json'
def collision(p,index,x,y):
 style=p['style'];dx=x-p['start'][0];dy=y-p['start'][1]
 if style==0:return p['responses'][min(index,len(p['responses'])-1)]
 if style==1:return int(abs(dx)+abs(dy)<=2)
 if style==2:return int(dx>=0)
 if style==3:return int(not(dx==-1 and dy==0))
 if style==4:return int(dy!=3)
 raise AssertionError(style)
class Nearby(Footprints):
 def __init__(self):
  super().__init__();path=FRESH/'callees-path-commit-nearby-20261103.json';proof=json.loads(path.read_text());self.artifacts.append(path)
  for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  r=next(r for r in proof['records'] if r['rva']=='0x2a6630');assert r['allUnwindFragmentsCaptured'] and len(r['fragments'])==6
  for f in r['fragments']:
   assert f['captured'] and f['invalidInstructionMarkers']==0
   found=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(found)==1
   p=found[0];body=p.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(body));self.uc.mem_write(a,body);self.ranges.append((a,a+len(body)));self.artifacts.append(p)
 def hook(self,uc,a,size,user):
  name=next((n for n,v in LOWER.items() if v==a),None)
  if name in ('point','adjacent','box'):
   reg=uc.reg_read;c,d,v,z=[reg(r) for r in (UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9)]
   code=20+list(LOWER).index(name)
   if name=='box':
    box=[self.i(d+i*4) for i in range(4)];x=signed(box[0]+1);y=signed(box[1]+1);self.trace.append([code,self.token(c),*box,signed(v)])
   else:x=signed(d);y=signed(v);self.trace.append([code,self.token(c),x,y,signed(z)])
   value=collision(self.p,self.checks,x,y);self.checks+=1;self.ret(value);return
  return super().hook(uc,a,size,user)
 def execute(self,p):
  self.p=p;self.mode='nearby';self.starting=False;self.trace=[];self.roomCalls=0;self.checks=0;self.fatal=False
  self.uc.mem_write(MEM,b'\0'*0x10000);self.uc.mem_write(STACK,b'\0'*0x40000);self.wi(POINT,p['start'][0]);self.wi(POINT+4,p['start'][1])
  rsp=STACK+0x20008;self.wq(rsp,STOP)
  for r,v in [(UC_X86_REG_RSP,rsp),(UC_X86_REG_RCX,[0,ROOM,OTHER][p['initialRoom']]),(UC_X86_REG_RDX,POINT if p['present'] else 0),(UC_X86_REG_R8,0),(UC_X86_REG_R9,p['pattern']&0xffffffff)]:self.uc.reg_write(r,v)
  for n,value in enumerate([p['mask'],p['mask2'],p['fallback'],p['distance'],p['increment']],5):self.wq(rsp+n*8,value&0xffffffffffffffff)
  try:self.uc.emu_start(BASE+0x2a6630,STOP,count=5000000)
  except unicorn.UcError:
   if not self.fatal:raise
  if not self.fatal:assert self.uc.reg_read(UC_X86_REG_RIP)==STOP,('Nearby instruction budget',p)
  return [-1 if self.fatal else self.token(self.uc.reg_read(UC_X86_REG_RAX)),[self.i(POINT),self.i(POINT+4)],self.trace]
def params(**values):
 p=param();p.update(start=[20,30],initialRoom=1,present=True,pattern=2,mask=0x1c09,mask2=0x1c09,fallback=0,distance=6,increment=1,rooms=[1],responses=[1,0],style=0);p.update(values);return p
def cases():
 for pattern,distance,increment,rooms,responses in itertools.product([-1,0,1,2,3,4],[-1,0,1,2,3,6],[1,2,3],[[0],[1],[0,2],[2,0,1]],[[0],[1],[1,0],[1,1,0],[65536],[-1]]):
  yield params(pattern=pattern,distance=distance,increment=increment,rooms=rooms,responses=responses)
 for point,pattern,style,increment,fallback in itertools.product([[0,0],[-5,7],[65535,65536],[100000,-100000]],[0,2,3],range(5),[1,2],[0,1,-1]):
  yield params(start=point,pattern=pattern,style=style,increment=increment,fallback=fallback)
 for point,pattern,responses,rooms in itertools.product([[2147483647,-2147483648],[-2147483648,2147483647]],[0,2,3,4],[[0],[1]],[[0],[1]]):
  yield params(start=point,pattern=pattern,responses=responses,rooms=rooms,distance=1,fallback=1)
 for pattern,rooms,responses in itertools.product([0,1,2,3,4],[[1],[0,1]],[[1],[1]*30+[0]]):yield params(pattern=pattern,rooms=rooms,responses=responses,distance=50)
 for room,pattern in itertools.product([0,1,2],[0,2]):yield params(initialRoom=room,pattern=pattern,present=False)
def main(check=False):
 em=Nearby();rows=[[p,em.execute(p)] for p in cases()];payload=json.dumps(rows,separators=(',',':'))+'\n'
 proof=dict(referenceVersion='3.3.93847',component='Current nearby-placement no-field path and shared footprint checks',cases=len(rows),unsupportedCases=sum(row[1][0]==-1 for row in rows),offlineMachineCodeExecuted=True,processMemoryReadDuringProbe=False,targetProcessCodeExecuted=False,worldMovementVerified=False,handlerEnabled=False,
  limits=['All six current nearby fragments execute for the actual null-field pet caller. Optional allocated-field/line validation is not exposed by the consumer. Shared complete pattern checker and declared point/adjacent/box/room services retain query order.',
   'Current use is distance50/step1;bounded positive increments/ranges through50 are compared. Unverified nonpositive increments,larger searches and extreme overflowing search coordinates are explicitly refused only when expansion is needed. No actual native room/terrain/controller integration is claimed.'],
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_footprints.py',ROOT/'tools/probe_generic_pet_path_commit.py',ROOT/'tools/probe_generic_pet_decisions.py',*em.artifacts,em.image.reference]},fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
 target=ROOT/'tools/reference/generic-pet-nearby-source-evidence.json'
 if check:assert OUT.read_text()==payload and json.loads(target.read_text())==proof
 else:OUT.write_text(payload);target.write_text(json.dumps(proof,indent=2)+'\n')
 print('Current nearby:',len(rows),'complete no-field captured-code cases;unsupported',proof['unsupportedCases'])
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
