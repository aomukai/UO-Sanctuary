#!/usr/bin/env python3
"""Execute all captured placement fragments for the actual monster/last-arg0 path."""
import argparse,itertools,json
from pathlib import Path
from unicorn.x86_const import *
import probe_generic_pet_warp_primitives as core
from probe_generic_pet_warp_primitives import Primitives,defaults,BASE,STACK,STOP,MEM,GAME,UNIT,ROOM,PATH,RDATA,FRESH
from probe_warlock_runtime import ROOT,digest
OUT=ROOT/'.local/class-skills/generic-pet-monster-placement-source-probe.json'
core.ENTRY['placement']=0x3739a0
FUNCS={n:BASE+r for n,r in dict(roomat=0x2972c0,before=0x264460,word1=0x277db0,word2=0x277e60,nearby=0x2a6630,after=0x2641b0,update=0x286fa0,sync=0x36f050).items()}
signed=lambda v:(v&0x7fffffff)-(v&0x80000000)
class Placement(Primitives):
 def __init__(self):
  super().__init__();pp=FRESH/'callees-follow-warp-placement-20261103.json';proof=json.loads(pp.read_text());self.artifacts.append(pp)
  for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  row=next(r for r in proof['records'] if r['rva']=='0x3739a0');assert row['allUnwindFragmentsCaptured'] and len(row['fragments'])==6
  for f in row['fragments']:
   assert f['captured'] and f['invalidInstructionMarkers']==0
   paths=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(paths)==1
   path=paths[0];body=path.read_bytes();start=BASE+int(f['rva'],16);self.map(start,len(body));self.uc.mem_write(start,body);self.ranges.append((start,start+len(body)));self.artifacts.append(path)
  for a in FUNCS.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
 def token(self,p):assert p in [0,ROOM,RDATA];return {0:0,ROOM:1,RDATA:2}[p]
 def hook(self,uc,a,size,user):
  if not self.ready:
   self.ready=True;p=self.p;self.wi(UNIT+4,p['classId']);self.wi(UNIT+0x128,signed(p['flags128']))
   for off,key in [(0x178,'cacheKind'),(0x17c,'cacheClass'),(0x180,'cache180'),(0x184,'cache184')]:self.wi(UNIT+off,p[key])
   self.wi(PATH+0x34,p['path34']);rsp=uc.reg_read(UC_X86_REG_RSP)
   uc.reg_write(UC_X86_REG_RCX,GAME);uc.reg_write(UC_X86_REG_RDX,UNIT if p['present'] else 0);uc.reg_write(UC_X86_REG_R8,[0,ROOM,RDATA][p['targetRoom']]);uc.reg_write(UC_X86_REG_R9,p['target'][0]&0xffffffff)
   for n,value in enumerate([p['target'][1],p['skip'],0],5):self.wq(rsp+n*8,value&0xffffffffffffffff)
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((n for n,v in FUNCS.items() if v==a),None)
  if name is None:return super().hook(uc,a,size,user)
  reg=uc.reg_read;c,d,v,z=[reg(r) for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];rsp=reg(UC_X86_REG_RSP);arg=lambda n:self.q(rsp+n*8);p=self.p;result=0
  if name=='roomat':
   token=p['rooms'][min(self.roomCalls,len(p['rooms'])-1)];self.roomCalls+=1;self.trace.append([11,self.token(c),signed(d),signed(v),token]);result=[0,ROOM,RDATA][token]
  elif name=='before':assert c==UNIT;self.trace.append([12,signed(d)])
  elif name=='word1':assert c==UNIT;self.trace.append([13,p['get180']]);result=p['get180']
  elif name=='word2':assert c==UNIT;self.trace.append([14,p['get184']]);result=p['get184']
  elif name=='nearby':
   assert v==0;values=[self.token(c),self.i(d),self.i(d+4),0,signed(z),signed(arg(5)),signed(arg(6)),signed(arg(7)),signed(arg(8)),signed(arg(9))]
   self.trace.append([15,*values,p['nearRoom'],*p['nearPoint']]);self.wi(d,p['nearPoint'][0]);self.wi(d+4,p['nearPoint'][1]);result=[0,ROOM,RDATA][p['nearRoom']]
  elif name=='after':assert c==UNIT;self.trace.append([16])
  elif name=='update':assert c==PATH;self.trace.append([17,self.token(d),v&0xffffffff,p['update']]);result=p['update']
  elif name=='sync':assert c==GAME and d==UNIT;self.trace.append([18,signed(v),self.i(UNIT+0x128)&0xffffffff])
  self.ret(result)
 def run(self,p):
  self.p=p;self.ready=False;out=super().run('placement',p)
  fields=[self.i(UNIT+0x128)&0xffffffff,*[self.i(UNIT+off) for off in [0x178,0x17c,0x180,0x184]],self.i(PATH+0x34)]
  return [*out,fields]
def params(**values):
 p=defaults();p.update(dict(kind=1,present=True,classId=742,flags128=0x1234,cacheKind=0,cacheClass=0,cache180=21,cache184=22,path34=77,get180=2,get184=3,targetRoom=2,target=[65,75],skip=0,rooms=[2],nearRoom=2,nearPoint=[67,77],update=1));p.update(values);return p
def cases():
 for present,path,targetRoom,room,skip,cache,nearRoom,update,rooms in itertools.product([False,True],[False,True],[0,2],[0,1],[-1,0,1],[[1,742],[-1,-1],[1,999]],[0,1,2],[-7,0,1,256],[[0],[2],[0,2]]):
  yield params(present=present,path=path,targetRoom=targetRoom,roomPresent=room,skip=skip,cacheKind=cache[0],cacheClass=cache[1],nearRoom=nearRoom,update=update,rooms=rooms)
 for target,near,flags in itertools.product([[-1,-5],[65536,65535],[2147483647,-2147483648]],[[-1,-5],[65536,65535],[2147483647,-2147483648]],[0,65536,4294967295]):yield params(target=target,nearPoint=near,flags128=flags)
 for marker,roomFlags,flags in itertools.product([False,True],[0,4,255],[0,8192,4294967295]):yield params(marker=marker,roomFlags=roomFlags,flags=flags)
def main(check=False):
 em=Placement();rows=[[p,em.run(p)] for p in cases()];payload=json.dumps(rows,separators=(',',':'))+'\n'
 proof=dict(referenceVersion='3.3.93847',component='Current monster placement policy with actual room-update composition',cases=len(rows),offlineMachineCodeExecuted=True,processMemoryReadDuringProbe=False,targetProcessCodeExecuted=False,worldMovementVerified=False,handlerEnabled=False,
  limits=['All6placement unwind fragments execute for the actual monster/last-arg0path,including the complete room-update body. Player packet/history and nonzero-last-argument branches are outside this interface.',
   'Nearby adjustment,path commit,geometry fields,before/after hooks and synchronization remain explicit services. This does not prove actual native world relocation or its complete path/collision effects.'],
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_warp_primitives.py',ROOT/'tools/probe_generic_pet_decisions.py',*em.artifacts,em.image.reference]},fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
 out=ROOT/'tools/reference/generic-pet-monster-placement-source-evidence.json'
 if check:assert OUT.read_text()==payload and json.loads(out.read_text())==proof
 else:OUT.write_text(payload);out.write_text(json.dumps(proof,indent=2)+'\n')
 print('Current monster placement:',len(rows),'captured-code cases;all6fragments/room-update composition;no world/learned claim.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
