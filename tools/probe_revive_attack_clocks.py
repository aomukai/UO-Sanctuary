#!/usr/bin/env python3
"""Reuse unchanged source attack-rate/scheduler bodies with every admitted A1 row."""
import hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/revive-attack-clock-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build():
    STAGE.mkdir(exist_ok=False)
    parent=json.loads((ROOT/'tools/reference/spirit-wolf-rate-source-evidence.json').read_text())
    original=Path('/home/aomukai/.codex/worktrees/death-sentry-damage/Sanctuary/.local/class-skills/spirit-wolf-rate-source-probe.cpp')
    assert sha(original)==parent['generatedSourceSha256']
    prefix=original.read_text().split('int main(){',1)[0]
    tail=r'''
int main(){bool first=true;int index,cls,frames,base,flag,ias,rate,weapon;printf("[");
while(std::cin>>index>>cls>>frames>>base>>flag>>ias>>rate>>weapon){
owner={};owner.dwClassId=cls;owner.dwFrameCountPrecise=frames*256;anim={};anim.dwAnimSpeed=base;anim.pFrameFlags[flag]=1;
faster=ias;attack=rate-weapon;meleeOnly=0;alternate=0;merges=0;events.clear();game.dwGameFrame=0;
assert(UNITS_CanAnimModeUseAttackRate(UNIT_MONSTER,4,&owner,cls));UNITS_UpdateAttackAnimRateAndVelocity(&owner);sub_6FCBCE70(&game,&owner);
printf("%s[%d,%d,%d,%d,%d,[",first?"":",",index,ias,rate,weapon,owner.wAnimSpeed);first=false;
bool sep=false;for(auto e:events){printf("%s[%d,%d,%d,%d]",sep?",":"",e.type,e.frame,e.kind,e.param);sep=true;}printf("]]");
}printf("]\n");}
'''
    path=STAGE/'source.cpp';path.write_text('#include <iostream>\n'+prefix+tail)
    rows=json.loads((ROOT/'data/Sanctuary/Balance/revive-attack-clocks.json').read_text());classes={r['Key']:r['BaseId']for r in json.loads((ROOT/'data/Sanctuary/Balance/revive-melee-geometry.json').read_text())}
    cases=[]
    for i,row in enumerate(rows):
        if row['Issue']is not None:continue
        for ias in (-119,-10,0,10,20,100,10000):
            for rate in (-100,0,14,15,50,100,175,250):
                for weapon in (-30,0,10,20):cases.append((i,classes[row['Source']],row['Frames'],row['Speed'],row['Flags'][0]['Frame'],ias,rate,weapon))
    assert len(cases)==97664
    input_path=STAGE/'inputs.txt';input_path.write_text(''.join(' '.join(map(str,row))+'\n'for row in cases))
    with (STAGE/'compiler.log').open('w')as output:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(STAGE/'probe')],stdout=output,stderr=subprocess.STDOUT,check=True)
    with input_path.open('rb')as stream:result=subprocess.run([str(STAGE/'probe')],stdin=stream,capture_output=True)
    (STAGE/'sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr.decode()[-2000:]
    values=json.loads(result.stdout);assert len(values)==len(cases)
    fixture=ROOT/'.local/class-skills/revive-attack-clock-source-probe.json';fixture.write_text(json.dumps(values,separators=(',',':'))+'\n')
    evidence=dict(referenceVersion='3.3.93847',legacyCommit=parent['legacyCommit'],parentGeneratedSourceSha256=sha(original),
        unchangedRoutineHashes=parent['unchangedRoutineHashes'],inputs={str(p.relative_to(ROOT)):sha(p)for p in (ROOT/'data/Sanctuary/Balance/revive-attack-clocks.json',ROOT/'data/Sanctuary/Balance/revive-melee-geometry.json')},
        artifacts={str(p.relative_to(ROOT)):sha(p)for p in (path,input_path,STAGE/'compiler.log',STAGE/'sanitizer.log',fixture)},cases=len(values),
        scope='Complete unchanged A1 rate predicate/writer and initial event scheduler;436 owned single-melee-action rows,explicit IAS/rate/weapon-speed inputs;not a native controller or modern executable certificate.')
    (ROOT/'tools/reference/revive-attack-clock-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
    print('Revive A1 source:97664 complete rate/predicate/scheduler contexts;ASan/UBSan clean;unchanged shared routine bodies.')
if __name__=='__main__':build()
