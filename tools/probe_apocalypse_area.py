#!/usr/bin/env python3
"""Execute current area enumerator and its actual0x8783 filter with explicit world leaves."""
import argparse,hashlib,json,struct
from pathlib import Path
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9,UC_X86_REG_RSP,UC_X86_REG_RIP,UC_X86_REG_RAX
ROOT=Path(__file__).resolve().parents[1];BASE=0x140000000;DATA=0x20000000;STACK=0x30000000;STOP=0x40000000
CASTER=DATA;CONTEXT=DATA+0x800;ROOTROOM=DATA+0x1000;ADJ=DATA+0x2000;CALLBACK=STOP+0x100;GAME=DATA+0x700
CAPS=['callees-apocalypse-packet-area-20261109.json','callees-apocalypse-area-filter-20261109.json']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
class Probe:
 def __init__(self):
  self.u=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
  for a,n in [(DATA,0x50000),(STACK,0x20000),(STOP,4096)]:self.map(a,n)
  self.u.mem_write(STOP,b'\xf4');self.u.mem_write(CALLBACK,b'\xc3')
  for name,rva in zip(CAPS,[0x3a6160,0x3a5950]):
   p=ROOT/'.local/class-skills/warlock-runtime-reference'/name;d=json.loads(p.read_text());self.artifacts.append(p)
   entry=next(x for x in d['records']if int(x['rva'],16)==rva);assert entry['allUnwindFragmentsCaptured']
   for f in entry['fragments']:
    p=next(ROOT/p for p,h in d['artifacts'].items()if p.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();assert sha(p)==f['sha256']
    a=BASE+int(f['rva'],16);self.map(a,len(raw));self.u.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
  for a in [0x278450,0x372ec0,0x2a6c40]:self.map(BASE+a,1);self.u.mem_write(BASE+a,b'\xc3')
  self.u.hook_add(UC_HOOK_CODE,self.hook)
 def map(self,a,n):
  for p in range(a&~4095,(a+n+4095)&~4095,4096):
   if p not in self.pages:self.u.mem_map(p,4096);self.pages.add(p)
 def pi(self,a,n):self.u.mem_write(a,struct.pack('<I',n&0xffffffff))
 def pq(self,a,n):self.u.mem_write(a,struct.pack('<Q',n))
 def q(self,a):return struct.unpack('<Q',self.u.mem_read(a,8))[0]
 def i(self,a):return struct.unpack('<i',self.u.mem_read(a,4))[0]
 def xy(self,a):return[self.i(a),self.i(a+4)]
 def hook(self,u,a,n,_):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  sp=u.reg_read(UC_X86_REG_RSP);x,y,z,w=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]]
  if a==BASE+0x278450:value=self.targets[x]['room']if self.targets[x]['hasroom']else 0
  elif a==BASE+0x372ec0:assert(x,y,w&255)==(GAME,CASTER,1);value=self.targets[z]['hostile']
  elif a==BASE+0x2a6c40:
   target=next(t for t in self.targets.values()if[t['x'],t['y']]==self.xy(z));assert self.xy(y)==self.xy(CONTEXT+8)and w==4
   self.rays.append([*self.xy(y),*self.xy(z)]);value=target['blocked']
  elif a==CALLBACK:
   t=self.targets[y];assert self.q(x)==GAME and self.q(x+8)==CASTER
   self.hits.append([t['id'],self.i(x+16)]);value=t['accept']
   if self.c['mutateNext']:self.pq(y+0x160,0) # Enumerator must retain cached next.
  else:raise AssertionError(hex(a))
  u.reg_write(UC_X86_REG_RAX,int(value));u.reg_write(UC_X86_REG_RIP,self.q(sp));u.reg_write(UC_X86_REG_RSP,sp+8)
 def run(self,c):
  self.c=c;self.targets={};self.hits=[];self.rays=[];self.u.mem_write(DATA,b'\0'*0x50000);self.u.mem_write(STACK,b'\0'*0x20000)
  self.pi(CASTER,0);self.pq(CASTER+0xd8,GAME);self.pq(CASTER+0x38,DATA+0x600)
  self.u.mem_write(DATA+0x602,struct.pack('<H',c['casterX']));self.u.mem_write(DATA+0x606,struct.pack('<H',c['casterY']))
  self.pq(CONTEXT,ROOTROOM);self.pi(CONTEXT+8,c['centerX']);self.pi(CONTEXT+12,c['centerY']);self.pq(ROOTROOM,ADJ);self.pi(ROOTROOM+0x40,2)
  lists=[[],[]]
  for index,t in enumerate(c['targets']):
   t=t.copy();a=DATA+0x10000+index*0x400;path=a+0x200;room=DATA+0x30000+index*0x400;t['room']=room;self.targets[a]=t
   self.pi(a,t['kind']);self.pi(a+0xc,t['mode']);self.pi(a+0x124,t['flags']);self.pq(a+0x38,path)
   if t['kind']in[2,4,5]:self.pi(path+0x10,t['x']);self.pi(path+0x14,t['y'])
   else:self.u.mem_write(path+2,struct.pack('<H',t['x']));self.u.mem_write(path+6,struct.pack('<H',t['y']))
   self.pq(room+0x18,room+0x100);self.pq(room+0x190,room+0x200);self.pi(room+0x3f8,1 if t['town']else 2)
   lists[index%2].append(a)
  for i,units in enumerate(lists):
   room=DATA+0x4000+i*0x1000;self.pq(ADJ+i*8,room);self.pi(room+0x80,0);self.pi(room+0x84,0);self.pi(room+0x88,1000);self.pi(room+0x8c,1000)
   if c['includeCaster']and i==0:units.insert(0,CASTER)
   self.pq(room+0xa8,units[0]if units else 0)
   for j,a in enumerate(units):self.pq(a+0x160,units[j+1]if j+1<len(units)else 0)
  sp=STACK+0x10008;self.pq(sp,STOP);self.pi(sp+0x28,0x8783);self.pq(sp+0x30,CALLBACK);self.pq(sp+0x38,DATA+0x900);self.pi(sp+0x40,0)
  self.u.reg_write(UC_X86_REG_RSP,sp)
  for r,v in zip([UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9],[GAME,CASTER,CONTEXT,c['radius']]):self.u.reg_write(r,v)
  self.u.emu_start(BASE+0x3a6160,STOP,count=30000);assert self.u.reg_read(UC_X86_REG_RIP)==STOP
  return dict(hits=self.hits,center=self.xy(CONTEXT+8),rays=self.rays)
def build(check):
 p=Probe();cases=[]
 for radius in [13,15,17]:
  for fallback in [False,True]:
   cx,cy=(80,90)if fallback else(100,110);targets=[]
   for index,(dx,dy)in enumerate([(0,0),(radius,0),(radius+1,0),(0,-radius),(radius-1,5),(3,4),(-radius,0),(1,1),(2,2),(4,0),(6,0),(8,0)]):
    targets.append(dict(id=index+1,x=cx+dx,y=cy+dy,kind=1,mode=1,flags=12,hasroom=True,town=False,hostile=True,blocked=False,accept=True))
   for index,override in [(5,dict(blocked=True)),(6,dict(hostile=False)),(7,dict(town=True)),(8,dict(flags=4)),(9,dict(mode=12)),(10,dict(hasroom=False)),(11,dict(kind=0,mode=17))]:targets[index].update(override)
   c=dict(casterX=80,casterY=90,centerX=0 if fallback else cx,centerY=cy,radius=radius,includeCaster=True,mutateNext=True,targets=targets)
   result=p.run(c);assert [h[0]for h in result['hits']]==[1,5,2,4],result
   assert result['center']==[cx,cy];cases.append(dict(input=c,output=result))
 stage=ROOT/'.local/class-skills/apocalypse-area-source';stage.mkdir(exist_ok=True);fixture=stage/'cases.json';body=json.dumps(cases,indent=2)+'\n'
 if check:assert fixture.read_text()==body
 else:fixture.write_text(body)
 result=dict(referenceVersion='3.3.93847',currentMachineCodeExecutedOffline=True,cases=len(cases),functions=['0x3a6160','0x3a5950'],scope='Current enumeration plus actual0x8783 filter;supplied room,hostility and collision leaves. Two adjacency lists,center fallback,inclusive boundaries,cached-next mutation,dead/flags/town/missing-room/LOS rejection. Does not certify native Britannia room ordering,collision or hostility metadata.',artifacts={str(x.relative_to(ROOT)):sha(x)for x in[Path(__file__).resolve(),fixture,*p.artifacts]})
 out=ROOT/'tools/reference/apocalypse-area-current-evidence.json';body=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==body
 else:out.write_text(body)
 print('Current Apocalypse area/filter:',len(cases),'world contexts;inclusive radius,explosion-origin rays and cached-next traversal.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');build(a.parse_args().check)
