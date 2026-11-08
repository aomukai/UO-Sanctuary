#!/usr/bin/env python3
"""Extract owned Warlock SC rows and reuse the identical Necromancer source clock."""
import argparse,hashlib,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 priorpath=ROOT/'tools/reference/necromancer-cast-clock-engine-evidence.json';prior=json.loads(priorpath.read_text());assert prior['referenceVersion']=='3.3.93847'and prior['contexts']==2072
 for rel,h in prior['artifacts'].items():assert sha(ROOT/rel)==h,rel
 origin=ROOT/'tools/reference/druid-form-cast-clock-engine-evidence.json';owned=json.loads(origin.read_text());animation=ROOT/'.local/research/d2r-3.3.93847/AnimData.d2';assert sha(animation)==owned['animationSha256']
 raw=animation.read_bytes();offset=0;rows=[]
 for _ in range(256):
  n=struct.unpack_from('<I',raw,offset)[0];offset+=4
  for _ in range(n):
   code=raw[offset:offset+8].split(b'\0')[0].decode();frames,speed=struct.unpack_from('<II',raw,offset+8)
   if code.startswith('WKSC'):
    flags=[(i,f)for i,f in enumerate(raw[offset+16:offset+160])if f];assert flags==[(8,1)]and(frames,speed)==(16,256)
    rows.append(dict(WeaponClass=code[4:],Frames=frames,Speed=speed,Action=8,Flag=1))
   offset+=160
 assert offset==len(raw);rows.sort(key=lambda r:r['WeaponClass']);assert rows==sorted(prior['animations'],key=lambda r:r['WeaponClass'])and len(rows)==8
 skills=[r for r in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text())if r['characterClass']=='Warlock'and not r['passive']]
 admitted=[r for r in skills if r['sourceFields'].get('anim')=='SC'and not r['sourceFields'].get('UseAttackRate')];assert len(admitted)==22 and all(r['sourceFields']['seqtrans']=='SC'for r in admitted)
 excluded=[r['talentId']for r in skills if r not in admitted];assert set(excluded)=={'warlock.binddemon','warlock.cleave','warlock.mirroredblades','warlock.miasmachain'}
 runtime=ROOT/'data/Sanctuary/Balance/warlock-cast-clock.json';encoded=json.dumps(rows,indent=2)+'\n'
 proof=dict(referenceVersion='3.3.93847',animationSha256=sha(animation),priorClockEvidenceSha256=sha(priorpath),retainedSourceContexts=2072,newCompilerMatrices=0,animations=rows,sourceFixture=prior['fixture'],eligibleSourceIds=[r['sourceId']for r in admitted],excluded=excluded,currentExecutableParity=False,scope='Owned WKSC rows are identical in all numeric scheduling fields to the existing complete-source Necromancer fixture;reuse SourceCastClock(16,256,8). Declared ordinary human SC/FCR projection only. Miasma Chain carries UseAttackRate despite SC and stays separate with the three SQ routes. No active handler or current executable mode/skill dispatch parity follows from timing alone.')
 target=ROOT/'tools/reference/warlock-cast-clock-data-evidence.json';evidence=json.dumps(proof,indent=2)+'\n'
 if check:assert runtime.read_text()==encoded and target.read_text()==evidence
 else:runtime.write_text(encoded);target.write_text(evidence)
 print('Warlock clock:8owned WKSC rows,22ordinary SC candidates;identical2072case source clock reused;Chain/three SQ routes excluded;no compiler matrix rerun.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
