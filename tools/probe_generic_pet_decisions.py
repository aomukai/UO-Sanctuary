#!/usr/bin/env python3
"""Run captured GenericPet root/follow instructions offline with explicit services."""
import argparse,itertools,json,struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_warlock_runtime import ROOT,STAGE,Image,digest
BASE=0x140000000;STACK=0x20000000;MEM=0x30000000;STOP=0x40000000
GAME=MEM;PET=MEM+0x1000;OWNER=MEM+0x2000;TICK=MEM+0x3000;CTRL=MEM+0x4000;MON=MEM+0x5000;PATH=MEM+0x6000;OWNERPATH=MEM+0x7000;PLAYER=MEM+0x8000;CFG=MEM+0x9000;ROOM=MEM+0xa000;ROOMDATA=MEM+0xb000;LEVEL=MEM+0xc000;REGION=MEM+0xd000;EVENTS=MEM+0xe000
FNS={k:BASE+v for k,v in dict(resolve=0x370680,init=0x4b0b90,distance=0x40bfd0,follow=0x4b2060,room=0x278450,event=0x32e790,combat=0x4b58a0,idle=0x425550,finish=0x4b07b0,cookie=0x155eeb0,moving=0x373f60,roomcoords=0x2972c0,region=0x2e0ae0,execute=0x4b16c0).items()}
class Emulator:
 def __init__(self,kind):
  assert unicorn.__version__=='2.1.4';self.kind=kind;self.image=Image();self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  proofpath=STAGE/('generic-pet-candidate.json' if kind=='root' else 'generic-helpers-actions.json');proof=json.loads(proofpath.read_text());self.artifacts.append(proofpath)
  for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
  row=proof if kind=='root' else next(r for r in proof['records'] if r['rva']=='0x4b2060');assert row['allUnwindFragmentsCaptured']
  for i,f in enumerate(row['fragments']):
   p=STAGE/(f'generic-pet-candidate-part{i}.bin' if kind=='root' else f'generic-actions-4b2060-part{i}.bin');b=p.read_bytes();assert digest(b)==f['sha256'];a=BASE+int(f['rva'],16);self.map(a,len(b));self.uc.mem_write(a,b);self.ranges.append((a,a+len(b)));self.artifacts.append(p)
  for rva in [0x1813840,0x1813460,0x18141c0]:self.map(BASE+rva,16);self.uc.mem_write(BASE+rva,self.image.bytes(rva,16))
  self.map(BASE+0x197a178,8);self.uc.mem_write(BASE+0x197a178,b'\0'*8);self.map(STACK,0x40000);self.map(MEM,0x20000);self.map(STOP,0x1000)
  for addr in FNS.values():
   if not any(a<=addr<b for a,b in self.ranges):self.map(addr,1);self.uc.mem_write(addr,b'\xc3')
  self.uc.reg_write(UC_X86_REG_CR0,(self.uc.reg_read(UC_X86_REG_CR0)&~4)|2);self.uc.reg_write(UC_X86_REG_CR4,self.uc.reg_read(UC_X86_REG_CR4)|0x200);self.uc.hook_add(UC_HOOK_CODE,self.hook)
 def map(self,a,n):
  for p in range(a&~4095,(a+n+4095)&~4095,4096):
   if p not in self.pages:self.uc.mem_map(p,4096);self.pages.add(p)
 def q(self,a):return struct.unpack('<Q',self.uc.mem_read(a,8))[0]
 def i(self,a):return struct.unpack('<i',self.uc.mem_read(a,4))[0]
 def wq(self,a,v):self.uc.mem_write(a,struct.pack('<Q',v))
 def wi(self,a,v):self.uc.mem_write(a,struct.pack('<i',v))
 def coords(self,unit,kind,x,y,path):
  self.wi(unit,kind);self.wq(unit+0x38,path)
  if path:
   if kind in [2,4,5]:self.wi(path+0x10,x);self.wi(path+0x14,y)
   else:self.uc.mem_write(path+2,struct.pack('<H',x));self.uc.mem_write(path+6,struct.pack('<H',y))
 def reset(self):self.uc.mem_write(MEM,b'\0'*0x20000);self.uc.mem_write(STACK,b'\0'*0x40000);self.trace=[];self.followcalls=0;self.distancecalls=0;self.regionunit=0
 def run(self,entry,rcx,rdx,r8,r9):
  rsp=STACK+0x20008;self.wq(rsp,STOP)
  for reg,v in [(UC_X86_REG_RSP,rsp),(UC_X86_REG_RCX,rcx),(UC_X86_REG_RDX,rdx),(UC_X86_REG_R8,r8),(UC_X86_REG_R9,r9)]:self.uc.reg_write(reg,v)
  self.uc.emu_start(BASE+entry,STOP,count=10000);assert self.uc.reg_read(UC_X86_REG_RIP)==STOP
  return self.uc.reg_read(UC_X86_REG_RAX)&255
 def hook(self,uc,address,size,user):
  if any(a<=address<b for a,b in self.ranges):return
  reg=lambda r:uc.reg_read(r);c=reg(UC_X86_REG_RCX);d=reg(UC_X86_REG_RDX);v=reg(UC_X86_REG_R8);z=reg(UC_X86_REG_R9);rax=0
  signed=lambda x:struct.unpack('<i',struct.pack('<I',x&0xffffffff))[0]
  name=next((k for k,a in FNS.items() if a==address),None);assert name,('Undeclared code',hex(address))
  if self.kind=='root':
   p=self.input
   if name=='resolve':assert c==GAME and d==19 and v==7;self.trace.append([0]);rax=OWNER if p[2]>=0 else 0
   elif name=='init':
    assert c==GAME and d==PET and z==TICK;self.frame=self.q(reg(UC_X86_REG_RSP)+0x28);self.wi(self.frame,p[5]);self.wq(self.frame+0x50,0x777);self.uc.mem_write(self.frame+0x58,struct.pack('<3i',31,32,33));self.trace.append([1,int(v!=0)])
   elif name=='event':assert c==GAME;self.trace.append([2,(d-EVENTS)//0x80])
   elif name=='distance':assert c==PET;self.trace.append([3,signed(d),signed(v)]);rax=p[4]
   elif name=='follow':
    assert c==GAME and d==PET and z==self.frame;value=p[8+self.followcalls];self.followcalls+=1;self.trace.append([4,int(v!=0),value]);rax=value;self.wi(self.frame+0x5c,self.i(self.frame+0x5c)+1)
   elif name=='room':
    assert c in [PET,OWNER];which=int(c==OWNER);level=p[6+which];self.trace.append([5,which]);rax=ROOM if level!=-1 else 0
    self.wq(ROOM+0x18,ROOMDATA);self.wq(ROOMDATA+0x90,LEVEL);self.wi(LEVEL+0x1f8,level)
   elif name=='combat':assert c==GAME and d==PET and z==self.frame;self.trace.append([6,int(v!=0),p[10]]);rax=p[10];self.wi(self.frame+0x60,self.i(self.frame+0x60)+2)
   elif name=='idle':assert c==GAME and d==PET and v==15;self.trace.append([7,15])
   elif name=='finish':assert c==PET and d==0x777;self.trace.append([8,0x777]);self.wi(self.frame+0x58,self.i(self.frame+0x58)+4)
   elif name!='cookie':raise AssertionError(('Unexpected root service',name))
  else:
   p=self.input
   if name=='distance':
    assert c==PET;self.trace.append([3,signed(d),signed(v)]);rax=p[4] if self.distancecalls==0 else p[9];self.distancecalls+=1
   elif name=='moving':assert c==OWNER;self.trace.append([10,p[6]]);rax=p[6]
   elif name=='room':assert c in [PET,OWNER];self.regionunit=int(c==OWNER);self.trace.append([11,self.regionunit]);rax=ROOM
   elif name=='roomcoords':assert c==ROOM;self.trace.append([12,self.regionunit,signed(d),signed(v)]);rax=ROOMDATA if p[10+self.regionunit]!=0 else 0;self.wq(ROOMDATA+0x18,LEVEL)
   elif name=='region':
    assert c==LEVEL;self.trace.append([13,self.regionunit,signed(d),signed(v)]);rax=REGION if p[10+self.regionunit]==2 else 0;self.wi(REGION+0x28,p[7+self.regionunit])
   elif name=='execute':
    assert c==GAME and d==(PET if p[0] else 0) and v==(OWNER if p[1] else 0) and self.q(reg(UC_X86_REG_RSP)+0x28)==CFG
    self.trace.append([14,int(z),p[12]]);rax=p[12]
   else:raise AssertionError(('Unexpected follow service',name))
  rsp=reg(UC_X86_REG_RSP);ret=self.q(rsp);uc.reg_write(UC_X86_REG_RAX,rax&0xffffffffffffffff);uc.reg_write(UC_X86_REG_RSP,rsp+8);uc.reg_write(UC_X86_REG_RIP,ret)
 def root(self,p,events):
  # pet kind,owner chain depth,owner kind,path,distance,leash,pet/owner level,follow results,combat
  self.reset();self.input=p;kind,depth,own,path=p[:4];self.coords(PET,kind,20,30,PATH)
  self.wq(PET+0x10,MON if depth>=1 else 0);self.wq(MON+0x38,CTRL if depth>=2 else 0);self.wq(CTRL+0x60,GAME if depth>=3 else 0);self.wi(CTRL+0x68,7);self.wi(CTRL+0x6c,19);self.wq(TICK,CTRL)
  self.coords(OWNER,max(0,own),45,55,OWNERPATH if path else 0)
  self.wq(PET+0x148,EVENTS if events else 0)
  for i,t in enumerate(events):a=EVENTS+i*0x80;self.uc.mem_write(a,bytes([t]));self.wq(a+0x38,EVENTS+(i+1)*0x80 if i+1<len(events) else 0)
  self.run(0x4b60c0,GAME,PET,TICK,0);state=[self.i(CTRL+o) for o in [0x44,0x48,0x4c]];self.trace.append([9,*state]);return self.trace
 def follow(self,p):
  # pet/owner presence,owner kind,path,distance,warp,moving,pet/owner regions,destination distance,pet/owner region states,execute,wander,pet kind,player data
  self.reset();self.input=p;self.coords(PET,p[14],20,30,PATH);self.coords(OWNER,p[2],45,55,OWNERPATH if p[3] else 0)
  self.wq(OWNER+0x10,PLAYER if p[15] else 0);self.wi(PLAYER+0x1c0,65);self.wi(PLAYER+0x1c4,75);self.wi(CFG+4,p[5]);self.wi(CFG+12,p[13]);rsp=STACK+0x20008;self.wq(rsp+0x28,CFG)
  ret=self.run(0x4b2060,GAME,PET if p[0] else 0,OWNER if p[1] else 0,CFG);return [ret,self.trace]
def expected_root(p,events):
 kind,depth,own,path,distance,leash,pl,ol,f0,f1,combat=p;owner=kind==1 and depth==3 and own>=0;trace=[];history=[31,32,33];fc=0
 if kind==1 and depth==3:trace.append([0])
 trace.append([1,int(owner)]);trace += [[2,i] for i,t in enumerate(events) if t==2]
 def follow():
  nonlocal fc
  value=[f0,f1][fc];fc+=1;trace.append([4,int(owner),value]);history[1]+=1;return value
 done=False
 if owner:
  trace.append([3,45 if path else 0,55 if path else 0])
  if distance>leash:done=follow()!=0
 if not done:
  trace.append([5,0]);town=pl in [1,40,75,103,109]
  if not town and owner:trace.append([5,1]);town=ol in [1,40,75,103,109]
  if not town:trace.append([6,int(owner),combat]);history[2]+=2;done=combat!=0
  if not done and not follow():trace.append([7,15])
 trace.append([8,0x777]);history[0]+=4;trace.append([9,*history]);return trace
def expected_follow(p):
 has,owner,kind,path,d,warp,moving,pr,orr,dest,ps,os,success,wander,pk,pdata=p;trace=[];mode=2
 if not has:return [0,trace]
 if owner:
  x,y=(45,55) if path else (0,0);trace += [[3,x,y],[10,moving],[11,1],[12,1,x,y]]
  if os:trace.append([13,1,int(x/5),int(y/5)])
  ownerregion=orr if os==2 else -1 if os==1 else 0
  trace += [[11,0],[12,0,20,30]]
  if ps:trace.append([13,0,4,6])
  petregion=pr if ps==2 else -1 if ps==1 else 0
  if d>warp:mode=3
  elif d>100 or ownerregion!=petregion:mode=1
  elif moving:mode=0
  else:
   if kind==0 and pdata:trace.append([3,65,75])
   if kind==0 and pdata and dest<36:mode=4
   elif d<=4:mode=5
 if mode==2 and wander<=0:return [0,trace]
 trace.append([14,mode,success]);return [success,trace]
def build(check=False):
 root=Emulator('root');follow=Emulator('follow');roots=[];follows=[]
 eventsets=[[],[2],[0,2,2,3,2,255],[0,1,3]]
 def r(p,e):
  actual=root.root(p,e);assert actual==expected_root(p,e),(p,e,actual,expected_root(p,e));roots.append([p,e,actual])
 for kind,depth,own,path in itertools.product([0,1,2,4,5],range(4),[-1,0,1,2,4,5],[0,1]):
  if own in [2,4,5] and not path:continue
  for events in eventsets:r([kind,depth,own,path,901,900,2,2,0,1,0],events)
 for d,leash,pl,ol,f0,f1,combat in itertools.product([-1,0,4,900,901,2147483647],[-1,0,900],[ -1,0,1,39,40,74,75,102,103,108,109,110],[-1,1,2,40,75,103,109],[0,1],[0,1],[0,1]):
  r([1,3,0,1,d,leash,pl,ol,f0,f1,combat],[2,0,2])
 def f(p):
  actual=follow.follow(p);assert actual==expected_follow(p),(p,actual,expected_follow(p));follows.append([p,actual])
 for has,own,kind,path,pk,pdata in itertools.product([0,1],[0,1],[0,1,2,4,5],[0,1],[0,1,2,4,5],[0,1]):
  if kind in [2,4,5] and not path:continue
  f([has,own,kind,path,4,900,0,17,17,35,2,2,1,10,pk,pdata])
 for d,warp,moving,pr,orr,dest,success,wander,ps,os in itertools.product([-1,0,4,5,100,101,900,901],[-1,100,900],[0,1],[-1,0,17],[-1,0,17],[35,36],[0,1],[-1,0,1],[0,1,2],[0,1,2]):
  f([1,1,0,1,d,warp,moving,pr,orr,dest,ps,os,success,wander,1,1])
 out=ROOT/'.local/class-skills/generic-pet-decisions-source-probe.json';encoded=json.dumps(dict(root=roots,follow=follows),separators=(',',':'))+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 libraries=list(Path(unicorn.__file__).parent.rglob('libunicorn.so*'));assert len(libraries)==1
 artifacts=[out,Path(__file__).resolve(),*root.artifacts,*follow.artifacts,ROOT/'tools/probe_warlock_runtime.py'];result=dict(referenceVersion='3.3.93847',component='Current GenericPet root and follow policy',unicornVersion=unicorn.__version__,rootBytes=sum(b-a for a,b in root.ranges),followBytes=sum(b-a for a,b in follow.ranges),rootCases=len(roots),followCases=len(follows),processMemoryRead=False,targetProcessCodeExecuted=False,sourceCodeExecutedInOfflineEmulator=True,completeAiVerified=False,handlerEnabled=False,
  services=['Root initializer supplies declared parameters,target and state;owner lookup,event removal,distance,room lookup,follow,combat,idle and finish are explicit synthetic services. Finish mutates state before root copies it back. Dynamic temporary allocation is not exercised.', 'Follow distance and owner-moving predicate are supplied. Room-coordinate and region services have independent no-room/no-region/region controls. Final movement executor returns a supplied result. No actual movement,collision,combat or owner callback is certified.'],
  emulatorLibrary=dict(path=str(libraries[0].relative_to(ROOT)),sha256=digest(libraries[0].read_bytes())),artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))})
 p=ROOT/'tools/reference/generic-pet-decisions-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print(f'Current GenericPet decisions:{len(roots)} root/{len(follows)} follow contexts;explicit services,full AI remains gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
