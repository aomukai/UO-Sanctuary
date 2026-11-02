#!/usr/bin/env python3
"""Reuse complete basic aura/list/modifier bodies for Concentration/Fanaticism packets."""
import hashlib,json,subprocess,sys
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/offensive-aura-callback-source'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    prior=ROOT/'tools/reference/might-callback-source-evidence.json';proof=json.loads(prior.read_text());original=ROOT/'.local/class-skills/might-callback-source/probe.cpp'
    assert sha(original)==proof['artifacts'][str(original.relative_to(ROOT))];cpp=original.read_text().split('int main(){',1)[0]
    a='int SUNITDMG_GetColdEffect(D2GameStrc*,D2UnitStrc*){__builtin_trap();}';assert cpp.count(a)==1
    cpp=cpp.replace(a,'int coldEffect=0,coldReads=0;int SUNITDMG_GetColdEffect(D2GameStrc*,D2UnitStrc*){coldReads++;return coldEffect;}')
    rows=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/skills.json').read_text());fana=rows['122']
    assert (fana['Param5'],fana['Param6'],fana['aurastatcalc3'],fana['passivecalc1'])==(50,17,'ln56/2','ln56')
    cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int profile:{0,1})for(int kind:{0,1})for(int self:{0,1})for(int old:{0,1,2})for(int rank:{1,2})for(int cold:{-50,0})for(int full=0;full<(profile==1&&self?2:1);full++){
owner={};target={};owner.dwUnitId=202;target.dwUnitId=303;D2UnitStrc* receiver=self?&owner:&target;receiver->dwUnitType=kind;
mon={};mon2={};list={};present=old>0;list.skill=profile==0?113:122;list.state=profile==0?42:49;list.rank=old;list.owner=101;list.until=9999;
for(int id:{STAT_DAMAGEPERCENT,STAT_SKILL_CONCENTRATION,STAT_ATTACKRATE,STAT_OTHER_ANIMRATE,STAT_ITEM_TOHIT_PERCENT})list.values[id]=777;
for(int i=0;i<200;i++){active[i]=false;states[i]={};}writes=allocations=frees=refresh=gfx=events=rateupdates=coldReads=0;coldEffect=cold;
D2BasicAuraParamStrc args;args.nSkillId=list.skill;args.nAuraState=list.state;args.nSkillLevel=rank;for(int i=0;i<6;i++){args.nAuraStat[i]=-1;args.nAuraStatCalcValue[i]=0;}
if(profile==0){args.nAuraStat[0]=STAT_DAMAGEPERCENT;args.nAuraStatCalcValue[0]=60+15*(rank-1);args.nAuraStat[1]=STAT_SKILL_CONCENTRATION;args.nAuraStatCalcValue[1]=20;}
else{args.nAuraStat[0]=STAT_ATTACKRATE;args.nAuraStatCalcValue[0]=rank==1?14:18;args.nAuraStat[1]=STAT_ITEM_TOHIT_PERCENT;args.nAuraStatCalcValue[1]=40+5*(rank-1);args.nAuraStat[2]=STAT_DAMAGEPERCENT;args.nAuraStatCalcValue[2]=(50+17*(rank-1))/(full?1:2);}
D2AuraCallbackStrc cb{&game,&owner,&args};int result=SKILLS_AuraCallback_BasicAura(&cb,receiver);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",profile,kind,self,old,rank,cold,full,result,list.rank,list.owner,list.until,list.values[STAT_DAMAGEPERCENT],list.values[STAT_SKILL_CONCENTRATION],list.values[STAT_ATTACKRATE],list.values[STAT_OTHER_ANIMRATE],list.values[STAT_ITEM_TOHIT_PERCENT],writes,allocations,frees,events,args.field_40,coldReads,rateupdates,gfx);first=false;
}printf("]\n");}
'''
    for name,h in proof['routineHashes'].items():assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==h,name
    for path,h in (proof['sourceHashes']|proof['currentTables']).items():assert sha(ROOT/path)==h,path
    STAGE.mkdir(parents=True,exist_ok=True);source=STAGE/'probe.cpp';source.write_text(cpp);binary=STAGE/'probe'
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr
    cases=json.loads(run.stdout);assert len(cases)==120
    for profile,kind,self,old,rank,cold,full,result,after,owner,until,damage,concentration,attack,other,ar,writes,allocations,frees,events,changed,reads,rates,gfx in cases:
        ok=old<=rank;reuse=old==rank and old>0;allocated=ok and not reuse;untouched=777 if reuse or not ok else 0
        values=[60+15*(rank-1),20,untouched,untouched,untouched] if profile==0 else [(50+17*(rank-1)) if full else (50+17*(rank-1))//2,untouched,14 if rank==1 else 18,14 if rank==1 else 18,40+5*(rank-1)]
        expected=(int(ok),rank if ok else old,202 if allocated else 101,151 if ok else 9999,*(values if ok else [777]*5),
            (4 if profile==0 else 6) if ok else 0,int(allocated),int(allocated and old>0),int(ok),(2 if profile==0 else 3) if ok else 0,int(ok and profile==1),int(ok))
        assert (result,after,owner,until,damage,concentration,attack,other,ar,writes,allocations,frees,events,changed,reads,rates)==expected,(profile,kind,self,old,rank,cold,full,expected)
    out=STAGE/'cases.json';out.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',sourceCommit=proof['sourceCommit'],reusedSourceProof=str(prior.relative_to(ROOT)),routineHashes=proof['routineHashes'],sourceHashes=proof['sourceHashes'],currentTables=proof['currentTables'],cases=len(cases),sanitizers='ASan/UBSan clean',
        established=['Complete unchanged basic-aura/list/modifier composition executes supplied Concentration and Fanaticism multi-stat packets for player/monster,self/ally,rank priority and cold-effect controls. Concentration stores independent physical damage/recovery chance. Fanaticism consults cold effect,stores skill attack rate and other_animrate together,and separately stores AR and physical damage.',
            'Weaker lists refuse before every stat/cold/rate operation;equal rank retains creator and replaces packet values;stronger/absent creates current-creator lists. Both supplied half/full self damage packets are tested.'],
        declaredServices=['Controlled stat/list/allocation/state/clock/cold/rate/event services,not complete source stat/animation/world behavior or a modern executable certificate. Cold values-50/0 leave positive Fanaticism attack rates unchanged;actual cold/global rate composition remains separate.',
            'Supplied rank1/2 packets and optional full-self damage do not execute do65mana comparison,self-passive application,formulas,range/filter search or native consumers. Modern zero-mana self damage remains uncertain;this callback proof does not resolve it.'],
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in (Path(__file__).resolve(),prior,original,source,binary,out,STAGE/'sanitizer.log')})
    encoded=json.dumps(result,indent=2)+'\n';output=ROOT/'tools/reference/offensive-aura-callback-source-evidence.json'
    if '--check' in sys.argv:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Offensive auras:120complete multi-stat callback/list/modifier cases;Fanaticism cold consult/other_animrate/AR/damage,Concentration independent stats;ASan/UBSan clean.')
if __name__=='__main__':main()
