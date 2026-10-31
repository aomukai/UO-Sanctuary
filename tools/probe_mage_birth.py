#!/usr/bin/env python3
"""Execute complete summon birth helpers and MAXHP callback for current Skeletal Mage data."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'mage-birth-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);prior=json.loads((ROOT/'tools/reference/iron-golem-birth-source-evidence.json').read_text());parent=BASE/'iron-golem-birth-source/source.cpp'
    assert sha(parent)==prior['artifacts'][str(parent.relative_to(ROOT))]
    cpp=parent.read_text().split('int main(){')[0]
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    def source(path):
        raw=(BASE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);return raw.decode()
    body=source('source/D2Game/src/SKILLS/SkillNec.cpp');names=['D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10','D2GAME_SetSummonPassiveStats_6FD0C530','D2GAME_SetSummonResistance_6FD0C2E0']
    routines={n:function(body,n) for n in names}
    for b in routines.values():assert b in cpp
    raw=source('source/D2Game/src/ITEMS/ItemMode.cpp');callback=function(raw,'D2GAME_ITEMMODE_ServerStatlistCallback_6FC41910')
    branch=callback[callback.index('    case STAT_MAXHP:'):callback.index('    case STAT_MAXMANA:')]
    def replace(a,b):
        nonlocal cpp
        assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
    replace('struct D2UnitStrc{','struct D2MonStatsTxt{int dwDamageRegen=4;};struct MonsterData{D2MonStatsTxt* pMonstatsTxt;};D2MonStatsTxt monster;MonsterData monsterData{&monster};\nvoid ResourceChanged(struct D2UnitStrc*,int,int,int);\nstruct D2UnitStrc{MonsterData* pMonsterData=&monsterData;')
    replace('void STATLIST_SetUnitStat(D2UnitStrc*p,int s,int n,int){p->stats[s]=n;}','void STATLIST_SetUnitStat(D2UnitStrc*p,int s,int n,int){int old=p->stats[s];p->stats[s]=n;ResourceChanged(p,s,old,n);}')
    replace('void STATLIST_AddUnitStat(D2UnitStrc*p,int s,int n,int){p->stats[s]+=n;}','void STATLIST_AddUnitStat(D2UnitStrc*p,int s,int n,int){STATLIST_SetUnitStat(p,s,p->stats[s]+n,0);}')
    replace('D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){if(id<0){return nullptr;}assert(id==90);return &skill;}','D2SkillsTxt attackSkill;int missileLevel=0;D2SkillsTxt*SKILLS_GetSkillsTxtRecord(int id){if(id<0){return nullptr;}if(id==338)return &attackSkill;assert(id==80);return &skill;}')
    start=cpp.index('int SKILLS_EvaluateSkillFormula(');end=cpp.index('\nbool SUNITEVENT_GetEvent',start)
    cpp=cpp[:start]+'''int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int id,int r){assert(id==80&&r==rank);switch(f){
case 0:return gm*8*256;case 1:case 2:case 3:case 4:return 0;case 5:return(rank+gm)*10;case 11:return rank<4?0:10*(rank-3);case 12:return std::max(gm+(rank<4?0:(rank-2)/2),1);default:assert(false);return 0;}}
'''+cpp[end:]
    replace('void D2GAME_SetSkills_6FD14C60(D2UnitStrc*,int,int,int){assert(false);}','void D2GAME_SetSkills_6FD14C60(D2UnitStrc*,int id,int level,int remove){assert(id==338&&remove==0);missileLevel=level;}')
    replace('pet.stats[s]=n;if(s==39)','if(s==31)pet.stats[s]+=n;else pet.stats[s]=n;if(s==39)')
    cpp+='\nconstexpr int STAT_HPREGEN=74,UNIT_MONSTER=1;int callbackCount=0;\nvoid ResourceChanged(D2UnitStrc*pOwner,int nStatId,int nPreviousValue,int nNewValue){switch(nStatId){\n'+branch.replace('STATLIST_SetUnitStat(pOwner, STAT_HPREGEN,','callbackCount++;STATLIST_SetUnitStat(pOwner, STAT_HPREGEN,')+'default:break;}}\nint main(){\n'
    ref=ROOT/'tools/reference/d2r-named-equipment/json';levels=json.loads((ref/'monlvl.json').read_text());skills=json.loads((ref/'skills.json').read_text());stats=json.loads((ref/'itemstatcost.json').read_text());mon=json.loads((ref/'monstats.json').read_text())['necromage']
    row=skills['80'];assert row['skill']=='Raise Skeletal Mage' and row['srvdofunc']==31 and row['summon']=='necromage'
    assert row['passivestat1']=='maxhp' and row['aurastat1']=='armorclass' and row['sumskill1']=='NecromageMissile'
    for level in range(111):
        r=levels[str(level)]
        for member,field in [('dwAC','AC'),('dwTH','TH'),('dwLAC','L-AC'),('dwLTH','L-TH')]:
            for diff,suffix in enumerate(['','(N)','(H)']):cpp+=f'tables.pMonLvlTxt[{level}].{member}[{diff}]={r[field+suffix]};\n'
    ids=[stats[row['passivestat'+str(i)]]['*ID'] for i in range(1,6)]
    for i,n in enumerate(ids):cpp+=f'skill.nPassiveStat[{i}]={n};\n'
    cpp+='skill.wAuraStat[0]=31;for(int i=1;i<6;i++)skill.wAuraStat[i]=-1;skill.nAuraState=-1;skill.wSumSkill[0]=338;skill.dwSumSkCalc[0]=12;attackSkill.dwFlags[0]=0;\n'
    cpp+='int hp[3]={'+','.join(str(mon[x]*256) for x in ['minHP','MinHP(N)','MinHP(H)'])+'};\n'
    cpp+='int ac[3]={'+','.join(str(mon[x]) for x in ['AC','AC(N)','AC(H)'])+'};int ar[3]={'+','.join(str(mon[x]) for x in ['A1TH','A1TH(N)','A1TH(H)'])+'};\n'
    cpp+=r'''bool first=true;printf("[");
for(int level:{1,24,99})for(int r=1;r<=255;r++)for(int diff=0;diff<3;diff++)for(int mastery:{0,1,20,28,255}){
 owner={};pet={};rank=r;gm=mastery;owner.stats[STAT_LEVEL]=level;owner.stats[STAT_PASSIVE_SUMMON_RESIST]=mastery==0?0:mastery==1?20:mastery==20?52:mastery==28?65:75;
 game.nDifficulty=diff;game.nGameType=1;pet.stats[STAT_MAXHP]=pet.stats[STAT_HITPOINTS]=hp[diff];pet.stats[STAT_ARMORCLASS]=ac[diff];pet.stats[STAT_TOHIT]=ar[diff];pet.stats[74]=(hp[diff]*4)>>12;callbackCount=missileLevel=equipmentLevel=0;
 assert(D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10(&game,&owner,&pet,0,r)==1);
 assert(D2GAME_SetSummonPassiveStats_6FD0C530(&game,&owner,&pet,80,r,0)==1);D2GAME_SetSummonResistance_6FD0C2E0(&owner,&pet);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",level,r,diff,mastery,owner.stats[STAT_PASSIVE_SUMMON_RESIST],pet.stats[STAT_LEVEL],pet.stats[7],pet.stats[6],pet.stats[31],pet.stats[19],pet.stats[74],missileLevel,equipmentLevel,pet.stats[39],pet.stats[41],pet.stats[43],pet.stats[45]);first=false;
}printf("]\n");}
'''
    p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],capture_output=True)
    (STAGE/'compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()[-4000:]
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3500:]
    rows=json.loads(run.stdout);assert len(rows)==11475;(STAGE/'cases.json').write_text(json.dumps(rows,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],scope='Complete base/passive/resistance bodies and complete MAXHP callback branch with explicit current mage/table/formula/stat services;not the full spawn initializer or modern executable.',cases=11475,
        functions={n:hashlib.sha256(b.encode()).hexdigest() for n,b in routines.items()},maxHpCallbackBranchSha256=hashlib.sha256(branch.encode()).hexdigest(),
        inputs={str(x.relative_to(ROOT)):sha(x) for x in [parent,*[ref/(n+'.json') for n in ['skills','monlvl','itemstatcost','monstats']]]},
        artifacts={str(x.relative_to(ROOT)):sha(x) for x in [p,STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log']},
        established=['Current fixed noRatio mage life bases61/88/123 enter as FP256. Mastery flat maxhp is added before rank life growth;the full FP result is retained.',
            'The shared default pet level and expansion monlvl armor/AR compose with the armor aura. The missile skill rank follows current sumsk1calc;requested equipment level is recorded without inventing generated equipment.',
            'The complete MAXHP callback derives changed regeneration from floor(maxHP/256)*DamageRegen>>4. Rank1/mastery0 keeps spawn regeneration15/22/30;rank4/mastery0 Normal writes67.09765625 life and regeneration16.',
            'Elemental Summon Resist is applied in the same complete helper,with raw absorb pools0 and supplied bonus0/20/52/65/75. Modern sunder slots are present butzero;nonzero live inputs stay explicitly unsupported.'],
        remaining=['The initializer/component/AI clocks and modern NPC stat graph are not executed by these supplied scalar services. Source Summon Resist display rounding retains its earlier independent reference conflict.',
            'Full death/reaction,immunity-pierce and legacy-content migration remain separate. Native ceilings and facet rescaling are Sanctuary adapters,verified through actor tests.'])
    output=ROOT/'tools/reference/mage-birth-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==text
    else:output.write_text(text)
    print('Mage birth:11475complete base/passive/resistance/MAXHP-callback contexts;clean sanitizers;FP life and regeneration retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
