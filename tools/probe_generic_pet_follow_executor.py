#!/usr/bin/env python3
"""Execute the complete captured current six-way follow executor offline."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_warlock_runtime import ROOT,STAGE,Image,digest
from probe_generic_pet_decisions import Emulator,BASE,STACK,MEM,STOP,GAME,PET,OWNER,PATH,OWNERPATH,PLAYER,CFG,ROOM,ROOMDATA,LEVEL,REGION
OUT=ROOT/'.local/class-skills/generic-pet-follow-executor-source-probe.json'
ENTRY=0x4b16c0
FNS={k:BASE+v for k,v in dict(destination=0x40ea70,room=0x278450,roomat=0x2972c0,region=0x2e0ae0,direction=0x27aa90,
    prepare=0x4b1220,point=0x425ac0,move=0x426210,monster=0x6bd00,bound=0x4b1410,alternate=0x4b1360,
    toward=0x4b14b0,warp=0x40e7d0,think=0x4b12d0).items()}
MONSTER=MEM+0x12000
signed=lambda v:struct.unpack('<i',struct.pack('<I',v&0xffffffff))[0]
class FollowExecutor(Emulator):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  proofpath=STAGE/'callees-live-actions-execute-20261029.json';proof=json.loads(proofpath.read_text());self.artifacts.append(proofpath)
  for name,h in proof['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
  row=next(x for x in proof['records'] if int(x['rva'],16)==ENTRY);assert row['allUnwindFragmentsCaptured'] and len(row['fragments'])==1
  f=row['fragments'][0];p=STAGE/'callees-live-actions-execute-20261029-4b16c0-part0.bin';code=p.read_bytes();assert len(code)==2460 and digest(code)==f['sha256'];self.artifacts.append(p)
  self.map(BASE+ENTRY,len(code));self.uc.mem_write(BASE+ENTRY,code);self.ranges=[(BASE+ENTRY,BASE+ENTRY+len(code))]
  self.tables={}
  for rva,n in [(0x17195d0,64),(0x1724b28,8),(0x17195b0,8),(0x1719650,8)]:
   data=self.image.bytes(rva,n);self.tables[hex(rva)]=list(data);self.map(BASE+rva,n);self.uc.mem_write(BASE+rva,data)
  self.map(STACK,0x40000);self.map(MEM,0x20000);self.map(STOP,0x1000)
  for addr in FNS.values():self.map(addr,1);self.uc.mem_write(addr,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def hook(self,uc,address,size,user):
  if any(a<=address<b for a,b in self.ranges):return
  name=next((n for n,a in FNS.items() if a==address),None);assert name,('Undeclared code',hex(address))
  reg=uc.reg_read;rsp=reg(UC_X86_REG_RSP);c,d,v,z=[reg(r) for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]]
  arg=lambda n:self.q(rsp+8*n);ret=0;p=self.p
  if name=='destination':assert c==OWNER;self.wi(d,p['destination'][0]);self.wi(v,p['destination'][1]);self.trace.append([0,*p['destination']])
  elif name=='room':assert c==OWNER;self.trace.append([1]);ret=ROOM
  elif name=='roomat':
   assert c in [0,ROOM,ROOMDATA];self.trace.append([2,int(c==ROOMDATA),signed(d),signed(v)])
   ret=0 if p['regionState']==0 else ROOMDATA;self.wq(ROOMDATA+0x18,LEVEL)
  elif name=='region':
   assert c==LEVEL;self.trace.append([3,signed(d),signed(v)])
   ids=p.get('regionIds',[p['regionId']]);region=ids[min(self.regions,len(ids)-1)];self.regions+=1
   ret=0 if p['regionState']==1 else REGION;self.wi(REGION+0x28,region)
  elif name=='direction':assert c==OWNER;self.trace.append([4,signed(d),signed(v)]);ret=p['direction']
  elif name=='monster':assert c==p['selector'] and signed(d)==742;self.trace.append([5,p['selector'],742]);ret=MONSTER if p['monsterFlags']>=0 else 0;self.uc.mem_write(MONSTER+0xf1,bytes([max(0,p['monsterFlags'])]))
  elif name=='prepare':assert c==PET;self.trace.append([6,signed(d),v&255,signed(z)])
  elif name=='think':assert c==GAME and d==PET;self.trace.append([7])
  else:
   assert c==GAME
   if name=='warp':assert d==OWNER and v==PET;request=[13]
   else:
    assert d==PET
    request=[{'point':8,'move':9,'bound':10,'alternate':11,'toward':12}[name],signed(v),signed(z)]
    if name=='move':request += [arg(5)&255,signed(arg(6)),arg(7)&65535]
    elif name=='bound':request += [signed(arg(5)),arg(6)&255,signed(arg(7)),arg(8)&255,signed(arg(9))]
    elif name=='alternate':request += [signed(arg(5)),signed(arg(6)),arg(7)&255,signed(arg(8))]
    elif name=='toward':request += [arg(6)&255,arg(8)&255] # unused stack5/7 are not invented
   ret=p['responses'][min(self.calls,len(p['responses'])-1)];self.calls+=1;self.trace.append([*request,ret])
  uc.reg_write(UC_X86_REG_RAX,ret&0xffffffffffffffff);uc.reg_write(UC_X86_REG_RSP,rsp+8);uc.reg_write(UC_X86_REG_RIP,self.q(rsp))
 def execute(self,p):
  self.p=p;self.trace=[];self.calls=0;self.regions=0;self.uc.mem_write(MEM,b'\0'*0x20000);self.uc.mem_write(STACK,b'\0'*0x40000)
  xy=lambda kind,point:point if kind in [2,4,5] else [v&65535 for v in point]
  self.coords(PET,p['petKind'],*xy(p['petKind'],p['pet']),PATH if p['petPath'] else 0);self.wi(PET+4,742);self.wq(PET+0x28,p['seed'])
  self.coords(OWNER,p['ownerKind'],*xy(p['ownerKind'],p['owner']),OWNERPATH if p['ownerPath'] else 0);self.wq(OWNER+0x10,PLAYER if p['playerData'] else 0)
  self.wi(PLAYER+0x1c0,p['playerDestination'][0]);self.wi(PLAYER+0x1c4,p['playerDestination'][1]);self.wi(CFG+8,p['close']);self.wi(CFG+12,p['wander']);self.wi(CFG+0x70,p['word70']);self.uc.mem_write(GAME+0x106,bytes([p['selector']]))
  rsp=STACK+0x20008;self.wq(rsp,STOP);self.wq(rsp+0x28,CFG)
  for r,value in zip([UC_X86_REG_RSP,UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9],[rsp,GAME,PET if p['petPresent'] else 0,OWNER if p['ownerPresent'] else 0,p['mode']&0xffffffff]):self.uc.reg_write(r,value)
  self.uc.emu_start(BASE+ENTRY,STOP,count=30000);assert self.uc.reg_read(UC_X86_REG_RIP)==STOP
  return [self.uc.reg_read(UC_X86_REG_RAX)&255,self.q(PET+0x28),self.trace]
def defaults(**values):
 p=dict(petPresent=True,ownerPresent=True,petKind=1,ownerKind=0,petPath=True,ownerPath=True,pet=[20,30],owner=[45,55],destination=[65,75],playerDestination=[70,80],playerData=True,mode=0,selector=1,regionState=2,regionId=7,direction=0,monsterFlags=128,close=60,wander=50,word70=0,seed=0x1234567812345678,responses=[1]);p.update(values);return p
def cases():
 # Each success/failure position exercises the exact fallback order. Cross
 # independent coordinates, signed tuning, flags and all six jump-table arms.
 for mode,owner,kind,path,word,responses in itertools.product(range(6),[False,True],[0,1,2,4,5],[False,True],[-100,-1,0,1,19,58,100,2147483647],[[0],[1],[-7],[0,1],[0,0,1],[0,0,0,1]]):
  if kind in [2,4,5] and not path:continue
  yield defaults(mode=mode,ownerPresent=owner,ownerKind=kind,ownerPath=path,word70=word,responses=responses)
 for direction,region,responses in itertools.product(range(64),[0,1,2],[[0],[0,0,0,1],[1]]):yield defaults(direction=direction,regionState=region,responses=responses)
 for mode,seed,wander,owner in itertools.product(range(6),[1,2,3,0xffffffff,0xdeadbeef12345678],[-1,0,1,50,100,101],[False,True]):yield defaults(mode=mode,seed=seed,wander=wander,ownerPresent=owner,responses=[0])
 for mode in [-1,6,255]:yield defaults(mode=mode)
 for mode in range(6):yield defaults(mode=mode,petPresent=False)
 for xy in [[-1,-5],[-200,-301],[65535,65535],[2147483647,-2147483648]]:
  for mode in range(6):yield defaults(mode=mode,pet=xy,owner=xy,destination=xy,ownerKind=4,petKind=4,responses=[0])
 for mode,flags,data,responses in itertools.product(range(6),[-1,0,1,127,128,255],[False,True],[[256],[257],[-256],[0,0,0,0,0,-7],[0]*18+[1]]):
  yield defaults(mode=mode,monsterFlags=flags,playerData=data,responses=responses)
 for direction,ids,responses in itertools.product(range(64),[[7,8],[7,8,8,7],[7,-1,0,7]],[[0],[1],[0,1]]):
  yield defaults(direction=direction,regionIds=ids,responses=responses)
 for kind,path,mode in itertools.product([0,1,2,3,4,5],[False,True],range(6)):
  if kind in [2,4,5] and not path:continue
  yield defaults(petKind=kind,petPath=path,mode=mode,word70=-2147483648,pet=[-1,65536],responses=[0])
def main(check=False):
 em=FollowExecutor();rows=[[p,em.execute(p)] for p in cases()];payload=json.dumps(rows,separators=(',',':'))+'\n'
 evidence=dict(referenceVersion='3.3.93847',component='Complete captured six-way GenericPet follow executor with explicit leaf services',cases=len(rows),codeBytes=2460,jumpTableBytes=24,
  processMemoryRead=False,targetProcessCodeExecuted=False,offlineMachineCodeExecuted=True,currentWorldMovementVerified=False,handlerEnabled=False,
  immutableTables=em.tables,
  services=['Original owner destination helper,room/coordinate/region lookup,direction and monster-table byte are explicit supplied services; no source room geometry or destination interpretation is invented.',
   'Movement preparation,point/move/wrapper/warp and think-event callbacks are supplied independently with complete ordered traces and raw return values. Unused stack words are not assigned gameplay meaning.',
   'Cfg+0x70 is a neutral explicit signed field. Its actor-world producer remains unverified; raw byte narrowing and signed division/wrapping are retained.'],
  established=['All six guarded jump-table arms execute complete original captured bytes; null pet/owner,all unit coordinate routes,64direction inputs,region failures,response fallback order,negative controls and inline actor RNG are covered.',
   'Mode0tries eight directional offsets around the owner helper destination with same-region checks and point/midpoint fallbacks,then current-owner movement fallbacks.',
   'Mode1uses current movement wrappers and preserves walk/run metadata flag plus exact0/15/1fallback order;mode2has raw signed wander chance and actor-seed draws;mode3calls the owner/pet warp helper;4/5use distinct destination/current-owner wrapper branches.'],
  limits=['Complete wrapper/path/warp/event effects and derivation of field0x70 remain next; source code execution with declared leaves does not certify actual follow movement or a learned summon.'],
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),*em.artifacts,em.image.reference]},
  fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
 ep=ROOT/'tools/reference/generic-pet-follow-executor-source-evidence.json'
 if check:assert OUT.read_text()==payload and json.loads(ep.read_text())==evidence
 else:OUT.write_text(payload);ep.write_text(json.dumps(evidence,indent=2)+'\n')
 print('Current GenericPet follow executor:',len(rows),'complete captured-code cases; six arms/RNG/traces; no world/learned claim.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
