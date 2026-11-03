#!/usr/bin/env python3
"""Execute current collision reads, room resolution and box splitting offline."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn.x86_const import *
from probe_generic_pet_decisions import Emulator,BASE,STACK,MEM,STOP
from probe_generic_pet_path_commit import signed
from probe_warlock_runtime import ROOT,Image,digest
FRESH=ROOT/'.local/class-skills/pet-collision-reference-20261103'
OUT=ROOT/'.local/class-skills/pet-collision-reads-source-probe.json'
ENTRY=dict(point=0x2a4920,adjacent=0x2a5060,box=0x2a4b30,resolve=0x2972c0)
ROOMS=[0,*[MEM+i*0x1000 for i in range(1,5)]]
BOX=MEM+0x9000
TARGETS=[0x2a512e,0x2a5154,0x2a518b,0x2a51b8,0x2a51f9,0x2a5321,0x2a522f,0x2a5321,0x2a526f,0x2a5294,0x2a5321,0x2a5321,0x2a52db]
def geometry(p,i):return [20+((i-1)%2)*p['width'],30+((i-1)//2)*p['height'],p['width'],p['height']]
def values(i,n):return [((i*0x1234+j*0x1021)^(1<<(j%16)))&65535 for j in range(n)]
class Collision(Emulator):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=unicorn.Uc(unicorn.UC_ARCH_X86,unicorn.UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for label in ('checks','leaves'):
   path=FRESH/f'callees-pet-collision-{label}-20261103.json';proof=json.loads(path.read_text());self.artifacts.append(path)
   assert proof['executableSha256']==self.image.ref['executable']['sha256'] and proof['directCallTargetsVerified'] and proof['bytesRead']<=8192
   assert not any(proof[k] for k in ('processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'))
   for name,h in proof['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
   for row in proof['records']:
    assert row['allUnwindFragmentsCaptured']
    for f in row['fragments']:
     assert f['captured'] and f['invalidInstructionMarkers']==0
     paths=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(paths)==1
     p=paths[0];body=p.read_bytes();address=BASE+int(f['rva'],16);self.map(address,len(body));self.uc.mem_write(address,body);self.ranges.append((address,address+len(body)));self.artifacts.append(p)
  assert list(struct.unpack('<13I',self.uc.mem_read(BASE+0x2a5354,52)))==TARGETS
  assert bytes(self.uc.mem_read(BASE+0x2a5121,13))==b'\x41\x8b\x8c\x80\x54\x53\x2a\x00\x49\x03\xc8\xff\xe1'
  self.map(STACK,0x40000);self.map(MEM,0x10000);self.map(STOP,4096);self.map(BASE+0x197a178,8);self.uc.mem_write(BASE+0x197a178,b'\0'*8)
  self.map(BASE+0x155eeb0,1);self.uc.mem_write(BASE+0x155eeb0,b'\xc3')
  self.uc.hook_add(unicorn.UC_HOOK_CODE,self.hook);self.uc.hook_add(unicorn.UC_HOOK_MEM_READ,self.read);self.uc.hook_add(unicorn.UC_HOOK_MEM_INVALID,self.invalid)
 def fail(self):self.trace.append([99]);self.fatal=True;self.uc.emu_stop()
 def invalid(self,uc,access,address,size,value,user):
  # A missing box cell buffer can fault at a nonzero element offset from
  # null, not just at address zero. Admit only this explicit raw read site.
  assert access==unicorn.UC_MEM_READ_UNMAPPED and uc.reg_read(UC_X86_REG_RIP)==BASE+0x2a4a00 and size==2 and 0<=address<2*self.p['width']*self.p['height'] and self.p['cells']!=15,('Unexpected collision memory fault',hex(address))
  self.fail();return False
 def read(self,uc,access,address,size,value,user):
  for i in range(1,5):
   begin=ROOMS[i]+0x200
   if begin-0x100<=address<begin+0x100:
    # Grid/neighbor metadata share this block: only reject raw cell reads
    # while inside the instructions that actually dereference a cell.
    pc=uc.reg_read(UC_X86_REG_RIP)
    cellpc=(BASE+0x2a496f==pc or BASE+0x2a4a00==pc or BASE+0x2a512e<=pc<BASE+0x2a5321 and size==2)
    if not cellpc:return
    n=self.p['width']*self.p['height'];index=(address-begin)//2
    if address<begin or address+size>begin+2*n or size!=2:self.fail();return
    self.trace.append([1,i,index,int.from_bytes(uc.mem_read(address,2),'little')]);return
 def hook(self,uc,address,size,user):
  assert not BASE+0x2a5354<=address<BASE+0x2a5388,'Inline jump table executed'
  if address==BASE+ENTRY['resolve']:
   self.trace.append([0,ROOMS.index(uc.reg_read(UC_X86_REG_RCX)),signed(uc.reg_read(UC_X86_REG_RDX)),signed(uc.reg_read(UC_X86_REG_R8))])
  if any(a<=address<b for a,b in self.ranges):return
  assert address==BASE+0x155eeb0,('Undeclared collision callee',hex(address))
  rsp=uc.reg_read(UC_X86_REG_RSP);uc.reg_write(UC_X86_REG_RIP,self.q(rsp));uc.reg_write(UC_X86_REG_RSP,rsp+8)
 def execute(self,kind,p):
  self.p=p;self.trace=[];self.fatal=False;self.uc.mem_write(MEM,b'\0'*0x10000);self.uc.mem_write(STACK,b'\0'*0x40000)
  for i in range(1,5):
   room=ROOMS[i];grid=room+0x100;self.wq(room,room+0x300);self.wi(room+0x40,len(p['neighbors']));self.uc.mem_write(room+0x80,struct.pack('<4i',*geometry(p,i)))
   for j,n in enumerate(p['neighbors']):self.wq(room+0x300+j*8,ROOMS[n])
   self.wq(room+0x38,grid if p['grids']&(1<<(i-1)) else 0);self.uc.mem_write(grid,struct.pack('<4i',*geometry(p,i)))
   self.wq(grid+0x20,room+0x200 if p['cells']&(1<<(i-1)) else 0);v=values(i,p['width']*p['height']);self.uc.mem_write(room+0x200,struct.pack('<'+'H'*len(v),*v))
  self.uc.mem_write(BOX,struct.pack('<4i',*p['box']));rsp=STACK+0x20008;self.wq(rsp,STOP)
  args=(ROOMS[p['room']],BOX,p['mask']&0xffffffff,0) if kind=='box' else (ROOMS[p['room']],p['x']&0xffffffff,p['y']&0xffffffff,p['mask']&0xffffffff)
  self.uc.reg_write(UC_X86_REG_RSP,rsp)
  for reg,value in zip((UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9),args):self.uc.reg_write(reg,value)
  try:self.uc.emu_start(BASE+ENTRY[kind],STOP,count=100000)
  except unicorn.UcError:
   if not self.fatal:raise
  if not self.fatal:assert self.uc.reg_read(UC_X86_REG_RIP)==STOP,('Collision instruction budget',kind,p)
  result=-1 if self.fatal else ROOMS.index(self.uc.reg_read(UC_X86_REG_RAX)) if kind=='resolve' else self.uc.reg_read(UC_X86_REG_RAX)&0xffff
  return [result,self.trace]
def param(**extra):
 p=dict(room=1,x=21,y=31,box=[20,30,22,32],mask=0x1c09,width=3,height=3,grids=15,cells=15,neighbors=[0,1,2,3,4]);p.update(extra);return p
def cases():
 for kind,room,x,y,mask,(grids,cells) in itertools.product(['point','adjacent','box'],range(5),range(19,27),range(29,37),[0,1,0x27,0x1c09,65536,-1],[(15,15),(14,15),(15,14),(0,0)]):
  yield kind,param(room=room,x=x,y=y,box=[x,y,x+2,y+2],mask=mask,grids=grids,cells=cells)
 for room,x,y,neighbors in itertools.product(range(5),range(19,27),range(29,37),[[0,1,2,3,4],[4,3,2,1,0],[]]):yield 'resolve',param(room=room,x=x,y=y,neighbors=neighbors)
 for kind,width,height,room,x,y,mask in itertools.product(['point','adjacent','box'],[1,2],[1,2],range(5),[20,21,22],[30,31,32],[0,-1]):yield kind,param(width=width,height=height,room=room,x=x,y=y,box=[x,y,x+1,y+1],mask=mask)
 for box,mask in itertools.product([[22,32,20,30],[20,32,22,30],[24,34,23,35],[20,30,25,35],[19,29,26,36]],[0,0x27,65536,-1]):yield 'box',param(box=box,mask=mask)
def main(check=False):
 em=Collision();rows=[[kind,p,em.execute(kind,p)] for kind,p in cases()];payload=json.dumps(rows,separators=(',',':'))+'\n'
 proof=dict(referenceVersion='3.3.93847',component='Current point/cardinal/recursive box collision and complete room resolver',cases=len(rows),byFamily={k:sum(r[0]==k for r in rows) for k in ENTRY},unsupportedCases=sum(r[2][0]==-1 for r in rows),offlineMachineCodeExecuted=True,processMemoryReadDuringProbe=False,targetProcessCodeExecuted=False,worldMovementVerified=False,handlerEnabled=False,
  limits=['Six complete captured routines run on supplied room/grid/cell metadata,including all13inline adjacent dispatch entries. The table is read-only data and forbidden as instructions;synthetic stack-cookie verification preserves RAX.',
   'Source room resolver and recursive split/scan execute without substitute callbacks. Cell and resolver-entry query ordering are compared. Missing box cell buffers remain explicit unsupported controls. Native terrain/grid ownership,write kernels,controller and learned casts remain separate.'],
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_generic_pet_path_commit.py',ROOT/'tools/capture_pet_collision_services.py',*em.artifacts,em.image.reference]},fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
 target=ROOT/'tools/reference/pet-collision-reads-source-evidence.json'
 if check:assert OUT.read_text()==payload and json.loads(target.read_text())==proof
 else:OUT.write_text(payload);target.write_text(json.dumps(proof,indent=2)+'\n')
 print('Current collision reads:',len(rows),'cases;',proof['byFamily'],';unsupported',proof['unsupportedCases'])
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
