#!/usr/bin/env python3
"""Read bounded skill-do code through its preserved current dispatcher table."""
import argparse,json,os,re,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble,identity
from pe_unwind_groups import UnwindGroups
TABLE=0x163a8b0;COUNT=191
def capture(label,slots):
 assert re.fullmatch('[a-z0-9-]+',label)and len(slots)==len(set(slots))and all(0<s<COUNT for s in slots)
 parent=STAGE/'services-shadow-skill-dispatch-20261107.json';proof=json.loads(parent.read_text())
 record=next(r for r in proof['records']if r['rva']=='0x3aaf70');assert record['allUnwindFragmentsCaptured']and len(record['fragments'])==1
 fragment=record['fragments'][0];binary=next(ROOT/n for n,h in proof['artifacts'].items()if n.endswith('.bin')and h==fragment['sha256'])
 asm=binary.with_suffix('.asm');assert digest(binary.read_bytes())==fragment['sha256']and digest(asm.read_bytes())==proof['artifacts'][str(asm.relative_to(ROOT))]
 text=asm.read_text();assert all(s in text for s in ['[r13+0x4e]','mov    eax,0xbe','[r10+rax*8+0x163a8b0]','call   r10'])and '(bad)'not in text
 stage=STAGE/label;stage.mkdir(exist_ok=False);target=stage/'capture.json';image=Image();unwind=UnwindGroups(image);reader=CodeReader(image);records=[];artifacts=[parent,binary,asm,Path(__file__).resolve()]
 try:
  reader.anchors();size=COUNT*8;va=image.base+TABLE;section=image.section(TABLE,size);assert section['name']=='.rdata'
  entry=next(m for m in reader.maps if m[0]<=va and va+size<=m[1]);assert 'r'in entry[2]and'w'not in entry[2]
  raw=os.pread(reader.fd,size,va);again=os.pread(reader.fd,size,va);reader.total+=len(raw)+len(again)
  assert raw==again==image.bytes(TABLE,size)and identity(reader.identity['pid'])==reader.identity
  reader.reads.append(dict(kind='immutable-skill-do-table',rva=hex(TABLE),size=size,permissions=entry[2],stable=True,sha256=digest(raw)))
  pointers=struct.unpack('<191Q',raw)
  for slot in slots:
   rva=pointers[slot]-image.base;r=dict(slot=slot,rva=hex(rva),dispatcherIndexLinked=True,fragments=[])
   for i,(begin,end,u)in enumerate(unwind.fragments(rva)):
    va=image.base+begin;size=end-begin;entry=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None)
    f=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=entry[2]if entry else'cross-map',captured=False)
    if entry and'r'in entry[2]and'x'in entry[2]and size<=4096 and reader.total+size*2<=8192:
     raw=reader.stable(begin,size,'code');p=stage/f'do-{slot}-part{i}.bin';p.write_bytes(raw);dis,bad=disassemble(p,begin,image.base);artifacts.extend([p,dis]);f.update(captured=True,sha256=digest(raw),invalidInstructionMarkers=bad)
    else:f['reason']='Not readable executable or outside bounded capture budget;no forced read'
    r['fragments'].append(f)
   r['allUnwindFragmentsCaptured']=all(f['captured']for f in r['fragments']);records.append(r)
 finally:reader.close()
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,tableRva=hex(TABLE),immutableTableMatches=True,dispatcherIndexLinked=True,callbackSemanticsVerified=False,records=records,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,process=reader.identity,records=[{k:r[k]for k in ['slot','rva','allUnwindFragmentsCaptured']}for r in records])))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--label',required=True);p.add_argument('--slots',type=int,nargs='+',required=True);a=p.parse_args();capture(a.label,a.slots)
