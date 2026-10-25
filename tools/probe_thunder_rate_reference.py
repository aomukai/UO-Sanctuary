#!/usr/bin/env python3
"""Execute unchanged original current-family Thunder rate functions."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from probe_weapon_sequence_rate import CALC
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_assassin_claw_modern_rate_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 parent=json.loads((ROOT/'tools/reference/assassin-claw-modern-rate-evidence.json').read_text());constants=(CALC/'constants.js').read_text();initializer=next(x.strip().rstrip(',') for x in constants.splitlines() if x.strip().startswith('CLAWS_OF_THUNDER:'))
 text=(STAGE/'assassin-claw-modern-rate-probe.js').read_text();old=parent['unchangedInitializers']['FISTS_OF_FIRE'];assert text.count(old)==1;text=text.replace(old,old+','+initializer).replace("['FISTS_OF_FIRE','DRAGON_CLAW']","['CLAWS_OF_THUNDER']");path=STAGE/'thunder-rate-source-probe.js';path.write_text(text);rows=json.loads(subprocess.check_output(['node',str(path)]));assert len(rows)==2304
 for name,dual,p,s,i,j,g,bonus,slow,base,points,eias,advance in rows:
  assert name=='CLAWS_OF_THUNDER';expected=bonus-slow-30-p+120*(g+i)//(120+g+i);assert(base,points,eias,advance)==(256,16 if dual else 12,expected,256*(100+max(-85,min(75,expected)))//100)
 fixture=STAGE/'thunder-rate-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n');e=dict(referenceVersion='3.3.93847',talentId='assassin.clawsofthunder',originalAuthor=parent['originalAuthor'],unchangedInitializer=initializer,unchangedFunctionHashes=parent['unchangedFunctionHashes'],unchangedClassHash=parent['unchangedClassHash'],fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),jsSha256=hashlib.sha256(path.read_bytes()).hexdigest(),cases=len(rows)),established=['Actual original Thunder initializer has the same sequence=true/dualOnly=false configuration as Fists. Full original functions select primary WSM/IAS,base256,sequence penalty30 and12/16points. Secondary WSM/IAS does not enter this branch.'],services=parent['services'],limits=['Supplementary original current-family implementation,not a pinned executable oracle.','Source scheduler/contact ordinal and actual native selected-hand/rank/cancellation/input/reload remain separate.']);out=ROOT/'tools/reference/thunder-rate-source-evidence.json';s=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==s,'Thunder rate evidence stale'
 else:out.write_text(s)
 print('Thunder:2304 unchanged original current-family rate/base/sequence contexts pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
