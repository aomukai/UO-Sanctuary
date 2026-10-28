#!/usr/bin/env python3
"""Execute shared do115/passive birth for current Carrion and Solar rows."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    preflight_path=ROOT/'tools/reference/recycler-vines-preflight.json';pin=json.loads(preflight_path.read_text())
    original_path=ROOT/'tools/reference/creeper-components-source-evidence.json';original=json.loads(original_path.read_text())
    parent=BASE/'creeper-birth-source-probe.cpp';assert sha(parent)==original['generatedSourceHashes'][str(parent.relative_to(ROOT))]
    cpp=parent.read_text().split('bool first=true;',1)[0]
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==pin['legacyCommit']==original['legacyCommit']
    hashes,routines={},{}
    for relative,names in [('source/D2Game/src/SKILLS/SkillDruid.cpp',['SKILLS_SrvDo115_Vines']),
        ('source/D2Game/src/SKILLS/SkillNec.cpp',['D2GAME_SetSummonPassiveStats_6FD0C530']),
        ('source/D2Game/src/MONSTER/MonsterUnique.cpp',['MONSTERUNIQUE_CalculatePercentage'])]:
        path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=sha(path)
        for name in names:
            body=function(raw.decode(),name);assert body in cpp,name;routines[name]=hashlib.sha256(body.encode()).hexdigest()
    rows=pin['currentRows'];bases=[]
    for name in ['cycleoflife','vinecreature']:
        row=rows['monstats'][name];assert row['noRatio']=='1'
        for suffix in ['', '(N)', '(H)']:
            low=('minHP' if not suffix else 'MinHP')+suffix
            high=('maxHP' if not suffix else 'MaxHP')+suffix
            assert row[low]==row[high]==row['minHP'] and row['AC'+suffix]==row['AC']
        bases.append((int(row['minHP'])*256,int(row['AC']),int(row['DamageRegen'])))
    assert bases==[(24320,92,3),(42240,165,3)]
    assert rows['skills']['Cycle of Life']['Param3']=='25' and rows['skills']['Vines']['Param3']=='20'
    def replace(before,after):
        nonlocal cpp
        assert cpp.count(before)==1,before;cpp=cpp.replace(before,after,1)
    replace('bool validSkill=true;D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return id==222?(validSkill?&skill:nullptr):id==294?&actorSkill:nullptr;}',
        'int skillId=231,kind=0;bool validSkill=true;D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return id==skillId?(validSkill?&skill:nullptr):(id==294||id==307||id==325)?&actorSkill:nullptr;}')
    replace('f==11?50*(rank-1):','f==11?(kind?20:25)*(rank-1):')
    replace('int grantedRank=0;void D2GAME_SetSkills_6FD14C60(D2UnitStrc*,int s,int n,int){if(s==294)grantedRank=n;}',
        'int grantedRank=0,cyclerRank=0;void D2GAME_SetSkills_6FD14C60(D2UnitStrc*,int s,int n,int){if(s==294)grantedRank=n;else if(s==307||s==325)cyclerRank=n;else __builtin_trap();}')
    replace('pet.stats[STAT_ARMORCLASS]=25;pet.stats[20]=9;',
        'pet.stats[STAT_ARMORCLASS]=kind?165:92;pet.stats[20]=baseLife*3>>12;')
    cpp+=r'''
bool first=true;
void emit(int type,int level,int r,int difficulty,int delta,int failure){
kind=type;skillId=kind?241:231;owner={};pet={};skill={};actorSkill={};arg={};rank=r;pierce=0;game.nDifficulty=difficulty;owner.stats[STAT_LEVEL]=level;
validSkill=failure!=1;spawnId=failure==2?-1:kind?427:426;spawnSuccess=failure!=5;spawnCalls=stateCalls=grantedRank=cyclerRank=equipmentLevel=0;aiNode=-1;baseLife=(kind?42240:24320)+delta;
for(int&x:skill.nPassiveStat)x=-1;for(int&x:skill.wAuraStat)x=-1;skill.nAuraState=150;
for(int&x:skill.wSumSkill)x=-1;skill.wSumSkill[0]=294;skill.wSumSkill[1]=kind?325:307;skill.dwSumSkCalc[0]=skill.dwSumSkCalc[1]=12;
skill.nPetType=failure==3?-1:failure==4?32:14;
int result=SKILLS_SrvDo115_Vines(&game,&owner,skillId,rank);
printf("%s[%d,%d,%d,%d,%d,%d,[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]]",first?"":",",kind,level,r,difficulty,delta,failure,result,pet.stats[STAT_LEVEL],pet.stats[STAT_MAXHP],pet.stats[STAT_HITPOINTS],pet.stats[STAT_ARMORCLASS],pet.stats[STAT_TOHIT],pet.stats[20],grantedRank,cyclerRank,equipmentLevel,stateCalls,spawnCalls,arg.nMonMode,arg.nPetMax,aiNode,owner.dwFlags);first=false;
}
int main(){printf("[");for(int type:{0,1})for(int level:{1,30,99})for(int rank=1;rank<=255;rank++)for(int difficulty:{0,1,2})for(int delta:{0,17})emit(type,level,rank,difficulty,delta,0);
for(int type:{0,1})for(int failure=1;failure<=5;failure++)emit(type,99,1,0,0,failure);printf("]\n");}
'''
    generated=BASE/'recycler-vines-birth-source-probe.cpp';generated.write_text(cpp);binary=generated.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(binary)],check=True)
    process=subprocess.run([str(binary)],capture_output=True);sanitizer=BASE/'recycler-vines-birth-sanitizer.log';sanitizer.write_bytes(process.stderr)
    assert process.returncode==0 and not process.stderr,process.stderr.decode()[-3000:]
    cases=json.loads(process.stdout);assert len(cases)==9190,len(cases)
    for kind,level,rank,difficulty,delta,failure,actual in cases:
        if not failure:
            life=(42240 if kind else 24320)+delta;maximum=life+life*(20 if kind else 25)*(rank-1)//100
            expected=[1,max(1,level*3//4+rank),maximum,maximum,165 if kind else 92,0,life*3>>12,rank,rank,min(3*rank,level),2,1,8,1,123,64]
        else:
            expected=[0,0,0,0,0,0,0,0,0,0,0,int(failure==5),8 if failure==5 else 0,1 if failure==5 else 0,-1,64 if failure==5 else 0]
        assert actual==expected,(kind,level,rank,difficulty,delta,failure,actual,expected)
    output=BASE/'recycler-vines-birth-source-probe.json';output.write_bytes(process.stdout)
    paths=[Path(__file__).resolve(),preflight_path,original_path,parent,generated,output,sanitizer]
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,nativeAccepted=False,currentExecutableParity=False,
        contexts=len(cases),allRanks=list(range(1,256)),compiledRoutineHashes=routines,sourceHashes=hashes,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},sanitizerClean=True,
        declaredServices=['Shared factory supplies current noRatio base HP/defense/AR and pre-passive regeneration; complete current monster initialization is not executed.',
            'Formula service applies current HP multiplier,ownerLevel*3/4+rank,cap1and effective-rank inheritance; actual parser/skill/stat transport is separate.',
            'Summon/node/state/skill/equipment callbacks capture requests. Equipment level is a request only,not inventory support; shared state exposure remains unresolved.'],
        established=['Unchanged complete do115/passive/percentage bodies scale current Carrion95HP by25percent/rank and Solar165HP by20percent/rank,fill current life,retain92/165defense/0AR and pre-passive17/30FP regeneration,overwrite level without Raven-style base recomputation,and grant both Vine Attack and the corresponding recycler at summon rank.',
            'All255ranks,three owner levels,three difficulty controls and fractional base-life controls pass. Both VINE_BEAST toggles,shared vine cap1,mode8,owner node and equipment request level are captured. Missing row/summon/invalid category/failed allocation preserve distinct no-stat/owner-flag boundaries.'],
        remaining=['Native Carrion/Solar stats/body/controller/casting/payment,corpse-delay/ownership/resources/saves,input and graphical acceptance remain pending. Full spawn/room/seed/ABI/current executable parity is not certified. Shared underground exposure and Solar formula-slot conflict stay explicit; effects/audio/icons remain separately inventoried.'])
    path=ROOT/'tools/reference/recycler-vines-birth-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Recycler vine birth:9190complete do115/passive/percentage contexts,all255ranks,both inherited skills,ASan/UBSan clean; native actors remain pending.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
