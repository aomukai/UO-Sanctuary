#!/usr/bin/env python3
"""Record pinned local e9/lg animation metadata without assuming shared shot timing."""
import argparse,hashlib,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def build(check=False):
 monsterPath=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json';monsters=json.loads(monsterPath.read_bytes());manifest=json.loads((monsterPath.parent.parent/'sources.json').read_text());assert hashlib.sha256(monsterPath.read_bytes()).hexdigest()==manifest['files']['json/monstats.json']['sha256'];assert monsters['wakeofdestruction']['Code']=='e9' and monsters['chargeboltsentry']['Code']=='lg'
 parent=json.loads((ROOT/'tools/reference/d2r-local-animation-evidence.json').read_text());p=ROOT/parent['localRawPath'];raw=p.read_bytes();assert hashlib.sha256(raw).hexdigest()==parent['sha256'];cursor=0;records=[];total=0
 for bucket in range(256):
  n=struct.unpack_from('<I',raw,cursor)[0];cursor+=4
  for _ in range(n):
   record=raw[cursor:cursor+160];assert len(record)==160;cursor+=160;total+=1;code=record[:8].split(b'\0')[0].decode('ascii');frames,speed=struct.unpack_from('<II',record,8)
   if code[:2] in ['E9','LG']:records.append(dict(code=code,frames=frames,speed=speed,events=[[i,v] for i,v in enumerate(record[16:]) if v]))
 assert cursor==len(raw) and total==3661 and len(records)==11
 lookup={x['code']:x for x in records};assert lookup['E9S2HTH']==dict(code='E9S2HTH',frames=2,speed=256,events=[[1,1]]) and lookup['LGS2HTH']==dict(code='LGS2HTH',frames=15,speed=256,events=[[0,2]])
 assert lookup['E9S1HTH']['frames']==lookup['LGS1HTH']['frames']==15
 e=dict(referenceVersion='3.3.93847',source='Strict local CASC extraction already pinned by d2r-local-animation-evidence.json',parentEvidence='tools/reference/d2r-local-animation-evidence.json',rawSha256=parent['sha256'],monsterTableSha256=hashlib.sha256(monsterPath.read_bytes()).hexdigest(),monsterCodes={'wakeofdestruction':'e9','chargeboltsentry':'lg'},parsedRecords=total,records=records,established=['Wake e9and lightning lgshare15frame S1metadata,but their S2metadata differs:Wake2frames/action marker1atframe1;lightning15frames/marker2atframe0.','Wake DTmetadata is25frames;lightning DT23. Both have one-frame DDrecords. The Wake neutral record has2frames,lightning15.'],limits=['Animation metadata is not a complete live scheduler/mode/rate/event proof. Explicitly trace the distinct S2marker routing and actor skill before reusing any installed controller.','No learned skill is enabled and no executable/current-code behavior is inferred solely from these records. Final actor/animation/audio assets remain separately pending.'])
 out=ROOT/'tools/reference/d2r-local-sentry-animation-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Sentry animation evidence stale'
 else:out.write_text(encoded)
 print('Local sentry animation:11records pinned;S1shared15frames,S2Wake2/frame1marker1versuslightning15/frame0marker2;no timing equivalence assumed.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
