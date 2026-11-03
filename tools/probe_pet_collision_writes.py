#!/usr/bin/env python3
"""Execute current collision writes and composed footprint wrappers offline."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn.x86_const import *
from probe_pet_collision_reads import Collision,ROOT,BASE,MEM,STACK,STOP,ROOMS,BOX,param,values
from probe_generic_pet_footprints import ENTRIES,FRESH as WRAPPERS
from probe_warlock_runtime import digest
FRESH=ROOT/'.local/class-skills/pet-collision-write-reference-20261103'
OUT=ROOT/'.local/class-skills/pet-collision-writes-source-probe.json'
WRITE=dict(addAdjacent=0x2a6400,removeAdjacent=0x2a61a0,addBox=0x2a4e90,removeBox=0x2a4ca0)
MODES={**WRITE,**{n:v for n,v in ENTRIES.items() if n!='checkPattern'}}
class Writes(Collision):
 def __init__(self):
  super().__init__();self.pending=[]
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200)
  for path in [FRESH/'callees-pet-collision-writes-20261103.json',WRAPPERS/'callees-path-commit-footprints-20261103.json']:
   proof=json.loads(path.read_text());self.artifacts.append(path)
   assert proof['directCallTargetsVerified'] and proof['executableSha256']==self.image.ref['executable']['sha256'] and proof['bytesRead']<=8192
   for name,h in proof['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
   for row in proof['records']:
    assert row['allUnwindFragmentsCaptured']
    for f in row['fragments']:
     assert f['captured'] and (f['invalidInstructionMarkers']==0 or row['rva']=='0x2a5fd0' and f['rva']=='0x2a6175' and f['invalidInstructionMarkers']==2)
     paths=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(paths)==1
     p=paths[0];body=p.read_bytes();address=BASE+int(f['rva'],16);self.map(address,len(body));self.uc.mem_write(address,body);self.ranges.append((address,address+len(body)));self.artifacts.append(p)
  for table,expected in [(0x2a5ee4,[0x2a5ecf,0x2a5d6e,0x2a5dd7,0x2a5e21,0x2a5e7f,0x2a5ec2]),(0x2a6188,[0x2a6170,0x2a6017,0x2a607e,0x2a60c8,0x2a6120,0x2a6163])]:
   assert list(struct.unpack('<6I',self.uc.mem_read(BASE+table,24)))==expected
  self.uc.hook_add(unicorn.UC_HOOK_MEM_WRITE,self.write)
 def read(self,*args):pass # Write cases certify writes/final buffers,not SIMD load grouping.
 def invalid(self,uc,access,address,size,value,user):
  assert access in (unicorn.UC_MEM_READ_UNMAPPED,unicorn.UC_MEM_WRITE_UNMAPPED) and 0<=address<2*self.p['width']*self.p['height'] and self.p['cells']!=15 and uc.reg_read(UC_X86_REG_RIP) in (BASE+0x2a4ff1,BASE+0x2a4e11,BASE+0x2a4fa0,BASE+0x2a4dc0),('Unexpected write memory fault',hex(address))
  self.fail();return False
 def write(self,uc,access,address,size,value,user):
  for i in range(1,5):
   begin=ROOMS[i]+0x200
   if begin<=address<begin+0x100:
    assert address+size<=begin+2*self.p['width']*self.p['height'] and size in (2,8,16),('Unexpected write span',address,size)
    self.pending.append((i,address,size));return
 def flush(self):
  for i,address,size in self.pending:
   for offset in range(0,size,2):self.trace.append([2,i,(address+offset-ROOMS[i]-0x200)//2,int.from_bytes(self.uc.mem_read(address+offset,2),'little')])
  self.pending=[]
 def execute(self,kind,p):
  # Reuse the checked room/grid setup and dispatch to a write body on the
  # first instruction, before any read-family instruction executes.
  self.writeKind=kind;self.pending=[];self.redirect=True
  result=super().execute('box' if 'Box' in kind else 'point',p)
  self.flush();result[0]=-1 if self.fatal else 0
  if kind not in WRITE:result[1]=[t for t in result[1] if t[0]!=2]
  return [result[0],[list(struct.unpack('<'+'H'*(p['width']*p['height']),self.uc.mem_read(ROOMS[i]+0x200,2*p['width']*p['height']))) for i in range(1,5)],result[1]]
 def hook(self,uc,address,size,user):
  if getattr(self,'redirect',False):
   self.redirect=False;p=self.p;kind=self.writeKind;rsp=uc.reg_read(UC_X86_REG_RSP)
   uc.reg_write(UC_X86_REG_RCX,ROOMS[p['room']])
   if kind in ('addBox','removeBox'):
    uc.reg_write(UC_X86_REG_RDX,BOX);uc.reg_write(UC_X86_REG_R8,p['mask']&0xffffffff)
   else:
    uc.reg_write(UC_X86_REG_RDX,p['x']&0xffffffff);uc.reg_write(UC_X86_REG_R8,p['y']&0xffffffff);uc.reg_write(UC_X86_REG_R9,(p['pattern'] if kind not in WRITE else p['mask'])&0xffffffff)
    if kind not in WRITE:self.wq(rsp+0x28,p['mask']&0xffffffff)
   uc.reg_write(UC_X86_REG_RIP,BASE+MODES[kind]);return
  self.flush()
  assert not BASE+0x2a5ee4<=address<BASE+0x2a5efc and not BASE+0x2a6188<=address<BASE+0x2a61a0,'Footprint table executed'
  return super().hook(uc,address,size,user)
def params(**extra):
 p=param();p['pattern']=2;p.update(extra);return p
def cases():
 for kind,room,x,y,mask,(grids,cells) in itertools.product(WRITE,range(5),range(19,27),range(29,37),[0,1,0x1000,0x2000,65536,-1],[(15,15),(14,15),(15,14),(0,0)]):
  yield kind,params(room=room,x=x,y=y,box=[x,y,x+2,y+2],mask=mask,grids=grids,cells=cells)
 for kind,pattern,room,point,mask,(grids,cells) in itertools.product([n for n in MODES if n not in WRITE],[-1,0,1,2,3,4,5,6],[0,1,4],[(20,30),(22,32),(23,33),(25,35)],[0,1,0x1000,0x2000,65536,-1],[(15,15),(14,15),(15,14),(0,0)]):
  yield kind,params(room=room,x=point[0],y=point[1],pattern=pattern,mask=mask,grids=grids,cells=cells)
 for kind,width,mask in itertools.product(['addBox','removeBox'],[31,32,33,64],[0,1,65536,-1]):yield kind,params(width=width,height=1,box=[20,30,19+width,30],mask=mask)
 for kind,box,mask in itertools.product(['addBox','removeBox'],[[22,32,20,30],[20,32,22,30],[20,30,25,35],[19,29,26,36]],[0,-1]):yield kind,params(box=box,mask=mask)
def main(check=False):
 em=Writes();rows=[[kind,p,em.execute(kind,p)] for kind,p in cases()];payload=json.dumps(rows,separators=(',',':'))+'\n'
 proof=dict(referenceVersion='3.3.93847',component='Current collision writes and composed footprint wrappers',cases=len(rows),byFamily={k:sum(r[0]==k for r in rows) for k in MODES},unsupportedCases=sum(r[2][0]==-1 for r in rows),offlineMachineCodeExecuted=True,processMemoryReadDuringProbe=False,targetProcessCodeExecuted=False,worldMovementVerified=False,handlerEnabled=False,
  limits=['Four complete current write kernels reuse the actual captured room resolver/box splitter. Direct cases compare ordered resolver entries and normalized per-word stores plus complete final buffers;SIMD load grouping is not claimed.',
   'Four complete footprint write wrappers compose the actual kernels;these compositions compare room queries/final buffers rather than direct-cell callback events. Missing box buffers are explicit unsupported controls with prior writes retained. Native terrain/grid ownership,room links,controller and learned casts remain separate.'],
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),ROOT/'tools/probe_pet_collision_reads.py',ROOT/'tools/probe_generic_pet_footprints.py',ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_generic_pet_path_commit.py',ROOT/'tools/capture_pet_collision_writes.py',*em.artifacts,em.image.reference]},fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
 target=ROOT/'tools/reference/pet-collision-writes-source-evidence.json'
 if check:assert OUT.read_text()==payload and json.loads(target.read_text())==proof
 else:OUT.write_text(payload);target.write_text(json.dumps(proof,indent=2)+'\n')
 print('Current collision writes:',len(rows),'cases;',proof['byFamily'],';unsupported',proof['unsupportedCases'])
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
