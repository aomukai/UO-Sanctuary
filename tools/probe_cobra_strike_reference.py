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
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.cobrastrike',legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledRoutines={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},currentSkillsSha256=hashlib.sha256(modern).hexdigest(),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(data),sanitizers='ASan+UBSan'),declaredServices='Reuses Tiger harness stat-list/target/record/skill-level adapters. Complete Cobra consumer executes against one declared state. Unrelated elemental branches3/4 abort if reached; no subexpression is extracted or rewritten.',established=['Successful contact resolves max(initial stored,current effective rank); adds40+5per rank to life drain for1charge, life+mana for2, and double both for3. Invalid/missing/unsuccessful/zero-rank states leave existing packet fields unchanged.', 'Physical ED remains unchanged: Tiger and Cobra occupy separate damage stages. Source compound integer drain fields are added to existing values, not independently healed here.'],reuse={'chargeBuilder':'tools/reference/tiger-strike-source-evidence.json','drainArithmetic':'tools/reference/weapon-leech-engine-evidence.json'},limits=['No native Cobra handler or end-to-end leech delivery is established. Other declared progressive-state families and coexistence need their own integration; invalidcount4 is a corruption control, not a supported saved charge.', 'Current consume-one policy is separate from the obsolete source all-charge removal. Expiry/AR updates, current executable timing/seed/stat/event acceptance remain explicit.', 'Item-drain merge, prehealth/death order, current victim-mana mutation, natural input, actual gear/lifecycle and reload remain pending. Effects/icons are independent.'])
 out=ROOT/'tools/reference/cobra-strike-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Cobra source evidence stale'
 else:out.write_text(encoded)
 print('Cobra Strike:6000 complete source charge/drain packet cases pass; no native handler enabled.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
