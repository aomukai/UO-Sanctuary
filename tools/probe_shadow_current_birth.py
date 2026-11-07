#!/usr/bin/env python3
"""Execute the complete captured summon-stat helper with explicit services."""
import argparse, itertools, json, struct
from pathlib import Path
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_decisions import Emulator,BASE,STACK,MEM,STOP,GAME,PET,OWNER,CFG
from probe_warlock_runtime import ROOT,STAGE,digest

OUT=ROOT/'.local/class-skills/shadow-current-birth-cases.json'
TABLE=MEM+0x18000; LIST=MEM+0x15000; GLOBAL=BASE+0x1e03ac0
SERVICES={k:BASE+v for k,v in dict(skill=0x6bf40,life=0x24fa20,unitset=0x24dec0,allocate=0x24cef0,post=0x24d8e0,formula=0x2bae90,listset=0x24dcd0,modifier=0x3d5fd0).items()}
REGS=[UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]
def signed(v):return (v+2**31)%2**32-2**31
def percentage(value,percent):
    if value>1048576:return signed(percent*(value//100))
    if percent>65536:return signed(value*(percent//100))
    product=signed(value*percent)
    return (abs(product)//100)*(1 if product>=0 else -1)

class Birth(Emulator):
    def __init__(self):
        assert unicorn.__version__=='2.1.4'
        self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[];self.artifacts=[]
        path=STAGE/'callees-shadow-init-20261107.json';proof=json.loads(path.read_text());self.artifacts.append(path)
        for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
        record=proof['records'][0];assert record['rva']=='0x4473c0' and record['allUnwindFragmentsCaptured']
        for f in record['fragments']:
            assert f['captured'] and f['invalidInstructionMarkers']==0
            paths=[ROOT/n for n,h in proof['artifacts'].items()if n.endswith('.bin')and h==f['sha256']];assert len(paths)==1
            p=paths[0];data=p.read_bytes();a=BASE+int(f['rva'],16)
            assert len(data)==int(f['endRva'],16)-int(f['rva'],16)
            self.map(a,len(data));self.uc.mem_write(a,data);self.ranges.append((a,a+len(data)));self.artifacts.append(p)
        self.map(STACK,0x40000);self.map(MEM,0x20000);self.map(STOP,4096);self.map(GLOBAL,64)
        for a in SERVICES.values():self.map(a,1);self.uc.mem_write(a,b'\xc3')
        self.uc.hook_add(UC_HOOK_CODE,self.hook)
    def ret(self,v=0):
        sp=self.uc.reg_read(UC_X86_REG_RSP);to=self.q(sp)
        self.uc.reg_write(UC_X86_REG_RAX,v&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,sp+8);self.uc.reg_write(UC_X86_REG_RIP,to)
    def hook(self,uc,address,size,user):
        if any(a<=address<b for a,b in self.ranges):return
        name=next((k for k,v in SERVICES.items()if v==address),None);assert name,('Undeclared external code',hex(address))
        c,d,v,z=[uc.reg_read(r)for r in REGS];sp=uc.reg_read(UC_X86_REG_RSP);result=0
        if name=='skill':
            assert (c,d)==(self.selector,268);self.trace.append(['skill',268]);result=CFG
        elif name=='life':assert c==PET;self.trace.append(['life']);result=self.life
        elif name=='unitset':assert c==PET and z==0;self.trace.append(['unit',d,signed(v)])
        elif name=='allocate':
            assert (c,d,v,z)==(0,0,1,101);self.trace.append(['allocate']);result=LIST if self.allocate else 0
        elif name=='post':assert (c,d,v)==(PET,LIST,1);self.trace.append(['post'])
        elif name=='formula':
            rank=signed(struct.unpack('<I',bytes(uc.mem_read(sp+40,4)))[0]);assert c==self.selector and z==268 and rank==self.rank
            actor='pet'if d==PET else'owner'if d==OWNER else None;assert actor
            self.trace.append(['formula',actor,v]);result=self.modifier if v==900 else 10000+v
        elif name=='listset':
            assert c==self.selector and d==LIST and bytes(uc.mem_read(sp+40,2))==b'\0\0'
            self.trace.append(['stat',signed(v),signed(z)])
        elif name=='modifier':assert(c,d,z)==(GAME,PET,1);self.trace.append(['modifier',v])
        self.ret(result)
    def execute(self,rank,life,selector,allocate,profile,modifier):
        self.rank,self.life,self.selector,self.allocate,self.modifier=rank,life,selector,allocate,modifier;self.trace=[]
        self.uc.mem_write(MEM,b'\0'*0x20000);self.uc.mem_write(STACK,b'\0'*0x40000)
        self.uc.mem_write(GAME+0x106,bytes([selector]));self.wi(PET,1);self.wi(PET+8,101)
        for n in [1,2,3]:self.wq(GLOBAL+n*16,TABLE)
        self.wi(TABLE+0x1298,512);self.wi(CFG+0x1b8,15);self.wi(CFG+0x138,900)
        aura=[0,19,171,200,511,510];passive=list(range(310,324))
        if profile==1:aura[1]=-1;passive[2]=512
        if profile==2:aura=[-1]*6;passive=[-1]*14
        for offset,calc,stats,token in [(0x74,0x88,aura,100),(0xba,0xd8,passive,200)]:
            for k,stat in enumerate(stats):self.uc.mem_write(CFG+offset+2*k,struct.pack('<h',stat));self.wi(CFG+calc+4*k,token+k)
        sp=STACK+0x30000;self.wq(sp,STOP);self.wi(sp+40,268);self.wi(sp+48,rank)
        for r,v in zip(REGS,[GAME,OWNER,PET,CFG]):self.uc.reg_write(r,v)
        self.uc.reg_write(UC_X86_REG_RSP,sp);self.uc.emu_start(BASE+0x4473c0,STOP,count=10000)
        expected=[]
        if rank>=1:
            hp=signed(life+percentage(life,15*(rank-1)))
            expected=[['skill',268],['life'],['unit',7,hp],['unit',6,hp],['allocate']]
            if allocate:
                expected.append(['post'])
                for stats,token in [(aura,100),(passive,200)]:
                    for k,stat in enumerate(stats):
                        if 0<=stat<512:expected += [['formula','owner',token+k],['stat',stat,10000+token+k]]
                expected.append(['formula','pet',900])
                if 1<=modifier<=44:expected.append(['modifier',modifier])
        assert self.trace==expected,dict(case=[rank,life,selector,allocate,profile,modifier],actual=self.trace,expected=expected)
        return dict(case=[rank,life,selector,allocate,profile,modifier],trace=self.trace)

def build(check=False):
    em=Birth();cases=[em.execute(*v)for v in itertools.product([-1,0,1,2,10,20,40,255],[256,1048576,1048577,16000000],[1,2,3],[False,True],[0,1,2],[-1,0,1,43,44,45])]
    assert len(cases)==3456
    encoded=json.dumps(cases,separators=(',',':'))+'\n'
    if check:assert OUT.read_text()==encoded
    else:assert not OUT.exists()or OUT.read_text()==encoded;OUT.write_text(encoded)
    print('Current captured helper:3456 complete offline contexts;rank1,6 indexed aura/14 passive slots,owner formula context and pet modifier context;no learned summon enabled.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
