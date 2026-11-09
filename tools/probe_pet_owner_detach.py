#!/usr/bin/env python3
"""Current player-pet detachment with actual store and packed-context helpers."""
import argparse,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE,UC_HOOK_MEM_INVALID
from unicorn.x86_const import *
from probe_generic_pet_parameters import Initializer,BASE,STACK,OBJECTS,STOP
from probe_warlock_runtime import ROOT,STAGE,digest
OUT=ROOT/'.local/class-skills/pet-owner-detach-source'
OWNER=OBJECTS+0x1000;PET=OBJECTS+0x2000;DATA=OBJECTS+0x3000;STORE=OBJECTS+0x4000;SLOTS=OBJECTS+0x5000;NODES=OBJECTS+0x6000;TABLE=OBJECTS+0x8000;PDATA=OBJECTS+0xa000;CLIENT=OBJECTS+0xb000;NETVT=OBJECTS+0xc000
F={k:BASE+v for k,v in dict(remove=0x35c720,apply=0x2ce9b0,flush=0x358e80,broadcast=0x3722a0,fatal=0x7b6af0,cookie=0x155eeb0).items()};F['send']=STOP+0x1010
class Detach(Initializer):
 def __init__(self):
  assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for name,wanted in [('callees-pet-owner-loss-links-20261109.json',{'0x35c970'}),('callees-pet-detach-store-20261109.json',{'0x35d0e0','0x35b600','0x35d410'}),('callees-pet-detach-packed-20261109.json',{'0x3706f0'})]:
   path=STAGE/name;cap=json.loads(path.read_text());self.artifacts.append(path)
   for n,h in cap['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
   rows=[r for r in cap['records']if r['rva']in wanted];assert len(rows)==len(wanted)
   for row in rows:
    assert row['allUnwindFragmentsCaptured']
    for f in row['fragments']:
     path=next(ROOT/n for n,h in cap['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=path.read_bytes();a=BASE+int(f['rva'],16);self.map(a,len(raw));self.uc.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(path)
  self.map(OBJECTS,0x20000);self.map(STACK,0x40000);self.map(STOP,0x2000);self.map(BASE+0x197a178,8);self.map(BASE+0x1e03ac0,64);self.map(BASE+0x19f38a0,8)
  for a in F.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
  self.uc.hook_add(UC_HOOK_CODE,self.hook);self.uc.hook_add(UC_HOOK_MEM_INVALID,self.invalid)
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200)
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<I',v&0xffffffff))
 def si(self,a):return struct.unpack('<i',self.uc.mem_read(a,4))[0]
 def ret(self,value=0):
  sp=self.uc.reg_read(UC_X86_REG_RSP);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,sp+8);self.uc.reg_write(UC_X86_REG_RIP,self.q(sp))
 def invalid(self,u,access,address,size,value,user):self.unsupported=True;u.emu_stop();return False
 def hook(self,u,a,size,user):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  c,d,v,z=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]];name=next((n for n,x in F.items()if x==a),None);p=self.p
  if name=='cookie':self.ret(u.reg_read(UC_X86_REG_RAX));return
  if name=='remove':
   assert c==(OBJECTS if p['game']else 0)and d==OWNER and z==0;self.trace.append('remove:'+str((v+2**31)%2**32-2**31))
   if p['changeId']is not None:self.wi(PET+8,p['changeId'])
   if p['dropStore']:self.putq(DATA+0x98,0)
   if p['changeSelector']is not None:u.mem_write(OBJECTS+0x106,bytes([p['changeSelector']]))
   if p['changeCount']is not None:self.wi(TABLE+0x1300,p['changeCount'])
   self.ret();return
  if name=='apply':
   assert c==(PET if p['pet']else 0);self.trace.append(f'apply:{d}:{(v+2**31)%2**32-2**31}');self.applyFlags=struct.unpack('<I',u.mem_read(NODES,4))[0]
   if p['afterApplyKind']is not None:self.wi(OWNER,p['afterApplyKind'])
   if p['afterApplyField']is not None:u.mem_write(NODES+12,struct.pack('<H',p['afterApplyField']))
   self.ret(p['result']);return
  if name=='flush':assert c==CLIENT;self.trace.append('flush:'+str(self.si(CLIENT)));self.wi(CLIENT,p['afterFlushId']);self.ret();return
  if name=='send':
   assert c==BASE+0x19f38a0;begin,end=self.q(v),self.q(v+8);assert end-begin==7;raw=bytes(u.mem_read(begin,7));assert raw[0]==0x9b
   self.trace.append(f'send:{d}:{struct.unpack_from("<H",raw,1)[0]}:{struct.unpack_from("<i",raw,3)[0]}');self.ret();return
  if name=='broadcast':
   assert c==OBJECTS and d==BASE+0x35b3a0;raw=bytes(u.mem_read(v,28));assert raw[4:]==bytes(24)
   self.trace.append('broadcast:'+str(struct.unpack_from('<i',raw)[0]));self.ret();return
  if name=='fatal':self.unsupported=True;u.emu_stop();return
  raise AssertionError(('Unknown instruction',hex(a)))
 def run(self,p):
  self.p=p;u=self.uc;u.mem_write(OBJECTS,bytes(0x20000));u.mem_write(STACK,bytes([p['tail']])*0x40000);self.trace=[];self.unsupported=False;self.applyFlags=None
  self.wi(OWNER,p['kind']);u.mem_write(OWNER+0x1bd,bytes([p['ownerSelector']]));self.putq(OWNER+0x10,DATA if p['data']else 0);self.putq(DATA+0x98,STORE if p['store']else 0);self.putq(STORE,SLOTS)
  self.putq(SLOTS+p['type']*32,NODES)
  for i,guid in enumerate(p['ids']):
   node=NODES+i*32;self.wi(node,p['flags'][i]);self.wi(node+4,guid);self.wi(node+8,0x12340000+i);u.mem_write(node+12,struct.pack('<H',p['field0c']+i));self.wi(node+16,500+i);self.putq(node+24,node+32 if i+1<len(p['ids'])else 0)
  self.wi(PET,p['petKind']);self.wi(PET+8,p['id']);self.putq(PET+0x10,PDATA if p['petData']else 0);u.mem_write(PDATA+0x30,bytes([4 if p['snapshot']else 0]));self.putq(PDATA+0x8c,p['snapshotValue'])
  self.wi(OBJECTS+0x108,0x12345678);u.mem_write(OBJECTS+0x104,b'\x02');u.mem_write(OBJECTS+0x106,bytes([p['gameSelector']]));u.mem_write(OBJECTS+0x101,b'\x03')
  for s in [1,2,3]:self.putq(BASE+0x1e03ac0+s*16,TABLE)
  self.wi(TABLE+0x1300,p['count']);self.putq(DATA+0xf0,CLIENT if p['client']else 0);self.wi(CLIENT,31);self.putq(BASE+0x19f38a0,NETVT);self.putq(NETVT+0x28,F['send'])
  owner=OWNER if p['owner']else 0;pet=PET if p['pet']else 0;game=OBJECTS if p['game']else 0;action=p['action'];entry={'detach':0x35c970,'type':0x35d0e0,'data':0x35d410,'bucket':0x35b600,'packed':0x3706f0}[action]
  args=(game,owner,pet)if action=='detach'else(owner,p['id'],0)if action in ['type','data']else(p['gameSelector'],STORE,p['bucketType'])if action=='bucket'else(game,pet,0)
  sp=STACK+0x20008;self.putq(sp,STOP)
  for reg,value in zip([UC_X86_REG_RSP,UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8],[sp,*args]):u.reg_write(reg,value&0xffffffffffffffff)
  try:u.emu_start(BASE+entry,STOP,count=10000)
  except unicorn.UcError:assert self.unsupported
  if not self.unsupported:assert u.reg_read(UC_X86_REG_RIP)==STOP
  result=u.reg_read(UC_X86_REG_RAX)
  if self.unsupported or action=='detach':result=None
  elif action=='type':result=result&0xffffffff
  elif action=='data':result=(result-NODES-8)//32+1 if result else 0
  elif action=='bucket':result=(result-SLOTS)//32 if result else None
  return dict(result=result,unsupported=self.unsupported,trace=self.trace.copy(),flags=[struct.unpack('<I',u.mem_read(NODES+i*32,4))[0]for i in range(len(p['ids']))],id=self.si(PET+8),applyFlags=self.applyFlags)
def sample(**changes):
 p=dict(action='detach',owner=True,game=True,kind=0,data=True,store=True,ownerSelector=1,gameSelector=1,count=21,type=2,ids=[17,18,19],flags=[0xabc00010,0x80000000,0],id=17,pet=True,petKind=1,petData=True,snapshot=False,snapshotValue=0x1122334455667788,tail=0,client=True,field0c=65530,result=-71,afterFlushId=32,afterApplyKind=None,afterApplyField=None,changeId=None,dropStore=False,changeSelector=None,changeCount=None,bucketType=2)
 p.update(changes);return p
def build(check=False):
 em=Detach();cases=[]
 def add(**changes):p=sample(**changes);cases.append(dict(input=p,output=em.run(p)))
 for action in ['detach','type','data']:
  for changes in [{},dict(owner=False),dict(kind=1),dict(data=False),dict(store=False),dict(ownerSelector=0),dict(ownerSelector=4),dict(count=0),dict(count=2),dict(id=99),dict(type=7),dict(flags=[0xffffffff,0x80000000,0])]:add(action=action,**changes)
 for changes in [dict(pet=False),dict(game=False),dict(client=False),dict(gameSelector=2),dict(gameSelector=0),dict(changeId=18),dict(changeId=99),dict(dropStore=True),dict(changeCount=0),dict(changeSelector=3),dict(afterApplyKind=1),dict(afterApplyField=65535),dict(snapshot=True),dict(snapshot=True,snapshotValue=1<<40),dict(petKind=0,snapshot=True),dict(petData=False,snapshot=True),dict(ids=[17,17,19]),dict(id=-1,pet=False,ids=[-1,18,19])]:add(**changes)
 for selector in [0,1,3,4]:
  for typ in [-1,0,2,21]:add(action='bucket',gameSelector=selector,bucketType=typ)
 for tail in [0,255]:
  for changes in [{},dict(pet=False),dict(snapshot=True),dict(snapshot=True,snapshotValue=1<<40),dict(snapshot=True,petKind=0),dict(snapshot=True,petData=False)]:add(action='packed',tail=tail,**changes)
 add(action='packed',game=False);add(action='packed',game=False,snapshot=True)
 OUT.mkdir(exist_ok=True);path=OUT/'cases.json';encoded=json.dumps(cases,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 libs=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libs)==1
 result=dict(referenceVersion='3.3.93847',sourceContexts=len(cases),machineCodeBytes=sum(b-a for a,b in em.ranges),processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,
  scope='Complete detach/type/data/bucket/packed routines over supplied finite store. Count/status application,client flush/send and broadcast are declared callbacks;no UO wire alias or native birth/persistence claimed. Unsupported fatal/null-data boundaries have no result. Unwritten packed tail explicitly supplied.',
  artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set([path,Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_parameters.py',*em.artifacts,*libs]))})
 path=ROOT/'tools/reference/pet-owner-detach-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==encoded
 else:path.write_text(encoded)
 print(f"Player pet detachment:{len(cases)}current contexts/{result['machineCodeBytes']}bytes;all store helpers composed.")
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
