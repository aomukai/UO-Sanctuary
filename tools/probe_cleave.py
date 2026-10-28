#!/usr/bin/env python3
"""Read pinned current player SQ24 data; do not infer new start68/do164 bodies."""
import argparse,csv,hashlib,json,re,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'cleave'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def encoded(x):return json.dumps(x,indent=2)+'\n'
def build(check=False):
 STAGE.mkdir(exist_ok=True);inputs={}
 def pin(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
 tree=json.loads(pin(BASE/'d2moo-source-tree.json'));assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def legacy(rel):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);return pin(p)
 seq=legacy('source/D2Common/src/DataTbls/SequenceTbls.cpp')
 legacy('source/D2Common/include/DataTbls/SequenceTbls.h');legacy('source/D2Common/include/Units/Player.h')
 start=legacy('source/D2Game/src/SKILLS/Skills.cpp')
 slots=re.search(r'gpSkillSrvStartFnTable_6FD408B0\[\]\s*=\s*\{(.*?)\};',start,re.S)[1].split(',');assert slots[68].strip()=='nullptr'
 ref=json.loads(pin(ROOT/'tools/reference/d2r-local-installation-evidence.json'));p=Path(ref['installationPath'])/ref['executable']['filename'];b=p.read_bytes();assert hashlib.sha256(b).hexdigest()==ref['executable']['sha256']
 pe=struct.unpack_from('<I',b,0x3c)[0];assert b[pe:pe+4]==b'PE\0\0' and struct.unpack_from('<H',b,pe+4)[0]==0x8664
 opt=pe+24;assert struct.unpack_from('<H',b,opt)[0]==0x20b;base=struct.unpack_from('<Q',b,opt+24)[0];sections=[]
 for i in range(struct.unpack_from('<H',b,pe+6)[0]):
  off=opt+struct.unpack_from('<H',b,pe+20)[0]+i*40;size,rva,rawsize,raw=struct.unpack_from('<IIII',b,off+8);sections.append((rva,rawsize,raw))
 def offset(va):
  rva=va-base;s=next(s for s in sections if s[0]<=rva<s[0]+s[1]);return s[2]+rva-s[0]
 tableRva=0x162ba30;ptrs=struct.unpack_from('<27Q',b,offset(base+tableRva));assert ptrs[0]==0
 # Anchor layout/index using every classic descriptor, including zero-filled slots.
 modes={n:i for i,n in enumerate(['DEATH','NEUTRAL','WALK','RUN','GETHIT','TOWNNEUTRAL','TOWNWALK','ATTACK1','ATTACK2','BLOCK','CAST','THROW','KICK','SPECIAL1','SPECIAL2','SPECIAL3','SPECIAL4','DEAD','SEQUENCE','KNOCKBACK'])}
 constants={'PLRMODE_'+n:i for n,i in modes.items()};constants.update({'ANIMSEQ_EVENT_'+n:i for i,n in enumerate(['NONE','MELEE_ATTACK','MISSILE_ATTACK','PLAY_SOUND','TRIGGER'])})
 def num(s):return constants[s.strip()] if s.strip() in constants else int(s.strip(),0)
 arrays={}
 for name,count,body in re.findall(r'D2AnimSeqTxt\s+(\w+)\[(\d+)\]\s*=\s*\{(.*?)\};',seq,re.S):
  rows=[[num(s) for s in row.split(',')] for row in re.findall(r'\{([^{}]+)\}',body)];assert len(rows)==int(count);arrays[name]=rows
 records={}
 for name,body in re.findall(r'D2PlayerWeaponSequencesStrc\s+(\w+)\s*=\s*\{(.*?)\};',seq,re.S):
  entries=re.findall(r'(\w+)\s*,\s*(\d+)\s*,\s*(\d+)',body);assert len(entries)<=14;records[name]=entries+[('0','0','0')]*(14-len(entries))
 names=re.findall(r'&(\w+)',re.search(r'gPlayerWeaponsSequenceTable\[24\]\s*=\s*\{(.*?)\};',seq,re.S)[1]);assert len(names)==23
 def descriptor(slot,weapon):
  ptr,points,frames,aux,pad=struct.unpack_from('<Qiiii',b,offset(ptrs[slot])+24*weapon)
  assert 0<=points<=144 and 0<=frames<=144 and pad==0
  rows=[list(struct.unpack_from('<HBBBB',b,offset(ptr)+i*6)) for i in range(points)] if points else []
  return dict(points=points,frames=frames,aux=aux,padding=pad,rows=rows)
 for slot,name in enumerate(names,1):
  for weapon,(array,points,frames) in enumerate(records[name]):
   d=descriptor(slot,weapon);assert (d['points'],d['frames'],d['rows'])==(int(points),int(frames),arrays.get(array,[])),(slot,weapon,name)
 codes=['HTH','1HT','2HT','1HS','2HS','BOW','XBW','STF','1JS','1JT','1SS','1ST','HT1','HT2'];cleave=[]
 for i,code in enumerate(codes):
  d=descriptor(24,i);assert d['aux']==256 and d['points']==d['frames'] and all(r[0]==0 and r[1]==10 and r[3]==0 for r in d['rows']) and [r[4] for r in d['rows']].count(1)==1 and all(r[4] in [0,1] for r in d['rows'])
  cleave.append(dict(Code=code,Sequence=24,Points=d['points'],Frames=d['frames'],RawAuxiliary=d['aux'],Rows=d['rows']))
 manifest=json.loads(pin(ROOT/'tools/reference/d2r-named-equipment/sources.json'));tables={}
 for name in ['skills','skilldesc']:
  path=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(path)==manifest['files'][f'json/{name}.json']['sha256'];tables[name]=json.loads(pin(path))
 skill=tables['skills']['391'];desc=next(r for r in tables['skilldesc'].values() if r.get('skilldesc')=='cleave')
 assert skill['srvstfunc']==68 and skill['srvdofunc']==164 and skill['seqnum']==24 and skill['calc5']=='(lvl >= 20) ? 20 : lvl +10' and desc['desccalca3']=='clc5*pa10'
 for file,key,expected in [('.local/research/d2r-3.3.93847/skills.txt','skill',skill),('.local/class-skills/cleave/skilldesc.txt','skilldesc',desc)]:
  path=ROOT/file;pin(path)
  with path.open(encoding='utf-8-sig',newline='') as f:row=next(r for r in csv.DictReader(f,delimiter='\t') if r.get(key)==expected[key])
  if key=='skill':assert row['calc5']==skill['calc5'] and row['seqnum']=='24' and row['srvstfunc']=='68'
  else:assert row['desccalca3']==desc['desccalca3']
 extraction=json.loads(pin(STAGE/'skilldesc-extraction.json'));assert extraction['sha256']==sha(STAGE/'skilldesc.txt') and extraction['executableSha256']==ref['executable']['sha256'] and extraction['buildInfoSha256']==ref['buildInfo']['sha256'] and extraction['processMemoryRead']==False
 research=pin(ROOT/'.local/research/warlock-community/warlock-skills-notes.txt');assert '30' in research and 'Cleave' in research
 data=ROOT/'data/Sanctuary/Balance/cleave-sequence.json';payload=encoded(cleave)
 if check:assert data.read_text()==payload
 else:data.write_text(payload)
 probe=BASE/'cleave-source-probe.json';body=encoded(dict(classicDescriptorsMatched=322,sequence=24,weaponProfiles=cleave,legacyStart68IsNull=True,arcControls=[dict(rank=r,rawSlices=r+10 if r<20 else 20,tooltipDegrees=(r+10 if r<20 else 20)*12) for r in [1,19,20,21,255]]))
 if check:assert probe.read_text()==body
 else:probe.write_text(body)
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,executableSha256=ref['executable']['sha256'],readOnlyFileInspection=True,processMemoryRead=False,processInput=False,tableRva=hex(tableRva),recordStride=24,classicDescriptorsMatched=322,cleaveSequence=24,profileCount=14,legacyStart68IsNull=True,modernStartOrDoBodyVerified=False,auxiliaryWordSemanticsVerified=False,skill=skill,description=desc,inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in [data,probe,Path(__file__).resolve()]},
  established=['All23classic player sequences times14weapon slots match older point counts,frame counts and exact6-byte rows. This anchors the appended current SQ24 file data:one melee event,weapon-dependent16..22points,CAST10pose rows. Raw auxiliary256is retained without an invented meaning. File-data corroboration is not a current getter/dispatcher or clock-rate certificate.',
   'Pinned current skill391and owned skills/skilldesc files agree:physical ED20+5per extra rank+10per hard MirroredBlades/HexPurge/EldritchBlast;AR50+7per extra rank;dm34speed10..30;mana3;raw slices lvl+10below20and20thereafter;tooltip degrees slices*12.'],
  remaining=['Older start slot68is null,including DoubleSwing;do68BasicShout is unrelated. New start68/do164 bodies,payment,release,target selection/range/arc,hit chronology and weapon/durability order remain unavailable. PlayerSQ24is not NPC monseq wargoatmancleave.',
   'Rank20arc conflict:owned current table/tooltip supplies20slices=240degrees,where supplied research describes30=360degrees. No actual geometry is selected. Supplied advance/framebonus event plans and explicit qualified single-target weapon contacts are prepared services,not a learned cast or proof of modern capture timing.',
   'CAST10poses do not establish FCR:skill isSQ/UseAttackRate1. Frame rate,auxiliary word and mode/input/client behavior remain separately unverified.'])
 target=ROOT/'tools/reference/cleave-source-evidence.json';body=encoded(result)
 if check:assert target.read_text()==body
 else:target.write_text(body)
 print('Cleave current SQ24:322classic descriptors corroborated,14new weapon rows;start68/do164 and rank20arc remain unresolved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
