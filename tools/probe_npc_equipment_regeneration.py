#!/usr/bin/env python3
"""Execute full older monster regeneration with separate base/item/poison lanes."""
import hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'npc-equipment-regeneration-source'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()


def build():
    STAGE.mkdir(exist_ok=True)
    proof=ROOT/'tools/reference/poison-state-engine-evidence.json';prior=json.loads(proof.read_text())
    seed=BASE/'poison-state-source-probe.cpp';text=seed.read_text();prefix=text.split('int main(){')[0]
    source=BASE/'d2moo-pinned-source/source/D2Game/src/MONSTER/MonsterMode.cpp'
    assert sha(source)==prior['sourceHashes']['source/D2Game/src/MONSTER/MonsterMode.cpp']
    name='D2GAME_MONSTER_ApplyStatRegen_6FC63440'
    def body(text):
        m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);end=text.index('{',m.start())+1;depth=1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        return text[m.start():end]
    assert body(prefix)==body(source.read_text()) and hashlib.sha256(body(prefix).encode()).hexdigest()==prior['blockHashes'][name]
    cpp=prefix+r'''int main(){bool first=true;printf("[\n");
for(int life:{1,128,257,512,1024})for(int base:{0,5,64})for(int item:{-8,0,3,20})for(int poison:{0,1,16,40})for(int mask:{0,1})for(int blocked:{0,1})for(int inTown:{0,1}){
defender={};defender.dwUnitType=1;defender.stats[STAT_HPREGEN]=base+item-poison;defender.stats[STAT_HITPOINTS]=life;
list={};list.ownerId=77;hasList=poison>0;baseRegen=base;lifeMask=mask;town=inTown;prevent=blocked;uninterrupt=false;killed=deferred=writes=scheduled=removed=0;killOwner=-1;
D2GAME_MONSTER_ApplyStatRegen_6FC63440(&game,&defender,0,0);
printf("%s[[%d,%d,%d,%d,%d,%d,%d],%d]",first?"":",\n",life,base,item,poison,mask,blocked,inTown,defender.stats[STAT_HITPOINTS]);first=false;
}printf("\n]\n");}'''
    p=STAGE/'source.cpp';p.write_text(cpp)
    built=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(p),'-o',str(STAGE/'probe')],capture_output=True,text=True)
    (STAGE/'compiler.log').write_text(built.stdout+built.stderr);assert built.returncode==0,built.stderr
    result=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(result.stderr)
    assert result.returncode==0 and not result.stderr,result.stderr
    cases=json.loads(result.stdout);assert len(cases)==1920
    for inputs,actual in cases:
        life,basis,item,poison,mask,blocked,town=inputs
        delta=(0 if mask else basis)+item-poison
        if blocked and delta>=0:delta=0
        expected=life if delta<0 and life<256 and town else min(1024,max(0,life+delta))
        assert expected==actual,(inputs,expected,actual)
    (STAGE/'cases.json').write_text(json.dumps(cases,indent=2)+'\n')
    record=dict(referenceVersion='3.3.93847',legacyCommit=prior['legacyCommit'],cases=1920,
        compiledUnchangedRoutineHashes={name:prior['blockHashes'][name]},
        inputs={str(proof.relative_to(ROOT)):sha(proof),str(source.relative_to(ROOT)):sha(source),str(seed.relative_to(ROOT)):sha(seed)},
        artifacts={str(x.relative_to(ROOT)):sha(x) for x in [p,STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log']},
        scope='Full unchanged older monster event body with declared base HPRegen, item replenishment and poison stat lanes;life-mask/prevent-heal/town and near-zero thresholds. Maxlife1024, stats/events/death services are the declared reused harness. Not modern executable equivalence, stat birth/callback recalculation, complete scheduler or player mana/stamina recovery on NPCs.')
    (ROOT/'tools/reference/npc-equipment-regeneration-source-evidence.json').write_text(json.dumps(record,indent=2)+'\n')
    print('NPC item regeneration:1920 full older monster contexts pass ASan/UBSan.')


if __name__=='__main__':build()
