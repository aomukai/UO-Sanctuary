#!/usr/bin/env python3
"""Compose the complete current path reset and primary monster getter offline."""
import argparse,itertools,json
from pathlib import Path
from unicorn.x86_const import UC_X86_REG_RCX,UC_X86_REG_RDX
from probe_generic_pet_path_commit import Commit,params,BASE,MEM,FUNCS,signed
from probe_warlock_runtime import ROOT,STAGE,digest

LOCAL=ROOT/'.local/class-skills/pet-path-targets-reference-20261103'
OUT=ROOT/'.local/class-skills/pet-path-targets-source-probe.json'
TABLE=MEM+0x20000;DATA=MEM+0x30000

class Targets(Commit):
    def __init__(self):
        super().__init__()
        path=STAGE/'callees-live-search-services-idle-20261030.json';proof=json.loads(path.read_text());self.artifacts.append(path)
        for n,h in proof['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
        row=next(r for r in proof['records'] if r['rva']=='0x6bc90');assert row['allUnwindFragmentsCaptured']
        for f in row['fragments']:
            assert f['captured'] and f['invalidInstructionMarkers']==0
            found=[ROOT/n for n,h in proof['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(found)==1
            p=found[0];b=p.read_bytes();a=BASE+int(f['rva'],16)
            self.map(a,len(b));self.uc.mem_write(a,b);self.ranges.append((a,a+len(b)));self.artifacts.append(p)
        self.primary=(LOCAL/'monstats.bin').read_bytes()[4:];assert len(self.primary)==751*508
        self.artifacts.append(LOCAL/'monstats.bin');self.map(TABLE,0x10000);self.map(DATA,len(self.primary));self.uc.mem_write(DATA,self.primary)
        self.wq(TABLE+0xf90,DATA);self.wq(TABLE+0xf98,751)
        self.map(BASE+0x1e03ac0,64)
        for selector in [1,2,3]:self.wq(BASE+0x1e03ac0+16*selector,TABLE)
    def hook(self,uc,a,size,user):
        if a==FUNCS['metadata']:
            selector=uc.reg_read(UC_X86_REG_RCX)&255;cls=signed(uc.reg_read(UC_X86_REG_RDX))
            value=self.primary[cls*508+61] if selector in [1,2,3] and 0<=cls<751 else None
            self.trace.append([6,selector,cls,value])
        super().hook(uc,a,size,user)

def cases():
    # Every pinned class, all explicit views, both monster and player reset.
    for selector,cls,actor in itertools.product([1,2,3],range(751),[0,1]):
        yield 'reset',params(actor=actor,target=1,targetClass=cls,variant=selector,metadata=None,flags=0x20)
    for selector,cls,kind,flags,typ,actor in itertools.product([0,1,3,4,255],[-1,0,750,751],[0,1],[0,4],[0,17],[0,1]):
        yield 'reset',params(actor=actor,target=kind,targetClass=cls,variant=selector,targetFlags=flags,type=typ,metadata=None,flags=0x20)

def build(check=False):
    em=Targets();rows=[[f,p,em.run(f,p)] for f,p in cases()]
    assert len(rows)==4826
    payload=json.dumps(rows,separators=(',',':'))+'\n'
    proof=dict(referenceVersion='3.3.93847',component='Complete current path reset with actual primary monster getter and owned rows',
        cases=len(rows),unsupportedCases=sum(r[2][0]==-1 for r in rows),offlineMachineCodeExecuted=True,
        processMemoryReadDuringProbe=False,targetProcessCodeExecuted=False,worldMovementVerified=False,handlerEnabled=False,
        limits=['Real getter and complete reset/coordinate routines execute. The three synthetic selector slots explicitly bind the same supplied owned table; actual game selector aliasing is not inferred.',
            'Room/coordinate link leaves remain the existing declared services. The native movement scenes are separate evidence.'],
        artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [Path(__file__).resolve(),ROOT/'tools/probe_generic_pet_path_commit.py',ROOT/'tools/probe_generic_pet_decisions.py',*em.artifacts,em.image.reference]},
        fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=digest(payload.encode())))
    target=ROOT/'tools/reference/pet-path-targets-source-evidence.json'
    if check:assert OUT.read_text()==payload and json.loads(target.read_text())==proof
    else:OUT.write_text(payload);target.write_text(json.dumps(proof,indent=2)+'\n')
    print('Path targets:',len(rows),'complete reset/getter cases;',proof['unsupportedCases'],'unsupported selector controls.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
