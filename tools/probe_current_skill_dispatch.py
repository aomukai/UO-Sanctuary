#!/usr/bin/env python3
"""Execute the captured dispatch slice,not the full modern skill executor."""
import argparse,itertools,json,struct
import unicorn
from unicorn import Uc,UC_ARCH_X86,UC_MODE_64,UC_HOOK_CODE
from unicorn.x86_const import *
from probe_generic_pet_decisions import Emulator,BASE,STACK,MEM,GAME,PET,CFG
from probe_warlock_runtime import ROOT,STAGE,Image,digest
OUT=ROOT/'.local/class-skills/current-skill-dispatch-cases.json';ENTRY=BASE+0x3ab2b5;END=BASE+0x3ab337;REFUSED=BASE+0x3ab13b;MANA=BASE+0x3a7ba0;TABLE=BASE+0x163a8b0
def signed(n):return(n+2**31)%2**32-2**31
class Dispatch(Emulator):
    def __init__(self):
        assert unicorn.__version__=='2.1.4';self.uc=Uc(UC_ARCH_X86,UC_MODE_64);self.pages=set();self.ranges=[]
        p=STAGE/'services-shadow-skill-dispatch-20261107.json';proof=json.loads(p.read_text());r=proof['records'][0]
        assert r['rva']=='0x3aaf70' and r['allUnwindFragmentsCaptured'] and len(r['fragments'])==1
        for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
        f=r['fragments'][0];assert f['invalidInstructionMarkers']==0
        binary=next(ROOT/n for n,h in proof['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);data=binary.read_bytes();self.map(BASE+0x3aaf70,len(data));self.uc.mem_write(BASE+0x3aaf70,data)
        # The range guard,record reads,indexed table load and actual call are
        # executed from preserved bytes. Outer permission/mana bodies are not.
        assert data[0x345:0x34a]==bytes.fromhex('b8 be 00 00 00')
        self.map(STACK,0x40000);self.map(MEM,0x10000);self.map(TABLE,191*8)
        table=(ROOT/'.local/class-skills/skill-do-dispatch-current/candidate-table.bin').read_bytes();assert len(table)==191*8 and table==Image().bytes(0x163a8b0,len(table))
        self.words=struct.unpack('<191Q',table);self.uc.mem_write(TABLE,table)
        self.targets=set(self.words)-{0}
        for address in self.targets|{MANA}:
            assert not BASE+0x3aaf70<=address<BASE+0x3aaf70+len(data)
            self.map(address,1);self.uc.mem_write(address,b'\xc3')
        self.uc.hook_add(UC_HOOK_CODE,self.hook)
    def ret(self,value):
        sp=self.uc.reg_read(UC_X86_REG_RSP);to=self.q(sp);self.uc.reg_write(UC_X86_REG_RAX,value&0xffffffffffffffff);self.uc.reg_write(UC_X86_REG_RSP,sp+8);self.uc.reg_write(UC_X86_REG_RIP,to)
    def hook(self,uc,address,size,user):
        if address in [END,REFUSED]:self.finished=address==END;uc.emu_stop();return
        if ENTRY<=address<END:return
        if address==MANA:
            assert uc.reg_read(UC_X86_REG_RCX)==PET;self.trace.append(['mana']);self.ret(self.mana);return
        assert address in self.targets,hex(address)
        index=uc.reg_read(UC_X86_REG_RAX);assert 0<=index<191 and self.words[index]==address
        args=[uc.reg_read(r)for r in [UC_X86_REG_RCX,UC_X86_REG_RDX,UC_X86_REG_R8,UC_X86_REG_R9]]
        assert args==[GAME,PET,268,20],args
        self.trace.append(['callback',index,hex(address-BASE),268,20]);self.ret(42)
    def execute(self,primary,override,living,usemana,mana,item,outgoing):
        self.mana=mana;self.trace=[];self.finished=False;self.uc.mem_write(MEM,b'\0'*0x10000);self.uc.mem_write(STACK,b'\0'*0x40000)
        self.uc.mem_write(CFG+0x4e,struct.pack('<H',primary));self.uc.mem_write(CFG+0x20a,struct.pack('<h',override));self.uc.mem_write(CFG+0x28,bytes([usemana]))
        sp=STACK+0x20000;self.wi(sp+0xc8,20);self.wi(sp+0xd8,item);self.wi(sp+0xe0,outgoing)
        for reg,value in [(UC_X86_REG_RSP,sp),(UC_X86_REG_R13,CFG),(UC_X86_REG_R14,0),(UC_X86_REG_R15,living),(UC_X86_REG_R12,268),(UC_X86_REG_RDI,20),(UC_X86_REG_RSI,GAME),(UC_X86_REG_RBX,PET)]:self.uc.reg_write(reg,value)
        self.uc.emu_start(ENTRY,END+1,count=1000)
        actual=dict(finished=self.finished,trace=self.trace,result=signed(struct.unpack('<I',bytes(self.uc.mem_read(sp+0xb0,4)))[0]))
        trace=[];valid=primary<=190
        if valid and living and usemana:trace.append(['mana']);valid=bool(mana)
        index=override if item and outgoing and override>1 else primary
        result=0
        if valid and self.words[index]:trace.append(['callback',index,hex(self.words[index]-BASE),268,20]);result=42
        expected=dict(finished=valid,trace=trace,result=result)
        assert actual==expected,dict(case=[primary,override,living,usemana,mana,item,outgoing],actual=actual,expected=expected)
        return dict(case=[primary,override,living,usemana,mana,item,outgoing],result=actual)
def build(check=False):
    em=Dispatch();cases=[em.execute(*v)for v in itertools.product([0,1,6,16,48,49,190,191,65535],[0,1,2,49,190],[0,1],[0,32],[0,1],[0,1],[0,1])];assert len(cases)==1440
    encoded=json.dumps(cases,separators=(',',':'))+'\n'
    if check:assert OUT.read_text()==encoded
    else:assert not OUT.exists()or OUT.read_text()==encoded;OUT.write_text(encoded)
    print('Current dispatch slice:1440contexts,ordinary bound190,actual immutable table/index/arguments,mana gate and item/outgoing override;full executor not certified.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
