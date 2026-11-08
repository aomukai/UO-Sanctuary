#!/usr/bin/env python3
"""Execute captured current maximum getter; table/tier/formula/stat leaves supplied."""
import argparse,hashlib,json,struct
from pathlib import Path
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import UC_X86_REG_RSP,UC_X86_REG_RIP,UC_X86_REG_RAX,UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9
ROOT=Path(__file__).resolve().parents[1];BASE=0x140000000;DATA=0x20000000;ROW=DATA;CASTER=DATA+0x1000;STACK=0x30000000;STOP=0x40000000
CAP=ROOT/'.local/class-skills/warlock-runtime-reference/callees-apocalypse-packet-leaves-20261109.json'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def tier(rank):
 return sum([25,50,75,95,115][0 if r<=8 else 1 if r<=16 else 2 if r<=22 else 3 if r<=28 else 4]for r in range(2,rank+1))
class Probe:
 def __init__(self):
  self.u=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[CAP]
  for a,n in [(DATA,0x3000),(STACK,0x20000),(STOP,4096)]:self.map(a,n)
  self.u.mem_write(STOP,b'\xf4')
  capture=json.loads(CAP.read_text());entry=next(x for x in capture['records']if int(x['rva'],16)==0x28d050)
  assert entry['allUnwindFragmentsCaptured']
  for f in entry['fragments']:
   p=next(ROOT/p for p,h in capture['artifacts'].items()if p.endswith('.bin')and h==f['sha256']);raw=p.read_bytes();assert sha(p)==f['sha256'];a=BASE+int(f['rva'],16);self.map(a,len(raw));self.u.mem_write(a,raw);self.ranges.append((a,a+len(raw)));self.artifacts.append(p)
  for a in [0x6bf40,0x28c5b0,0x2bae90,0x28cc10]:self.map(BASE+a,1);self.u.mem_write(BASE+a,b'\xc3')
  self.u.hook_add(UC_HOOK_CODE,self.hook)
 def map(self,a,n):
  for p in range(a&~4095,(a+n+4095)&~4095,4096):
   if p not in self.pages:self.u.mem_map(p,4096);self.pages.add(p)
 def pi(self,a,n):self.u.mem_write(a,struct.pack('<I',n&0xffffffff))
 def pq(self,a,n):self.u.mem_write(a,struct.pack('<Q',n))
 def q(self,a):return struct.unpack('<Q',self.u.mem_read(a,8))[0]
 def hook(self,u,a,n,_):
  if any(lo<=a<hi for lo,hi in self.ranges):return
  sp=u.reg_read(UC_X86_REG_RSP);x,y,z,w=[u.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]]
  if a==BASE+0x6bf40:assert(x,y)==(1,401);value=ROW
  elif a==BASE+0x28c5b0:assert(x,y)==(self.rank,ROW+0x2a4);value=tier(self.rank)
  elif a==BASE+0x2bae90:assert(x,y,z,w)==(1,CASTER,123,401)and struct.unpack('<I',u.mem_read(sp+0x28,4))[0]==self.rank;value=self.hard*20
  elif a==BASE+0x28cc10:assert(x,y)==(CASTER,1);value=self.mastery
  else:raise AssertionError(hex(a))
  u.reg_write(UC_X86_REG_RAX,value&0xffffffff);u.reg_write(UC_X86_REG_RIP,self.q(sp));u.reg_write(UC_X86_REG_RSP,sp+8)
 def run(self,rank,hard,mastery):
  self.rank,self.hard,self.mastery=rank,hard,mastery;self.u.mem_write(DATA,b'\0'*0x3000);self.u.mem_write(STACK,b'\0'*0x20000)
  self.pi(ROW+0x28c,100);self.pi(ROW+0x2b8,123);self.u.mem_write(ROW+0x24c,b'\x08');self.u.mem_write(ROW+0x284,b'\x01')
  sp=STACK+0x10008;self.pq(sp,STOP);self.pi(sp+0x28,1);self.u.reg_write(UC_X86_REG_RSP,sp)
  for r,v in zip([UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9],[1,CASTER,401,rank]):self.u.reg_write(r,v)
  self.u.emu_start(BASE+0x28d050,STOP,count=2000);assert self.u.reg_read(UC_X86_REG_RIP)==STOP
  return self.u.reg_read(UC_X86_REG_RAX)&0xffffffff
 def cases(self):
  return [dict(rank=r,hard=h,mastery=m,maximum256=self.run(r,h,m))for r in [1,2,8,9,16,17,22,23,28,29,40,255]for h in [0,1,20]for m in [0,15,100]]
def main(check):
 p=Probe();cases=p.cases();stage=ROOT/'.local/class-skills/apocalypse-maximum-source';stage.mkdir(exist_ok=True)
 out=stage/'cases.json';body=json.dumps(cases,indent=2)+'\n'
 if check:assert out.read_text()==body
 else:out.write_text(body)
 result=dict(referenceVersion='3.3.93847',cases=len(cases),currentMachineCodeExecutedOffline=True,processMemoryRead=False,rva='0x28d050',
  scope='Current maximum elemental getter with supplied pinned Apocalypse table,tier bonus,hard synergy formula and fire mastery leaves. Confirms percentage ordering/rounding;does not execute protected minimum getter or supplied leaves.',
  artifacts={str(x.relative_to(ROOT)):sha(x)for x in [Path(__file__).resolve(),out,*p.artifacts]})
 path=ROOT/'tools/reference/apocalypse-maximum-current-evidence.json';body=json.dumps(result,indent=2)+'\n'
 if check:assert path.read_text()==body
 else:path.write_text(body)
 print('Current maximum getter:',len(cases),'contexts;minimum fallback remains explicitly approved.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');main(a.parse_args().check)
