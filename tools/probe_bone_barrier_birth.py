#!/usr/bin/env python3
"""Reuse complete summon passive/MAXHP bodies for pinned Bone Wall/Prison resources."""
import argparse, hashlib, json, subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'bone-barrier-birth-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    parent=BASE/'mage-birth-source/probe.cpp';prior=json.loads((ROOT/'tools/reference/mage-birth-source-evidence.json').read_text())
    assert sha(parent)==prior['artifacts'][str(parent.relative_to(ROOT))]
    cpp=parent.read_text().split('int main(){')[0]
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs={str(parent.relative_to(ROOT)):sha(parent),str(Path(__file__).relative_to(ROOT)):sha(Path(__file__))}
    def source(rel):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    nec=source('source/D2Game/src/SKILLS/SkillNec.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp')
    monster=source('source/D2Game/src/MONSTER/Monster.cpp');missile=source('source/D2Game/src/MISSILES/MissMode.cpp');ai=source('source/D2Game/src/AI/AiThink.cpp')
    reviewed={n:function(s,n) for s,n in [(nec,'SKILLS_SrvDo060_BoneWall'),(nec,'SKILLS_SrvDo062_BonePrison'),(nec,'SKILLS_SrvSt19_BonePrison'),(skills,'D2GAME_SummonPet_6FD14430'),(monster,'MONSTER_InitializeStatsAndSkills'),(missile,'MISSMODE_SrvDo13_BoneWallMaker'),(ai,'D2GAME_AI_Unk084_6FCDEC70'),(ai,'AITHINK_Fn084_BoneWall')]}
    for n in ['SKILLS_SrvDo060_BoneWall','SKILLS_SrvDo062_BonePrison','MISSMODE_SrvDo13_BoneWallMaker']:
        assert 'D2GAME_SetSummonPassiveStats_6FD0C530' in reviewed[n]
        assert 'D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10' not in reviewed[n] and 'D2GAME_SetSummonResistance_6FD0C2E0' not in reviewed[n]
    assert 'pSkillsTxtRecord->dwParam[1]' in reviewed['D2GAME_AI_Unk084_6FCDEC70'] and 'STAT_LEVEL' not in reviewed['AITHINK_Fn084_BoneWall']
    spawn=reviewed['MONSTER_InitializeStatsAndSkills'];assert 'int32_t nLevel = pMonStatsTxtRecord->nLevel[nDifficulty]' in spawn and 'MONSTATSFLAGINDEX_NORATIO' in spawn
    passive=function(nec,'D2GAME_SetSummonPassiveStats_6FD0C530');assert passive in cpp
    percentage=function(cpp,'MONSTERUNIQUE_CalculatePercentage')
    # Barriers do not call the skeleton level/base-stat helper.
    cpp=cpp.replace(function(cpp,'D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10'),'')
    cpp=cpp.replace('int rank=1,gm=0,','int skillId=78,armor=0,other=0,rank=1,gm=0,').replace('assert(id==80)','assert(id==skillId)')
    start=cpp.index('int SKILLS_EvaluateSkillFormula(');end=cpp.index('\nbool SUNITEVENT_GetEvent',start)
    cpp=cpp[:start]+'''int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int id,int r){assert(id==skillId&&r==rank&&f==11);return 25*(rank-1)+(armor+other)*(id==78?10:8);}
'''+cpp[end:]
    ref=ROOT/'tools/reference/d2r-named-equipment/json';manifest=json.loads((ref.parent/'sources.json').read_text())['files']
    data={}
    for n in ['skills','monstats']:
        p=ref/(n+'.json');assert sha(p)==manifest['json/'+n+'.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);data[n]=json.loads(p.read_text())
    wall,prison=data['skills']['78'],data['skills']['88'];mon=data['monstats']['bonewall']
    assert wall['srvdofunc']==60 and prison['srvdofunc']==62 and mon['noRatio']==1 and mon['DamageRegen']==2
    assert wall['Param1']==prison['Param1']==25 and wall['Param8']==10 and prison['Param8']==8
    for row in [wall,prison]:assert not any(row.get(k) for k in ['passivestat1','aurastat1','sumskill1'])
    cpp+='\nint main(){monster.dwDamageRegen=2;for(int&i:skill.nPassiveStat)i=-1;for(int&i:skill.wAuraStat)i=-1;for(int&i:skill.wSumSkill)i=-1;skill.nAuraState=-1;\n'
    for name,keys,scale in [('hp',['minHP','MinHP(N)','MinHP(H)'],256),('ac',['AC','AC(N)','AC(H)'],1),('levels',['Level','Level(N)','Level(H)'],1)]:
        cpp+='int '+name+'[3]={'+','.join(str(mon[k]*scale) for k in keys)+'};\n'
    cpp+='''int hard[7][2]={{0,0},{1,1},{20,0},{0,20},{20,20},{5,17},{255,255}};bool first=true;printf("[");
for(int id:{78,88})for(int r=1;r<=255;r++)for(int diff=0;diff<3;diff++)for(auto&h:hard)for(int ownerLevel:{1,99}){
owner={};pet={};skillId=id;rank=r;armor=h[0];other=h[1];owner.stats[12]=ownerLevel;owner.stats[349]=75;
pet.stats[12]=levels[diff];pet.stats[7]=pet.stats[6]=hp[diff];pet.stats[31]=ac[diff];pet.stats[74]=(hp[diff]*2)>>12;
callbackCount=equipmentLevel=0;assert(D2GAME_SetSummonPassiveStats_6FD0C530(&game,&owner,&pet,id,r,0)==1);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",id,r,diff,armor,other,ownerLevel,pet.stats[7],pet.stats[6],pet.stats[74],pet.stats[12],pet.stats[31],equipmentLevel,callbackCount);first=false;
}printf("]\\n");}
'''
    p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe'
    result=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],capture_output=True)
    (STAGE/'compiler.log').write_bytes(result.stdout+result.stderr);assert result.returncode==0,result.stderr.decode()[-3000:]
    result=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(result.stderr);assert result.returncode==0 and not result.stderr,result.stderr.decode()[-3000:]
    rows=json.loads(result.stdout);assert len(rows)==21420
    (STAGE/'cases.json').write_text(json.dumps(rows,separators=(',',':'))+'\n')
    proof=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(rows),inputs=inputs,
        executed={'D2GAME_SetSummonPassiveStats_6FD0C530':hashlib.sha256(passive.encode()).hexdigest(),'MONSTERUNIQUE_CalculatePercentage':hashlib.sha256(percentage.encode()).hexdigest()},
        maxHpCallbackBranchSha256=prior['maxHpCallbackBranchSha256'],reviewedNotExecuted={n:hashlib.sha256(b.encode()).hexdigest() for n,b in reviewed.items()},
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [STAGE/'probe.cpp',STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log']},
        established=['21420 complete passive/percentage/MAXHP callback contexts retain fractional life and callback-adjusted recovery. No base-stat helper or Summon Resist helper is called by either barrier or the wall maker.',
            'Pinned noRatio spawn inputs are fixed 19/147/431 life,35/174/334 defense and7/40/73 level. Initializer branch preserves species level for noRatio; full initializer is reviewed, not executed. HP-percent uses effective cast rank and hard Armor/other-barrier ranks; owner level changes only the recorded equipment request in this scope.'],
        remaining=['Spawn/room/player-count/RNG/equipment/minion/event/AI/missile geometry and current executable remain outside this execution scope. Supplied one-player fixed noRatio spawn inputs are declared services, not full initializer execution.',
            'Native learned casting, FP health/recovery/facet/save and wall growth/prison geometry still require separate acceptance. Full class completion is not claimed.'])
    target=ROOT/'tools/reference/bone-barrier-birth-source-evidence.json';render=json.dumps(proof,indent=2)+'\n'
    if check:assert target.read_text()==render
    else:target.write_text(render)
    print('Bone barriers:21420 complete passive/percentage/MAXHP contexts; spawn/world/current-executable limits explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
