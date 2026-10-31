#!/usr/bin/env python3
"""Compose Fire Golem birth through complete shared summon/resistance/MAXHP bodies."""
import argparse,csv,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'fire-golem-birth-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(local_root,check=False):
    STAGE.mkdir(parents=True,exist_ok=True);parent=BASE/'mage-birth-source/probe.cpp';prior=json.loads((ROOT/'tools/reference/mage-birth-source-evidence.json').read_text());assert sha(parent)==prior['artifacts'][str(parent.relative_to(ROOT))]
    cpp=parent.read_text().split('int main(){')[0];tree=json.loads((BASE/'d2moo-source-tree.json').read_text());path=BASE/'d2moo-pinned-source/source/D2Game/src/SKILLS/SkillNec.cpp';raw=path.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']=='source/D2Game/src/SKILLS/SkillNec.cpp')
    names=['D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10','D2GAME_SetSummonPassiveStats_6FD0C530','D2GAME_SetSummonResistance_6FD0C2E0'];bodies={n:function(raw.decode(),n) for n in names}
    for b in bodies.values():assert b in cpp
    refs=ROOT/'tools/reference/d2r-named-equipment/json';tables={n:json.loads((refs/(n+'.json')).read_text()) for n in ['skills','monstats','monlvl','itemstatcost']};manifest=json.loads((refs.parent/'sources.json').read_text())
    for n in tables:assert sha(refs/(n+'.json'))==manifest['files']['json/'+n+'.json']['sha256']
    skill=tables['skills']['94'];mon=tables['monstats']['firegolem'];stats=tables['itemstatcost'];holy=next(r for r in tables['skills'].values() if r['skill'].lower()=='holy fire');assert holy['*Id']==102
    assert skill['srvdofunc']==56 and skill['summon']=='FireGolem' and mon['DamageRegen']==3 and mon['noRatio']==1
    def replace(a,b):
        nonlocal cpp
        assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
    replace('clay=0,blood=0,fire=0','clay=0,blood=0,iron=0')
    replace('assert(id==80)','assert(id==94)');replace('if(id==338)','if(id==102)')
    a=cpp.index('int SKILLS_EvaluateSkillFormula(');b=cpp.index('\nbool SUNITEVENT_GetEvent',a)
    mins=[skill['EMin']]+[skill['EMinLev'+str(i)] for i in range(1,6)];maxs=[skill['EMax']]+[skill['EMaxLev'+str(i)] for i in range(1,6)]
    cpp=cpp[:a]+'''int dim(int low,int high,int r){return r>0?low+(high-low)*std::min(100,110*r/(r+6))/100:0;}
int elemental(bool maximum){int low[]={MIN};int high[]={MAX};int*values=maximum?high:low;int r=rank-1,value=values[0];for(int i=1;i<=5;i++){int width=i==1?7:i==2?8:i<=4?6:r;int points=std::min(r,width);value+=points*values[i];r-=points;}return value;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int id,int r){assert(id==94&&r==rank);switch(f){
case 0:return dim(0,40,gm);case 1:return 25*gm+20*clay;case 2:return 35*iron;case 3:case 4:return 0;
case 5:return 100-dim(25,100,rank);case 6:return dim(25,100,rank);case 7:return elemental(false);case 8:return elemental(true);
case 11:return 20*gm+5*blood;case 12:return std::min(7+rank-1,30);default:assert(false);return 0;}}
'''.replace('MIN',','.join(map(str,mins))).replace('MAX',','.join(map(str,maxs)))+cpp[b:]
    replace('if(s==31)pet.stats[s]+=n;else pet.stats[s]=n;','pet.stats[s]+=n;')
    replace('assert(id==338&&remove==0)','assert(id==102&&remove==0)')
    replace('void D2GAME_AssignSkill_6FD13800(D2UnitStrc*,int,int,int){assert(false);}','void D2GAME_AssignSkill_6FD13800(D2UnitStrc*,int slot,int id,int flag){assert(slot==0&&id==102&&flag==-1);state++;}')
    local_pins={}
    for filename,key,name,cached in [('monstats-sentries-verified.txt','Id','firegolem',mon),('skills.txt','skill',skill['skill'],skill)]:
        p=local_root/filename;local=next(r for r in csv.DictReader(p.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t') if r[key]==name);count=0
        for k,v in cached.items():
            if k!='lineNumber' and k in local:assert str(v).strip(chr(34))==local[k].strip(chr(34)),(k,v,local[k]);count+=1
        local_pins[filename]=dict(sha256=sha(p),corroboratedFields=count)
    cpp+='\nint main(){monster.dwDamageRegen=3;attackSkill.dwFlags[0]=1;\n'
    for level in range(111):
        row=tables['monlvl'][str(level)]
        for member,field in [('dwAC','AC'),('dwTH','TH'),('dwLAC','L-AC'),('dwLTH','L-TH')]:
            for diff,suffix in enumerate(['','(N)','(H)']):cpp+=f'tables.pMonLvlTxt[{level}].{member}[{diff}]={row[field+suffix]};\n'
    for i in range(1,6):cpp+=f'skill.nPassiveStat[{i-1}]={stats[skill["passivestat"+str(i)]]["*ID"] if "passivestat"+str(i) in skill else -1};\n'
    cpp+='for(int i=0;i<6;i++)skill.wAuraStat[i]=-1;for(int i=0;i<3;i++)skill.wAuraEvent[i]=-1;skill.nAuraState=-1;for(int&i:skill.wSumSkill)i=-1;skill.wSumSkill[0]=102;skill.dwSumSkCalc[0]=12;\n'
    for i in range(1,5):cpp+=f'skill.wAuraStat[{i-1}]={stats[skill["aurastat"+str(i)]]["*ID"]};skill.dwAuraStatCalc[{i-1}]={i+4};\n'
    for name,keys,scale in [('hp',['minHP','MinHP(N)','MinHP(H)'],256),('ac',['AC','AC(N)','AC(H)'],1),('ar',['A1TH','A1TH(N)','A1TH(H)'],1)]:cpp+='int '+name+'[3]={'+','.join(str(mon[k]*scale) for k in keys)+'};\n'
    cpp+=r'''bool first=true;printf("[");
for(int level:{1,24,99})for(int r=1;r<=255;r++)for(int diff=0;diff<3;diff++)for(int pattern=0;pattern<7;pattern++){
owner={};pet={};rank=r;gm=pattern==0?0:pattern==1?1:pattern==2?20:pattern==3?28:pattern==4?255:pattern==5?8:2;
blood=pattern==0?0:pattern==1?1:pattern<5?20:pattern==5?2:11;clay=pattern<5?blood:pattern==5?4:0;iron=pattern<5?blood:pattern==5?3:7;
int resist=pattern==0?0:pattern==1?20:pattern==2?52:pattern==3?65:75;owner.stats[STAT_LEVEL]=level;owner.stats[STAT_PASSIVE_SUMMON_RESIST]=resist;game.nDifficulty=diff;game.nGameType=1;
pet.stats[7]=pet.stats[6]=hp[diff];pet.stats[31]=ac[diff];pet.stats[19]=ar[diff];pet.stats[39]=100;pet.stats[74]=(hp[diff]*3)>>12;equipmentLevel=callbackCount=events=state=missileLevel=0;
assert(D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10(&game,&owner,&pet,0,r)==1);assert(D2GAME_SetSummonPassiveStats_6FD0C530(&game,&owner,&pet,94,r,0)==1);D2GAME_SetSummonResistance_6FD0C2E0(&owner,&pet);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,0,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",level,r,diff,gm,blood,clay,iron,resist,pet.stats[12],pet.stats[7],pet.stats[6],pet.stats[31],pet.stats[19],pet.stats[74],pet.stats[67],pet.stats[39],pet.stats[142],pet.stats[48],pet.stats[49],missileLevel,equipmentLevel,events,state,pet.stats[41],pet.stats[43],pet.stats[45]);first=false;
}printf("]\n");}
'''
    src=STAGE/'probe.cpp';src.write_text(cpp);run=subprocess.run(['g++','-std=c++20','-O1','-g','-fwrapv','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(src),'-o',str(STAGE/'probe')],capture_output=True);(STAGE/'compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode();rows=json.loads(run.stdout);assert len(rows)==16065
    assert all(0<r[10] and [mon["minHP"],mon["MinHP(N)"],mon["MinHP(H)"]][r[2]]*256*(20*r[3]+5*r[4])<=2147483647 for r in rows)
    (STAGE/'cases.json').write_text(json.dumps(rows,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(rows),independentSynergyCases=4590,unsafePercentageCases=0,localTables=local_pins,functions={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},maxHpCallbackBranchSha256=prior['maxHpCallbackBranchSha256'],
        reviewedOnly={n:hashlib.sha256(function(raw.decode(),n).encode()).hexdigest() for n in ['SKILLS_SrvDo056_Golem']},inputs={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__),parent,path,*[refs/(n+'.json') for n in tables]]},artifacts={str(p.relative_to(ROOT)):sha(p) for p in [src,STAGE/'cases.json',STAGE/'compiler.log',STAGE/'sanitizer.log']},
        established=['Complete shared base/passive/resistance/MAXHP helpers execute16065current Fire contexts with declared formula/stat/spawn services,including4590independent hard-synergy contexts. No life percentage hazard occurs in this matrix.',
            'Supplied noRatio313/613/1013HP bases and DamageRegen3 retain FP256. Effective Mastery and hard Blood life,Clay attack rating and Iron armor are distinct. Holy Fire rank/selection and four fire-stat writes are captured. Positive absorption suppresses Summon Resist fire addition;other elements still receive it.'],
        remaining=['Native actor/cast/resources/facet/buff/save acceptance remains separate. Fire resistance uses declared base100and additive stat-layer services;full initializer/aggregation/current-executable parity remains open.',
            'Elemental formula outputs are supplied current-table scalar services. Fire-stat units/delivery,absorption caps/ordering,Holy Fire pulse/weapon behavior,death explosion,unarmed/critical/contact/AI/animation/client parity need separate acceptance.'])
    dest=ROOT/'tools/reference/fire-golem-birth-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert dest.read_text()==text
    else:dest.write_text(text)
    print('Fire birth:16065complete contexts/4590independent synergies;native/affinity/damage/aura/death acceptance separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--local-root',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();build(a.local_root,a.check)
