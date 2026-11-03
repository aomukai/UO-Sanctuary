#!/usr/bin/env python3
"""Execute complete legacy Frenzy release/stats/damage and shared GUID selection."""
import argparse,hashlib,itertools,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'frenzy-release'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def cases():
    def p(**kw):
        d=dict(frame=0,flags=0,present=True,anchor=5,range=3,row=True,state=2,used=True,param=1,old=True,count=19,expiry=900,
            allocate=True,rank=20,skill=147,gameFrame=1000,duration=150,damage=123,conversion=50,source=128,element=6,result=1,
            extra=4,hitFlags=32,hitClass=0,replace=0,replacement=True,replacementParam=-7,zeroAura=False,candidates=[0,5])
        d.update(kw);return d
    for rank,param,row,state,old,count,allocate in itertools.product([1,3,20,255],[-1,0,1],[False,True],[-1,2,5],[False,True],[0,1,19,255],[False,True]):
        yield 'stats',p(rank=rank,param=param,row=row,state=state,old=old,count=count,allocate=allocate)
    for row,present,result,extra,hitClass,element,conversion,source,used,replace in itertools.product([False,True],[False,True],[0,1,16,17,-1,65536,65537],[0,32768],[0,4294967295],[0,6,255],[-1,0,50],[0,128,255],[False,True],[0,1]):
        yield 'roll',p(row=row,present=present,result=result,extra=extra,hitClass=hitClass,element=element,conversion=conversion,source=source,used=used,replace=replace)
    for frame,present,row,param,old,group,result,replace,replacement in itertools.product([0,256,-1],[False,True],[False,True],[0,1],[False,True],[[],[0],[0,5]],[0,1],[0,1,2],[False,True]):
        yield 'phase',p(frame=frame,present=present,row=row,param=param,old=old,candidates=group,result=result,replace=replace,replacement=replacement)
    for used,zero,gameFrame,duration in itertools.product([False,True],[False,True],[0,1000,4294967280],[-1,0,150]):
        yield 'stats',p(used=used,zeroAura=zero,gameFrame=gameFrame,duration=duration)
    for frame in [-2147483648,-2147483392,2147483647]:yield 'phase',p(frame=frame)
    for frame,flags in itertools.product([0,256,-1],[32,4294967295]):yield 'phase',p(frame=frame,flags=flags)
    for result in [0,1,65537]:yield 'roll',p(result=result,extra=1,hitFlags=4294967295)
def main(check=False):
    STAGE.mkdir(exist_ok=True);parentpath=ROOT/'tools/reference/double-swing-phase-source-evidence.json';parent=json.loads(parentpath.read_text())
    original=BASE/'double-swing-phase/source.cpp';assert sha(original)==parent['artifacts'][str(original.relative_to(ROOT))]
    cpp=original.read_text().split('int main()',1)[0]
    def replace(old,new):
        nonlocal cpp
        assert cpp.count(old)==1,old;cpp=cpp.replace(old,new,1)
    replace('struct D2GameStrc{};','struct D2GameStrc{uint32_t dwGameFrame;void* pMemoryPool=nullptr;};')
    replace('int32_t dwUnitId,slot,dwSeqFrame;uint32_t dwFlags;','int32_t dwUnitId,slot,dwSeqFrame;uint32_t dwFlags;int32_t dwUnitType=0;')
    replace('#define __fastcall','#define __fastcall\n#define __stdcall\n#include <cassert>\n#define D2_ASSERT assert\nvoid frenzyDrainHook();')
    replace('trace.push_back({6,slot(p),zero});}','trace.push_back({6,slot(p),zero});frenzyDrainHook();}')
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==parent['legacyCommit'];sources={};routines={}
    def source(relative):
        p=BASE/'d2moo-pinned-source'/relative;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        sources[relative]=sha(p);return raw.decode()
    def body(relative,name):
        text=function(source(relative),name);routines[name]=hashlib.sha256(text.encode()).hexdigest();return text+'\n'
    for relative,fields in [
        ('source/D2Game/include/UNIT/SUnitDmg.h',['uint16_t wResultFlags;','uint32_t dwHitFlags;','uint32_t dwHitClass;','int8_t nConvType;']),
        ('source/D2Common/include/DataTbls/SkillsTbls.h',['int16_t nAuraState;','uint16_t wResultFlags;','uint32_t dwHitClass;','uint8_t nEType;','uint8_t nSrcDam;'])]:
        text=source(relative);assert all(field in text for field in fields)
    cpp+=r'''
struct D2SkillStrc{int32_t nPar1,token;};
struct D2SkillsTxt{int16_t nAuraState=2;uint16_t wResultFlags=4;uint32_t dwHitFlags=32,dwHitClass=0;uint8_t nSrcDam=128,nEType=6;int dwCalc[4]={0,1,2,3};int dwAuraLenCalc=-1;int wAuraStat[6]={1,2,-1,-1,-1,-1};int dwAuraStatCalc[6]={200,201,0,0,0,0};};
struct D2DamageStrc{uint32_t dwHitFlags=0;uint16_t wResultFlags=0;uint32_t dwHitClass=0;int dwEnDmgPct=0,dwConvPct=0;int8_t nConvType=0;int elemental=0;};
struct D2StatListStrc{int state=-1,charges=0,expiry=0;bool callback=false,posted=false;int velocity=0,attack=0,other=0;};
struct Tables{int nStatesTxtRecordCount=5;}tables;Tables* sgptDataTables=&tables;
enum{STAT_SKILL_FRENZY,STAT_VELOCITY,STAT_ATTACKRATE,STAT_OTHER_ANIMRATE};
constexpr int DAMAGERESULTFLAG_SUCCESSFULHIT=1,EVENTTYPE_REMOVESTATE=10;
D2SkillsTxt row;D2SkillStrc firstSkill,replacementSkill;D2SkillStrc* used=nullptr;
D2StatListStrc storage;D2StatListStrc* list=nullptr;D2DamageStrc savedPacket;bool packetPresent=false;
bool rowPresent=true,allocation=true,replacementPresent=true,zeroAura=false;
int replacementAt=0,drainCalls=0,duration=150,damage=123,conversion=50,rawResult=1,rankValue=20;
void frenzyDrainHook(){if(++drainCalls==replacementAt)used=replacementPresent?&replacementSkill:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int skill){trace.push_back({8,skill,rowPresent});return rowPresent?&row:nullptr;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){trace.push_back({7,used?used->token:0,used?used->nPar1:0});return used;}
int SKILLS_GetToHitFactor(D2UnitStrc*,int skill,int rank){trace.push_back({9,skill,rank});return 137;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc* p,int ar,int zero){trace.push_back({10,slot(p),ar,zero,rawResult});return rawResult;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int skill,int level){
 int value=formula==-1?duration:formula==0?damage:formula==3?conversion:zeroAura?0:formula==200?level*3:level*2;
 trace.push_back({12,formula,skill,level,value});return value;
}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*p,int skill,int rank){trace.push_back({11,skill,rank});p->elemental=77;}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*p,D2DamageStrc*d,uint8_t source){savedPacket=*d;packetPresent=true;trace.push_back({13,slot(p),source});}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int state){trace.push_back({14,state,list!=nullptr});return list;}
D2StatListStrc* STATLIST_AllocStatList(void*,uint32_t flags,int expiry,int kind,int id){trace.push_back({16,flags,expiry,kind,id,allocation});if(!allocation)return nullptr;storage={};storage.charges=37;storage.expiry=expiry;return list=&storage;}
void STATLIST_SetState(D2StatListStrc*p,int state){p->state=state;}
void sub_6FD10E50(D2UnitStrc*,int,D2StatListStrc*){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*p,void(*)(D2UnitStrc*,int,D2StatListStrc*)){trace.push_back({17,p->charges});p->callback=true;}
void STATLIST_SetStat(D2StatListStrc*p,int stat,int value,int){if(stat==STAT_SKILL_FRENZY)p->charges=value;else if(stat==STAT_VELOCITY)p->velocity=value;else if(stat==STAT_ATTACKRATE)p->attack=value;else if(stat==STAT_OTHER_ANIMRATE)p->other=value;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc*p,int stat,int value,int layer){STATLIST_SetStat(p,stat,value,layer);}
int STATLIST_GetStatValue(D2StatListStrc*p,int stat,int){assert(stat==STAT_SKILL_FRENZY);return p->charges;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*p,int flag){trace.push_back({18,p->charges,flag});p->posted=true;}
void STATES_ToggleState(D2UnitStrc*,int state,int flag){trace.push_back({19,state,flag});}
void D2COMMON_10476(D2StatListStrc*p,int expiry){p->expiry=expiry;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int zero,int zero2){assert(type==EVENTTYPE_REMOVESTATE);trace.push_back({20,frame,zero,zero2});}
void STATES_ToggleGfxStateFlag(D2UnitStrc*,int state,int flag){trace.push_back({21,state,flag});}
void* SKILLS_GetItemStatCostTxtRecord(int id){return id==1||id==2?(void*)1:nullptr;}
void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int){trace.push_back({22,storage.charges,storage.velocity,storage.attack,storage.other});}
void SUNITDMG_FreeAttackerDefenderCombatList(D2GameStrc*,D2UnitStrc*,D2UnitStrc*p){trace.push_back({23,slot(p)});}
void sub_6FC80A30(D2GameStrc*,D2UnitStrc*){trace.push_back({24});}
void sub_6FC80E10(D2GameStrc*,D2UnitStrc*){trace.push_back({25});}
'''
    common='source/D2Common/src/D2Skills.cpp';bar='source/D2Game/src/SKILLS/SkillBar.cpp'
    for relative,names in [(common,['SKILLS_GetParam1','SKILLS_SetParam1']),('source/D2Game/src/SKILLS/SkillDruid.cpp',['sub_6FCFE0E0']),(bar,['SKILLS_ApplyFrenzyStats','SKILLS_RollFrenzyDamage','SKILLS_SrvDo009_Frenzy'])]:
        for name in names:cpp+=body(relative,name)
    cpp+=r'''int main(){bool first=true;printf("[");int family,frame,present,known,state,hasUsed,parameter,old,oldCount,oldExpiry,alloc,skill,n,replacement,repParameter,zero;uint32_t flags,anchor,gameFrame,hitFlags,hitClass;int src,element,extra;
while(std::cin>>family>>frame>>flags>>present>>anchor>>reach>>known>>state>>hasUsed>>parameter>>old>>oldCount>>oldExpiry>>alloc>>rankValue>>skill>>gameFrame>>duration>>damage>>conversion>>src>>element>>rawResult>>extra>>hitFlags>>hitClass>>replacementAt>>replacement>>repParameter>>zero>>n){
 D2GameStrc game{gameFrame};D2UnitStrc actor{999,999,frame,flags},initial{(int32_t)anchor,1,0,0};std::vector<D2UnitStrc> units(n);eligible.clear();trace.clear();target=present?&initial:nullptr;
 for(int i=0;i<n;i++){uint32_t id;std::cin>>id;units[i]={(int32_t)id,i+2,0,0};eligible.push_back(&units[i]);}
 row={};row.nAuraState=state;row.wResultFlags=extra;row.dwHitFlags=hitFlags;row.dwHitClass=hitClass;row.nEType=element;row.nSrcDam=src;
 rowPresent=known;allocation=alloc;replacementPresent=replacement;zeroAura=zero;drainCalls=0;packetPresent=false;savedPacket={};
 firstSkill={parameter,1};replacementSkill={repParameter,2};used=hasUsed?&firstSkill:nullptr;storage={state,oldCount,oldExpiry,true,true,17,23,23};list=old?&storage:nullptr;
 int result=0;if(family==0){SKILLS_ApplyFrenzyStats(&game,&actor,skill,rankValue);result=1;}else if(family==1)result=SKILLS_RollFrenzyDamage(&game,&actor,target,skill,rankValue);else result=SKILLS_SrvDo009_Frenzy(&game,&actor,skill,rankValue);
 printf("%s[%d,%u,%d,%d,%d,%d,[",first?"":",",result,actor.dwFlags,slot(target),used?used->token:0,firstSkill.nPar1,replacementSkill.nPar1);first=false;
 if(list)printf("%d,%d,%d,%d,%d,%d,%d,%d",list->charges,list->expiry,list->state,list->callback,list->posted,list->velocity,list->attack,list->other);printf("],[");
 if(packetPresent)printf("%u,%u,%u,%d,%d,%d,%d",savedPacket.wResultFlags,savedPacket.dwHitFlags,savedPacket.dwHitClass,savedPacket.dwEnDmgPct,savedPacket.dwConvPct,(int)savedPacket.nConvType,savedPacket.elemental);printf("],[");
 for(size_t i=0;i<trace.size();i++){printf("%s[",i?",":"");for(size_t j=0;j<trace[i].size();j++)printf("%s%lld",j?",":"",(long long)trace[i][j]);printf("]");}printf("]]");
}printf("]\n");}'''
    path=STAGE/'source.cpp';path.write_text(cpp);compiler=STAGE/'source-compiler.log'
    with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(STAGE/'source')],check=True,stdout=log,stderr=subprocess.STDOUT)
    inputs=list(cases());lines=[];fields=['frame','flags','present','anchor','range','row','state','used','param','old','count','expiry','allocate','rank','skill','gameFrame','duration','damage','conversion','source','element','result','extra','hitFlags','hitClass','replace','replacement','replacementParam','zeroAura']
    for family,p in inputs:lines.append(' '.join(map(str,[['stats','roll','phase'].index(family),*[int(p[k]) for k in fields],len(p['candidates']),*p['candidates']])))
    run=subprocess.run([str(STAGE/'source')],input=('\n'.join(lines)+'\n').encode(),capture_output=True);sanitizer=STAGE/'source-sanitizer.log';sanitizer.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
    outputs=json.loads(run.stdout);assert len(outputs)==len(inputs);rows=[[f,p,r] for (f,p),r in zip(inputs,outputs)];encoded=json.dumps(rows,separators=(',',':'))+'\n'
    fixture=BASE/'frenzy-release-source-probe.json';out=ROOT/'tools/reference/frenzy-release-source-evidence.json'
    proof=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(rows),byFamily={f:sum(x[0]==f for x in rows) for f in ['stats','roll','phase']},currentExecutableParity=False,handlerEnabled=False,
        sourceHashes=sources,completeRoutineHashes=routines,inheritedGuidReference=sha(parentpath),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__).resolve(),path,compiler,sanitizer,parentpath,original]},fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(encoded.encode()).hexdigest()),
        limits=['Complete legacy do9/ApplyFrenzyStats/RollFrenzyDamage/shared aura writer/Param1helpers execute with existing complete GUID selector. Target eligibility,formulas,stat/equipment/combat services are explicit;there is no modern start67,payment or player/world certification.',
            'Formula values are supplied control services,including synthetic aura values. This does not certify modern Frenzy rounding,full stat consumers or source physical damage. Replacement-used-skill and narrow-word/element controls include synthetic contexts.'])
    text=json.dumps(proof,indent=2)+'\n'
    if check:assert fixture.read_text()==encoded and out.read_text()==text
    else:fixture.write_text(encoded);out.write_text(text)
    print('Frenzy release:',len(rows),'complete legacy contexts;',proof['byFamily'],';ASan/UBSan clean;modern start67 remains gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
