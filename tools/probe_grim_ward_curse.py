#!/usr/bin/env python3
"""Compose frozen complete legacy curse bodies with explicit Ward formula inputs."""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/grim-ward-curse-source'
OUT=ROOT/'.local/class-skills/grim-ward-curse-source-probe.json'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    parent=ROOT/'tools/reference/necromancer-ai-curse-source-evidence.json';receipt=json.loads(parent.read_text())
    ancestor=ROOT/'.local/class-skills/necromancer-ai-curse-source/probe.cpp'
    assert sha(ancestor)==receipt['artifacts'][str(ancestor.relative_to(ROOT))]
    skill=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/skills.json').read_text())['150']
    assert [skill['Param'+str(i)] for i in range(1,9)]==[6,1,20,5,10,60,0,75]
    assert skill['auralencalc']=='par6' and skill['aurarangecalc']=='ln12'
    assert [skill['aurastat'+str(i)] for i in range(1,5)]==['velocitypercent','attackrate','other_animrate','damageresist']
    assert [skill['aurastatcalc'+str(i)] for i in range(1,5)]==['-dm78']*3+["-par3 - (skill('Find Potion'.blvl) * par4)"]
    stats=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/itemstatcost.json').read_text())
    assert [stats[k]['*ID'] for k in ['velocitypercent','attackrate','other_animrate','damageresist']]==[67,68,69,36]
    assert all(stats[k]['UpdateAnimRate']==1 for k in ['velocitypercent','attackrate','other_animrate'])
    cpp=ancestor.read_text().split('int main(){')[0]
    def replace(old,new):
        nonlocal cpp
        assert cpp.count(old)==1,old;cpp=cpp.replace(old,new)
    cpp='#include <vector>\nstd::vector<int> trace;\n'+cpp
    replace('STAT_ATTACKRATE=9,STAT_OTHER_ANIMRATE=10','STAT_ATTACKRATE=68,STAT_OTHER_ANIMRATE=69')
    replace('STAT_DAMAGERESIST=20,','STAT_DAMAGERESIST=36,')
    replace('nItemStatCostTxtRecordCount=64','nItemStatCostTxtRecordCount=512')
    replace('D2StatesTxt* SKILLS_GetStatesTxtRecord(int){return validState?&state:nullptr;}',
        'D2StatesTxt* SKILLS_GetStatesTxtRecord(int){trace.push_back(2);return validState?&state:nullptr;}')
    replace('p->list=nullptr;removals++;','p->list=nullptr;removals++;trace.push_back(3);')
    replace('void STATLIST_SetStatIfListIsValid(D2StatListStrc* p,int s,int v,int){p->stats[s]=v;}',
        'void STATLIST_SetStatIfListIsValid(D2StatListStrc* p,int s,int v,int){p->stats[s]=v;trace.push_back(s==67?4:s==68?6:s==69?7:s==36?8:99);}')
    replace('permissionCalls++;permissionState=state;return p->aiAllowed;',
        'permissionCalls++;permissionState=state;trace.push_back(1);return p->aiAllowed;')
    replace('aiCalls++;aiState=state;','aiCalls++;aiState=state;trace.push_back(5);')
    replace('void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){updates++;}',
        'void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){updates++;trace.push_back(9);}')
    replace('return s>=0?&cost:nullptr;', 'cost.dwItemStatFlags=(s>=67&&s<=69)?1:0;return s>=0?&cost:nullptr;')
    replace('int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int f,int skill,int r){formulaUnit=p?p->dwUnitId:-1;return f==1?(skill==71?175+50*(r-1):200+25*(r-1)):f==2?(skill==71?4+r-1:4):0;}',
        'int hardPotion=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int f,int skill,int r){formulaUnit=p?p->dwUnitId:-1;return f==1?60:f==2?5+r:f==3?-(75*std::min(100,110*r/(r+6))/100):f==4?-20-5*hardPotion:0;}')
    cpp+=r'''
void sample(int rank,int hard,int divisor,int allowed,int alignment,int resist,int base,int hire,int oldKind,int kind,bool& first){
 D2UnitStrc owner,body,target;owner.dwUnitType=0;owner.dwUnitId=7;owner.pvp=true;owner.room=&room;
 body.dwUnitType=3;body.dwUnitId=9;body.owner=&owner;body.room=&room;
 target.dwFlags=7;target.room=&room;target.dwUnitType=kind<0?1:kind;target.alignment=alignment;target.aiAllowed=allowed;
 target.stats[STAT_CURSE_RESISTANCE]=resist;target.baseResistance=base;target.hireling=hire;target.attract=oldKind==5;
 D2StatListStrc old;old.state=oldKind==5?57:56;old.skill=oldKind==4?77:oldKind==5?86:150;
 old.rank=oldKind==2?rank+1:oldKind==3?rank-1:rank;old.creator=999;old.expire=777;
 old.stats[67]=-101;old.stats[68]=-102;old.stats[69]=-103;old.stats[36]=-104;target.list=oldKind?&old:nullptr;
 recipient=kind<0?nullptr:&target;row.wAuraTargetState=56;row.dwAuraFilter=2;
 int stats[]={67,68,69,36,-1,-1},calcs[]={3,3,3,4,0,0};
 for(int i=0;i<6;i++){row.wAuraStat[i]=stats[i];row.dwAuraStatCalc[i]=calcs[i];}
 hardPotion=hard;validDifficulty=true;difficulty.dwAiCurseDiv=divisor;
 removals=events=updates=permissionCalls=aiCalls=traversals=0;permissionState=aiState=filterArg=rangeArg=-1;trace.clear();
 int result=SKILLS_SrvDo030_Curse(&game,&body,150,rank);auto* p=target.list;
 printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,",first?"":",",rank,hard,divisor,allowed,alignment,resist,base,hire,oldKind,kind,result);first=false;
 if(p)printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d]",p->state,p->skill,p->rank,p->creator,p->expire,p->stats[67],p->stats[68],p->stats[69],p->stats[36]);else printf("null");
 printf(",%d,%d,%d,%d,%d,[",permissionCalls,aiCalls,removals,events,updates);
 for(unsigned i=0;i<trace.size();i++)printf("%s%d",i?",":"",trace[i]);
 printf("],%d,%d]]",formulaUnit,rangeArg);
}
int main(){bool first=true;printf("[");
 for(int rank:{1,3,20,255})for(int hard:{0,1,20})for(int divisor:{1,2,4})for(int allowed:{0,1})for(int alignment:{0,1,2})for(int resist:{0,50,99,100})for(int base:{0,99,100})for(int hire:{0,1})for(int old=0;old<6;old++)
  sample(rank,hard,divisor,allowed,alignment,resist,base,hire,old,1,first);
 for(int kind:{-1,0,2,3,4})for(int old=0;old<6;old++)sample(20,20,4,1,1,0,100,0,old,kind,first);
 printf("]\n");}
'''
    if check:
        proof=json.loads((ROOT/'tools/reference/grim-ward-curse-source-evidence.json').read_text())
        assert (STAGE/'probe.cpp').read_text()==cpp and proof['cases']==31134
        for name,h in proof['artifacts'].items():assert sha(ROOT/name)==h,name
        assert sha(OUT)==proof['fixture']['sha256'] and len(json.loads(OUT.read_text()))==31134
        print('Ward curse frozen source,table,compiler/sanitizer and fixture evidence verified;no files changed.')
        return
    STAGE.mkdir(exist_ok=True);source=STAGE/'probe.cpp';source.write_text(cpp)
    c=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(STAGE/'probe')],capture_output=True)
    (STAGE/'compiler.log').write_bytes(c.stdout+c.stderr);assert c.returncode==0,c.stderr.decode()
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0,run.stderr.decode()
    rows=json.loads(run.stdout);assert len(rows)==31134
    payload=json.dumps(rows,separators=(',',':'))+'\n'
    artifacts=[Path(__file__).resolve(),parent,ancestor,source,STAGE/'probe',STAGE/'compiler.log',STAGE/'sanitizer.log',
        ROOT/'tools/reference/d2r-named-equipment/json/skills.json',ROOT/'tools/reference/d2r-named-equipment/json/itemstatcost.json']
    proof=dict(referenceVersion='3.3.93847',legacyCommit=receipt['legacyCommit'],cases=len(rows),component='Ward explicit-context curse stat-list operation',
        compiledUnchangedRoutines=['SKILLS_SrvDo030_Curse','sub_6FD0B450','sub_6FD10EC0'],sanitizers='ASan/UBSan',
        services=['Formula hard-Find-Potion input is supplied explicitly. The caster is a synthetic missile9,owned by player7. This does not resolve how current missile formula lookup obtains that input.',
            'Single declared radial visit,hostility/AI eligibility services,stat-list allocation and scheduling remain supplied. Results compare final lists,counters and ordered permission/factory/stat/AI/animation service operations. No actual AI transition or world curse transport is certified.'],
        handlerEnabled=False,currentExecutableParity=False,processMemoryRead=False,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},fixture=dict(path=str(OUT.relative_to(ROOT)),sha256=hashlib.sha256(payload.encode()).hexdigest()))
    path=ROOT/'tools/reference/grim-ward-curse-source-evidence.json'
    OUT.write_text(payload)
    path.write_text(json.dumps(proof,indent=2)+'\n')
    print('Ward curse:',len(rows),'complete legacy curse-path comparisons;explicit formula context;ordered stat/AI operations;ASan/UBSan clean.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
