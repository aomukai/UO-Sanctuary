#!/usr/bin/env python3
"""Compose regular skeleton birth with existing complete summon/MAXHP helpers."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'skeleton-birth-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);parent=BASE/'mage-birth-source/probe.cpp';previous=json.loads((ROOT/'tools/reference/mage-birth-source-evidence.json').read_text());assert sha(parent)==previous['artifacts'][str(parent.relative_to(ROOT))]
    cpp=parent.read_text().split('int main(){')[0];tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    path=BASE/'d2moo-pinned-source/source/D2Game/src/SKILLS/SkillNec.cpp';raw=path.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']=='source/D2Game/src/SKILLS/SkillNec.cpp')
    names=['D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10','D2GAME_SetSummonPassiveStats_6FD0C530','D2GAME_SetSummonResistance_6FD0C2E0'];bodies={n:function(raw.decode(),n) for n in names}
    for b in bodies.values():assert b in cpp
    cpp=cpp.replace('assert(id==80)','assert(id==70)')
    a=cpp.index('int SKILLS_EvaluateSkillFormula(');b=cpp.index('\nbool SUNITEVENT_GetEvent',a)
    cpp=cpp[:a]+'''int elementMinimum(int r){int value=0,left=r-1;int limits[5]={7,8,6,6,999},increments[5]={0,1,2,3,4};for(int i=0;i<5;i++){int n=std::min(left,limits[i]);value+=n*increments[i];left-=n;}return value;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int id,int r){assert(id==70&&r==rank);switch(f){
case 0:return gm*8*256;case 1:return gm*2+elementMinimum(rank);case 2:case 3:case 4:return 0;
case 5:return rank<4?0:7*(rank-3);case 6:case 7:return 15*(rank+gm);case 11:return rank<4?0:50*(rank-3);default:assert(false);return 0;}}
''' + cpp[b:]
    cpp=cpp.replace('if(s==31)pet.stats[s]+=n;','if(s==31||s==19||s==25)pet.stats[s]+=n;')
    refs=ROOT/'tools/reference/d2r-named-equipment/json';levels=json.loads((refs/'monlvl.json').read_text());skills=json.loads((refs/'skills.json').read_text());stats=json.loads((refs/'itemstatcost.json').read_text());mon=json.loads((refs/'monstats.json').read_text())['necroskeleton'];skill=skills['70']
    assert skill['skill']=='Raise Skeleton' and skill['srvdofunc']==31 and skill['summon']=='necroskeleton' and mon['noRatio']==1
    assert [skill['Param'+str(i)] for i in range(1,6)]==[5,50,7,15,15] and skill['passivestat2']=='item_normaldamage' and skill['EMinLev5']==4
    cpp+='\nint main(){\n'
    for level in range(111):
        row=levels[str(level)]
        for member,field in [('dwAC','AC'),('dwTH','TH'),('dwLAC','L-AC'),('dwLTH','L-TH')]:
            for diff,suffix in enumerate(['','(N)','(H)']):cpp+=f'tables.pMonLvlTxt[{level}].{member}[{diff}]={row[field+suffix]};\n'
    for i in range(1,6):
        key=skill.get("passivestat"+str(i));cpp+=f'skill.nPassiveStat[{i-1}]={stats[key]["*ID"] if key else -1};\n'
    cpp+='skill.wAuraStat[0]=25;skill.wAuraStat[1]=19;skill.wAuraStat[2]=31;skill.dwAuraStatCalc[0]=5;skill.dwAuraStatCalc[1]=6;skill.dwAuraStatCalc[2]=7;for(int i=3;i<6;i++)skill.wAuraStat[i]=-1;skill.nAuraState=-1;for(int&i:skill.wSumSkill)i=-1;\n'
    for name,keys,scale in [('hp',['minHP','MinHP(N)','MinHP(H)'],256),('ac',['AC','AC(N)','AC(H)'],1),('ar',['A1TH','A1TH(N)','A1TH(H)'],1)]:cpp+='int '+name+'[3]={'+','.join(str(mon[k]*scale) for k in keys)+'};\n'
    cpp+=r'''bool first=true;printf("[");
for(int level:{1,24,99})for(int r=1;r<=255;r++)for(int diff=0;diff<3;diff++)for(int mastery:{0,1,20,28,255}){
owner={};pet={};rank=r;gm=mastery;owner.stats[STAT_LEVEL]=level;owner.stats[STAT_PASSIVE_SUMMON_RESIST]=mastery==0?0:mastery==1?20:mastery==20?52:mastery==28?65:75;
game.nDifficulty=diff;game.nGameType=1;pet.stats[STAT_MAXHP]=pet.stats[STAT_HITPOINTS]=hp[diff];pet.stats[STAT_ARMORCLASS]=ac[diff];pet.stats[STAT_TOHIT]=ar[diff];pet.stats[74]=(hp[diff]*4)>>12;equipmentLevel=callbackCount=0;
assert(D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10(&game,&owner,&pet,0,r)==1);assert(D2GAME_SetSummonPassiveStats_6FD0C530(&game,&owner,&pet,70,r,0)==1);D2GAME_SetSummonResistance_6FD0C2E0(&owner,&pet);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",level,r,diff,mastery,pet.stats[12],pet.stats[7],pet.stats[6],pet.stats[31],pet.stats[19],pet.stats[74],pet.stats[111],pet.stats[25],equipmentLevel,pet.stats[39],pet.stats[41],pet.stats[43],pet.stats[45],pet.stats[192]);first=false;
}printf("]\n");}
'''
    source=STAGE/'probe.cpp';source.write_text(cpp);binary=STAGE/'probe';run=subprocess.run(['g++','-std=c++20','-O1','-g','-fwrapv','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(binary)],capture_output=True);compiler=STAGE/'compiler.log';compiler.write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()[-4000:]
    run=subprocess.run([str(binary)],capture_output=True);san=STAGE/'sanitizer.log';san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:];rows=json.loads(run.stdout);assert len(rows)==11475
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    invalid=sum(row[5]<=0 for row in rows)
    unsafe=sum((([21,30,42][row[2]]+8*row[3])*256)*max(0,row[1]-3)*50>2147483647 for row in rows)
    caller=function(raw.decode(),'SKILLS_SrvDo031_RaiseSkeleton_Mage')
    assert 'D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10(pGame, pUnit, pPet, 0, nSkillLevel);' in caller
    assert next(row for row in rows if row[:4]==[99,4,0,0])[4:10]==[78,8064,8064,533,902,7]
    result=dict(inspectedCallerSha256=hashlib.sha256(caller.encode()).hexdigest(),inspectedCallerLevelArgument=0,unsafePercentageCases=unsafe,nonpositiveWrappedLifeCases=invalid,referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=11475,functions={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},maxHpCallbackBranchSha256=previous['maxHpCallbackBranchSha256'],
        inputs={str(p.relative_to(ROOT)):sha(p) for p in [parent,*[refs/(n+'.json') for n in ['skills','monlvl','itemstatcost','monstats']]]},artifacts={str(p.relative_to(ROOT)):sha(p) for p in [source,fixture,compiler,san]},
        established=['Complete shared base/passive/resistance and MAXHPcallback bodies execute11475current skeleton contexts. Fixed noRatio life21/30/42,flat Mastery life and50percent-per-extra-rank life retain FP256. Normal rank4/Mastery0 is8064FP256(31.5HP),not31whole HP;MAXHPcallback regeneration is7FP256/frame,not the unchanged spawn5.',
            'Default pet level is min(ownerLevel,rank+floor(3*ownerLevel/4)),not automatically owner level. Expansion monlvl armor/AR are evaluated at that pet level;current flat15*(rank+Mastery)auras then join the base pools.',
            'Item normal damage receives effective Mastery*2 plus current tiered edmn. The7percent-per-extra-rank damage aura and requested equipment level are recorded separately. Elemental resistance receives the supplied Summon Resist through the complete shared helper.'],
        limits=['World spawn/equipment/shield choice,AI/animation,ordinary attack/critical and full modern executable events are not executed. Formula/stat composition,spawn fixed bases and positive arithmetic are declared current-data services. Physical Sunder input is zero;nonzero delivery remains unsupported.','The source percentage helper can wrap its signed32-bit product at extreme rank/Mastery inputs. The probe uses explicit-fwrapv x86-style arithmetic;nonpositive life results are recorded and must be refused by native delivery rather than invented as positive life.',
            'This proves birth/stat composition only. Native exact life/damage/healing/regen,buff/facet/save and learned cast acceptance still follow;no class completion or deployment is claimed.'])
    output=ROOT/'tools/reference/skeleton-birth-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==text
    else:output.write_text(text)
    print('Skeleton birth:11475complete base/passive/resistance/MAXHPcallback contexts;native exact resource delivery pending.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
