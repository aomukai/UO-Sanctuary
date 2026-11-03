#!/usr/bin/env python3
"""Execute actual forced monster footprint remove/restore with current kernels."""
import argparse,itertools,json,struct
from pathlib import Path
from unicorn.x86_const import *
from probe_pet_collision_writes import Writes,ROOT,BASE,MEM,ROOMS,params,OUT as UNUSED
from probe_pet_collision_reads import Collision
from probe_warlock_runtime import digest
FRESH=ROOT/'.local/class-skills/pet-world-placement-reference-20261103'
OLD=ROOT/'.local/class-skills/generic-pet-live-follow-reference-20261103'
OUT=ROOT/'.local/class-skills/pet-monster-placement-hooks-source-probe.json'
UNIT=MEM+0xb000;PATH=MEM+0xc000;ROOM_GETTER=BASE+0x278450
ENTRY=dict(remove=0x264460,restore=0x2641b0)
class Hooks(Writes):
 def __init__(self):
  super().__init__()
  for path,rvas in [(FRESH/'callees-pet-world-hooks-20261103.json',list(ENTRY.values())),(FRESH/'callees-pet-world-hook-leaves-20261103.json',[0x278110]),(OLD/'callees-follow-warp-leaves-20261103.json',[0x266860])]:
   proof=json.loads(path.read_text());self.artifacts.append(path)
   assert proof['directCallTargetsVerified'] and proof['executableSha256']==self.image.ref['executable']['sha256'] and proof['bytesRead']<=8192
   for name,h in proof['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
   for rva in rvas:
    row=next(r for r in proof['records'] if int(r['rva'],16)==rva);assert row['allUnwindFragmentsCaptured']
    for f in row['fragments']:
     assert f['captured'] and f['invalidInstructionMarkers']==0
     paths=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(paths)==1
     p=paths[0];body=p.read_bytes();address=BASE+int(f['rva'],16);self.map(address,len(body));self.uc.mem_write(address,body);self.ranges.append((address,address+len(body)));self.artifacts.append(p)
  self.map(ROOM_GETTER,1);self.uc.mem_write(ROOM_GETTER,b'\xc3')
 def hook(self,uc,address,size,user):
  if getattr(self,'hookStarting',False):
   self.hookStarting=False;p=self.p;self.wi(UNIT,1);self.wi(UNIT+0xc,p['mode']);self.wq(UNIT+0x38,PATH)
   self.uc.mem_write(PATH,struct.pack('<II',((p['x']&65535)<<16)|0x8000,((p['y']&65535)<<16)|0x8000));self.wq(PATH+0x20,ROOMS[p['room']]);self.wi(PATH+0x5c,p['pattern']);self.wi(PATH+0x60,p['mask'])
   uc.reg_write(UC_X86_REG_RCX,UNIT);uc.reg_write(UC_X86_REG_RDX,1);uc.reg_write(UC_X86_REG_RIP,BASE+ENTRY[self.kind]);return
  if address==ROOM_GETTER:
   assert uc.reg_read(UC_X86_REG_RCX)==UNIT;self.flush();rsp=uc.reg_read(UC_X86_REG_RSP);uc.reg_write(UC_X86_REG_RAX,self.q(PATH+0x20));uc.reg_write(UC_X86_REG_RIP,self.q(rsp));uc.reg_write(UC_X86_REG_RSP,rsp+8);return
  return super().hook(uc,address,size,user)
 def execute(self,kind,p):
  self.kind=kind;self.hookStarting=True;self.redirect=False;self.pending=[]
  result=Collision.execute(self,'point',p);self.flush()
  buffers=[list(struct.unpack('<'+'H'*(p['width']*p['height']),self.uc.mem_read(ROOMS[i]+0x200,2*p['width']*p['height']))) for i in range(1,5)]
  return [-1 if self.fatal else 0,buffers,[t for t in result[1] if t[0]!=2]]
def cases():
 for kind,mode,room,pattern,mask,point in itertools.product(ENTRY,[-1,0,1,12,18],range(5),[-1,0,1,2,3,4,5,6],[0,1,0x100,0x1000,65536,-1],[(20,30),(22,32),(23,33),(25,35)]):
  yield kind,params(mode=mode,room=room,pattern=pattern,mask=mask,x=point[0],y=point[1])
 for kind,pattern,mask,(grids,cells),point in itertools.product(ENTRY,[1,2,3,4,5],[0,65536,-1],[(14,15),(15,14),(0,0)],[(20,30),(22,32)]):
  yield kind,params(mode=0,pattern=pattern,mask=mask,grids=grids,cells=cells,x=point[0],y=point[1])
def main(check=False):
 em=Hooks();rows=[[kind,p,em.execute(kind,p)] for kind,p in cases()];payload=json.dumps(rows,separators=(',',':'))+'\n'
 proof=dict(referenceVersion='3.3.93847',component='Current forced monster placement footprint hooks with complete current mask/size getters and write kernels',cases=len(rows),byFamily={k:sum(r[0]==k for r in rows) for k in ENTRY},unsupportedCases=sum(r[2][0]==-1 for r in rows),offlineMachineCodeExecuted=True,processMemoryReadDuringProbe=False,targetProcessCodeExecuted=False,worldMovementVerified=False,handlerEnabled=False,
  limits=['Complete remove/restore bodies execute the actual monster/valid-path branch. The real placement caller passes force1;conditional force0 and other unit kinds are not exposed. Complete current mask/size getters and composed footprint/write kernels execute;unit-to-room projection is a declared service.',
   'Comparison covers final buffers and resolver/fatal query order,including unknown patterns,full-mask occupancy gates,missing metadata and retained partial writes. SIMD load grouping and direct-cell callback events are not claimed. Global world/controller/visibility remain unfinished.'],
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),ROOT/'tools/probe_pet_collision_writes.py',ROOT/'tools/probe_pet_collision_reads.py',ROOT/'tools/probe_generic_pet_footprints.py',ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_generic_pet_path_commit.py',*em.artifacts,em.image.reference]},fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
 target=ROOT/'tools/reference/pet-monster-placement-hooks-source-evidence.json'
 if check:assert OUT.read_text()==payload and json.loads(target.read_text())==proof
 else:OUT.write_text(payload);target.write_text(json.dumps(proof,indent=2)+'\n')
 print('Current monster hooks:',len(rows),'cases;',proof['byFamily'],';unsupported',proof['unsupportedCases'])
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
