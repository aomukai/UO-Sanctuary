#!/usr/bin/env python3
"""Compose Blood Golem birth with the existing complete summon/MAXHP source bodies."""
import argparse,csv,hashlib,json,re,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'blood-golem-birth-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(local_root,check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    parent=BASE/'mage-birth-source/probe.cpp';prior=json.loads((ROOT/'tools/reference/mage-birth-source-evidence.json').read_text())
    assert sha(parent)==prior['artifacts'][str(parent.relative_to(ROOT))]
    cpp=parent.read_text().split('int main(){')[0]
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    path=BASE/'d2moo-pinned-source/source/D2Game/src/SKILLS/SkillNec.cpp';raw=path.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']=='source/D2Game/src/SKILLS/SkillNec.cpp')
    names=['D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10','D2GAME_SetSummonPassiveStats_6FD0C530','D2GAME_SetSummonResistance_6FD0C2E0']
    bodies={n:function(raw.decode(),n) for n in names}
    for body in bodies.values():assert body in cpp
    def replace(a,b):
        nonlocal cpp
        assert cpp.count(a)==1,a;cpp=cpp.replace(a,b)
    replace('assert(id==80)','assert(id==85)')
    a=cpp.index('int SKILLS_EvaluateSkillFormula(');b=cpp.index('\nbool SUNITEVENT_GetEvent',a)
    cpp=cpp[:a]+'''int dim(int low,int high,int rank){return rank>0?low+(high-low)*std::min(100,110*rank/(rank+6))/100:0;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int f,int id,int r){assert(id==85&&r==rank);switch(f){
case 0:return dim(0,40,gm);case 1:return 25*gm+20*clay;case 2:return 55*(rank-1)+6*fire;case 3:return 35*clay;case 4:return 0;
case 5:return dim(0,75,rank);case 11:return (100+20*(rank-1))*(100+20*gm)/100-100;default:assert(false);return 0;}}
''' + cpp[b:]
    replace('assert(event==1&&layer==(78<<16)&&zero==0&&func==6&&type==2&&guid==17);events++;','assert(layer==85&&zero==rank&&type==1&&guid==-1&&((event==5&&func==23)||((event==1||event==2)&&func==26)));events++;')
    replace('void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int,int){assert(false);}','void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int type,int state){assert(type==1&&state==-1);}')
    replace('if(s==31)pet.stats[s]+=n;else pet.stats[s]=n;','pet.stats[s]+=n;')
    refs=ROOT/'tools/reference/d2r-named-equipment/json';tables={n:json.loads((refs/(n+'.json')).read_text()) for n in ['skills','monstats','monlvl','itemstatcost']}
    manifest=json.loads((refs.parent/'sources.json').read_text())
    for n in tables:assert sha(refs/(n+'.json'))==manifest['files']['json/'+n+'.json']['sha256']
    skill=tables['skills']['85'];mon=tables['monstats']['bloodgolem'];stats=tables['itemstatcost']
    assert skill['srvdofunc']==56 and skill['summon']=='BloodGolem' and mon['noRatio']==1 and mon['DamageRegen']==3
    assert [skill['Param'+str(i)] for i in [3,4,7,8]]==[30,55,20,5]
    assert [skill['auraevent'+str(i)] for i in [1,2,3]]==['domeleedamage','damagedinmelee','damagedbymissile'] and [skill['auraeventfunc'+str(i)] for i in [1,2,3]]==[23,26,26]
    enum=BASE/'d2moo-pinned-source/source/D2Game/include/UNIT/SUnitEvent.h'
    eraw=enum.read_bytes();assert hashlib.sha1(b'blob '+str(len(eraw)).encode()+b'\0'+eraw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']=='source/D2Game/include/UNIT/SUnitEvent.h')
    enum_names=re.findall(r'\bUNITEVENT_[A-Z0-9_]+',enum.read_text())
    assert enum_names.index('UNITEVENT_DAMAGEDINMELEE')==1 and enum_names.index('UNITEVENT_DOMELEEDMG')==5 and enum_names.index('UNITEVENT_DAMAGEDBYMISSILE')==2
    local_pins={}
    for filename,key,name,cached in [('monstats-sentries-verified.txt','Id','bloodgolem',mon),('skills.txt','skill',skill['skill'],skill)]:
        owned=local_root/filename
        local=next(r for r in csv.DictReader(owned.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t') if r[key]==name)
        count=0
        for field,value in cached.items():
            if field!='lineNumber' and field in local:
                assert str(value).strip(chr(34))==local[field].strip(chr(34)),(field,value,local[field]);count+=1
        local_pins[filename]=dict(sha256=sha(owned),corroboratedFields=count)
    # The pinned enum starts HITBYMISSILE=0 and DAMAGEDINMELEE=1.
    cpp+='\nint main(){monster.dwDamageRegen=3;\n'
    for level in range(111):
        row=tables['monlvl'][str(level)]
        for member,field in [('dwAC','AC'),('dwTH','TH'),('dwLAC','L-AC'),('dwLTH','L-TH')]:
            for diff,suffix in enumerate(['','(N)','(H)']):cpp+=f'tables.pMonLvlTxt[{level}].{member}[{diff}]={row[field+suffix]};\n'
    for i in range(1,6):cpp+=f'skill.nPassiveStat[{i-1}]={stats[skill["passivestat"+str(i)]]["*ID"]};\n'
    slowid=stats['item_slow']['*ID']
    cpp+='for(int i=0;i<6;i++)skill.wAuraStat[i]=-1;skill.nAuraState=-1;skill.wAuraEvent[0]=5;skill.wAuraEvent[1]=1;skill.wAuraEvent[2]=2;skill.wAuraEventFunc[0]=23;skill.wAuraEventFunc[1]=26;skill.wAuraEventFunc[2]=26;for(int&i:skill.wSumSkill)i=-1;\n'
    for name,keys,scale in [('hp',['minHP','MinHP(N)','MinHP(H)'],256),('ac',['AC','AC(N)','AC(H)'],1),('ar',['A1TH','A1TH(N)','A1TH(H)'],1)]:cpp+='int '+name+'[3]={'+','.join(str(mon[k]*scale) for k in keys)+'};\n'
    cpp+=r'''bool first=true;printf("[");
for(int level:{1,24,99})for(int r=1;r<=255;r++)for(int diff=0;diff<3;diff++)for(int pattern=0;pattern<5;pattern++){
owner={};pet={};rank=r;gm=pattern==0?0:pattern==1?1:pattern==2?20:pattern==3?28:255;clay=blood=fire=pattern==0?0:pattern==1?1:20;int resist=pattern==0?0:pattern==1?20:pattern==2?52:pattern==3?65:75;
owner.stats[STAT_LEVEL]=level;owner.stats[STAT_PASSIVE_SUMMON_RESIST]=resist;game.nDifficulty=diff;game.nGameType=1;
pet.stats[7]=pet.stats[6]=hp[diff];pet.stats[31]=ac[diff];pet.stats[19]=ar[diff];pet.stats[74]=(hp[diff]*3)>>12;equipmentLevel=callbackCount=events=0;
int percent=SKILLS_EvaluateSkillFormula(&owner,11,85,rank);bool unsafe=hp[diff]<=1048576&&percent<=65536&&(int64_t)hp[diff]*percent>INT32_MAX;
assert(D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10(&game,&owner,&pet,0,r)==1);assert(D2GAME_SetSummonPassiveStats_6FD0C530(&game,&owner,&pet,85,r,0)==1);D2GAME_SetSummonResistance_6FD0C2E0(&owner,&pet);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",level,r,diff,gm,blood,clay,fire,resist,(int)unsafe,pet.stats[12],pet.stats[7],pet.stats[6],pet.stats[31],pet.stats[19],pet.stats[74],pet.stats[67],pet.stats[25],pet.stats[SLOWID],equipmentLevel,events,pet.stats[39],pet.stats[41],pet.stats[43],pet.stats[45]);first=false;
}printf("]\n");}
'''.replace('SLOWID',str(slowid))
    source=STAGE/'probe.cpp';source.write_text(cpp)
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fwrapv','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(STAGE/'probe')],capture_output=True)
    compiler=STAGE/'compiler.log';compiler.write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()[-3500:]
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);san=STAGE/'sanitizer.log';san.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3500:]
    rows=json.loads(run.stdout);assert len(rows)==11475;fixture=STAGE/'cases.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',localTables=local_pins,legacyCommit=tree['sha'],cases=len(rows),unsafePercentageCases=sum(r[8] for r in rows),nonpositiveWrappedLifeCases=sum(r[10]<=0 for r in rows),functions={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},maxHpCallbackBranchSha256=prior['maxHpCallbackBranchSha256'],
        reviewedOnly={n:hashlib.sha256(function(raw.decode(),n).encode()).hexdigest() for n in ['SKILLS_SrvDo056_Golem']},
        inputs={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__),parent,path,enum,*[refs/(n+'.json') for n in tables]]},artifacts={str(p.relative_to(ROOT)):sha(p) for p in [source,fixture,compiler,san]},
        established=['Complete shared base/passive/resistance/MAXHPcallback bodies execute11475current Blood Golem contexts with declared current formula/stat/spawn services. Fixed noRatio201/388/637HP bases preserveFP256 percentage life and changed maximum regeneration from DamageRegen3.',
            'Effective Golem Mastery modifies life/AR/velocity;Clay/Iron/Fire synergies use hard ranks. Current source writes attack,armor,damagepercent and velocity separately;three event23/26registrations are captured without executing their callbacks.',
            'The source percentage helper can wrap signed multiplication in an extreme rank domain. Explicit-fwrapv records those rows;native delivery must refuse unsupported arithmetic rather than invent positive life.'],
        remaining=['Initializer/equipment generation,initial S1/AI/clock/movement,full events/current executable and native combat/owner/save delivery are not certified by scalar source services.','The three event23/26callbacks and their full modern health/damage-sharing behavior are not executed here. Native birth,regen,facet,buff,persistence and combat acceptance remain separate. Physical Sunder is supplied zero and remains unsupported.'])
    p=ROOT/'tools/reference/blood-golem-birth-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==text
    else:p.write_text(text)
    print(f'Blood Golem birth:{len(rows)}complete contexts,{result["unsafePercentageCases"]}explicit unsafe percentage rows;native acceptance remains separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');p.add_argument('--local-root',type=Path,required=True);a=p.parse_args();build(a.local_root,a.check)
