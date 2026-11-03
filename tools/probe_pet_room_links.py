#!/usr/bin/env python3
"""Execute complete current monster room/update-list routines offline."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn.x86_const import *
from probe_generic_pet_decisions import Emulator,BASE,MEM,STACK,STOP
from probe_warlock_runtime import ROOT,Image,digest
FRESH=ROOT/'.local/class-skills/pet-world-placement-reference-20261103'
OLD=ROOT/'.local/class-skills/generic-pet-live-follow-reference-20261103'
OUT=ROOT/'.local/class-skills/pet-room-links-source-probe.json'
UNITS=[0,MEM+0x1000,MEM+0x2000,MEM+0x3000]
PATHS=[0,MEM+0x5000,MEM+0x6000,MEM+0x7000]
ROOMS=[0,MEM+0x9000,MEM+0xa000];MARKS=[0,MEM+0xc000,MEM+0xd000]
ENTRY=dict(add=0x2a3940,remove=0x2a3c80,removeUpdate=0x2a3e70,touch=0x2a3b30)
HELPERS=dict(room=BASE+0x278450,alignment=BASE+0x24eb30,fatal=BASE+0x7b6af0)
def signed(v):return struct.unpack('<i',struct.pack('<I',v&0xffffffff))[0]
class Links(Emulator):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=unicorn.Uc(unicorn.UC_ARCH_X86,unicorn.UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for path,rvas in [(FRESH/'callees-pet-world-links-20261103.json',[ENTRY['add'],ENTRY['remove']]),(FRESH/'callees-pet-world-link-leaves-20261103.json',[ENTRY['removeUpdate']]),(OLD/'callees-follow-warp-leaves-20261103.json',[ENTRY['touch']])]:
   proof=json.loads(path.read_text());self.artifacts.append(path)
   assert proof['executableSha256']==self.image.ref['executable']['sha256'] and proof['directCallTargetsVerified'] and proof['bytesRead']<=8192
   assert not any(proof[k] for k in ('processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'))
   for name,h in proof['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
   for rva in rvas:
    row=next(r for r in proof['records'] if int(r['rva'],16)==rva);assert row['allUnwindFragmentsCaptured']
    for f in row['fragments']:
     assert f['captured'] and f['invalidInstructionMarkers']==0
     found=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(found)==1
     path=found[0];body=path.read_bytes();address=BASE+int(f['rva'],16);self.map(address,len(body));self.uc.mem_write(address,body);self.ranges.append((address,address+len(body)));self.artifacts.append(path)
  assert bytes(self.uc.mem_read(BASE+0x2a3b6b,6))==b'\xc7\x00\x01\x00\x00\x00'
  self.map(STACK,0x40000);self.map(MEM,0x20000);self.map(STOP,4096)
  for address in HELPERS.values():self.map(address,1);self.uc.mem_write(address,b'\xc3')
  self.uc.hook_add(unicorn.UC_HOOK_CODE,self.hook);self.uc.hook_add(unicorn.UC_HOOK_MEM_INVALID,self.invalid)
 def fail(self):self.trace.append([99]);self.fatal=True;self.uc.emu_stop()
 def invalid(self,uc,access,address,size,value,user):
  assert address==0 and uc.reg_read(UC_X86_REG_RIP)==BASE+0x2a3b6b,('Unexpected room-list memory fault',hex(address),hex(uc.reg_read(UC_X86_REG_RIP)))
  self.fail();return False
 def ret(self,value):
  rsp=self.uc.reg_read(UC_X86_REG_RSP);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RIP,self.q(rsp));self.uc.reg_write(UC_X86_REG_RSP,rsp+8)
 def hook(self,uc,address,size,user):
  if any(lo<=address<hi for lo,hi in self.ranges):return
  if address==HELPERS['fatal']:self.fail();return
  unit=uc.reg_read(UC_X86_REG_RCX);assert unit in UNITS[1:]
  if address==HELPERS['room']:
   path=self.q(unit+0x38);self.ret(self.q(path+0x20) if path else 0);return
  assert address==HELPERS['alignment'] and unit==UNITS[1],('Undeclared room-list helper',hex(address))
  self.trace.append([0,1,self.p['alignment']]);self.ret(self.p['alignment'])
 def run(self,kind,p):
  self.p=p;self.trace=[];self.fatal=False;self.uc.mem_write(MEM,b'\0'*0x20000);self.uc.mem_write(STACK,b'\0'*0x40000)
  links={i:0 for i in range(1,4)};updates=links.copy()
  for chain,result in [(p['members'],links),(p['updates'],updates)]:
   for i,unit in enumerate(chain):result[unit]=chain[i+1] if i+1<len(chain) else 0
  if p['next']>=0:links[1]=p['next']
  if p['nextUpdate']>=0:updates[1]=p['nextUpdate']
  for i in range(1,4):
   path=PATHS[i] if i!=1 or p['path'] else 0;self.coords(UNITS[i],1,p['point'][0]&65535,p['point'][1]&65535,path)
   self.wq(PATHS[i]+0x20,ROOMS[p['room']] if i==1 else ROOMS[1]);self.wi(UNITS[i]+0x124,signed(p['flags'] if i==1 else 0x1234+i));self.wi(UNITS[i]+0x128,signed(p['flags128'] if i==1 else 0))
   self.uc.mem_write(UNITS[i]+0xb9,bytes([99 if i==1 else 10*i]));self.wq(UNITS[i]+0x160,UNITS[links[i]]);self.wq(UNITS[i]+0x150,UNITS[updates[i]])
  for i in range(1,3):
   self.uc.mem_write(ROOMS[i]+0x80,struct.pack('<4i',*p['bounds']));self.wq(ROOMS[i]+0xa8,UNITS[p['members'][0]] if p['members'] else 0)
   self.wq(ROOMS[i]+0x30,UNITS[p['updates'][0]] if p['updates'] else 0);self.wi(ROOMS[i]+0x44,p['count']);self.wq(ROOMS[i]+0x48,MARKS[i] if p['marker'] else 0)
   self.uc.mem_write(ROOMS[i]+0x54,bytes([p['roomFlags']]));self.wi(MARKS[i],7+i)
  rsp=STACK+0x20008;self.wq(rsp,STOP);self.uc.reg_write(UC_X86_REG_RSP,rsp);self.uc.reg_write(UC_X86_REG_RCX,UNITS[1] if p['present'] else 0);self.uc.reg_write(UC_X86_REG_RDX,ROOMS[p['targetRoom']])
  try:self.uc.emu_start(BASE+ENTRY[kind],STOP,count=100000)
  except unicorn.UcError:
   if not self.fatal:raise
  if not self.fatal:assert self.uc.reg_read(UC_X86_REG_RIP)==STOP,('Room-list instruction budget',kind,p)
  result=-1 if self.fatal else 1 if kind=='add' else 0
  units=[[UNITS.index(self.q(u+0x160)),UNITS.index(self.q(u+0x150)),self.i(u+0x124)&0xffffffff,self.i(u+0x128)&0xffffffff,int.from_bytes(self.uc.mem_read(u+0xb9,1),'little'),ROOMS.index(self.q(PATHS[i]+0x20)) if self.q(u+0x38) else -1] for i,u in enumerate(UNITS[1:],1)]
  rooms=[[UNITS.index(self.q(r+0xa8)),self.i(r+0x44),UNITS.index(self.q(r+0x30)),self.i(MARKS[i])] for i,r in enumerate(ROOMS[1:],1)]
  return [result,units,rooms,self.trace]
def params(**extra):
 p=dict(present=True,path=True,point=[20,30],room=1,targetRoom=1,bounds=[0,0,50,50],members=[2,1,3],updates=[2,1,3],next=-1,nextUpdate=-1,flags=0,flags128=0,alignment=2,count=2,roomFlags=0,marker=True);p.update(extra);return p
def cases():
 chains=[[],[1],[2,1],[1,2],[2,3,1],[2,3]]
 for kind,members,updates,alignment,flags in itertools.product(ENTRY,chains,chains,[0,1,2],[0,8192,4294967295]):yield kind,params(members=members,updates=updates,alignment=alignment,flags=flags)
 for kind,room,path,count,flags128,roomFlags,marker,point,bounds in itertools.product(ENTRY,[0,1,2],[False,True],[-1,0,1,2147483647],[0,0x4000000],[0,4],[False,True],[[20,30],[0,0],[65535,65535]],[[0,0,50,50],[20,30,1,1]]):
  yield kind,params(room=room,path=path,count=count,flags128=flags128,roomFlags=roomFlags,marker=marker,point=point,bounds=bounds)
 for kind,next,nextUpdate in itertools.product(ENTRY,[0,2,3],[0,2,3]):yield kind,params(room=0,next=next,nextUpdate=nextUpdate)
 for kind,targetRoom in itertools.product(ENTRY,[0,1]):yield kind,params(present=False,targetRoom=targetRoom)
 for kind in ENTRY:yield kind,params(targetRoom=0)
def main(check=False):
 em=Links();rows=[[kind,p,em.run(kind,p)] for kind,p in cases()];payload=json.dumps(rows,separators=(',',':'))+'\n'
 proof=dict(referenceVersion='3.3.93847',component='Current monster room registration/unlink/update removal and shared room touch',cases=len(rows),byFamily={k:sum(r[0]==k for r in rows) for k in ENTRY},unsupportedCases=sum(r[2][0]==-1 for r in rows),offlineMachineCodeExecuted=True,processMemoryReadDuringProbe=False,targetProcessCodeExecuted=False,worldMovementVerified=False,handlerEnabled=False,
  limits=['Complete add/remove/update-removal/touch bodies run for kind1 units on supplied dynamic paths and room/update lists. Unit-to-room projection and alignment remain declared services. All room/unit fields and alignment/fatal service order are compared.',
   'Duplicate membership,null units/rooms,missing markers,count underflow,missing membership,head/middle/tail unlink and retained partial writes are explicit controls. Arbitrary cyclic lists and other unit kinds remain unsupported/unverified. Global/native world ownership,visibility and automatic AI remain separate.'],
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_decisions.py',*em.artifacts,em.image.reference]},fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
 target=ROOT/'tools/reference/pet-room-links-source-evidence.json'
 if check:assert OUT.read_text()==payload and json.loads(target.read_text())==proof
 else:OUT.write_text(payload);target.write_text(json.dumps(proof,indent=2)+'\n')
 print('Current room links:',len(rows),'cases;',proof['byFamily'],';unsupported',proof['unsupportedCases'])
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
