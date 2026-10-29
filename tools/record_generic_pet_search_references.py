#!/usr/bin/env python3
"""Preserve bounded nested references and annotate an inline switch table."""
import argparse,json,re,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,digest,disassemble
OUT=ROOT/'tools/reference/generic-pet-search-nested-reference-evidence.json'
LOCAL=ROOT/'.local/class-skills/generic-pet-search-services'
def build(check=False):
 artifacts=[Path(__file__).resolve(),ROOT/'tools/capture_reference_callees.py',ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py'];records=[];reads=0;sizeX=None
 for label in ['search-allegiance','search-geometry','search-state-mask','search-alignment']:
  p=STAGE/('callees-'+label+'-idle-20261030.json');r=json.loads(p.read_text());artifacts.append(p)
  assert r['referenceVersion']=='3.3.93847' and r['directCallTargetsVerified'] and r['bytesRead']<=8192 and r['executableSha256']=='e1f5436e3d9687f644ef16938b1b183d1fdef434f18cf66d852cf68f48cc8936'
  assert not any(r[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten']);reads+=r['bytesRead']
  for n,h in r['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n;artifacts.append(ROOT/n)
  for rec in r['records']:
   assert rec['allUnwindFragmentsCaptured'] and all(f['captured'] for f in rec['fragments'])
   bad=sum(f['invalidInstructionMarkers'] for f in rec['fragments']);assert bad==(1 if rec['rva']=='0x277db0' else 0)
   records.append(dict(rva=rec['rva'],bytes=rec['bytes'],rawDisassemblyMarkers=bad,allUnwindFragmentsCaptured=True))
   if rec['rva']=='0x277db0':
    assert len(rec['fragments'])==1 and rec['bytes']==176
    paths=[ROOT/n for n,h in r['artifacts'].items() if n.endswith('.bin') and h==rec['fragments'][0]['sha256']];assert len(paths)==1;sizeX=paths[0].read_bytes()
 assert len(records)==10 and sum(r['bytes'] for r in records)==1976 and reads==6192 and sizeX is not None
 code,table=sizeX[:152],sizeX[152:];assert len(table)==24
 codePath=LOCAL/'size-x-instructions.bin';asmPath=codePath.with_suffix('.asm')
 if check:assert codePath.read_bytes()==code
 else:codePath.write_bytes(code)
 asm,bad=disassemble(codePath,0x277db0,0x140000000);assert not bad
 text=asm.read_text();assert 'r9+rax*4+0x277e48' in text and 'cmp    eax,0x5' in text and 'jmp    rdx' in text
 starts={int(v,16)-0x140000000 for v in re.findall(r'^\s+([0-9a-f]+):',text,re.M)};targets=list(struct.unpack('<6I',table))
 assert targets==[0x277de1,0x277deb,0x277e05,0x277e21,0x277e17,0x277e3e] and all(t in starts for t in targets)
 artifacts += [codePath,asmPath]
 result=dict(referenceVersion='3.3.93847',component='Nested search references only; not mechanical acceptance',processMemoryRead=True,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,handlerEnabled=False,nativeMechanicsAccepted=False,
 verified=dict(completeFunctions=10,completeFunctionBytes=1976,instructionAndPaddingBytes=1952,inlineSwitchDataBytes=24,totalReadBytes=6192,maximumReadBytesPerCall=3108),records=records,
 instructionDataAnnotation=dict(rva='0x277db0',codeBytes=152,tableRva='0x277e48',tableBytes=24,entries=targets,rawInvalidMarkers=1,codeOnlyInvalidMarkers=0,reason='The six-entry switch table is included in the captured unwind range. The single raw disassembly marker lies in table data;all six RVA entries target actual code instruction boundaries. The original capture and raw disassembly are preserved.'),
 artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},
 remaining=['These are cached references,not accepted managed behavior. Compare nested allegiance/alignment,player relation,point/ray geometry,size and corpse-mask services next. Their own nested metadata/stat/collision services still need explicit contracts;do not infer Britannia geometry or ownership from these captures.'])
 encoded=json.dumps(result,indent=2)+'\n'
 if check:assert OUT.read_text()==encoded
 else:OUT.write_text(encoded)
 print('Nested search references:10complete functions/1976bytes;inline24-byte switch table annotated;6192bounded read bytes;mechanical comparison pending.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
