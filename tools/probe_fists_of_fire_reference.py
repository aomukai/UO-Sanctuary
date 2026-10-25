#!/usr/bin/env python3
"""Complete do35/do34 charge builder and prgdam4 conversion under declared services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_cobra_strike_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/SKILLS/SkillAss.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);text=raw.decode();blocks={}
 def routine(name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m;end=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
  blocks[name]=text[m.start():end];return blocks[name]
 base=(STAGE/'cobra-strike-source-probe.cpp').read_text().split('int main(){',1)[0]
 base=base.replace('struct D2UnitStrc{','struct D2SkillStrc{};struct D2UnitStrc{void* pInventory=(void*)1;unsigned dwSeqFrame=0,dwFlags=0;')
 base=base.replace('dwParam[2]={40,5}','dwParam[5]={4,4,375,50,1}').replace('nPrgDamage=2','nPrgDamage=4').replace('wResultFlags=0,','wResultFlags=0,dwPhysDamage=0,dwFireDamage=0,dwColdDamage=0,')
 base=base.replace('void* SKILLS_GetHighestLevelSkillFromUnitAndId','D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId').replace('100+20*(rank-1)','3*rank')
 base=base.replace('void sub_6FCF5BC0(D2UnitStrc*,D2StatListStrc*,D2DamageStrc*){std::abort();}','void sub_6FCF5BC0(D2UnitStrc*,D2StatListStrc*,D2DamageStrc*);')
 base=base.replace('void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*,int,int){std::abort();}', 'int elementalCalls=0;void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc* d,int,int rank){++elementalCalls;if(row.nEType==0)d->dwFireDamage+=rank*100;else d->dwColdDamage+=rank*100;}')
 base+=r'''
constexpr int DAMAGERESULTFLAG_SOFTHIT=0x4000,UNITFLAG_SKSRVDOFUNC=0x40,BODYLOC_RARM=4,BODYLOC_LARM=5,ITEMTYPE_WEAPON=42;
D2UnitStrc rightWeapon,leftWeapon;int handMode=3;
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(void*,int body){if(handMode==0)return nullptr;if(body==4)return &rightWeapon;return handMode==1?nullptr:handMode==4?&rightWeapon:&leftWeapon;}
bool ITEMS_CanBeEquipped(D2UnitStrc* p){return p && !(handMode==5 && p==&leftWeapon);}
bool ITEMS_CheckItemTypeId(D2UnitStrc* p,int){return p && !(handMode==6 && p==&leftWeapon);}
void sub_6FD11E40(D2UnitStrc*,D2DamageStrc* d,int element,int value,int,int,int){if(element==0)d->dwFireDamage+=value;else d->dwColdDamage+=value;}
int MONSTERUNIQUE_CalculatePercentage(int value,int percent,int divisor){return int(int64_t(value)*percent/divisor);}
'''
 base+='\n'+routine('sub_6FCF5BC0')+'\n'+routine('SKILLS_SrvDo035_FistsOfFire_ClawsOfThunder_BladesOfIce')
 conversion=base+r'''
int main(){bool comma=false;printf("[");
for(int count:{0,1,2,3,4})for(int stored:{0,1,20,34})for(int current:{0,1,10,34})for(int hit:{0,1})for(int active:{0,1})for(int present:{0,1})for(int valid:{0,1})for(int physical:{0,1,255,256,257,99999})for(int element:{0,1}){
 row={};row.nEType=element;list={count,count*50,stored,259,321};currentRank=current;exists=present;record=valid;enabled=active;saved={};saved.wResultFlags=hit;saved.dwPhysDamage=physical;saved.dwFireDamage=17;saved.dwColdDamage=23;saved.dwColdLen=120;elementalCalls=0;sub_6FCF5870(&owner,&saved);
 if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",count,stored,current,hit,active,present,valid,physical,element,saved.dwPhysDamage,saved.dwFireDamage,saved.dwColdDamage,saved.wResultFlags,saved.dwFrzLen,elementalCalls);
}printf("]\n");}
'''
 def compile_run(name,cpp):
  path=STAGE/(name+'.cpp');path.write_text(cpp);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));path=STAGE/(name+'.json');path.write_text(json.dumps(data,separators=(',',':'))+'\n');return data,dict(path=str(path.relative_to(ROOT)),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),cases=len(data))
 rows,convertFixture=compile_run('fists-fire-conversion-source-probe',conversion);assert len(rows)==15360
 for count,stored,current,hit,active,present,valid,physical,element,p,f,c,flags,freeze,calls in rows:
  rank=max(stored,current);applies=bool(hit and active and present and valid and rank>0 and count>0);converted=physical*min(100,3*rank)//100 if applies else 0
  assert(p,f,c,flags,freeze,calls)==(physical-converted,17+(rank*100+converted if applies and element==0 else 0),23+(rank*100+converted if applies and element==1 else 0),hit|(0x4000 if applies else 0),120 if applies and element==1 and count==3 else 0,int(applies))
 builder=base+r'''
int main(){bool comma=false;printf("[");
for(int mode=0;mode<8;++mode)for(int phase:{0,1,2,3})for(int count:{0,1,2,3})for(int rank:{1,3,20,255})for(int success:{0,1})for(int hasTarget:{0,1})for(int valid:{0,1}){
 row={};handMode=mode;owner.pInventory=mode==7?nullptr:(void*)1;owner.dwSeqFrame=phase<<8;owner.dwFlags=0;list={count,count*50,9,259,321};exists=count>0;enabled=exists;selected=hasTarget;record=valid;allocation=true;hit=success;drains=allocs=events=0;saved={};int result=SKILLS_SrvDo035_FistsOfFire_ClawsOfThunder_BladesOfIce(&game,&owner,259,rank);
 if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",mode,phase,count,rank,success,hasTarget,valid,result,int(bool(owner.dwFlags&UNITFLAG_SKSRVDOFUNC)),int(exists),list.count,list.rank,list.ar,list.expires,allocs,drains);
}printf("]\n");}
'''
 rows,builderFixture=compile_run('fists-fire-builder-source-probe',builder);assert len(rows)==4096
 for mode,phase,count,rank,hit,target,valid,result,flag,present,newcount,initial,ar,expiry,allocs,drains in rows:
  accepted=target and valid;gain=accepted and hit;expectedCount=min(3,count+1) if gain else count
  assert(result,flag,present,newcount,initial,ar,expiry,allocs,drains)==(1,phase%2 if mode in [2,3,7] else 1,int(bool(count or gain)),expectedCount,9 if count or not gain else rank,expectedCount*50,475 if gain else 321,int(bool(accepted)),int(bool(accepted)))
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.fistsoffire',legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledRoutines={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},reusedCompleteRoutines=['sub_6FCF5870','SKILLS_SrvDo034_TigerStrike_CobraStrike_RoyalStrike'],conversionFixture=convertFixture,builderFixture=builderFixture,sanitizers='ASan+UBSan',declaredServices=['Reuses complete Cobra/Tiger source builder/consumer harness; element roll supplies rank*100 in the selected channel, not actual skill endpoint/mastery proof. Formula service supplies current Fists calc1=3*rank; typed writer adds the converted packet.','Inventory services expose absent/single/dual/same-pointer/invalid-offhand and no-inventory contexts. Flag phase is raw source dwSeqFrame; scheduler/player-callback ordinal bridge requires separate composition.','Target/hit/allocation/stat-list/world services retain prior explicit boundaries. Source soft-hit constant0x4000 matches SUnitDmg.h; no native reaction behavior is certified.'],established=['Complete do35 delegates each event to complete do34; valid successful hits gain up to3 charges/refresh375frames while preserving initial rank. Wrapper always returns1 even when delegated builder returns0.','True distinct/equippable weapon pairs (or absent inventory) clear SKSRVDOFUNC on even raw phase and set it on odd; single/invalid pairs set the flag. This flag is not a per-strike mana payment.','Complete prgdam4 path uses max(stored,current) rank, applies skill element roll once, sets soft-hit, then converts min(3*rank,100) percent of existing physical into that element. Fire conversion remains once per charged contact, not multiplied by charge count. Cold controls are declared sibling behavior, not a Blades of Ice handler.'],limits=['No native Fists handler, current player sequence/rate/input or full progressive143/38/39 area/field/lifecycle certificate.','Skill damage endpoint/mastery/RNG, modern consume-one expiry/AR and full weapon/typed/leech/soft-hit/proc ordering remain separate. Invalid count4 is a source corruption control; native saved ledger only accepts1..3.','Actual source mana payment is once after successful start23 for this non-usemanaondo skill; full start/payment integration still requires native acceptance.'])
 out=ROOT/'tools/reference/fists-fire-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Fists source evidence stale'
 else:out.write_text(encoded)
 print('Fists of Fire:15360 complete conversion/consumer and4096 full do35/do34 builder cases pass; no native handler certificate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
