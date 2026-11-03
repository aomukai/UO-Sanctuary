#!/usr/bin/env python3
"""Execute complete captured current relocation/coordinate/reset bodies offline."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE,UC_HOOK_MEM_INVALID
from unicorn.x86_const import *
from probe_generic_pet_decisions import Emulator,BASE,STACK,MEM,STOP
from probe_warlock_runtime import ROOT,Image,digest
FRESH=ROOT/'.local/class-skills/pet-path-commit-reference-20261103'
OUT=ROOT/'.local/class-skills/generic-pet-path-commit-source-probe.json'
PATH=MEM;ACTOR=MEM+0x1000;TARGET=MEM+0x2000;ROOM=MEM+0x3000;OTHER=MEM+0x4000;ROW=MEM+0x5000
ENTRY=dict(commit=0x286fa0,coordinates=0x285440,reset=0x2863f0)
FUNCS={n:BASE+r for n,r in dict(removePattern=0x2a5f00,checkPattern=0x2a5bf0,addPattern=0x2a5c70,removeSize=0x2a5fd0,addSize=0x2a5d30,roomAt=0x2972c0,metadata=0x06bc90,unlink=0x2a3c80,link=0x2a3940,touch=0x2a3b30,fatal=0x7b6af0).items()}
signed=lambda v:struct.unpack('<i',struct.pack('<I',v&0xffffffff))[0]
class Commit(Emulator):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for name,rvas in [('callees-path-commit-nearby-20261103.json',[ENTRY['commit']]),('callees-path-commit-leaves-20261103.json',[ENTRY['coordinates'],ENTRY['reset']])]:
   path=FRESH/name;proof=json.loads(path.read_text());self.artifacts.append(path)
   assert proof['executableSha256']==self.image.ref['executable']['sha256'] and proof['bytesRead']<=8192
   assert not any(proof[k] for k in ('processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'))
   for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for rva in rvas:
    row=next(r for r in proof['records'] if int(r['rva'],16)==rva);assert row['allUnwindFragmentsCaptured']
    for f in row['fragments']:
     assert f['captured'] and f['invalidInstructionMarkers']==0
     found=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(found)==1
     p=found[0];body=p.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(body));self.uc.mem_write(a,body);self.ranges.append((a,a+len(body)));self.artifacts.append(p)
  self.reset96=int.from_bytes(self.image.bytes(0x16294b0,8),'little');self.map(BASE+0x16294b0,8);self.wq(BASE+0x16294b0,self.reset96)
  self.map(STACK,0x40000);self.map(MEM,0x10000);self.map(STOP,4096)
  for a in FUNCS.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook);self.uc.hook_add(UC_HOOK_MEM_INVALID,self.invalid)
 def token(self,ptr):return {0:0,ROOM:1,OTHER:2}[ptr]
 def ret(self,value):
  rsp=self.uc.reg_read(UC_X86_REG_RSP);dest=self.q(rsp);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,rsp+8);self.uc.reg_write(UC_X86_REG_RIP,dest)
 def invalid(self,uc,access,address,size,value,user):
  assert address==0,('Unexpected path invalid memory',hex(address));self.trace.append([99]);self.fatal=True;uc.emu_stop();return False
 def hook(self,uc,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((n for n,v in FUNCS.items() if v==a),None);assert name,('Undeclared current path helper',hex(a))
  reg=uc.reg_read;c,d,v,z=[reg(r) for r in (UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9)];rsp=reg(UC_X86_REG_RSP);result=0
  if name=='fatal':self.trace.append([99]);self.fatal=True;uc.emu_stop();return
  if name in ('removePattern','checkPattern','addPattern','removeSize','addSize'):
   code={'removePattern':0,'checkPattern':1,'addPattern':2,'removeSize':3,'addSize':4}[name]
   self.trace.append([code,self.token(c),signed(d),signed(v),signed(z),signed(self.q(rsp+0x28))]);result=self.p['collision'] if code==1 else 0
  elif name=='roomAt':
   result=self.p['rooms'][min(self.roomCalls,len(self.p['rooms'])-1)];self.roomCalls+=1;self.trace.append([5,self.token(c),signed(d),signed(v),result]);result=[0,ROOM,OTHER][result]
  elif name=='metadata':
   assert c==self.p['variant'] and signed(d)==self.p['targetClass'];self.trace.append([6,c,signed(d),self.p['metadata']]);result=ROW if self.p['metadata'] is not None else 0
  elif name=='unlink':assert c==ACTOR;self.trace.append([7,1])
  elif name=='link':assert c==ACTOR;self.trace.append([8,1,self.token(d)])
  elif name=='touch':assert c==ACTOR;self.trace.append([9,1])
  self.ret(result)
 def run(self,family,p):
  self.p=p;self.trace=[];self.fatal=False;self.roomCalls=0;self.uc.mem_write(MEM,b'\0'*0x10000);self.uc.mem_write(STACK,b'\0'*0x40000)
  self.uc.mem_write(PATH,struct.pack('<II',*p['point']));self.wi(PATH+8,101);self.wi(PATH+12,-202)
  self.wq(PATH+0x20,[0,ROOM,OTHER][p['oldRoom']]);self.wq(PATH+0x28,OTHER);self.wi(PATH+0x30,17);self.wi(PATH+0x34,18)
  self.wq(PATH+0x40,ACTOR if p['actor']>=0 else 0);self.wi(ACTOR,p['actor']);self.wi(PATH+0x48,signed(p['flags']));self.wi(PATH+0x50,p['type'])
  for off,key in ((0x58,'pattern'),(0x5c,'size'),(0x60,'mask'),(0x64,'checkMask')):self.wi(PATH+off,p[key])
  self.uc.mem_write(PATH+0x68,struct.pack('<H',0x7654));self.wq(PATH+0x70,TARGET if p['target']>=0 else 0)
  self.wi(TARGET,p['target']);self.wi(TARGET+4,p['targetClass']);self.wi(TARGET+0x124,signed(p['targetFlags']));self.uc.mem_write(TARGET+0x1bd,bytes([p['variant']]))
  self.wq(PATH+0x96,0x1122334455667788);self.wi(PATH+0x200,19);self.wi(PATH+0x204,-20)
  if p['metadata'] is not None:self.uc.mem_write(ROW+0x3d,bytes([p['metadata']]))
  for ptr in (ROOM,OTHER):self.uc.mem_write(ptr+0x80,struct.pack('<4i',*p['bounds']))
  rsp=STACK+0x20008;self.wq(rsp,STOP);self.uc.reg_write(UC_X86_REG_RSP,rsp);self.uc.reg_write(UC_X86_REG_RCX,PATH)
  if family=='commit':
   self.uc.reg_write(UC_X86_REG_RDX,[0,ROOM,OTHER][p['newRoom']]);self.uc.reg_write(UC_X86_REG_R8,p['packed'])
  elif family=='coordinates':
   self.uc.reg_write(UC_X86_REG_RDX,p['precise'][0]|(p['precise'][1]<<32));self.uc.reg_write(UC_X86_REG_R8,[0,ROOM,OTHER][p['newRoom']])
  try:self.uc.emu_start(BASE+ENTRY[family],STOP,count=5000)
  except unicorn.UcError:
   if not self.fatal:raise
  if not self.fatal:assert self.uc.reg_read(UC_X86_REG_RIP)==STOP
  result=-1 if self.fatal else 1 if family=='commit' else 0
  fields=[*struct.unpack('<II',self.uc.mem_read(PATH,8)),self.i(PATH+8),self.i(PATH+12),self.token(self.q(PATH+0x20)),self.token(self.q(PATH+0x28)),self.i(PATH+0x48)&0xffffffff,self.i(PATH+0x30),self.i(PATH+0x34),int.from_bytes(self.uc.mem_read(PATH+0x68,2),'little'),self.i(PATH+0x200),self.i(PATH+0x204)&0xffffffff,self.q(PATH+0x96)]
  return [result,fields,self.trace]
def params(**values):
 p=dict(actor=1,oldRoom=1,newRoom=2,point=[(20<<16)|123,(30<<16)|456],precise=[(65<<16)|1234,(75<<16)|5678],packed=65|(75<<16),flags=0x20,type=0,pattern=2,size=2,mask=0x100,checkMask=0x1c09,collision=0,target=-1,targetClass=742,targetFlags=0,variant=1,metadata=2,rooms=[2],bounds=[10,10,25,25]);p.update(values);return p
def cases():
 for actor,old,new,point,flags,rooms,collision in itertools.product([-1,0,1,3],[0,1],[0,1,2],[(0,0),(20,30),(65,75),(65535,0),(0,65535)],[0,1,0x20,0x40000,0xffffffff],[[0],[1],[2],[0,2]],[-1,0,65536]):
  yield 'commit',params(actor=actor,oldRoom=old,newRoom=new,packed=point[0]|(point[1]<<16),flags=flags,rooms=rooms,collision=collision)
 for actor,flags,precise,rooms,bounds in itertools.product([-1,1],[0,1,0x40001,0xffffffff],[[0,0],[0xffffffff,0x80000000],[(20<<16)|1,(30<<16)|65535]],[[0],[1],[0,2]],[[10,10,25,25],[-1,-2,0,0],[2147483647,-2147483648,2,2]]):
  yield 'coordinates',params(actor=actor,flags=flags,precise=precise,rooms=rooms,bounds=bounds)
 for actor,kind,flags,targetFlags,metadata,typ in itertools.product([-1,0,1,3],[-1,0,1],[0,1,0x40000,0xffffffff],[0,4],[None,0,2,255],[0,17]):
  yield 'reset',params(actor=actor,target=kind,flags=flags,targetFlags=targetFlags,metadata=metadata,type=typ)
def main(check=False):
 em=Commit();rows=[[f,p,em.run(f,p)] for f,p in cases()];payload=json.dumps(rows,separators=(',',':'))+'\n'
 proof=dict(referenceVersion='3.3.93847',component='Complete current path commitment,coordinate update and reset with explicit collision/room services',cases=len(rows),byFamily={k:sum(r[0]==k for r in rows) for k in ENTRY},offlineMachineCodeExecuted=True,processMemoryReadDuringProbe=False,targetProcessCodeExecuted=False,worldMovementVerified=False,handlerEnabled=False,reset96=em.reset96,
  limits=['All5commit fragments plus complete335-byte coordinate and165-byte reset bodies execute. Collision footprint operations,room geometry/lookup/link lifecycle and target metadata remain declared services.',
   'Fatal/missing-actor paths retain ordered partial writes and are unsupported by the C# consumer. No native movement,learned cast or actual source collision footprint certification is claimed.'],
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_decisions.py',*em.artifacts,em.image.reference]},fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
 target=ROOT/'tools/reference/generic-pet-path-commit-source-evidence.json'
 if check:assert OUT.read_text()==payload and json.loads(target.read_text())==proof
 else:OUT.write_text(payload);target.write_text(json.dumps(proof,indent=2)+'\n')
 print('Current path commitment:',len(rows),'captured-code cases;',proof['byFamily'],';complete coordinate/reset bodies;world/learned integration remains open.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
