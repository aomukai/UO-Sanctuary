#!/usr/bin/env python3
"""Complete captured footprint dispatch, inline tables and path composition."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn.x86_const import *
from probe_generic_pet_path_commit import Commit,params,BASE,MEM,ROOT,FRESH,ROOM,OTHER,signed
from probe_warlock_runtime import digest
ENTRIES=dict(checkPattern=0x2a5bf0,addPattern=0x2a5c70,removePattern=0x2a5f00,addSize=0x2a5d30,removeSize=0x2a5fd0)
LOWER={n:BASE+r for n,r in dict(point=0x2a4920,adjacent=0x2a5060,box=0x2a4b30,addAdjacent=0x2a6400,addBox=0x2a4e90,removeAdjacent=0x2a61a0,removeBox=0x2a4ca0).items()}
GRID=MEM+0x6000;CELLS=MEM+0x8000
INITIAL=[0,1,0xffff,0x1000,0x2000,0x1234,0xabcd,0x8000,7]
OUT=ROOT/'.local/class-skills/generic-pet-footprints-source-probe.json'
class Footprints(Commit):
 def __init__(self):
  super().__init__();self.starting=False;proofpath=FRESH/'callees-path-commit-footprints-20261103.json';proof=json.loads(proofpath.read_text());self.artifacts.append(proofpath)
  assert proof['executableSha256']==self.image.ref['executable']['sha256'] and proof['bytesRead']<=8192
  assert not any(proof[k] for k in ('processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'))
  for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  for r in proof['records']:
   assert int(r['rva'],16) in ENTRIES.values() and r['allUnwindFragmentsCaptured']
   for f in r['fragments']:
    assert f['captured'] and (f['invalidInstructionMarkers']==0 or r['rva']=='0x2a5fd0' and f['rva']=='0x2a6175' and f['invalidInstructionMarkers']==2)
    paths=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(paths)==1
    path=paths[0];body=path.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(body));self.uc.mem_write(a,body);self.ranges.append((a,a+len(body)));self.artifacts.append(path)
  # Validate both six-entry RVA tables and their indexed load/jump; data is
  # mapped for reads, but never admitted to the instruction hook below.
  for entry,table,targets in [(0x2a5d30,0x2a5ee4,[0x2a5ecf,0x2a5d6e,0x2a5dd7,0x2a5e21,0x2a5e7f,0x2a5ec2]),(0x2a5fd0,0x2a6188,[0x2a6170,0x2a6017,0x2a607e,0x2a60c8,0x2a6120,0x2a6163])]:
   assert list(struct.unpack('<6I',self.uc.mem_read(BASE+table,24)))==targets
   body=bytes(self.uc.mem_read(BASE+entry,table-entry))
   assert b'\x41\x83\xf9\x05' in body and b'\x44\x8b\x8c\x81'+struct.pack('<I',table) in body and b'\x4c\x03\xc9\x41\xff\xe1' in body
  for a in LOWER.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.hook_add(unicorn.UC_HOOK_MEM_READ|unicorn.UC_HOOK_MEM_WRITE,self.cell_access)
 def setup(self):
  p=self.p
  for r in (ROOM,OTHER):self.wq(r+0x38,GRID if p['grid'] else 0)
  for off,value in enumerate(p['gridBounds']):self.wi(GRID+off*4,value)
  self.wq(GRID+0x20,CELLS if p['cells'] else 0);self.uc.mem_write(CELLS,struct.pack('<9H',*INITIAL))
  if self.mode!='bound':
   self.uc.reg_write(UC_X86_REG_RCX,[0,ROOM,OTHER][p['fpRoom']]);self.uc.reg_write(UC_X86_REG_RDX,p['fx']&0xffffffff)
   self.uc.reg_write(UC_X86_REG_R8,p['fy']&0xffffffff);self.uc.reg_write(UC_X86_REG_R9,p['fpType']&0xffffffff)
   self.wq(self.uc.reg_read(UC_X86_REG_RSP)+0x28,p['fpMask']&0xffffffff)
   self.uc.reg_write(UC_X86_REG_RIP,BASE+ENTRIES[self.mode])
 def index(self,x,y):
  gx,gy,width=self.p['gridBounds'];return signed(x-gx)+signed(signed(y-gy)*width)
 def bad_addresses(self):
  p=self.p;xy=[(p['fx'],p['fy']),(p['point'][0]>>16,p['point'][1]>>16),(p['packed']&65535,p['packed']>>16)]
  return {(CELLS+2*self.index(x,y))&0xffffffffffffffff for x,y in xy if not 0<=self.index(x,y)<9}
 def cell_access(self,uc,access,address,size,value,user):
  if address in self.bad_addresses():self.trace.append([99]);self.fatal=True;uc.emu_stop()
 def invalid(self,uc,access,address,size,value,user):
  if address in self.bad_addresses():self.trace.append([99]);self.fatal=True;uc.emu_stop();return False
  return super().invalid(uc,access,address,size,value,user)
 def hook(self,uc,a,size,user):
  if self.starting:
   self.starting=False;self.setup()
   if self.mode!='bound':return
  assert not BASE+0x2a5ee4<=a<BASE+0x2a5efc and not BASE+0x2a6188<=a<BASE+0x2a61a0,'Dispatch table executed as instructions'
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((n for n,v in LOWER.items() if v==a),None)
  if name is None:return super().hook(uc,a,size,user)
  reg=uc.reg_read;c,d,v,z=[reg(r) for r in (UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9)]
  code=20+list(LOWER).index(name)
  if name in ('box','addBox','removeBox'):self.trace.append([code,self.token(c),*[self.i(d+i*4) for i in range(4)],signed(v)])
  else:self.trace.append([code,self.token(c),signed(d),signed(v),signed(z)])
  self.ret(self.p['collision'] if code<=22 else 0)
 def execute(self,mode,p):
  self.mode=mode;self.starting=True;out=super().run('commit',p)
  result=out[0] if mode=='bound' else -1 if self.fatal else signed(self.uc.reg_read(UC_X86_REG_RAX)) if mode=='checkPattern' else 0
  return [result,out[1] if mode=='bound' else [],list(struct.unpack('<9H',self.uc.mem_read(CELLS,18))),out[2]]
def param(**values):
 p=params();p.update(fpRoom=1,fx=21,fy=31,fpType=1,fpMask=1,grid=True,cells=True,gridBounds=[20,30,3]);p.update(values);return p
def cases():
 types=[-1,0,1,2,3,4,5,6,2147483647];masks=[0,1,0x1000,0x2000,0xffff,0x10000,-1]
 for mode,typ,room,mask,point in itertools.product(ENTRIES,types,[0,1],masks,[(20,30),(21,31),(22,32),(19,30),(23,32)]):
  yield mode,param(fpType=typ,fpRoom=room,fpMask=mask,fx=point[0],fy=point[1])
 for mode,typ,rooms,grid,cells,mask in itertools.product(ENTRIES,[1,2,3,4],[[0],[1],[2]],[False,True],[False,True],[0,1,65536]):
  yield mode,param(fpType=typ,rooms=rooms,grid=grid,cells=cells,fpMask=mask)
 for typ,room,value in itertools.product(types,[0,1],[-7,0,1,65536,65535]):yield 'checkPattern',param(fpType=typ,fpRoom=room,collision=value)
 for mode,typ,point,mask,width in itertools.product(ENTRIES,[1,3],[(-2147483648,2147483647),(2147483647,-2147483648)],[-1,65536],[-1,0,3,2147483647]):
  yield mode,param(fpType=typ,fx=point[0],fy=point[1],fpMask=mask,gridBounds=[20,30,width])
 for actor,room,pattern,size,point,flags,mask in itertools.product([0,1,3],[0,2],[0,1,2,3,4],range(6),[(0,0),(20,30),(22,32)],[0,0x40000],[0,65536,-1]):
  yield 'bound',param(actor=actor,newRoom=room,pattern=pattern,size=size,packed=point[0]|(point[1]<<16),flags=flags,mask=mask)
def main(check=False):
 em=Footprints();rows=[[mode,p,em.execute(mode,p)] for mode,p in cases()];payload=json.dumps(rows,separators=(',',':'))+'\n'
 proof=dict(referenceVersion='3.3.93847',component='Current footprint pattern/size dispatch and composed path commitment',cases=len(rows),byFamily={k:sum(r[0]==k for r in rows) for k in [*ENTRIES,'bound']},unsupportedCases=sum(r[2][0]==-1 for r in rows),offlineMachineCodeExecuted=True,processMemoryReadDuringProbe=False,targetProcessCodeExecuted=False,worldMovementVerified=False,handlerEnabled=False,
  limits=['Complete five wrappers,including both inline six-entry dispatch tables,run with explicit adjacent/box/check/room services. Tables are data and are forbidden instruction addresses.',
   'Direct low16 grid writes execute against supplied source grid metadata. Out-of-span access is unsupported with retained prior service effects. Low-level adjacent/box geometry and real native room/collision ownership remain separate.'],
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_path_commit.py',ROOT/'tools/probe_generic_pet_decisions.py',*em.artifacts,em.image.reference]},fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
 target=ROOT/'tools/reference/generic-pet-footprints-source-evidence.json'
 if check:assert OUT.read_text()==payload and json.loads(target.read_text())==proof
 else:OUT.write_text(payload);target.write_text(json.dumps(proof,indent=2)+'\n')
 print('Current footprints:',len(rows),'complete captured-code cases;',proof['byFamily'],';unsupported',proof['unsupportedCases'])
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
