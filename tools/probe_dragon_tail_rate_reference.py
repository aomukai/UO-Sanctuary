#!/usr/bin/env python3
"""Execute original current-family Dragon Tail IAS branches and compare pinned KK metadata."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
from probe_weapon_sequence_rate import block,CALC_COMMIT,CALC
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 # Reuse the already compiled unchanged Skill class and declared UI surface.
 subprocess.run(['python3',str(ROOT/'tools/probe_assassin_claw_modern_rate_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 commit=json.loads((CALC/'commit.json').read_text());assert commit['sha']==CALC_COMMIT
 text=(CALC/'calculator.js').read_text();const=(CALC/'constants.js').read_text();initializer=next(x.strip().rstrip(',') for x in const.splitlines() if x.strip().startswith('DRAGON_TAIL:'))
 base=(STAGE/'assassin-claw-modern-rate-probe.js').read_text().split('let rows=[];',1)[0];base=re.sub(r'const skills=\{[^\n]*\};','const skills={'+initializer+'};',base,count=1)
 frames=block(text,'calculateFramesPerDirection',True);base+='\n'+frames+'\n'
 base+=r'''
wt.CLAW={getFramesPerDirection:()=>11};primaryWeapon.type=wt.CLAW;skill=skills.DRAGON_TAIL;
let rows=[];for(let d of [0,1])for(let p of [-30,-25,-20,-15,-10,-5,0,10])for(let s of [-30,10])for(let i of [0,20,120])for(let j of [0,40])for(let global of [0,35])for(let bonus of [-100,0,20,200])for(let slow of [0,40,85]){
 dual=!!d;primaryWeapon.WSM=p;secondaryWeapon={WSM:s,type:dual?wt.CLAW:wt.UNARMED};number.PRIMARY_WEAPON_IAS.value=i;number.SECONDARY_WEAPON_IAS.value=j;number.IAS.value=global;number.SLOWED_BY.value=slow;rate=bonus;
 let eias=calculateEIAS(true)[0],baseSpeed=calculateAnimationSpeed(wt.CLAW,13),advance=trun(baseSpeed*(100+limitEIAS(eias))/100);
 rows.push([d,p,s,i,j,global,bonus,slow,calculateFramesPerDirection(wt.CLAW),baseSpeed,eias,advance]);
}process.stdout.write(JSON.stringify(rows)+'\n');
'''
 p=STAGE/'dragon-tail-rate-source-probe.js';p.write_text(base);rows=json.loads(subprocess.check_output(['node',str(p)]));assert len(rows)==4608
 for dual,wsm,otherWsm,ias,otherIas,globalIas,bonus,slow,framesCount,baseSpeed,eias,advance in rows:
  expected=bonus-slow-40-wsm+120*(ias+globalIas)//(120+ias+globalIas)
  assert(framesCount,baseSpeed,eias,advance)==(13,256,expected,256*(100+max(-85,min(75,expected)))//100)
 fixture=STAGE/'dragon-tail-rate-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 metadata=json.loads((ROOT/'tools/reference/d2r-local-animation-evidence.json').read_text());kk=[x for x in metadata['records'] if x['code'].startswith('AIKK')];assert len(kk)==10 and all(x['frames']==13 and x['speed']==256 and x['events']==[[4,1]] for x in kk)
 raw=ROOT/metadata['localRawPath'];assert hashlib.sha256(raw.read_bytes()).hexdigest()==metadata['sha256']
 parent=json.loads((ROOT/'tools/reference/assassin-claw-modern-rate-evidence.json').read_text())
 e=dict(referenceVersion='3.3.93847',talentId='assassin.dragontail',handlerEnabled=False,originalAuthor=parent['originalAuthor'],initializer=initializer,reusedUnchangedClassHash=parent['unchangedClassHash'],reusedUnchangedFunctions=parent['unchangedFunctionHashes'],framesFunctionSha256=hashlib.sha256(frames.encode()).hexdigest(),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),jsSha256=hashlib.sha256(p.read_bytes()).hexdigest(),cases=len(rows)),localAnimation=dict(evidence='tools/reference/d2r-local-animation-evidence.json',rawSha256=metadata['sha256'],records=kk),established=['Actual original Dragon Tail Skill is not a sequence and does not use a dual-only averaging branch. EIAS uses primary WSM and primary+global diminishing IAS,with separate-40skill rate and supplied buffs/slows. No SQ-30penalty is added.','Actual original animation branch retains256base and13frames. All10current local AIKK weapon-class records independently show13frames,speed256 and action marker4.'],services=['Reuses declared UI buff/item/hand/visibility services from claw rate probe;only Burst skill-rate and SLOWED_BY are varied. Current executable stat collection and runtime rate posting are not compiled.','Original author calculator is supplementary primary current-family evidence;it is not a pinned3.3.93847 executable oracle. Raw current animation bytes are separately hash-verified.'],limits=['Frame metadata/advance are established in this bounded scope;exact server initial clock/action/end boundary and modifier lifetime still need composition and native input verification.','Charged guaranteed-hit/block,one-charge consumption,packet/stat/proc ordering and learned Dragon Tail remain gated.'])
 out=ROOT/'tools/reference/dragon-tail-rate-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Dragon Tail rate evidence stale'
 else:out.write_text(encoded)
 print('Dragon Tail rate:4608 unchanged original numeric contexts and10current local KK records pass;native scheduling remains gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
