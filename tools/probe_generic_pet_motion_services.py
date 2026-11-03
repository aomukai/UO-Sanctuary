#!/usr/bin/env python3
"""Compare freshly captured moving/relative-step bodies inside full follow composition."""
import argparse,itertools,json,struct
from pathlib import Path
from unicorn.x86_const import *
from probe_generic_pet_follow_movement import FollowMovement,defaults,EXTRA
from probe_generic_pet_decisions import BASE,MEM,OWNER,OWNERPATH
from probe_warlock_runtime import ROOT,digest
FRESH=ROOT/'.local/class-skills/generic-pet-live-follow-reference-20261103'
OUT=ROOT/'.local/class-skills/generic-pet-motion-services-source-probe.json'
AUX100=MEM+0x2d000;AUX18=MEM+0x2e000;OWNERMON=MEM+0x2f000
class MotionServices(FollowMovement):
 def __init__(self):
  super().__init__()
  for name in ['callees-follow-movement-20261103.json','callees-follow-monster-mode-20261103.json']:
   pp=FRESH/name;proof=json.loads(pp.read_text());self.artifacts.append(pp)
   assert proof['executableSha256']==self.image.ref['executable']['sha256']
   assert proof['bytesRead']<=8192 and not any(proof[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
   for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for r in proof['records']:
    assert r['allUnwindFragmentsCaptured']
    for f in r['fragments']:
     assert f['captured'] and f['invalidInstructionMarkers']==0
     paths=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(paths)==1
     path=paths[0];body=path.read_bytes();start=BASE+int(f['rva'],16);self.map(start,len(body));self.uc.mem_write(start,body);self.ranges.append((start,start+len(body)));self.artifacts.append(path)
  for rva,n in [(0x163a3f0,160),(0x1723f90,192),(0x1648ec0,32)]:
   body=self.image.bytes(rva,n);self.tables[hex(rva)]=list(body);self.map(BASE+rva,n);self.uc.mem_write(BASE+rva,body)
 def invalid(self,uc,access,address,size,value,user):
  if address==0x48:self.trace.append([99]);self.fatal=True;uc.emu_stop();return False
  return super().invalid(uc,access,address,size,value,user)
 def hook(self,uc,a,size,user):
  if not self.initialized:
   p=self.p;self.initialized=True;self.wi(OWNER+4,743);self.wi(OWNER+0xc,p['ownerMode']);self.uc.mem_write(OWNER+0x1bd,bytes([p['selector']]))
   self.wq(OWNER+0x100,AUX100 if p['aux100'] else 0);self.wq(AUX100+0x18,AUX18 if p['aux18'] else 0);self.wi(AUX18+0x14,p['aux14'])
   self.uc.mem_write(OWNERPATH+0x48,bytes([p['path48']]));self.uc.mem_write(OWNERMON+0x104,struct.pack('<H',p['modeBits']))
  if any(lo<=a<hi for lo,hi in self.ranges):return
  if a==BASE+0x6bd00 and (uc.reg_read(UC_X86_REG_RDX)&0xffffffff)==743:
   selector=uc.reg_read(UC_X86_REG_RCX);assert selector==self.p['selector'];self.trace.append([26,selector,743,self.p['ownerMonster']]);self.ret(OWNERMON if self.p['ownerMonster']>=0 else 0);return
  return super().hook(uc,a,size,user)
 def run(self,family,p):self.initialized=False;return super().run(family,p)
def params(**values):
 p=defaults();p.update(dict(ownerMode=2,aux100=True,aux18=True,aux14=1,path48=32,modeBits=65535,ownerMonster=1));p.update(values);return p
def cases():
 for kind,mode,path,aux100,aux18,flags,path48,bits,monster in itertools.product([0,1,2,3,4,5],range(20),[False,True],[False,True],[False,True],[0,1],[0,32],[0,65535],[-1,1]):
  if kind in [2,4,5] and not path:continue
  yield 'destination',params(ownerKind=kind,ownerMode=mode,ownerPath=path,aux100=aux100,aux18=aux18,aux14=flags,path48=path48,modeBits=bits,ownerMonster=monster)
 for mode,bits in itertools.product(range(20),[0,*[1<<i for i in range(16)]]):
  yield 'destination',params(ownerKind=1,ownerMode=mode,aux14=0,modeBits=bits)
 for pet,point,radius,response in itertools.product([[20,30],[0,0],[65535,65535]],[[0,0],[20,30],[40,50],[-1,-1],[2147483647,-2147483648]],[0,1,4,255,256],[0,1,-7]):
  yield 'toward',params(pet=pet,x=point[0],y=point[1],radius=radius,responses=[response],tuning=100)
 for mode,kind,ownerMode,flags,response in itertools.product([0,1,2,4,5],[0,1],[0,2,4,13],[0,32],[[0],[1],[0,0,1]]):
  yield 'follow',params(mode=mode,ownerKind=kind,ownerMode=ownerMode,path48=flags,responses=response)
def main(check=False):
 em=MotionServices();rows=[[f,p,em.run(f,p)] for f,p in cases()];payload=json.dumps(rows,separators=(',',':'))+'\n'
 evidence=dict(referenceVersion='3.3.93847',component='Current moving predicate,monster mode eligibility and relative coordinate movement',cases=len(rows),byFamily={k:sum(r[0]==k for r in rows) for k in ['destination','toward','follow']},offlineMachineCodeExecuted=True,processMemoryReadDuringProbe=False,targetProcessCodeExecuted=False,worldMovementVerified=False,handlerEnabled=False,immutableTables=em.tables,
  limits=['Actual mode/path acceptance,warp,source speed/timing,cfg+0x70 producer,persistent controller and complete learned skill lifecycle remain unfinished.',
   'Moving predicate and relative coordinate step execute complete freshly captured bytes together with destination,follow,wrappers,preparation,mode wrapper and think queue. Source uses unsigned comparisons and can step away from supplied coordinates; no direction correction is invented.'],
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_follow_movement.py',ROOT/'tools/probe_generic_pet_movement.py',ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_generic_pet_actions.py',*em.artifacts,em.image.reference]},fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
 out=ROOT/'tools/reference/generic-pet-motion-services-source-evidence.json'
 if check:assert OUT.read_text()==payload and json.loads(out.read_text())==evidence
 else:OUT.write_text(payload);out.write_text(json.dumps(evidence,indent=2)+'\n')
 print('Current GenericPet motion services:',len(rows),'captured-code cases;',evidence['byFamily'],';no world/learned claim.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
