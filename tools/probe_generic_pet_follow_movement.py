#!/usr/bin/env python3
"""Execute captured follow wrappers, fixed/random movement and destination together."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn.x86_const import *
from probe_generic_pet_movement import Movement,default,MON,MOVE,AUX,CTRL,EVENTS,GLOBAL,TABLE
from probe_generic_pet_decisions import BASE,STACK,MEM,STOP,GAME,PET,PATH,OWNER,OWNERPATH,PLAYER,CFG,ROOM,ROOMDATA,LEVEL,REGION
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/generic-pet-follow-movement-source-probe.json'
EXTRA={n:BASE+r for n,r in dict(moving=0x373f60,room=0x278450,roomat=0x2972c0,region=0x2e0ae0,direction=0x27aa90,warp=0x40e7d0).items()}
ENTRY=dict(point=0x425ac0,random=0x426210,alternate=0x4b1360,bound=0x4b1410,toward=0x4b14b0,destination=0x40ea70,follow=0x4b16c0)
signed=lambda v:struct.unpack('<i',struct.pack('<I',v&0xffffffff))[0]
class FollowMovement(Movement):
 def __init__(self):
  super().__init__()
  for name,rvas in [('callees-live-follow-services-idle-20261030.json',list(ENTRY.values())[:-1]),('callees-live-actions-execute-20261029.json',[ENTRY['follow']])]:
   pp=STAGE/name;proof=json.loads(pp.read_text());self.artifacts.append(pp)
   for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   for rva in rvas:
    row=next(r for r in proof['records'] if int(r['rva'],16)==rva);assert row['allUnwindFragmentsCaptured']
    for f in row['fragments']:
     paths=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(paths)==1
     path=paths[0];body=path.read_bytes();assert len(body)==int(f['endRva'],16)-int(f['rva'],16)
     start=BASE+int(f['rva'],16);self.map(start,len(body));self.uc.mem_write(start,body);self.ranges.append((start,start+len(body)));self.artifacts.append(path)
  self.tables={}
  for rva,n in [(0x17195d0,64),(0x1724b28,8),(0x17195b0,8),(0x1719650,8)]:
   body=self.image.bytes(rva,n);self.tables[hex(rva)]=list(body);self.map(BASE+rva,n);self.uc.mem_write(BASE+rva,body)
  for a in EXTRA.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
 def invalid(self,uc,access,address,size,value,user):
  assert address in [0x18,0x1a,0x20,0x28],('Unexpected invalid follow memory',hex(address));self.trace.append([99]);self.fatal=True;uc.emu_stop();return False
 def hook(self,uc,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  name=next((n for n,v in EXTRA.items() if v==a),None)
  if name is None:return super().hook(uc,a,size,user)
  c,d,v=[uc.reg_read(r) for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8]];p=self.p;result=0
  if name=='moving':assert c==OWNER;self.trace.append([20,p['moving']]);result=p['moving']
  elif name=='room':assert c==OWNER;self.trace.append([21]);result=ROOM
  elif name=='roomat':
   assert c in [0,ROOM,ROOMDATA];self.trace.append([22,int(c==ROOMDATA),signed(d),signed(v)]);result=0 if p['regionState']==0 else ROOMDATA;self.wq(ROOMDATA+0x18,LEVEL)
  elif name=='region':
   assert c==LEVEL;self.trace.append([23,signed(d),signed(v)]);ids=p['regionIds'];region=ids[min(self.regions,len(ids)-1)];self.regions+=1
   result=0 if p['regionState']==1 else REGION;self.wi(REGION+0x28,region)
  elif name=='direction':assert c==OWNER;self.trace.append([24,signed(d),signed(v)]);result=p['direction']
  elif name=='warp':assert c==GAME and d==OWNER and v==PET;self.trace.append([25,p['warp']]);result=p['warp']
  self.ret(result)
 def run(self,family,p):
  self.p=p;self.calls=0;self.regions=0;self.trace=[];self.fatal=False;self.uc.mem_write(MEM,b'\0'*0x40000);self.uc.mem_write(STACK,b'\0'*0x40000)
  xy=lambda kind,point:point if kind in [2,4,5] else [v&65535 for v in point]
  self.coords(PET,max(0,p['kind']),*xy(p['kind'],p['pet']),PATH if p['path'] else 0);self.wi(PET+4,742);self.wq(PET+0x28,p['seed'])
  self.wq(PET+0x10,MON if p['metadata'] else 0);self.wq(MON+0x40,MOVE if p['movement'] else 0);self.wq(MON+0x38,CTRL if p['control'] else 0)
  self.wi(MOVE+0x20,-11);self.wi(MOVE+0x24,22);self.uc.mem_write(MOVE+0x28,b'\x21');self.uc.mem_write(CTRL+0x38,struct.pack('<H',p['ai']));self.uc.mem_write(PATH+0xbf,b'\x2c')
  self.wq(PET+0x88,AUX if p['aux'] else 0);self.wi(AUX+0x1c,p['auxWord']);self.wi(AUX+0xb34,signed(p['auxFlags']));self.uc.mem_write(PET+0x1bd,bytes([p['selector']]));self.uc.mem_write(GAME+0x106,bytes([p['selector']]));self.wi(GAME+0x170,p['frame'])
  for i in [1,2,3]:self.wq(GLOBAL+i*16,TABLE)
  self.wi(TABLE+0x2d0,p['table']);self.uc.mem_write(MON+0xf1,bytes([max(0,p['monster'])]))
  self.coords(OWNER,p['ownerKind'],*xy(p['ownerKind'],p['owner']),OWNERPATH if p['ownerPath'] else 0);self.wq(OWNER+0x10,PLAYER if p['playerData'] else 0)
  for offset,value in zip([0x18,0x1a],p['future']):self.uc.mem_write(OWNERPATH+offset,struct.pack('<H',value&65535))
  self.wi(PLAYER+0x1c0,70);self.wi(PLAYER+0x1c4,80);self.wi(CFG+8,p['close']);self.wi(CFG+12,p['wander']);self.wi(CFG+0x70,p['word70'])
  self.events=[[i,*e] for i,e in enumerate(p['events'])]
  for i,t,frame in self.events:self.uc.mem_write(EVENTS+i*0x80,bytes([t]));self.wi(EVENTS+i*0x80+4,frame)
  self.relink();rsp=STACK+0x20008;self.wq(rsp,STOP);unit=PET if p['kind']>=0 else 0;args=[GAME,unit,p['x']&0xffffffff,p['y']&0xffffffff]
  if family=='destination':args=[OWNER,CFG+0x100,CFG+0x104,0]
  elif family=='follow':args=[GAME,unit,OWNER if p['ownerPresent'] else 0,p['mode']&0xffffffff];self.wq(rsp+0x28,CFG)
  elif family=='random':
   for n,value in enumerate([p['radius'],p['hint'],p['flags']],5):self.wq(rsp+n*8,value&0xffffffffffffffff)
  elif family=='alternate':
   for n,value in enumerate([p['hint'],p['speed'],p['tuning'],p['options']],5):self.wq(rsp+n*8,value&0xffffffffffffffff)
  elif family=='bound':
   for n,value in enumerate([p['hint'],p['radius'],p['speed'],p['tuning'],p['options']],5):self.wq(rsp+n*8,value&0xffffffffffffffff)
  elif family=='toward':
   for n,value in enumerate([0x1122334455667788,p['radius'],0x2233445566778899,p['tuning']],5):self.wq(rsp+n*8,value&0xffffffffffffffff)
  for r,value in zip([UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9],args):self.uc.reg_write(r,value)
  self.uc.reg_write(UC_X86_REG_RSP,rsp)
  try:self.uc.emu_start(BASE+ENTRY[family],STOP,count=60000)
  except unicorn.UcError:
   if not self.fatal:raise
  assert self.fatal or self.uc.reg_read(UC_X86_REG_RIP)==STOP
  raw=self.uc.reg_read(UC_X86_REG_RAX);result=-1 if self.fatal else 0 if family=='destination' else raw&255 if family in ['bound','alternate','toward','follow'] else signed(raw)
  seed=self.q(PET+0x28);state=[self.i(MOVE+0x20),self.i(MOVE+0x24),int(self.uc.mem_read(MOVE+0x28,1)[0]),int(self.uc.mem_read(PATH+0xbf,1)[0]),struct.unpack('<H',self.uc.mem_read(CTRL+0x38,2))[0],seed&0xffffffff,seed>>32]
  return [result,state,self.events,self.trace,[self.i(CFG+0x100),self.i(CFG+0x104)] if family=='destination' and not self.fatal else []]
def defaults(**fields):
 p=default();p.update(dict(pet=[20,30],owner=[45,55],future=[65,75],ownerKind=0,ownerPath=True,ownerPresent=True,playerData=True,moving=0,regionState=2,regionIds=[7],direction=0,warp=1,close=60,wander=50,word70=0,radius=4));p.update(fields);return p
def cases():
 for family,hint,radius,tuning,options,response in itertools.product(['alternate','bound','toward'],[-1,0,1,2],[0,1,4,255,256],[-1,0,77,100],[0,1,15],[-7,0,1,256]):
  yield family,defaults(hint=hint,radius=radius,tuning=tuning,options=options,responses=[response])
 for seed,radius,hint,flags in itertools.product([0,1,2,3,0xffffffff,0xffffffffffffffff],[0,1,2,3,4,255,256],[-1,0,1],[0,2,4,7]):yield 'random',defaults(seed=seed,radius=radius,hint=hint,flags=flags,responses=[0])
 for kind,path,response in itertools.product([-1,0,1,2,3,4,5],[0,1],[-7,0,1]):yield 'point',defaults(kind=kind,path=path,responses=[response])
 for kind,path,moving in itertools.product([0,1,2,3,4,5],[0,1],[0,1,256,257]):
  if kind in [2,4,5] and not path:continue
  yield 'destination',defaults(ownerKind=kind,ownerPath=bool(path),moving=moving,owner=[-1,65536],future=[-1,65536])
 for mode,moving,seed,responses,ids in itertools.product(range(6),[0,1],[1,2,0xffffffffffffffff],[[0],[1],[0,0,1],[0]*18+[1]],[[7],[7,8,8,7]]):
  yield 'follow',defaults(mode=mode,moving=moving,seed=seed,responses=responses,regionIds=ids)
 for mode in range(6):yield 'follow',defaults(mode=mode,ownerPresent=False)
 for move in [-7,0,1,256]:yield 'toward',defaults(move=move,radius=255,tuning=100)
 for mode,region,kind,data in itertools.product(range(6),[0,1],[0,1,2],[False,True]):
  yield 'follow',defaults(mode=mode,regionState=region,ownerKind=kind,playerData=data,responses=[0])
 for family,kind,metadata,movement in itertools.product(['alternate','bound','toward'],[0,1],[0,1],[0,1]):
  yield family,defaults(kind=kind,metadata=metadata,movement=movement,responses=[0])
def main(check=False):
 em=FollowMovement();rows=[[family,p,em.run(family,p)] for family,p in cases()];payload=json.dumps(rows,separators=(',',':'))+'\n'
 evidence=dict(referenceVersion='3.3.93847',component='Current composed follow wrappers,point/random movement and owner destination',cases=len(rows),byFamily={k:sum(r[0]==k for r in rows) for k in ENTRY},offlineMachineCodeExecuted=True,processMemoryRead=False,targetProcessCodeExecuted=False,worldMovementVerified=False,handlerEnabled=False,immutableTables=em.tables,
  limits=['Complete captured follow executor,wrappers,destination,fixed/random movement,mode wrapper,preparation and think queue execute together. Mode initialization/acceptance,table records,425bb0movement,warp,moving predicate and native geometry remain supplied services.',
   'Cfg+0x70 remains neutral and supplied. Actor-world clock/seed persistence,movement animation/speed,path/warp and complete combat/learned lifecycle are separate acceptance.'],
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_movement.py',ROOT/'tools/probe_generic_pet_decisions.py',ROOT/'tools/probe_generic_pet_actions.py',*em.artifacts,em.image.reference]},fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
 out=ROOT/'tools/reference/generic-pet-follow-movement-source-evidence.json'
 if check:assert OUT.read_text()==payload and json.loads(out.read_text())==evidence
 else:OUT.write_text(payload);out.write_text(json.dumps(evidence,indent=2)+'\n')
 print('Current composed follow movement:',len(rows),'captured-code cases;',evidence['byFamily'],';no world/learned claim.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
