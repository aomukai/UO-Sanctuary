#!/usr/bin/env python3
"""Execute complete current warp recovery alone and through the real follow caller."""
import argparse,itertools,json
from pathlib import Path
from unicorn import UC_HOOK_MEM_READ
from unicorn.x86_const import *
from probe_generic_pet_motion_services import MotionServices,params,FRESH
from probe_generic_pet_decisions import BASE,MEM,GAME,PET,OWNER,ROOM,ROOMDATA,LEVEL,REGION
from probe_generic_pet_movement import TABLE
from probe_warlock_runtime import ROOT,digest
OUT=ROOT/'.local/class-skills/generic-pet-warp-source-probe.json'
ENTRY=BASE+0x40e7d0
LEAVES={n:BASE+r for n,r in dict(room=0x278450,region=0x2e0ae0,find=0x358f20,roomat=0x2972c0,place=0x3739a0,packed=0x277ff0,size=0x266860,collision=0x2a5740,after=0x2a3b30,idle=0x425550).items()}
signed=lambda v:(v&0x7fffffff)-(v&0x80000000)
class Warp(MotionServices):
 def __init__(self):
  super().__init__();pp=FRESH/'callees-follow-warp-20261103.json';proof=json.loads(pp.read_text());self.artifacts.append(pp)
  for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  row=proof['records'][0];assert row['rva']=='0x40e7d0' and row['allUnwindFragmentsCaptured']
  for f in row['fragments']:
   assert f['captured'] and f['invalidInstructionMarkers']==0
   paths=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(paths)==1
   path=paths[0];body=path.read_bytes();assert len(body)==669;self.map(ENTRY,len(body));self.uc.mem_write(ENTRY,body);self.ranges.append((ENTRY,ENTRY+len(body)));self.artifacts.append(path)
  for a in LEAVES.values():
   if not any(lo<=a<hi for lo,hi in self.ranges):self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.hook_add(UC_HOOK_MEM_READ,self.table_read,begin=TABLE+0xf98,end=TABLE+0xf9b)
 def table_read(self,uc,access,address,size,value,user):
  assert address==TABLE+0xf98 and size==4;self.trace.append([30,self.p['selector'],self.p['count']])
 def invalid(self,uc,access,address,size,value,user):
  if address in [0x10,0x14]:self.trace.append([99]);self.fatal=True;uc.emu_stop();return False
  return super().invalid(uc,access,address,size,value,user)
 def token(self,p):
  assert p in [0,ROOM,ROOMDATA];return {0:0,ROOM:1,ROOMDATA:2}[p]
 def hook(self,uc,a,size,user):
  first=not self.initialized
  if first:
   super().hook(uc,a,size,user);p=self.p;self.wi(PET+4,p['classId']);self.wi(PET+0x128,signed(p['flags128']));self.wi(TABLE+0xf98,p['count']);self.wq(ROOM+0x18,LEVEL);self.wq(ROOMDATA+0x18,LEVEL)
   if self.family=='warp':
    uc.reg_write(UC_X86_REG_RDX,OWNER if p['ownerPresent'] else 0);uc.reg_write(UC_X86_REG_R8,PET if p['kind']>=0 else 0);uc.reg_write(UC_X86_REG_RIP,ENTRY);return
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((n for n,r in LEAVES.items() if r==a),None)
  if name is None:return super().hook(uc,a,size,user)
  r=uc.reg_read;c,d,v,z=[r(x) for x in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];rsp=r(UC_X86_REG_RSP);arg=lambda n:self.q(rsp+n*8);p=self.p;result=0;pet=PET if p['kind']>=0 else 0
  if name=='room':assert c==(OWNER if p['ownerPresent'] else 0);self.trace.append([31,p['ownerRoom']]);result=ROOM if p['ownerRoom'] else 0
  elif name=='region':assert c==LEVEL;self.trace.append([32,signed(d),signed(v),p['warpRegion']]);result=REGION if p['warpRegion'] else 0
  elif name=='find':
   assert c==GAME and d==ROOM and v in [0,REGION] and arg(7)==0;self.trace.append([33,self.token(d),int(v!=0),signed(z),0,p['findResult'],*p['found']]);self.wi(arg(5),p['found'][0]);self.wi(arg(6),p['found'][1]);result=p['findResult']
  elif name=='roomat':
   token=p['rooms'][min(self.roomCalls,len(p['rooms'])-1)];self.roomCalls+=1;self.trace.append([34,self.token(c),signed(d),signed(v),token]);result=[0,ROOM,ROOMDATA][token]
  elif name=='place':
   assert c==GAME and d==pet;result=p['placements'][min(self.placeCalls,len(p['placements'])-1)];self.placeCalls+=1;self.trace.append([35,int(pet!=0),self.token(v),signed(z),signed(arg(5)),signed(arg(6)),signed(arg(7)),result])
  elif name=='packed':assert c==OWNER;self.trace.append([36,p['packed']]);result=p['packed']
  elif name=='size':assert c==pet;self.trace.append([37,int(pet!=0),p['size']]);result=p['size']
  elif name=='collision':assert arg(5)==0xc01;self.trace.append([38,self.token(c),signed(d),signed(v),signed(z),0xc01,p['collision']]);result=p['collision']
  elif name=='after':assert c==pet;self.trace.append([39,int(pet!=0),self.i(PET+0x128)&0xffffffff])
  elif name=='idle':assert c==GAME and d==pet;self.trace.append([40,int(pet!=0),signed(v),self.i(PET+0x128)&0xffffffff])
  self.ret(result)
 def run(self,family,p):
  self.family=family;self.roomCalls=0;self.placeCalls=0;result=super().run('follow',p)
  return [*result,self.i(PET+0x128)&0xffffffff]
def defaults(**values):
 p=params(mode=3);p.update(dict(classId=742,flags128=0x1234,count=1000,ownerRoom=1,warpRegion=1,findResult=1,found=[65,75],rooms=[2],placements=[1],packed=0x0037002d,size=2,collision=0));p.update(values);return p
def cases():
 for family,find,rooms,placements,collision in itertools.product(['warp','follow'],[-7,0,1],[[0],[2],[2,0],[0,2]],[[0],[1],[-7],[0,1],[0,0,1]],[-1,0,1]):
  yield family,defaults(findResult=find,rooms=rooms,placements=placements,collision=collision)
 for classId,selector,count,kind in itertools.product([-2147483648,-65537,-32769,-32768,-1,0,741,742,743,32767,32768,65535,65536,2147483647],[0,1,2,3,4,255],[-1,0,742,743,32768],[-1,1]):
  yield 'warp',defaults(classId=classId,selector=selector,count=count,kind=kind)
 for kind,path,room in itertools.product([0,1,2,3,4,5],[False,True],[0,1]):
  yield 'warp',defaults(ownerKind=kind,ownerPath=path,ownerRoom=room,owner=[-1,-5],findResult=0)
 for flags,packed,found,placements in itertools.product([0,1,65536,4294967295],[0,1,0xffffffff,0x8000ffff],[[0,0],[-1,-5],[2147483647,-2147483648]],[[1],[0,0,1]]):
  yield 'warp',defaults(flags128=flags,packed=packed,found=found,placements=placements,rooms=[2])
 for family in ['warp','follow']:yield family,defaults(ownerPresent=False,ownerRoom=0)
 for family,region,size,room in itertools.product(['warp','follow'],[0,1],[-2147483648,0,1,255],[0,1]):
  yield family,defaults(warpRegion=region,size=size,ownerRoom=room,findResult=0,placements=[0,1])
 for find,placements in itertools.product([0,256],[[256],[0,256],[0,0,256]]):
  yield 'warp',defaults(findResult=find,placements=placements)
def main(check=False):
 em=Warp();rows=[[f,p,em.run(f,p)] for f,p in cases()];payload=json.dumps(rows,separators=(',',':'))+'\n'
 evidence=dict(referenceVersion='3.3.93847',component='Complete current warp recovery and actual follow caller',cases=len(rows),byFamily={k:sum(r[0]==k for r in rows) for k in ['warp','follow']},offlineMachineCodeExecuted=True,processMemoryReadDuringProbe=False,targetProcessCodeExecuted=False,worldMovementVerified=False,handlerEnabled=False,
  limits=['Original669-byte warp body and caller execute offline with explicit region/search/placement/packed-coordinate/size/collision/post-placement/idle services; actual room search,path relocation and scheduling remain separate world integration.',
   'Signed16class selection,table bounds,current room loss,full integer leaf results,packed fallback coordinates,collision mask,force retry and success-only post-placement/flag/idle ordering are covered. No teleport or live summon handler is enabled.'],
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_motion_services.py',ROOT/'tools/probe_generic_pet_follow_movement.py',ROOT/'tools/probe_generic_pet_movement.py',ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_generic_pet_actions.py',*em.artifacts,em.image.reference]},fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
 out=ROOT/'tools/reference/generic-pet-warp-source-evidence.json'
 if check:assert OUT.read_text()==payload and json.loads(out.read_text())==evidence
 else:OUT.write_text(payload);out.write_text(json.dumps(evidence,indent=2)+'\n')
 print('Current GenericPet warp:',len(rows),'captured-code cases;',evidence['byFamily'],';no world/learned claim.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
