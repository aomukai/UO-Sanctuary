#!/usr/bin/env python3
"""Reuse authenticated complete aura/list/modifier bodies for direct HP and Cleansing."""
import hashlib,json,subprocess,sys
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/resource-aura-callback-source'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    prior=ROOT/'tools/reference/might-callback-source-evidence.json';proof=json.loads(prior.read_text())
    original=ROOT/'.local/class-skills/might-callback-source/probe.cpp';assert sha(original)==proof['artifacts'][str(original.relative_to(ROOT))]
    cpp=original.read_text().split('int main(){',1)[0]
    def replace(a,b):
        nonlocal cpp
        assert cpp.count(a)==1,a;cpp=cpp.replace(a,b,1)
    replace('int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return 0;}','int unitStats[512]={},hpwrites=0;int STATLIST_UnitGetStatValue(D2UnitStrc*,int id,int){return unitStats[id];}')
    replace('bool STATES_CheckState(D2UnitStrc*,int id){return active[id];}','bool STATES_CheckState(D2UnitStrc*,int id){return active[id];}\nD2StatListStrc poison,curable,uncurable;')
    replace('return present&&list.state==id?&list:nullptr;','if(id==2&&active[2])return &poison;if(id==9&&active[9])return &curable;if(id==89&&active[89])return &uncurable;return present&&list.state==id?&list:nullptr;')
    replace('void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){events++;}','int eventFrames[8]={};void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int frame,int,int){eventFrames[events++]=frame;}')
    replace('int D2COMMON_10473(D2StatListStrc*){__builtin_trap();}','int D2COMMON_10473(D2StatListStrc* p){return p->until;}')
    replace('void D2COMMON_10476(D2StatListStrc*,int){__builtin_trap();}','void D2COMMON_10476(D2StatListStrc* p,int frame){p->until=frame;}')
    replace('bool STATES_CheckStateMaskCurableByStateId(int){__builtin_trap();}','bool STATES_CheckStateMaskCurableByStateId(int id){return id==9;}')
    replace('void STATLIST_SetUnitStat(D2UnitStrc*,int,int,int){__builtin_trap();}','void STATLIST_SetUnitStat(D2UnitStrc*,int id,int value,int){hpwrites++;unitStats[id]=value;}')
    cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;int curseIds[2]={9,89};tables.nCurseStates=2;tables.pCurseStates=curseIds;
costs[STAT_HITPOINTS].dwItemStatFlags=gdwBitMasks[ITEMSTATCOSTFLAGINDEX_DIRECT];costs[STAT_HITPOINTS].wMaxStat=STAT_MAXHP;
printf("[");bool first=true;
for(int profile:{0,1,2})for(int kind:{0,1})for(int self:{0,1})for(int old:{0,1,2})for(int rank:{1,2})for(int hp:{25600,25599,25088,19200})for(int healing:{0,1})for(int status=0;status<(profile==1?8:1);status++){
owner={};target={};owner.dwUnitId=202;target.dwUnitId=303;D2UnitStrc* receiver=self?&owner:&target;receiver->dwUnitType=kind;
mon={};mon2={};list={};present=old>0;list.skill=profile==0?99:profile==1?109:120;list.state=profile==0?34:profile==1?45:48;list.rank=old;list.owner=101;list.until=9999;list.values[STAT_MANARECOVERYBONUS]=777;
for(int i=0;i<200;i++){active[i]=false;states[i]={};}active[2]=(status&1)!=0;active[9]=(status&2)!=0;active[89]=(status&4)!=0;
poison={};curable={};uncurable={};poison.until=299;curable.until=399;uncurable.until=499;
for(int i=0;i<512;i++)unitStats[i]=0;unitStats[STAT_HITPOINTS]=hp;unitStats[STAT_MAXHP]=25600;
writes=allocations=frees=refresh=gfx=events=rateupdates=hpwrites=0;for(int i=0;i<8;i++)eventFrames[i]=0;
D2BasicAuraParamStrc args;args.nSkillId=list.skill;args.nSkillLevel=rank;args.nAuraState=list.state;
for(int i=0;i<6;i++){args.nAuraStat[i]=-1;args.nAuraStatCalcValue[i]=0;}
int heal=healing?(rank==1?512:768):0;
if(profile==0){args.nAuraStat[0]=STAT_HITPOINTS;args.nAuraStatCalcValue[0]=heal;}
else{args.nAuraStat[0]=profile==1?STAT_ITEM_POISONLENGTHRESIST:STAT_MANARECOVERYBONUS;args.nAuraStatCalcValue[0]=profile==1?61:300;args.nAuraStat[1]=STAT_HITPOINTS;args.nAuraStatCalcValue[1]=heal;}
D2AuraCallbackStrc cb{&game,&owner,&args};int result=SKILLS_AuraCallback_BasicAura(&cb,receiver);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",profile,kind,self,old,rank,hp,healing,status,result,unitStats[STAT_HITPOINTS],hpwrites,poison.until,curable.until,uncurable.until,list.rank,list.owner,list.until,list.values[STAT_MANARECOVERYBONUS],writes,allocations,frees,events,args.field_40,eventFrames[1],eventFrames[2]);first=false;
}printf("]\n");}
'''
    for name,h in proof['routineHashes'].items():assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==h,name
    for path,h in (proof['sourceHashes']|proof['currentTables']).items():assert sha(ROOT/path)==h,path
    STAGE.mkdir(parents=True,exist_ok=True);source=STAGE/'probe.cpp';source.write_text(cpp);binary=STAGE/'probe'
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr
    cases=json.loads(run.stdout);assert len(cases)==1920
    for profile,kind,self,old,rank,hp,healing,status,result,life,hpw,puntil,cuntil,uuntil,after,owner,until,recovery,writes,allocations,frees,events,changed,ev1,ev2 in cases:
        ok=old<=rank;reuse=old==rank and old>0;allocated=ok and not reuse;heal=(512 if rank==1 else 768) if healing else 0;healed=ok and heal>0 and hp<25600
        poison=ok and profile==1 and status&1;curse=ok and profile==1 and status&2;deadlines=([221] if poison else [])+([282] if curse else [])
        expected=(int(ok),min(25600,hp+heal) if ok else hp,int(healed),221 if poison else 299,282 if curse else 399,499,
            rank if ok else old,202 if allocated else 101,151 if ok else 9999,300 if ok and profile==2 else 777 if reuse or not ok else 0,
            (2+int(profile==2)) if ok else 0,int(allocated),int(allocated and old>0),int(ok)+len(deadlines),int(ok)*(int(profile==2)+int(profile==1 and status>0)+int(healed)),*(deadlines+[0,0])[:2])
        assert (result,life,hpw,puntil,cuntil,uuntil,after,owner,until,recovery,writes,allocations,frees,events,changed,ev1,ev2)==expected,(profile,kind,self,old,rank,hp,healing,status,expected)
    out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',sourceCommit=proof['sourceCommit'],reusedSourceProof=str(prior.relative_to(ROOT)),routineHashes=proof['routineHashes'],sourceHashes=proof['sourceHashes'],currentTables=proof['currentTables'],cases=len(cases),sanitizers='ASan/UBSan clean',
        established=['Complete unchanged basic-aura callback,list helper and modifier run together for supplied Prayer/Cleansing/Meditation payloads across player/monster,self/ally,rank priority,full/fractional/missing life and optional healing. Stronger same-skill lists refuse before healing;equal reuse retains creator;replacement allocates current creator.',
            'Direct HP caps at maximum and increments useful count only on a change. Cleansing shortens active poison and curable Amplify Damage deadlines,records removal events and leaves noncurable Battle Cry unchanged while counting its presence. Meditation stores non-direct recovery separately from direct healing.'],
        declaredServices=['Supplied single-receiver stat/list/allocation/clock/state/curability/event services reused from authenticated Might scaffolding,not full source stat graph or world execution. Source removal callbacks are recorded,not invoked;events are captured,not scheduled by a real D2 world.',
            'Healing512/768,Cleansing remaining61 and Meditation300 are supplied payload controls,not formula/synergy/rank-table certification. Complete do65 enumeration,mana consume/regen suppression,formula evaluation,modern executable and native gameplay acceptance remain separate evidence. No modern cure support is invented for unrelated UO states.'],
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in (Path(__file__).resolve(),prior,original,source,binary,out,STAGE/'sanitizer.log')})
    encoded=json.dumps(result,indent=2)+'\n';output=ROOT/'tools/reference/resource-aura-callback-source-evidence.json'
    if '--check' in sys.argv:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Resource auras:1920 complete callback/list/modifier cases;direct HP,useful count,rank priority,poison/curable/noncurable deadlines/events;ASan/UBSan clean.')
if __name__=='__main__':main()
