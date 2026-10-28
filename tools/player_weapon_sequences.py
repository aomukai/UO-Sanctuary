#!/usr/bin/env python3
"""Read pinned 64-bit player sequence data, anchored by every classic descriptor."""
import hashlib,json,re,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills'
CODES=['HTH','1HT','2HT','1HS','2HS','BOW','XBW','STF','1JS','1JT','1SS','1ST','HT1','HT2']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(sequence):
 assert sequence in [24,25,26];inputs={}
 def pin(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
 tree=json.loads(pin(BASE/'d2moo-source-tree.json'));assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def legacy(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);return pin(p)
 text=legacy('source/D2Common/src/DataTbls/SequenceTbls.cpp');legacy('source/D2Common/include/DataTbls/SequenceTbls.h');legacy('source/D2Common/include/Units/Player.h')
 start=legacy('source/D2Game/src/SKILLS/Skills.cpp');assert re.search(r'gpSkillSrvStartFnTable_6FD408B0\[\]\s*=\s*\{(.*?)\};',start,re.S)[1].split(',')[68].strip()=='nullptr'
 ref=json.loads(pin(ROOT/'tools/reference/d2r-local-installation-evidence.json'));blob=(Path(ref['installationPath'])/ref['executable']['filename']).read_bytes();assert hashlib.sha256(blob).hexdigest()==ref['executable']['sha256']
 pe=struct.unpack_from('<I',blob,0x3c)[0];assert blob[pe:pe+4]==b'PE\0\0' and struct.unpack_from('<H',blob,pe+4)[0]==0x8664
 opt=pe+24;assert struct.unpack_from('<H',blob,opt)[0]==0x20b;base=struct.unpack_from('<Q',blob,opt+24)[0];sections=[]
 for i in range(struct.unpack_from('<H',blob,pe+6)[0]):
  off=opt+struct.unpack_from('<H',blob,pe+20)[0]+i*40;size,rva,rawsize,raw=struct.unpack_from('<IIII',blob,off+8);sections.append((rva,rawsize,raw))
 def offset(va):
  rva=va-base;s=next(s for s in sections if s[0]<=rva<s[0]+s[1]);return s[2]+rva-s[0]
 pointers=struct.unpack_from('<27Q',blob,offset(base+0x162ba30));assert pointers[0]==0
 constants={'PLRMODE_'+n:i for i,n in enumerate(['DEATH','NEUTRAL','WALK','RUN','GETHIT','TOWNNEUTRAL','TOWNWALK','ATTACK1','ATTACK2','BLOCK','CAST','THROW','KICK','SPECIAL1','SPECIAL2','SPECIAL3','SPECIAL4','DEAD','SEQUENCE','KNOCKBACK'])}
 constants.update({'ANIMSEQ_EVENT_'+n:i for i,n in enumerate(['NONE','MELEE_ATTACK','MISSILE_ATTACK','PLAY_SOUND','TRIGGER'])})
 def num(s):return constants[s.strip()] if s.strip() in constants else int(s.strip(),0)
 arrays={}
 for name,count,body in re.findall(r'D2AnimSeqTxt\s+(\w+)\[(\d+)\]\s*=\s*\{(.*?)\};',text,re.S):
  rows=[[num(s) for s in row.split(',')] for row in re.findall(r'\{([^{}]+)\}',body)];assert len(rows)==int(count);arrays[name]=rows
 records={}
 for name,body in re.findall(r'D2PlayerWeaponSequencesStrc\s+(\w+)\s*=\s*\{(.*?)\};',text,re.S):
  entries=re.findall(r'(\w+)\s*,\s*(\d+)\s*,\s*(\d+)',body);assert len(entries)<=14;records[name]=entries+[('0','0','0')]*(14-len(entries))
 names=re.findall(r'&(\w+)',re.search(r'gPlayerWeaponsSequenceTable\[24\]\s*=\s*\{(.*?)\};',text,re.S)[1]);assert len(names)==23
 def descriptor(slot,weapon):
  ptr,points,frames,aux,pad=struct.unpack_from('<Qiiii',blob,offset(pointers[slot])+24*weapon);assert 0<=points<=144 and 0<=frames<=144 and pad==0
  rows=[list(struct.unpack_from('<HBBBB',blob,offset(ptr)+i*6)) for i in range(points)] if points else []
  return dict(Code=CODES[weapon],Sequence=slot,Points=points,Frames=frames,RawAuxiliary=aux,Rows=rows)
 for slot,name in enumerate(names,1):
  for weapon,(array,points,frames) in enumerate(records[name]):
   d=descriptor(slot,weapon);assert (d['Points'],d['Frames'],d['Rows'])==(int(points),int(frames),arrays.get(array,[])),(slot,weapon,name)
 inputs[str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__).resolve())
 return [descriptor(sequence,i) for i in range(14)],inputs,ref['executable']['sha256']
