#!/usr/bin/env python3
"""Complete Cobra charge consumer using the verified Tiger declared stat/target services."""
import argparse,hashlib,json,re,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run([sys.executable,str(ROOT/'tools/probe_tiger_strike_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/SKILLS/SkillAss.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes()
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
 text=raw.decode();blocks={}
 def routine(name):
  m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
  blocks[name]=text[m.start():e];return blocks[name]+'\n'
 modern=(ROOT/'tools/reference/d2r-named-equipment/json/skills.json').read_bytes();rows=json.loads(modern);cobra=next(x for x in rows.values() if x.get('skill')=='Cobra Strike')
 assert(cobra['srvstfunc'],cobra['srvdofunc'],cobra['prgdam'],cobra['Param1'],cobra['Param2'],cobra['Param3'],cobra['Param4'])==(23,34,2,40,5,375,50)
 base=(STAGE/'tiger-strike-source-probe.cpp').read_text().split('int32_t __fastcall SKILLS_SrvSt23_AssasinChargeStrikes',1)[0]
 base=base.replace('nPrgDamage=1','nPrgDamage=2').replace('wTgtOverlay=1;}','wTgtOverlay=1,nEType=0,dwParam[2]={40,5};}')
 base=base.replace('dwEnDmgPct=0;}','dwEnDmgPct=0,dwLifeLeech=0,dwManaLeech=0,dwFrzLen=0,dwColdLen=0;}')
 base+='''
constexpr int ELEMTYPE_COLD=1;
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*,int,int){std::abort();}
void sub_6FCF5BC0(D2UnitStrc*,D2StatListStrc*,D2DamageStrc*){std::abort();}
'''
 for name in ['SKILLS_SrvSt23_AssasinChargeStrikes','SKILLS_StatRemoveCallback_ProgressiveStrike','SKILLS_SrvDo034_TigerStrike_CobraStrike_RoyalStrike','sub_6FCF5870']:base+=routine(name)
 compiled_base=base
 base+=r'''
int main(){bool first=true;printf("[");
for(int count:{0,1,2,3,4})for(int stored:{0,1,3,20,255})for(int current:{0,1,3,20,255})for(int success:{0,1})for(int active:{0,1})for(int listPresent:{0,1})for(int validRow:{0,1})for(int kind:{0,1,2}){
row={};row.nPrgDamage=kind;list={count,count*50,stored,265,321};currentRank=current;exists=listPresent;record=validRow;enabled=active;saved={success,17,13,19,0,0};selected=true;sub_6FCF5870(&owner,&saved);
if(!first)printf(",");first=false;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",count,stored,current,success,active,listPresent,validRow,kind,saved.dwEnDmgPct,saved.dwLifeLeech,saved.dwManaLeech,saved.wResultFlags);
}printf("]\n");}
'''
 cpp=STAGE/'cobra-strike-source-probe.cpp';cpp.write_text(base);binary=cpp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(cpp),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==6000
 for count,stored,current,success,active,present,record,kind,ed,life,mana,flags in data:
  applies=success and active and present and record and kind==2 and count in [1,2,3] and max(stored,current)>0
  steal=(40+5*(max(stored,current)-1))*(2 if count==3 else 1) if applies else 0
  assert(ed,life,mana,flags)==(17,13+steal,19+(steal if count in [2,3] else 0),success)
 fixture=STAGE/'cobra-strike-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 # Full Tiger/Cobra consumers walk two independently stored progressive lists.
 mixed=compiled_base+routine('sub_6FCF5680')
 mixed=mixed.replace('} row;','} row,tigerRow,cobraRow;').replace('nProgressiveStates=1,pProgressiveStates[1]={4}','nProgressiveStates=2,pProgressiveStates[2]={4,5}')
 mixed=mixed.replace('D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return record?&row:nullptr;}', 'D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return record?(id==254?&tigerRow:&cobraRow):nullptr;}')
 mixed=mixed.replace('D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return exists?&list:nullptr;}', 'D2StatListStrc tigerList,cobraList;int tigerRank=0,cobraRank=0;bool tigerOn=false,cobraOn=false;D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int state){return state==4?(tigerOn?&tigerList:nullptr):(cobraOn?&cobraList:nullptr);}')
 mixed=mixed.replace('bool STATES_CheckState(D2UnitStrc*,int){return enabled;}', 'bool STATES_CheckState(D2UnitStrc*,int state){return state==4?tigerOn:cobraOn;}')
 mixed=mixed.replace('void* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return nullptr;}', 'void* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int id){return reinterpret_cast<void*>(intptr_t(id));}').replace('int SKILLS_GetSkillLevel(D2UnitStrc*,void*,int){return currentRank;}', 'int SKILLS_GetSkillLevel(D2UnitStrc*,void* skill,int){return intptr_t(skill)==254?tigerRank:cobraRank;}')
 mixed+=r'''
int main(){bool first=true;printf("[");
for(int tc:{0,1,2,3})for(int cc:{0,1,2,3})for(int ts:{1,3,20})for(int tr:{0,1,3,20})for(int cs:{1,3,20})for(int cr:{0,1,3,20})for(int hit:{0,1}){
 tigerRow={};tigerRow.nPrgDamage=1;cobraRow={};cobraRow.nPrgDamage=2;cobraRow.nAuraState=5;tigerOn=tc>0;cobraOn=cc>0;tigerRank=tr;cobraRank=cr;tigerList={tc,tc*50,ts,254,321};cobraList={cc,cc*50,cs,265,400};record=true;selected=true;overlays=0;saved={hit,17,13,19,0,0};sub_6FCF5680(&owner,&saved);sub_6FCF5870(&owner,&saved);
 if(!first)printf(",");first=false;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",tc,cc,ts,tr,cs,cr,hit,saved.dwEnDmgPct,saved.dwLifeLeech,saved.dwManaLeech,overlays);
}printf("]\n");}
'''
 cp=STAGE/'cobra-strike-composition-source-probe.cpp';cp.write_text(mixed);cb=cp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-pie','-no-pie',str(cp),'-o',str(cb)],check=True)
 composition=json.loads(subprocess.check_output([str(cb)]));assert len(composition)==4608
 for tc,cc,ts,tr,cs,cr,hit,ed,life,mana,overlay in composition:
  tiger=tc*(100+20*(max(ts,tr)-1)) if hit else 0
  cobra=(40+5*(max(cs,cr)-1))*(2 if cc==3 else 1) if hit and cc else 0
  assert(ed,life,mana,overlay)==(17+tiger,13+cobra,19+(cobra if cc>=2 else 0),int(bool(hit and tc)))
 composition_path=STAGE/'cobra-strike-composition-source-probe.json';composition_path.write_text(json.dumps(composition,separators=(',',':'))+'\n')
 # Cobra-shaped do34 builder: same full body, current2mana belongs to caller.
 builder=compiled_base+r'''
int main(){bool first=true;printf("[");for(int count:{0,1,2,3})for(int stored:{1,20})for(int rank:{1,3,20,255})for(int success:{0,1})for(int available:{0,1})for(int hasTarget:{0,1})for(int validRow:{0,1}){
row={};list={count,count*50,stored,265,321};exists=count>0;enabled=exists;selected=hasTarget;record=validRow;allocation=available;hit=success;drains=allocs=events=0;saved={};int result=SKILLS_SrvDo034_TigerStrike_CobraStrike_RoyalStrike(&game,&owner,265,rank);
if(!first)printf(",");first=false;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",count,stored,rank,success,available,hasTarget,validRow,result,exists,list.count,list.ar,list.rank,list.expires,allocs,drains,events);
}printf("]\n");}
'''
 bp=STAGE/'cobra-strike-builder-source-probe.cpp';bp.write_text(builder);bb=bp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-pie','-no-pie',str(bp),'-o',str(bb)],check=True)
 builds=json.loads(subprocess.check_output([str(bb)]));assert len(builds)==512
 build_path=STAGE/'cobra-strike-builder-source-probe.json';build_path.write_text(json.dumps(builds,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.cobrastrike',legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledRoutines={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},currentSkillsSha256=hashlib.sha256(modern).hexdigest(),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(data),sanitizers='ASan+UBSan'),compositionFixture=dict(path=str(composition_path.relative_to(ROOT)),sha256=hashlib.sha256(composition_path.read_bytes()).hexdigest(),cases=len(composition)),builderFixture=dict(path=str(build_path.relative_to(ROOT)),sha256=hashlib.sha256(build_path.read_bytes()).hexdigest(),cases=len(builds)),declaredServices='Reuses Tiger harness stat-list/target/record/skill-level adapters. The6000-case matrix uses one declared Cobra state;4608 complete two-list compositions resolve independent Tiger and Cobra initial/current ranks. A separate512-case fixture executes the complete Cobra-shaped builder. Unrelated elemental branches3/4 abort if reached; no subexpression is extracted or rewritten.',established=['Successful contact resolves max(initial stored,current effective rank); adds40+5per rank to life drain for1charge, life+mana for2, and double both for3. Invalid/missing/unsuccessful/zero-rank states leave existing packet fields unchanged.', 'Physical ED remains unchanged: Tiger and Cobra occupy separate damage stages. Source compound integer drain fields are added to existing values, not independently healed here.'],reuse={'chargeBuilder':'tools/reference/tiger-strike-source-evidence.json','drainArithmetic':'tools/reference/weapon-leech-engine-evidence.json'},limits=['No native Cobra handler or end-to-end leech delivery is established. Other declared progressive-state families need their own integration; native coexistence remains pending; invalidcount4 is a corruption control, not a supported saved charge.', 'Current consume-one policy is separate from the obsolete source all-charge removal. Expiry/AR updates, current executable timing/seed/stat/event acceptance remain explicit.', 'Item-drain merge, prehealth/death order, current victim-mana mutation, natural input, actual gear/lifecycle and reload remain pending. Effects/icons are independent.'])
 out=ROOT/'tools/reference/cobra-strike-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Cobra source evidence stale'
 else:out.write_text(encoded)
 print('Cobra Strike:6000 complete drain cases,4608 complete Tiger/Cobra compositions and512 Cobra builders pass; no native handler certificate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
