#!/usr/bin/env python3
"""Bounded, read-only reference-code sampling; no attach, input or writes.

Only pinned PE headers, a known immutable player-sequence table and readable
executable ranges may be sampled. Runtime binaries/disassembly stay ignored.
--check is offline and never opens process memory.
"""
import argparse,hashlib,json,os,struct,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/warlock-runtime-reference'
def digest(data):return hashlib.sha256(data).hexdigest()
class Image:
 def __init__(self):
  self.reference=ROOT/'tools/reference/d2r-local-installation-evidence.json';self.ref=json.loads(self.reference.read_text());self.path=Path(self.ref['installationPath'])/self.ref['executable']['filename'];self.data=self.path.read_bytes();assert digest(self.data)==self.ref['executable']['sha256']
  pe=struct.unpack_from('<I',self.data,0x3c)[0];assert self.data[pe:pe+4]==b'PE\0\0' and struct.unpack_from('<H',self.data,pe+4)[0]==0x8664
  opt=pe+24;assert struct.unpack_from('<H',self.data,opt)[0]==0x20b;self.base=struct.unpack_from('<Q',self.data,opt+24)[0];self.sections=[]
  for i in range(struct.unpack_from('<H',self.data,pe+6)[0]):
   off=opt+struct.unpack_from('<H',self.data,pe+20)[0]+i*40;size,rva,rawsize,raw=struct.unpack_from('<IIII',self.data,off+8);self.sections.append(dict(name=self.data[off:off+8].rstrip(b'\0').decode(),rva=rva,size=size,raw=raw,rawsize=rawsize))
  rva,size=struct.unpack_from('<II',self.data,opt+112+3*8);off=self.offset(rva);self.functions=[struct.unpack_from('<III',self.data,q) for q in range(off,off+size,12)]
 def section(self,rva,size=1):return next(s for s in self.sections if s['rva']<=rva and rva+size<=s['rva']+s['rawsize'])
 def offset(self,rva):
  s=self.section(rva);return s['raw']+rva-s['rva']
 def bytes(self,rva,size):self.section(rva,size);off=self.offset(rva);return self.data[off:off+size]
def identity(pid):
 p=Path('/proc')/str(pid);args=(p/'cmdline').read_bytes().split(b'\0')
 assert any(a.strip(b'"').replace(b'\\',b'/').rsplit(b'/',1)[-1].lower()==b'd2r.exe' for a in args),'Not a D2R process'
 assert p.stat().st_uid==os.getuid(),'Different process owner'
 return dict(pid=pid,startTicks=int((p/'stat').read_text().rsplit(')',1)[1].split()[19]),name=(p/'comm').read_text().strip())
def processes():
 result=[]
 for p in Path('/proc').iterdir():
  if not p.name.isdigit():continue
  try:result.append(identity(int(p.name)))
  except (OSError,ValueError,AssertionError):pass
 return result
class CodeReader:
 def __init__(self,image):
  candidates=processes();assert len(candidates)==1,{'matchingProcesses':candidates};self.identity=candidates[0];self.image=image;self.total=0;self.reads=[]
  maps=(Path('/proc')/str(self.identity['pid'])/'maps').read_text().splitlines();self.maps=[]
  for line in maps:
   parts=line.split();start,end=(int(x,16) for x in parts[0].split('-'));self.maps.append((start,end,parts[1]))
  self.fd=os.open('/proc/'+str(self.identity['pid'])+'/mem',os.O_RDONLY)
 def close(self):os.close(self.fd)
 def stable(self,rva,size,kind):
  assert identity(self.identity['pid'])==self.identity,'Process identity changed'
  assert 0<size<=4096 and self.total+2*size<=8192,'Read budget exceeded'
  if kind=='header':assert rva==0 and size==64
  elif kind=='sequence-table':assert rva==0x162ba30 and size==27*8 and self.image.section(rva,size)['name']=='.rdata'
  elif kind=='code':assert self.image.section(rva,size)['name']=='.text'
  else:raise AssertionError('Unsupported sampling kind')
  va=self.image.base+rva;entry=next(m for m in self.maps if m[0]<=va and va+size<=m[1]);assert 'r' in entry[2] and (kind!='code' or 'x' in entry[2]),'Range is not readable executable code'
  first=os.pread(self.fd,size,va);second=os.pread(self.fd,size,va);self.total+=2*size
  assert len(first)==size and first==second and identity(self.identity['pid'])==self.identity,'Unstable or incomplete sample'
  self.reads.append(dict(kind=kind,rva=hex(rva),size=size,permissions=entry[2],stable=True,sha256=digest(first)));return first
 def anchors(self):
  assert self.stable(0,64,'header')==self.image.data[:64],'DOS image header differs'
  assert self.stable(0x162ba30,27*8,'sequence-table')==self.image.bytes(0x162ba30,27*8),'Pinned immutable sequence table differs'
def disassemble(path,rva,base):
 out=subprocess.check_output(['objdump','-D','-b','binary','-m','i386:x86-64','-M','intel','--adjust-vma='+hex(base+rva),str(path)],text=True);target=path.with_suffix('.asm');target.write_text(out);return target,out.count('(bad)')
def capture():
 STAGE.mkdir(exist_ok=True);target=STAGE/'code-sample.json';assert not target.exists(),'Existing capture must remain frozen; select a fresh stage for later captures'
 image=Image();reader=CodeReader(image)
 try:
  reader.anchors();candidate=None
  for begin,end,unwind in image.functions:
   if end-begin<32:continue
   size=min(128,end-begin);va=image.base+begin
   if any(a<=va and va+size<=b and 'r' in perms and 'x' in perms for a,b,perms in reader.maps):candidate=(begin,end,size);break
  assert candidate;begin,end,size=candidate;runtime=reader.stable(begin,size,'code');disk=image.bytes(begin,size)
 finally:reader.close()
 rp=STAGE/'sample-runtime.bin';dp=STAGE/'sample-file.bin';rp.write_bytes(runtime);dp.write_bytes(disk);ra,rb=disassemble(rp,begin,image.base);da,db=disassemble(dp,begin,image.base)
 artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in [rp,dp,ra,da,Path(__file__).resolve()]}
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],process=reader.identity,pinnedHeaderMatches=True,pinnedImmutableSequenceTableMatches=True,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,
  sample=dict(rva=hex(begin),functionEndRva=hex(end),size=size,changedBytes=sum(a!=b for a,b in zip(runtime,disk)),runtimeInvalidInstructionMarkers=rb,fileInvalidInstructionMarkers=db,functionIdentityVerified=False),
  processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,goalComplete=False,currentMechanicsVerified=False,artifacts=artifacts,
  established='A bounded stable readable executable sample and known immutable image anchors only. This does not identify a skill callback or prove mechanics. No heap/save/authentication data is sampled.',next='If current code decodes coherently,follow specific immutable function-table references and verify dispatcher/identity links before using a body. Never read inaccessible pages or reuse a stale PID.')
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:result[k] for k in ['bytesRead','sample','currentMechanicsVerified']}))
def check():
 result=json.loads((STAGE/'code-sample.json').read_text());image=Image();assert result['executableSha256']==image.ref['executable']['sha256']
 for name,value in result['artifacts'].items():assert digest((ROOT/name).read_bytes())==value,name
 assert result['pinnedHeaderMatches'] and result['pinnedImmutableSequenceTableMatches'] and result['bytesRead']<=8192 and not any(result[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten','currentMechanicsVerified'])
 assert all(r['stable'] and 'r' in r['permissions'] and (r['kind']!='code' or 'x' in r['permissions']) for r in result['reads']);print('Bounded runtime sample verified offline; no process access during this check.')
if __name__=='__main__':
 p=argparse.ArgumentParser();g=p.add_mutually_exclusive_group(required=True);g.add_argument('--capture',action='store_true');g.add_argument('--check',action='store_true');args=p.parse_args();capture() if args.capture else check()
