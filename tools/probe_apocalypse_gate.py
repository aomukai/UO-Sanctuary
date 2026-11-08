#!/usr/bin/env python3
"""Reuse current area probe host to check non-aura bypass of3a6090."""
import argparse,json,struct
from pathlib import Path
from probe_apocalypse_area import ROOT,BASE,DATA,STACK,STOP,CASTER,GAME,Probe,sha
from unicorn.x86_const import UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9,UC_X86_REG_RSP,UC_X86_REG_RIP,UC_X86_REG_RAX

def build(check):
 p=Probe();cap=ROOT/'.local/class-skills/warlock-runtime-reference/callees-apocalypse-packet-area-20261109.json';d=json.loads(cap.read_text());r=next(r for r in d['records']if int(r['rva'],16)==0x3a6090);assert r['allUnwindFragmentsCaptured']
 artifacts=[cap,Path(__file__).resolve(),ROOT/'tools/probe_apocalypse_area.py']
 for f in r['fragments']:
  path=next(ROOT/n for n,h in d['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=path.read_bytes();assert sha(path)==f['sha256'];a=BASE+int(f['rva'],16);p.map(a,len(raw));p.u.mem_write(a,raw);p.ranges.append((a,a+len(raw)));artifacts.append(path)
 skill=next(s for s in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text())if s['talentId']=='warlock.apocalypse');assert skill['sourceFields'].get('aura',0)==0
 cases=[];row=DATA+0x40000
 for enabled in [0,1]:
  for immediate in [0,1]:
   for deadline in [0,100,1000]:
    p.u.mem_write(DATA,b'\0'*0x50000);p.u.mem_write(STACK,b'\0'*0x20000);p.pi(GAME+0x19050,enabled);p.pi(GAME+0x170,100);p.pi(CASTER+0x188,deadline)
    p.pi(row+0x24,immediate<<15);sp=STACK+0x10008;p.pq(sp,STOP);p.pi(sp+0x28,1);p.u.reg_write(UC_X86_REG_RSP,sp)
    for reg,value in zip([UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9],[GAME,CASTER,row,401]):p.u.reg_write(reg,value)
    p.u.emu_start(BASE+0x3a6090,STOP,count=1000);assert p.u.reg_read(UC_X86_REG_RIP)==STOP
    refused=p.u.reg_read(UC_X86_REG_RAX)&255;after=p.i(CASTER+0x188);assert refused==0 and after==deadline
    cases.append(dict(gameEnabled=enabled,immediate=immediate,aura=0,deadline=deadline,refused=refused,after=after))
 result=dict(referenceVersion='3.3.93847',cases=cases,currentMachineCodeExecutedOffline=True,rva='0x3a6090',scope='Pinned Apocalypse aura0 guarantees bypass,irrespective of immediate bit/game enable/future deadline. Does not implement or verify immediate-aura scheduling;localdelay45 is separate.',artifacts={str(x.relative_to(ROOT)):sha(x)for x in artifacts})
 out=ROOT/'tools/reference/apocalypse-gate-current-evidence.json';body=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==body
 else:out.write_text(body)
 print('Current area gate:',len(cases),'non-aura contexts bypass;no deadline mutation or formula callback.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');build(a.parse_args().check)
