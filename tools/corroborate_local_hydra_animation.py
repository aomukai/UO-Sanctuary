#!/usr/bin/env python3
"""Corroborate all three Hydra models against the pinned local AnimData export."""
import argparse,hashlib,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 parent=ROOT/'tools/reference/d2r-local-animation-evidence.json';meta=json.loads(parent.read_text());rawpath=ROOT/meta['localRawPath'];assert sha(rawpath)==meta['sha256'];raw=rawpath.read_bytes()
 monsterpath=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json';monsters=json.loads(monsterpath.read_text());manifest=json.loads((monsterpath.parent.parent/'sources.json').read_text());assert sha(monsterpath)==manifest['files']['json/monstats.json']['sha256']
 codes={name:str(monsters[name]['Code']).upper() for name in ('hydra1','hydra2','hydra3')};assert codes=={'hydra1':'HX','hydra2':'21','hydra3':'HZ'}
 cursor=total=0;records=[]
 for _ in range(256):
  count=struct.unpack_from('<I',raw,cursor)[0];cursor+=4
  for _ in range(count):
   r=raw[cursor:cursor+160];assert len(r)==160;cursor+=160;total+=1;code=r[:8].split(b'\0')[0].decode('ascii')
   if code[:2] in {*codes.values(),'HY'}:
    frames,speed=struct.unpack_from('<II',r,8);records.append(dict(code=code,frames=frames,speed=speed,events=[[i,v] for i,v in enumerate(r[16:]) if v]))
 assert cursor==len(raw) and total==3661 and len(records)==20
 bycode={r['code']:r for r in records}
 for code in (*codes.values(),'HY'):
  for mode,frames,events in (('S2',12,[]),('NU',12,[]),('A1',12,[[8,2]]),('DT',10,[]),('DD',1,[])):
   key=code+mode+'HTH';assert bycode[key]==dict(code=key,frames=frames,speed=256,events=events)
 result=dict(referenceVersion='3.3.93847',source='Read-only local AnimData export previously pinned by d2r-local-animation-evidence.json',rawSha256=meta['sha256'],monsterCodes=codes,parsedRecords=total,records=records,
  established=['All three current Hydra model codes HX/21/HZ have12frame S2/neutral,12frame A1with marker2atframe8,10frame death and1frame dead metadata,at speed256. The HYalias has the same metadata but is not substituted for current middle-head code21.',
  'These current owned records corroborate the previously recorded emergence/attack marker counts and additionally provide death metadata. No live game interaction is needed to read these already extracted files.'],
  limits=['Metadata alone does not prove startup timing,AI think/expiry order,attack marker dispatch,animation completion/retirement or projectile lifetime after owner/head removal. Compose actual source scheduling and declare modern gaps separately.','No runtime/controller/data/install/save change or class completion. Final head/bolt/death effects/audio and talent/hotkey icons remain separate.'],
  artifacts={str(p.relative_to(ROOT)):sha(p) for p in (Path(__file__).resolve(),parent,monsterpath,rawpath)})
 target=ROOT/'tools/reference/d2r-local-hydra-animation-evidence.json';render=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print('Hydra current local animation:20records;HX/21/HZ12frame emergence/attack,frame8marker2,10frame death;outer timing still separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
