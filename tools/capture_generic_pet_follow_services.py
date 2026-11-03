#!/usr/bin/env python3
"""Fresh bounded read-only captures of verified follow movement/warp callees.

The historical runtime-reference directory stays frozen. CodeReader retains its
existing same-owner identity, image anchors, RX-only and8192-byte read limits.
"""
import argparse,json,subprocess
from pathlib import Path
import capture_reference_callees as callees
from probe_warlock_runtime import ROOT,STAGE as FROZEN,Image,digest
FRESH=ROOT/'.local/class-skills/generic-pet-live-follow-reference-20261103'
def prepare():
    FRESH.mkdir(exist_ok=True)
    image=Image();original=FROZEN/'callees-live-actions-execute-20261029.json'
    parent=json.loads(original.read_text())
    for n,h in parent['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
    row=next(r for r in parent['records'] if r['rva']=='0x4b16c0');assert row['allUnwindFragmentsCaptured']
    binary=FROZEN/'callees-live-actions-execute-20261029-4b16c0-part0.bin';data=binary.read_bytes()
    assert len(data)==2460 and digest(data)==row['fragments'][0]['sha256']
    assert data[2434:2436]==b'\xc3\x90'
    asm=subprocess.check_output(['objdump','-D','-b','binary','-m','i386:x86-64','-M','intel',
        '--adjust-vma='+hex(image.base+0x4b16c0),'--stop-address='+hex(image.base+0x4b2044),str(binary)],text=True)
    assert '(bad)' not in asm
    code=FRESH/'follow-executor-code-only.asm';manifest=FRESH/'follow-executor-parent.json'
    result=dict(referenceVersion='3.3.93847',records=[row],
        derivedView='Complete captured function;disassembly stops before the verified24-byte inline jump table. Original bytes are unchanged.',
        artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [original,binary,Path(__file__).resolve(),image.reference]})
    result['artifacts'][str(code.relative_to(ROOT))]=digest(asm.encode())
    if manifest.exists():assert code.read_text()==asm and json.loads(manifest.read_text())==result
    else:code.write_text(asm);manifest.write_text(json.dumps(result,indent=2)+'\n')
    original=FROZEN/'callees-live-follow-services-idle-20261030.json'
    copy=FRESH/original.name
    if copy.exists():assert copy.read_bytes()==original.read_bytes()
    else:copy.write_bytes(original.read_bytes())
    callees.STAGE=FRESH
def capture(part):
    prepare()
    if part=='movement':callees.capture('callees-live-follow-services-idle-20261030.json','follow-movement-20261103',[0x425bb0,0x373f60])
    else:callees.capture('follow-executor-parent.json','follow-warp-20261103',[0x40e7d0])
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('part',choices=['movement','warp']);capture(p.parse_args().part)
