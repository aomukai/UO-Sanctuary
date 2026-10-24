#!/usr/bin/env python3
"""Run the pinned original IAS calculator's unchanged Strafe rollback block.

Declared animation/weapon/rate inputs, not a current executable capture.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
folder=ROOT/'.local/research/ias-calculator-b7d7910';commit=json.loads((folder/'commit.json').read_text());tree=json.loads((folder/'tree.json').read_text());assert commit['sha']=='b7d791031b73b0c6e56a3659e9198578aed21261' and tree['sha']==commit['commit']['tree']['sha'];hashes={};functions={}
for name in ['README.md','calculator.js','constants.js']:
 raw=(folder/name).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(v['sha'] for v in tree['tree'] if v['path']==name);hashes[name]=hashlib.sha256(raw).hexdigest()
calc=(folder/'calculator.js').read_text()
def function(name):
 m=re.search(r'\bfunction\s+'+name+r'\([^;\n]*\)\s*\{',calc);assert m,name;e=calc.index('{',m.start())+1;depth=1
 while depth:depth+=(calc[e]=='{')-(calc[e]=='}');e+=1
 body=calc[m.start():e];functions[name]=hashlib.sha256(body.encode()).hexdigest();return body
start=calc.index('let firstHitLength = 256 *');at=calc.index('if (skill == skills.FURY || skill == skills.STRAFE || skill == skills.FEND',start);e=calc.index('{',at)+1;depth=1
while depth:depth+=(calc[e]=='{')-(calc[e]=='}');e+=1
block=calc[start:e]
constants=(folder/'constants.js').read_text()
for family,frames,action in [('BOW',14,6),('CROSSBOW',20,9)]:
 region=constants[constants.index(f'{family}: new WeaponType'):];assert f'[char.AMAZON, [{frames}, {action}]]' in region.split(']),',1)[0]
js=r'''const skills=Object.fromEntries(['FEND','FURY','STRAFE','DRAGON_TALON','ZEAL','WHIRLWIND','STANDARD'].map(x=>[x,{}]));
const skill=skills.STRAFE,char={AMAZON:0,SORCERESS:1},character=0;
const wt=Object.fromEntries(['BOW','CROSSBOW','UNARMED','ONE_HANDED_SWINGING','TWO_HANDED_SWORD','ONE_HANDED_THRUSTING','TWO_HANDED_THRUSTING','TWO_HANDED'].map(x=>[x,{}]));
const trun=Math.trunc;function formatRollbackHitLength(x){return x;}function frameLengthsNotEqual(){return true;}
'''+function('rbf')+'\n'+function('rhits')+'\n'+function('getStartingFrame')+r'''
let out=[];for(let family of [1,2])for(let speedIncrease of [38,128,180,256,320,448]){
const weaponType=family==1?wt.BOW:wt.CROSSBOW,startingFrame=getStartingFrame(weaponType),framesPerDirection1=family==1?6:9,framesPerDirection2=family==1?14:20,offset=0,acceleration=0,EIAS=0;
const previousFrameLengths=[[],[]],results=[[],[]],accelerationTables=results.map(x=>({addBreakpoint:(_,v)=>x.push(v)}));
'''+block+r'''
out.push([family,speedIncrease,startingFrame,rbf(),results[0][0],results[1][0]]);
}process.stdout.write(JSON.stringify(out)+'\n');
'''
path=STAGE/'strafe-timing-source-probe.js';path.write_text(js);data=json.loads(subprocess.check_output(['node',str(path)]));assert len(data)==12
for family,rate,bonus,factor,even,odd in data:
 assert bonus==0 and factor==50 and len(even)==6 and len(odd)==5
 action,end=(6,14) if family==1 else (9,20);starts=[0];lengths=[]
 for _ in range(5):
  n=starts[-1];dt=((action-n)*256+rate-1)//rate;lengths.append(dt);starts.append(((n*256+rate*dt)//256)*50//100)
 assert even==lengths+[((end-starts[4])*256+rate-1)//rate-1]
 assert odd==lengths[:4]+[((end-starts[3])*256+rate-1)//rate-1]
fixture=STAGE/'strafe-timing-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',talentId='amazon.strafe',handlerEnabled=False,repository='https://github.com/Warren1001/IAS_Calculator',commit=commit['sha'],fileHashes=hashes,functionHashes=functions,rollbackBlockHash=hashlib.sha256(block.encode()).hexdigest(),probe=dict(rows=12,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Original current-family calculator uses50percent rollback, zero Amazon bow/crossbow start bonus, action/end frames6/14 and9/20. Twelve unmodified block observations cover six declared rates and both weapon families.','Each repeat rolls back the rate-advanced animation position after the actual event: floor(floor((start*256+rate*elapsed)/256)*50/100). It does not simply halve ordinary attack duration or force a2-frame interval. Original four-hit and five-hit presentations retain different crossbow final recovery where oscillation applies.','Final cycle duration includes its last hit: ceil((end-lastStart)*256/rate)-1. It is not an extra whole recovery cycle after that hit. Existing Amazon Fend math is a reuse candidate with its separate start2/rollback30/action/end metadata.'],limits=['Primary author implementation with verified repository blobs; UI/table/weapon metadata and rate inputs are declared. This is not pinned3.3.93847 executable timing, a complete player-mode scheduler, or a native Strafe controller.','Four/five-hit original presentations establish recurring clock behavior, not current shot count. Source count/target/resource evidence is separate. Dynamic IAS/slow/gear transitions, end-event alignment, lag/catch-up/interrupt/lock, next-hit-delay and full rendered-client parity remain acceptance work.'])
out=ROOT/'tools/reference/strafe-timing-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Strafe timing evidence stale'
else:out.write_text(encoded)
print('Strafe:12 original current-family rollback/action/final-cycle observations verified separately from legacy callbacks.')
