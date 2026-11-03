#!/usr/bin/env python3
"""Execute complete current warp search,room update,packed and path-word helpers."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE,UC_HOOK_MEM_INVALID
from unicorn.x86_const import *
from probe_generic_pet_decisions import Emulator,BASE,STACK,MEM,STOP
from probe_warlock_runtime import ROOT,Image,digest
FRESH=ROOT/'.local/class-skills/generic-pet-live-follow-reference-20261103'
OUT=ROOT/'.local/class-skills/generic-pet-warp-primitives-source-probe.json'
GAME=MEM;UNIT=MEM+0x1000;ROOM=MEM+0x2000;AREA=MEM+0x3000;LEVEL=MEM+0x4000;MARK=MEM+0x5000;PATH=MEM+0x6000;RDATA=MEM+0x7000;ROUT=MEM+0x8000;OTHER=MEM+0x9000;OUTPUT=MEM+0xa000
ENTRY=dict(search=0x358f20,touch=0x2a3b30,packed=0x277ff0,word=0x266860)
ENTRY.update({'bound-'+k:v for k,v in list(ENTRY.items())})
FNS={k:BASE+v for k,v in dict(room=0x278450,roomat=0x2972c0,region=0x2e0ae0,candidate=0x428b20,fatal=0x7b6af0).items()}
signed=lambda v:struct.unpack('<i',struct.pack('<I',v&0xffffffff))[0]
class Primitives(Emulator):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for name,rvas in [('callees-follow-warp-placement-20261103.json',[ENTRY['search']]),('callees-follow-warp-leaves-20261103.json',[ENTRY[k] for k in ['touch','packed','word']])]:
   pp=FRESH/name;proof=json.loads(pp.read_text());self.artifacts.append(pp)
   assert proof['executableSha256']==self.image.ref['executable']['sha256'] and proof['bytesRead']<=8192
   assert not any(proof[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
   for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for rva in rvas:
    row=next(r for r in proof['records'] if int(r['rva'],16)==rva);assert row['allUnwindFragmentsCaptured']
    for f in row['fragments']:
     assert f['captured'] and f['invalidInstructionMarkers']==0
     paths=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(paths)==1
     p=paths[0];body=p.read_bytes();start=BASE+int(f['rva'],16);self.map(start,len(body));self.uc.mem_write(start,body);self.ranges.append((start,start+len(body)));self.artifacts.append(p)
  self.map(STACK,0x40000);self.map(MEM,0x20000);self.map(STOP,4096)
  for a in FNS.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200)
  self.uc.hook_add(UC_HOOK_CODE,self.hook);self.uc.hook_add(UC_HOOK_MEM_INVALID,self.invalid)
 def invalid(self,uc,access,address,size,value,user):
  assert address in [0,0x10,0x14,0x5c],('Unexpected primitive invalid memory',hex(address));self.trace.append([99]);self.fatal=True;uc.emu_stop();return False
 def ret(self,value):
  rsp=self.uc.reg_read(UC_X86_REG_RSP);dest=self.q(rsp);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,rsp+8);self.uc.reg_write(UC_X86_REG_RIP,dest)
 def hook(self,uc,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((n for n,v in FNS.items() if v==a),None);assert name,('Undeclared primitive helper',hex(a));reg=uc.reg_read;c,d,v=[reg(r) for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8]];p=self.p;result=0
  if name=='fatal':self.trace.append([99]);self.fatal=True;uc.emu_stop();return
  if name=='room':assert c==UNIT;self.trace.append([0,1,p['roomPresent']]);result=ROOM if p['roomPresent'] else 0
  elif name=='roomat':
   assert c==ROOM;token=p['rooms'][min(self.roomCalls,len(p['rooms'])-1)];self.roomCalls+=1;self.trace.append([1,1,signed(d),signed(v),token]);result=RDATA if token else 0
  elif name=='region':
   assert c==LEVEL;state=p['regionStates'][min(self.regionCalls,len(p['regionStates'])-1)];self.regionCalls+=1;self.trace.append([2,signed(d),signed(v),state]);result=ROUT if state is not None else 0
   if state is not None:self.wi(ROUT+0x28,state)
  elif name=='candidate':
   assert self.q(c)==GAME and self.q(c+8)==ROOM and self.q(c+16)==(AREA if p['area'] else 0)
   request=[1,1,int(p['area']),self.i(c+24),self.i(c+36),self.i(c+40),self.i(c+28),self.i(c+32),self.i(c+44),int.from_bytes(uc.mem_read(c+48,2),'little'),self.q(c+56)]
   result=p['responses'][min(self.calls,len(p['responses'])-1)];self.calls+=1;self.trace.append([3,*request,result])
  self.ret(result)
 def run(self,family,p):
  self.p=p;self.calls=0;self.roomCalls=0;self.regionCalls=0;self.trace=[];self.fatal=False;self.uc.mem_write(MEM,b'\0'*0x20000);self.uc.mem_write(STACK,b'\0'*0x40000)
  xy=p['point'] if p['kind'] in [2,4,5] else [x&65535 for x in p['point']]
  self.coords(UNIT,max(0,p['kind']),*xy,PATH if p['path'] else 0);self.wi(PATH+0x5c,p['word']);self.wi(UNIT+0x124,signed(p['flags']));self.wq(UNIT+0x150,[0,UNIT,OTHER][p['next']]);self.wq(ROOM+0x30,[0,UNIT,OTHER][p['head']]);self.uc.mem_write(ROOM+0x54,bytes([p['roomFlags']]));self.wq(ROOM+0x48,MARK if p['marker'] else 0);self.wi(MARK,p['mark'])
  self.uc.mem_write(ROOM+0x80,struct.pack('<8i',*p['bounds'],0,0,0,0));self.wq(ROOM+0xa0,p['seed']);self.uc.mem_write(AREA+0x10,struct.pack('<4i',*p['areaBounds']));self.wi(AREA+0x28,p['areaId']);self.wq(RDATA+0x18,LEVEL)
  self.wi(OUTPUT,p['out'][0]);self.wi(OUTPUT+4,p['out'][1]);unit=UNIT if p['kind']>=0 else 0
  for _ in range(p['repeat'] if family in ['touch','bound-touch','search'] else 1):
   rsp=STACK+0x20008;self.wq(rsp,STOP)
   args=[unit,0,0,0]
   if family in ['search','bound-search']:
    args=[GAME,ROOM,AREA if p['area'] else 0,p['classId']&0xffffffff];self.wq(rsp+0x28,OUTPUT);self.wq(rsp+0x30,OUTPUT+4);self.wq(rsp+0x38,0)
   for r,value in zip([UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9],args):self.uc.reg_write(r,value)
   self.uc.reg_write(UC_X86_REG_RSP,rsp)
   try:self.uc.emu_start(BASE+ENTRY[family],STOP,count=60000)
   except unicorn.UcError:
    if not self.fatal:raise
   if self.fatal:break
   assert self.uc.reg_read(UC_X86_REG_RIP)==STOP
  raw=self.uc.reg_read(UC_X86_REG_RAX);result=-1 if self.fatal else 0 if family in ['touch','bound-touch'] else raw&0xffffffff if family in ['packed','bound-packed'] else signed(raw)
  token=lambda ptr:{0:0,UNIT:1,OTHER:2}[ptr]
  return [result,self.q(ROOM+0xa0),[self.i(UNIT+0x124)&0xffffffff,token(self.q(UNIT+0x150)),token(self.q(ROOM+0x30)),self.i(MARK)],[self.i(OUTPUT),self.i(OUTPUT+4)],self.trace]
def defaults(**values):
 p=dict(kind=1,path=True,point=[20,30],word=2,roomPresent=1,roomFlags=0,flags=0,next=2,head=2,marker=True,mark=9,repeat=1,bounds=[40,50,25,25],seed=0x1234567812345678,area=False,areaBounds=[8,10,13,15],areaId=7,rooms=[1],regionStates=[7],responses=[1],classId=742,out=[777,-888]);p.update(values);return p
def cases():
 for area,seed,bounds,responses in itertools.product([False,True],[0,1,2,0xffffffff,0xffffffffffffffff],[[40,50,25,25],[0,0,1,1],[-2,-5,0,-1],[2147483647,-2147483648,2,2]],[[0],[1],[-7],[4294967296],[0,0,1],[0]*19+[1]]):yield 'search',defaults(area=area,seed=seed,bounds=bounds,responses=responses)
 for bounds,ids,rooms,states in itertools.product([[0,0,0,0],[-3,-2,0,1],[2147483647,-2147483648,-2147483648,2147483647]],[0,-1,7],[[0],[1],[0,1]],[[None],[7],[-1],[8,7]]):yield 'search',defaults(area=True,areaBounds=bounds,areaId=ids,rooms=rooms,regionStates=states,responses=[0,1])
 for kind,path,point,word in itertools.product([-1,0,1,2,3,4,5],[False,True],[[-1,-5],[0,0],[65535,65536],[2147483647,-2147483648]],[-2147483648,-1,0,1,255,2147483647]):
  yield 'packed',defaults(kind=kind,path=path,point=point,word=word)
  yield 'word',defaults(kind=kind,path=path,point=point,word=word)
 for kind,room,roomFlags,flags,marker,head,next,repeat in itertools.product([-1,1],[0,1],[0,4,255],[0,8192,4294967295],[False,True],[0,1,2],[0,1,2],[1,2]):yield 'touch',defaults(kind=kind,roomPresent=room,roomFlags=roomFlags,flags=flags,marker=marker,head=head,next=next,repeat=repeat)
 for family,response,area in itertools.product(['bound-search','bound-touch','bound-packed','bound-word'],[[0],[1],[-7]],[False,True]):yield family,defaults(out=[0,0],responses=response,area=area)
 for responses,seed in itertools.product([[0],[1],[0,1],[1,0]],[0,1,2]):yield 'search',defaults(responses=responses,seed=seed,repeat=2)
def main(check=False):
 em=Primitives();rows=[[f,p,em.run(f,p)] for f,p in cases()];payload=json.dumps(rows,separators=(',',':'))+'\n'
 proof=dict(referenceVersion='3.3.93847',component='Current warp search and room/coordinate/path primitives',cases=len(rows),byFamily={k:sum(r[0]==k for r in rows) for k in ENTRY},offlineMachineCodeExecuted=True,processMemoryReadDuringProbe=False,targetProcessCodeExecuted=False,worldMovementVerified=False,handlerEnabled=False,
  limits=['Actual warp caller arg7=0is executed;the unrelated nonzero restriction branch is not exposed by the implementation. Candidate validation remains an explicit64-bit result service.',
   'Room bounds,region geometry,room-owned RNG,room list and path fields are explicit source state;native region/search/collision/placement/room lifecycle bindings remain separate.',
   'Original placement and candidate bodies are freshly captured for subsequent comparison;this probe executes only search,room-update,packed-coordinate and path-word bodies.'],
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_decisions.py',*em.artifacts,em.image.reference]},fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
 out=ROOT/'tools/reference/generic-pet-warp-primitives-source-evidence.json'
 if check:assert OUT.read_text()==payload and json.loads(out.read_text())==proof
 else:OUT.write_text(payload);out.write_text(json.dumps(proof,indent=2)+'\n')
 print('Current warp primitives:',len(rows),'captured-code cases;',proof['byFamily'],';no world/learned claim.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
