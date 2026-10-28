#!/usr/bin/env python3
"""Pin Ward data and execute the complete shared defensive-buff route."""
import argparse,ast,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'psychic-ward'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs={};routines={};ref=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((ref/'sources.json').read_text());tables={}
    for name in ['skills','states','itemstatcost']:
        p=ref/f'json/{name}.json';assert sha(p)==manifest['files'][f'json/{name}.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())
    skill=tables['skills']['387'];assert skill['srvdofunc']==18 and skill['auraeventfunc1']==40 and skill['auraeventfunc2']==37
    group=sorted((s['state'],s['*ID']) for s in tables['states'].values() if s.get('group')==1);assert group==[('bonearmor',14),('chillingarmor',20),('frozenarmor',10),('justhit',86),('mindbarrier',203),('psychicward',206),('shiverarmor',88)]
    assert [tables['itemstatcost'][n]['*ID'] for n in ['psychicward','psychicwardmax']]==[361,362]
    def body(rel,name):
        p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p);b=function(raw.decode(),name);routines[name]=hashlib.sha256(b.encode()).hexdigest();return b
    old=ROOT/'tools/probe_assassin_buff_reference.py';inputs[str(old.relative_to(ROOT))]=sha(old)
    node=next(x.value for x in ast.walk(ast.parse(old.read_text())) if isinstance(x,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='cpp' for t in x.targets))
    while isinstance(node,ast.BinOp):node=node.left
    cpp=ast.literal_eval(node);cpp=cpp.replace('[200]','[256]').replace('nStatesTxtRecordCount=200','nStatesTxtRecordCount=256').replace('state<200','state<256').replace('[16]','[512]').replace('nItemStatCostTxtRecordCount=16','nItemStatCostTxtRecordCount=512').replace('id<16','id<512')
    cpp='#include <vector>\n#include <array>\n#include <bit>\n'+cpp
    old_factory='D2StatListStrc* sub_6FD10EC0(D2CurseStrc* c){factory++;auto* p=new D2StatListStrc{c->nState,c->nSkillLevel,c->nSkill,game.dwGameFrame+c->nDuration,{},c->pStateRemoveCallback};c->pTarget->lists[c->nState]=p;c->pTarget->active[c->nState]=true;return p;}'
    assert old_factory in cpp;cpp=cpp.replace(old_factory,old_factory.replace('factory++;','factory++;if(!allocation)return nullptr;').replace('game.dwGameFrame+c->nDuration','c->nDuration>0?game.dwGameFrame+c->nDuration:0'))
    cpp=cpp.replace('int values[7],','bool allocation=true;int values[7],')
    registration='void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int){}';assert registration in cpp
    cpp=cpp.replace(registration,'std::vector<std::array<int,4>> events;void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int event,int id,int rank,int callback,int,int state){events.push_back({event,callback,rank,state});}')
    for rel,names in [('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD11C90']),('source/D2Game/src/SKILLS/SkillDruid.cpp',['sub_6FCFE0E0','sub_6FCFE200']),('source/D2Game/src/SKILLS/SkillSor.cpp',['SKILLS_CurseStateCallback_DefensiveBuff','SKILLS_SrvDo018_DefensiveBuff'])]:
        for n in names:cpp+='\n'+body(rel,n)+'\n'
    cpp+=r'''int main(){for(int state:{14,20,10,86,203,206,88})states[state].wGroup=1;bool first=true;printf("[");
for(int mask=0;mask<128;mask++)for(int rank:{1,20,21})for(int alloc:{0,1})for(int invalid:{0,1,2}){
 D2UnitStrc owner;int ids[7]={14,20,10,86,203,206,88};for(int i=0;i<7;i++)if(mask&(1<<i)){int id=ids[i];owner.active[id]=true;owner.lists[id]=new D2StatListStrc{id,99,68,0,{},nullptr};}
 skill={};skill.nAuraState=invalid==2?300:206;skill.wAuraStat[0]=361;skill.wAuraStat[1]=362;skill.wAuraEvent[0]=1;skill.wAuraEvent[1]=2;skill.wAuraEvent[2]=-1;skill.wAuraEventFunc[0]=40;skill.wAuraEventFunc[1]=37;
 guard=invalid==1?1:0;allocation=alloc;values[0]=0;values[1]=values[2]=(15+10*(rank-1))*256;frees=updates=refreshes=unregisters=gfx=factory=0;events.clear();
 int ok=SKILLS_SrvDo018_DefensiveBuff(&game,&owner,387,rank);auto* current=STATLIST_GetStatListFromUnitAndState(&owner,206);int active=0;for(int i=0;i<7;i++)if(owner.active[ids[i]])active|=1<<i;
 bool registered=events.size()==2&&events[0]==std::array<int,4>{1,40,rank,206}&&events[1]==std::array<int,4>{2,37,rank,206};
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",mask,rank,alloc,invalid,ok,active,frees,factory,current?current->stats[361]:0,current?current->stats[362]:0,int(registered),unregisters);first=false;
 for(auto& p:owner.lists)delete p.second;
}printf("]\n");}'''
    p=STAGE/'ward-buff.cpp';p.write_text(cpp);compiler=p.with_suffix('.compiler.log');sanitizer=p.with_suffix('.sanitizer.log')
    with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(p.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(p.with_suffix(''))],capture_output=True);sanitizer.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
    cases=json.loads(run.stdout);assert len(cases)==2304
    for mask,rank,alloc,invalid,ok,active,freed,factory,minimum,maximum,registered,unregisters in cases:
        success=not invalid and alloc!=0
        assert (ok,active,freed,factory,registered,unregisters)==(int(success),mask if invalid else 32 if success else 0,0 if invalid else mask.bit_count(),int(not invalid),int(success),int(success))
        if success:assert minimum==maximum==(15+10*(rank-1))*256
    out=BASE/'psychic-ward-source-probe.json';out.write_bytes(run.stdout)
    bone=body('source/D2Game/src/SKILLS/SkillNec.cpp','D2GAME_EventFunc22_6FD0F000');assert 'nValue0 = 0; pDamage->dwPhysDamage -= nValue0;' in ' '.join(bone.split())
    research=ROOT/'.local/research/warlock-community/warlock-skills-notes.txt';assert 'Functions identically to Bone Armor, only negating physical damage' in research.read_text();inputs[str(research.relative_to(ROOT))]=sha(research)
    artifacts=[Path(__file__).resolve(),p,compiler,sanitizer,out]
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,inputs=inputs,routines=routines,cases=2304,skill=skill,stateGroup1=group,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts},
        established=['Current Ward387uses do18, physical-pool stats361/362 and new callbacks40(absorb)/37(damagedinmelee), state206in group1, zero duration, capacity(15+10*(rank-1)+15*(hardLevitate+hardCleave))*256, mana20+2*(rank-1), stun dm12(37,85)frames.',
            'The complete shared cast/group/stat/passive/remove routines execute2304contexts with all128old-group combinations, three captured ranks, allocation failure and invalid row/state guards. A valid group1cast clears old group states before allocation; the two event descriptors retain cast rank. Callback40and37bodies are not compiled or inferred from registration.',
            'User-supplied primary research reports physical-only absorption, always-applicable stun on a damaging melee event, and incompatibility with Bone Armor/cold armors. Its exact executable build is unstated.'],
        services=['Formula results, statlist allocation/attachment and event registry are declared services. Allocation failure deliberately remains a declared service. Current formulas are checked separately in actual native skill consumers.',
            'Source group contains justhit and unused mindbarrier too. Native group clearing covers the implemented armor members; no generic hit-recovery reset is inferred for those unmapped states.'],
        remaining=['The legacy Bone Armor event22exhaustion branch zeroes its local pool before subtracting it from damage. Do not copy that suspicious branch into modern Ward40. The candidate uses the existing Sanctuary min-pool absorption operation; exact modern event40overflow, display and priority remain unverified.',
            'Fully absorbed/breaking-hit stun delivery, event37qualification/order, modern shield priority and callback rank interpretation require further evidence. Prepared reaction consumes an explicit eligible event; no automatic HP-loss/on-hit rule is invented. Learned casting remains gated.'])
    target=ROOT/'tools/reference/psychic-ward-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Psychic Ward:2304complete defensive-buff/group/registration cases; new event40/37behavior remains explicitly separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
