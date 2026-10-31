#!/usr/bin/env python3
"""Execute complete Blood Golem healing/incoming callbacks with explicit unit/stat services."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'blood-golem-healing-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    inputs={}
    def source(rel):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
    nec=source('source/D2Game/src/SKILLS/SkillNec.cpp');skills=source('source/D2Common/src/D2Skills.cpp');unique=source('source/D2Game/src/MONSTER/MonsterUnique.cpp')
    names=['sub_6FD0F590','D2GAME_EventFunc23_6FD0F1F0','D2GAME_EventFunc26_6FD0F5E0']
    bodies={n:function(nec,n) for n in names};bodies.update({n:function(skills,n) for n in ['D2Common_11033','D2Common_11034']});bodies['MONSTERUNIQUE_CalculatePercentage']=function(unique,'MONSTERUNIQUE_CalculatePercentage')
    data=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';mon=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json';row=json.loads(data.read_text())['85'];m=json.loads(mon.read_text())['bloodgolem']
    assert [row['Param'+str(i)] for i in [1,2,3,5]]==[75,150,30,0] and m['Skill1']=='BloodGolem'
    cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
#define __stdcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_ALIGNMENT_EVIL=0,STAT_HITPOINTS=6;
struct D2SkillsTxt{int dwParam[8]={75,150,30,55,0,25,20,5};}skill;
struct D2MonStatsTxt{int nDrain[3]={100,100,100},nSkill[1]={85};}monster;
struct MonsterData{D2MonStatsTxt*pMonstatsTxt=&monster;}data;
struct D2UnitStrc{int dwUnitType=1,dwClassId=290,hp=0,max=0,alignment=0,dead=0,id=0;MonsterData*pMonsterData=&data;}owner,pet,target;
struct D2DamageStrc{int dwPhysDamage=0,dwDmgTotal=0;};struct D2GameStrc{int nDifficulty=0;};
D2UnitStrc*currentOwner=nullptr;bool monPresent=true,skillPresent=true;int ownerOverlay=0,petOverlay=0;std::vector<std::pair<int,int>> writes;
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){assert(id==85);return skillPresent?&skill:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return DATATBLS_GetSkillsTxtRecord(id);}
D2MonStatsTxt*MONSTERMODE_GetMonStatsTxtRecord(int id){assert(id==290);return monPresent?&monster:nullptr;}
int STATLIST_UnitGetStatValue(D2UnitStrc*p,int stat,int layer){assert(p&&stat==6&&layer==0);return p->hp;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*p){return p->max;}
void STATLIST_SetUnitStat(D2UnitStrc*p,int stat,int value,int layer){assert(stat==6&&layer==0);p->hp=value;writes.emplace_back(p->id,value);}
int STATLIST_GetUnitAlignment(D2UnitStrc*p){return p->alignment;}
D2UnitStrc*AIGENERAL_GetMinionOwner(D2UnitStrc*p){assert(p==&pet);return currentOwner;}
bool SUNIT_IsDead(D2UnitStrc*p){return !p||p->dead;}
void UNITS_SetOverlay(D2UnitStrc*p,int overlay,int layer){assert(overlay==151&&layer==0);if(p==&pet)petOverlay++;else{assert(p==&owner);ownerOverlay++;}}
'''
    for n in ['MONSTERUNIQUE_CalculatePercentage','D2Common_11033','D2Common_11034','sub_6FD0F590','D2GAME_EventFunc23_6FD0F1F0','D2GAME_EventFunc26_6FD0F5E0']:cpp+=bodies[n]+'\n'
    cpp+=r'''
void outWrites(){printf(",[");bool first=true;for(auto [id,hp]:writes){printf("%s[%d,%d]",first?"":",",id,hp);first=false;}printf("]]");}
void heal(int rank,int physical,int drain,int align,int mode,int injury,int kind,int dead,int mask,bool&first){
owner={};pet={};target={};owner.id=0;pet.id=1;owner.max=injury==5?3000001:8001;pet.max=injury==5?4000001:12005;
owner.hp=injury==0||injury==2?owner.max:injury==4?owner.max-1:1001;pet.hp=injury==0||injury==3?pet.max:injury==4?pet.max-257:2003;
owner.dead=mode==2;pet.dead=dead;currentOwner=mode?&owner:nullptr;target.dwUnitType=kind;target.hp=injury==5?2000000:10001;target.alignment=align;pet.dwUnitType=1;
monster.nDrain[0]=drain;monPresent=skillPresent=true;skill.dwParam[4]=0;writes.clear();ownerOverlay=petOverlay=0;D2DamageStrc damage;damage.dwPhysDamage=physical;D2GameStrc game;
int result=D2GAME_EventFunc23_6FD0F1F0(&game,5,mask&1?&pet:nullptr,mask&2?&target:nullptr,mask&4?&damage:nullptr,85,rank);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d]",first?"":",",rank,physical,drain,align,mode,injury,kind,dead,mask,result,owner.hp,pet.hp,ownerOverlay,petOverlay);outWrites();first=false;}
int main(int argc,char**){bool first=true;printf("[");
if(argc==1){for(int r:{0,1,4,12,20,255})for(int phys:{0,1,257,1048577})for(int drain:{0,50,100})for(int align:{0,1})for(int mode:{0,1,2})for(int injury:{0,1,2,3,4,5})for(int kind:{0,1,2})for(int dead:{0,1})heal(r,phys,drain,align,mode,injury,kind,dead,7,first);
for(int mask=0;mask<7;mask++)heal(4,257,100,0,1,1,1,0,mask,first);
}else{for(int mode:{0,1,2})for(int hp:{255,256,3001,2000000})for(int total:{0,1,257,1048577})for(int kind:{0,1,2})for(int hasMon:{0,1})for(int hasSkill:{0,1,2})for(int percent:{0,10,100,125}){
owner={};pet={};owner.id=0;pet.id=1;owner.hp=hp;owner.dead=mode==2;currentOwner=mode?&owner:nullptr;pet.dwUnitType=kind;monPresent=hasMon;skillPresent=hasSkill==1;monster.nSkill[0]=hasSkill?85:-1;skill.dwParam[4]=percent;writes.clear();D2DamageStrc damage;damage.dwDmgTotal=total;D2GameStrc game;
int result=D2GAME_EventFunc26_6FD0F5E0(&game,1,&pet,nullptr,&damage,85,4);
printf("%s[[%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d]",first?"":",",mode,hp,total,kind,hasMon,hasSkill,percent,result,owner.hp,damage.dwDmgTotal);outWrites();first=false;
}}
printf("]\n");}
'''
    src=STAGE/'probe.cpp';src.write_text(cpp);run=subprocess.run(['g++','-std=c++20','-O1','-g','-fwrapv','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(src),'-o',str(STAGE/'probe')],capture_output=True);(STAGE/'compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    counts={}
    for name,args in [('healing',[]),('incoming',['incoming'])]:
        run=subprocess.run([str(STAGE/'probe'),*args],capture_output=True);(STAGE/(name+'-sanitizer.log')).write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
        rows=json.loads(run.stdout);counts[name]=len(rows);(STAGE/(name+'-cases.json')).write_text(json.dumps(rows,separators=(',',':'))+'\n')
    assert counts==dict(healing=15559,incoming=3456),counts
    inputs.update({str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__),data,mon,ROOT/'tools/reference/blood-golem-birth-source-evidence.json']})
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=counts,currentParameters=dict(low=75,high=150,ownerShare=30,incomingShare=0,monsterSkill='BloodGolem'),functions={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()},inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in [src,*[STAGE/n for n in ['compiler.log','healing-cases.json','incoming-cases.json','healing-sanitizer.log','incoming-sanitizer.log']]]},
        established=['Complete callbacks23/26,heal helper,11033/11034and percentage bodies execute with declared unit/stat/ownership/alignment/row services. FP256,drain-before-HP-cap,owner-first/pet/overflow ordering,missing/dead/neutral targets and large-value division are observed.',
            'Current incoming Param5=0 yields no owner life or incoming damage transfer in eligible rows. Synthetic nonzero controls exercise the otherwise active branch without enabling it for the current actor.'],
        remaining=['Native event dispatch,alignment/ownership/notification adapters and current-executable verification remain separate. Legacy11034multiply-before-division rounding can differ from the current table calc2dm expression;do not claim current-engine rounding parity.'])
    path=ROOT/'tools/reference/blood-golem-healing-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Blood healing source:',counts,'; current incoming share0; native/modern rounding acceptance separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
