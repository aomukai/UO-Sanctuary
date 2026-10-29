#!/usr/bin/env python3
"""Sample only verified direct call/tail edges of one complete preserved function."""
import argparse,json,re
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups

def capture(parent_name,function,label,rvas,bounded_leaf=False):
 assert re.fullmatch('[a-z0-9-]+',label) and re.fullmatch('[a-z0-9-]+[.]json',parent_name)
 parentPath=STAGE/parent_name;parent=json.loads(parentPath.read_text());record=next(r for r in parent['records'] if int(r['rva'],16)==function);assert record['allUnwindFragmentsCaptured']
 for n,h in parent['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
 proofPath=ROOT/'tools/reference/generic-pet-search-nested-source-evidence.json';proof=json.loads(proofPath.read_text());annotation=next((r for r in proof['switchTables'] if int(r['rva'],16)==function),None)
 asmPaths=[];artifactParents=[parentPath,proofPath]
 for f in record['fragments']:
  assert f['captured'];bins=[ROOT/n for n,h in parent['artifacts'].items() if n.endswith('.bin') and h==f['sha256']];assert len(bins)==1
  binary=bins[0];asm=binary.with_suffix('.asm')
  if annotation:
   assert len(record['fragments'])==1 and annotation['inlineSwitchBytes']==24 and annotation['codeInvalidMarkers']==0
   binary=ROOT/'.local/class-skills/generic-pet-search-nested'/f'{function:x}-code.bin';asm=binary.with_suffix('.asm')
   assert digest(binary.read_bytes())==proof['artifacts'][str(binary.relative_to(ROOT))] and digest(asm.read_bytes())==proof['artifacts'][str(asm.relative_to(ROOT))]
   assert bins[0].read_bytes()[:annotation['codeAndPaddingBytes']]==binary.read_bytes()
  else:assert f['invalidInstructionMarkers']==0
  assert '(bad)' not in asm.read_text();asmPaths.append(asm);artifactParents += [binary,asm]
 edges={}
 for p in asmPaths:
  for kind,target in re.findall(r'\b(call|jmp)\s+0x([0-9a-f]+)',p.read_text()):
   rva=int(target,16)-0x140000000
   if kind=='jmp' and any(int(f['rva'],16)<=rva<int(f['endRva'],16) for f in record['fragments']):continue
   edges[rva]=kind
 assert set(rvas)<=set(edges),'Requested address is not a verified direct external edge'
 target=STAGE/('services-'+label+'.json');assert not target.exists(),'Preserve prior captures'
 image=Image();unwind=UnwindGroups(image);reader=CodeReader(image);records=[];artifacts=[]
 try:
  reader.anchors()
  for rva in rvas:
   leaf=False
   try:parts=unwind.fragments(rva)
   except StopIteration:
    assert bounded_leaf and not any(a<=rva<b for a,b,u in image.functions),'No primary unwind entry'
    end=min(a for a,b,u in image.functions if a>rva);assert 0<end-rva<=256,'Bounded leaf exceeds256bytes'
    parts=[(rva,end,0)];leaf=True
   r=dict(rva=hex(rva),bytes=sum(b-a for a,b,u in parts),fragments=[],semanticIdentityVerified=False)
   if leaf:r.update(boundedLeafReference=True,nextKnownUnwindStart=hex(parts[0][1]))
   for i,(begin,end,u) in enumerate(parts):
    size=end-begin;va=image.base+begin;entry=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None);f=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=entry[2] if entry else 'cross-map',captured=False)
    if entry and 'r' in entry[2] and 'x' in entry[2] and size<=4096 and reader.total+size*2<=8192:
     data=reader.stable(begin,size,'code');p=STAGE/f'services-{label}-{rva:x}-part{i}.bin';p.write_bytes(data);asm,bad=disassemble(p,begin,image.base);artifacts += [p,asm];f.update(captured=True,sha256=digest(data),invalidInstructionMarkers=bad)
    else:f['reason']='Not readable executable or outside this bounded capture'
    r['fragments'].append(f)
   r['allUnwindFragmentsCaptured']=not leaf and all(f['captured'] for f in r['fragments']);records.append(r)
 finally:reader.close()
 artifacts += artifactParents+[Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py']
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],parent=str(parentPath.relative_to(ROOT)),parentFunction=hex(function),sourceReferencesVerified=True,edges=[dict(rva=hex(r),kind=edges[r]) for r in rvas],process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,records=records,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))})
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,records=[{k:r[k] for k in ['rva','bytes','allUnwindFragmentsCaptured']} for r in records])))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--parent',required=True);p.add_argument('--function',type=lambda x:int(x,0),required=True);p.add_argument('--label',required=True);p.add_argument('--rvas',type=lambda x:int(x,0),nargs='+',required=True);p.add_argument('--bounded-leaf',action='store_true');a=p.parse_args();capture(a.parent,a.function,a.label,a.rvas,a.bounded_leaf)
