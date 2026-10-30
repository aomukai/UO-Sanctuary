#!/usr/bin/env python3
"""Execute unchanged HP/mana/stamina callback branches at positive next maxima."""
import hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills'
STAGE=BASE/'npc-resource-callbacks-source';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()


def build():
    STAGE.mkdir(exist_ok=True)
    proof=ROOT/'tools/reference/battle-orders-engine-evidence.json';prior=json.loads(proof.read_text())
    source=BASE/'d2moo-pinned-source/source/D2Game/src/ITEMS/ItemMode.cpp';assert sha(source)==prior['sourceHashes']['source/D2Game/src/ITEMS/ItemMode.cpp']
    text=source.read_text();branch=text[text.index('    case STAT_MAXHP:'):text.index('    case STAT_ITEM_NONCLASSSKILL:')]
    seed=BASE/'battle-orders-source-probe.cpp';prefix=seed.read_text().split('int main(){')[0];assert branch in prefix
    cpp=prefix+r'''int main(){bool first=true;printf("[\n");
for(int stat:{7,9,11})for(int kind:{0,1})for(int before:{0,1,128,256,8064,20864})for(int next:{1,128,256,8064,13184,20864})for(int balance:{0,1,128,10433,20864}){
D2UnitStrc actor;MonsterData md;actor.dwUnitType=kind;actor.pMonsterData=kind?&md:nullptr;actor.hp=actor.mana=actor.stam=balance;writes=0;
Changed(&actor,stat,before,next);int result=stat==7?actor.hp:stat==9?actor.mana:actor.stam;
printf("%s[%d,%d,%d,%d,%d,%d]",first?"":",\n",stat,kind,before,next,balance,result);first=false;
}printf("\n]\n");}'''
    p=STAGE/'source.cpp';p.write_text(cpp)
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(p),'-o',str(STAGE/'probe')],capture_output=True,text=True)
    (STAGE/'compiler.log').write_text(run.stdout+run.stderr);assert run.returncode==0,run.stderr
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr
    cases=json.loads(run.stdout);assert len(cases)==1080
    for stat,kind,before,next,balance,result in cases:
        expected=max(1,min(next,int(balance/max(256,before)*next))) if before>0 and next!=before and balance>0 else balance
        assert result==expected,(stat,kind,before,next,balance,result,expected)
    (STAGE/'cases.json').write_text(json.dumps(cases,indent=2)+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=prior['legacyCommit'],cases=1080,
        compiledUnchangedResourceBranchesSha256=hashlib.sha256(branch.encode()).hexdigest(),
        inputs={str(p.relative_to(ROOT)):sha(p) for p in [source,seed,proof]},
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [STAGE/'source.cpp',STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log']},
        scope='Complete unchanged MAXHP/MAXMANA/MAXSTAMINA branches,not the entire outer stat callback. Declared valid actor/game/stat services and positive next maximum. Includes unchanged/zero previous/current,sub-HP denominator and exact FP256 truncation. Zero-next bounds are excluded;the existing native zero-cap convention remains a separate modern-reference boundary.')
    (ROOT/'tools/reference/npc-resource-callbacks-source-evidence.json').write_text(json.dumps(result,indent=2)+'\n')
    print('NPC resource callbacks:1080 unchanged branch controls pass ASan/UBSan.')


if __name__=='__main__':build()
