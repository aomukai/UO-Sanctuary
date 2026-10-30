#!/usr/bin/env python3
"""Execute the complete unchanged MAXHP callback branch with declared services."""
import hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-equipment-regen-basis-source'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()


def build():
    STAGE.mkdir(exist_ok=True)
    proof=ROOT/'tools/reference/battle-orders-engine-evidence.json';prior=json.loads(proof.read_text())
    source=BASE/'d2moo-pinned-source/source/D2Game/src/ITEMS/ItemMode.cpp'
    assert sha(source)==prior['sourceHashes']['source/D2Game/src/ITEMS/ItemMode.cpp']
    seed=BASE/'battle-orders-source-probe.cpp';prefix=seed.read_text().split('int main(){')[0]
    text=source.read_text();branch=text[text.index('    case STAT_MAXHP:'):text.index('    case STAT_MAXMANA:')]
    assert branch in prefix
    cpp=prefix+r'''int main(){bool first=true;printf("[\n");
for(int kind:{0,1})for(int data:{0,1})for(int record:{0,1})for(int rate:{0,1,4,255})for(int before:{0,256,8064,8191})for(int next:{256,8064,8191,8192,13184,23424})for(int life:{0,1,128,4096}){
D2UnitStrc actor;MonsterData md;monrow.dwDamageRegen=rate;md.pMonstatsTxt=record?&monrow:nullptr;actor.dwUnitType=kind;actor.pMonsterData=data?&md:nullptr;actor.hp=life;actor.regen=17;writes=0;
Changed(&actor,STAT_MAXHP,before,next);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",\n",kind,data,record,rate,before,next,life,17,actor.regen,actor.hp,writes);first=false;
}printf("\n]\n");}'''
    p=STAGE/'source.cpp';p.write_text(cpp)
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(p),'-o',str(STAGE/'probe')],capture_output=True,text=True)
    (STAGE/'compiler.log').write_text(run.stdout+run.stderr);assert run.returncode==0,run.stderr
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr
    cases=json.loads(run.stdout);assert len(cases)==3072
    for kind,data,record,rate,before,next,life,old,result,hp,writes in cases:
        change=before>0 and next!=before and life>0
        changed=change and kind==1 and data and record and rate>0
        assert result==(rate*(next>>8)>>4 if changed else old)
        assert hp==(max(1,min(next,int(life/max(256,before)*next))) if change else life)
    (STAGE/'cases.json').write_text(json.dumps(cases,indent=2)+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=prior['legacyCommit'],cases=3072,
        compiledUnchangedMaxHpBranchSha256=hashlib.sha256(branch.encode()).hexdigest(),
        inputs={str(p.relative_to(ROOT)):sha(p) for p in [source,seed,proof]},
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [STAGE/'source.cpp',STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log']},
        scope='Complete unchanged MAXHP case from the server statlist callback,not the complete outer function. Declared valid game/owner/stat metadata services. Player/monster and missing monster data/record,zero regen coefficient,zero current/previous life,unchanged/exact fractional max values. The branch also rescales current HP;this probe records that output but the existing native item-balance clamp bridge is not certified as source-equivalent.')
    (ROOT/'tools/reference/npc-equipment-regen-basis-source-evidence.json').write_text(json.dumps(result,indent=2)+'\n')
    print('NPC regen basis:3072 complete MAXHP branch controls pass ASan/UBSan.')


if __name__=='__main__':build()
