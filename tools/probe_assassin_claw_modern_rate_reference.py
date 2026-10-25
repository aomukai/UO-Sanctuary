#!/usr/bin/env python3
"""Execute original pinned current-family calculator claw rate/sequence functions."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from probe_weapon_sequence_rate import block,CALC_COMMIT,CALC
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 commit=json.loads((CALC/'commit.json').read_text());tree=json.loads((CALC/'tree.json').read_text());assert commit['sha']==CALC_COMMIT and tree['sha']==commit['commit']['tree']['sha'];hashes={}
 for name in ['README.md','calculator.js','constants.js']:
  raw=(CALC/name).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==name);hashes[name]=hashlib.sha256(raw).hexdigest()
 calc=(CALC/'calculator.js').read_text();const=(CALC/'constants.js').read_text();start=const.index('class Skill {');end=start+len('class Skill {');depth=1
 while depth:depth+=(const[end]=='{')-(const[end]=='}');end+=1
 cls=const[start:end];names=['calculateAnimationSpeed','calculateSIAS','calculateEIAS','getWSM','getWeaponIAS','getSequence','isSecondWeaponSet','isCharacterSelected','limitEIAS','trun'];bodies={n:block(calc,n,True) for n in names};bodies['convertIAStoEIAS']=block(const,'convertIAStoEIAS',True)
 initializers={n:next(line.strip().rstrip(',') for line in const.splitlines() if line.strip().startswith(n+':')) for n in ['FISTS_OF_FIRE','DRAGON_CLAW']}
 speedLine=next(line.strip() for line in calc.splitlines() if 'speedIncrease = trun(animationSpeed * (100 + limitEIAS(EIAS + accelerationModified)) / 100);' in line)
 js='''const skillsMap=new Map();function addSkill(x){return x;}\n'''+cls+'\nconst skills={'+','.join(initializers.values())+'''};
const container=Object.fromEntries(['FANATICISM','BURST_OF_SPEED','WEREWOLF','FRENZY','MAUL','PURGE','CLEAVE','MIRRORED_BLADES','HOLY_FREEZE','MARK_OF_BEAR','LETHARGY','PRIMARY_WEAPON_IAS','SECONDARY_WEAPON_IAS','SECONDARY_WEAPON'].map(x=>[x,x]));
const tv={EIAS:0,IAS:1,PRIMARY_WEAPON_IAS:2,SECONDARY_WEAPON_IAS:3},wf={HUMAN:0},wt={CLAW:1,UNARMED:0},char={ASSASSIN:6,BARBARIAN:4,FRENZY_BARBARIAN:9};
const other={MIN_EIAS:-85,MAX_EIAS:75,MAX_EIAS_WEREFORMS:150};let tableVariable=tv.EIAS,character=char.ASSASSIN,wereform=wf.HUMAN,skill,dual=false,rate=0;
let primaryWeapon={type:wt.CLAW,WSM:0},secondaryWeapon={type:wt.UNARMED,WSM:0};
const number={IAS:{value:0},PRIMARY_WEAPON_IAS:{value:0},SECONDARY_WEAPON_IAS:{value:0},SLOWED_BY:{value:0}};
const constants={checkbox:Object.fromEntries(['MARK_OF_BEAR','DECREPIFY','CHILLED','LETHARGY'].map(x=>[x,{checked:false}]))};const speed={BURST_OF_SPEED:{calculate:()=>rate}};
function isElementHidden(x){if(x==container.BURST_OF_SPEED||x==container.PRIMARY_WEAPON_IAS)return false;if(x==container.SECONDARY_WEAPON_IAS||x==container.SECONDARY_WEAPON)return !dual;return true;}function log(){}
'''+ '\n'.join(bodies.values())+'\nconstants.convertIAStoEIAS=convertIAStoEIAS;\n'+'''
let rows=[];for(let name of ['FISTS_OF_FIRE','DRAGON_CLAW'])for(let d of [0,1])for(let p of [-30,-25,-20,-15,-10,-5,0,10])for(let s of [-30,10])for(let i of [0,20,120])for(let j of [0,40])for(let global of [0,35])for(let bonus of [0,20])for(let slow of [0,40,85]){
skill=skills[name];dual=!!d;primaryWeapon.WSM=p;secondaryWeapon={WSM:s,type:dual?wt.CLAW:wt.UNARMED};number.PRIMARY_WEAPON_IAS.value=i;number.SECONDARY_WEAPON_IAS.value=j;number.IAS.value=global;number.SLOWED_BY.value=slow;rate=bonus;
let values=calculateEIAS(true),EIAS=values[0],animationSpeed=calculateAnimationSpeed(wt.CLAW,11),accelerationModified=0,speedIncrease;
'''+speedLine+'''
rows.push([name,d,p,s,i,j,global,bonus,slow,animationSpeed,getSequence(wt.CLAW),EIAS,speedIncrease]);
}process.stdout.write(JSON.stringify(rows)+'\\n');
'''
 p=STAGE/'assassin-claw-modern-rate-probe.js';p.write_text(js);data=json.loads(subprocess.check_output(['node',str(p)]));assert len(data)==4608
 for name,dual,p,s,i,j,g,bonus,slow,base,frames,eias,advance in data:
  ie=lambda x:120*x//(120+x)
  expected=int(bonus-slow-30-(p+s)/2+(ie(g+i)+ie(g+(j if dual else 0)))/2) if name=='DRAGON_CLAW' else bonus-slow-30-p+ie(g+i)
  assert(base,frames,eias,advance)==(256,16 if dual else 12,expected,256*(100+max(-85,min(75,expected)))//100)
 fixture=STAGE/'assassin-claw-modern-rate-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',originalAuthor=dict(repository='https://github.com/Warren1001/IAS_Calculator',commit=CALC_COMMIT,date=commit['commit']['committer']['date'],fileHashes=hashes,scope='Original implementation with README credit for D2R3.3 player-sequence dump; supplementary current-family primary evidence, not a3.3.93847 executable oracle.'),unchangedClassHash=hashlib.sha256(cls.encode()).hexdigest(),unchangedInitializers=initializers,unchangedFunctionHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in bodies.items()},unchangedSpeedStatement=speedLine,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(data),fistsCases=2304,dragonControls=2304),established=['Original animation-speed function explicitly excludes Fists/Thunder/Ice/Dragon finishers from the208 ordinary claw branch, retaining256. Sequence getter returns12single/16dual. Ordinary A1HT metadata208 must not be substituted for sequence base.','Original actual Fists Skill initializer has dualOnly=false; full calculateEIAS therefore uses primary WSM and primary weapon IAS plus global IAS even with a secondary equipped. This conflicts with the old engine dual intrinsic correction and supersedes that legacy candidate policy; retain the conflict explicitly.','Actual Dragon Claw initializer dualOnly=true instead uses mean WSM and mean separately diminished handIAS, with truncation after combining those terms. These are original function controls, not a Dragon Claw handler.'],services=['Buff UI exposes supplied Burst skill-rate only; other buffs hidden, named checkbox slows false and supplied slow enters the original SLOWED_BY input. Global/weapon IAS and weapon identities are explicit UI services.','Primary/secondary field visibility and table-variable EIAS are declared inputs; no complete DOM table rendering or user-interface workflow certificate. The unchanged selected speed statement is executed with acceleration0.','Native source stat collection,selected hands,current executable/mode/event/seed/client behavior and eligibility remain separate. This corroborates current-family implementation, not live pinned-build testing.'],limits=['Current original calculator versus old engine dual WSM/IAS discrepancy is visible; native Fists adopts the current-family original Fists branch rather than inventing a blend.','The early208 candidate was stopped before installation; its passing tests established the supplied older context, not the missing base-selection premise.','Primary-hand chance-to-hit sharing was reported firsthand in2022; applicability to3.3.93847 remains uncertain and is not silently adopted.'],firsthandArReport='https://us.forums.blizzard.com/en/d2r/t/dual-wielded-sequence-skills-only-use-primary-hands-chance-to-hit/137434')
 out=ROOT/'tools/reference/assassin-claw-modern-rate-evidence.json';s=json.dumps(evidence,indent=2)+'\n'
 if check:assert out.read_text()==s,'Modern claw evidence stale'
 else:out.write_text(s)
 print('Modern claw:4608 unchanged original function contexts; Fists primary WSM/IAS,base256;Dragon distinct averaging controls pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
