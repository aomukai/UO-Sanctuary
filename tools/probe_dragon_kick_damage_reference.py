#!/usr/bin/env python3
"""Complete legacy kick body getters and boot arithmetic, with declared stat services."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_dragon_talon_hands_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Common/src/D2Skills.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);text=raw.decode();blocks={}
 for name in ['SKILLS_GetMinPhysDamage','SKILLS_GetMaxPhysDamage','SKILLS_CalculateKickDamage']:
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m;end=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
  blocks[name]=text[m.start():end]
 armor=ROOT/'tools/reference/d2r-named-equipment/json/armor.json';ar=json.loads(armor.read_bytes());boots=[(k,v) for k,v in ar.items() if v.get('type')=='boot'];assert len(boots)==15
 base=(STAGE/'dragon-talon-hands-source-probe.cpp').read_text().split('int main(){',1)[0]
 base=base.replace('#include <cstdio>','#include <cstdio>\n#include <cstdlib>').replace('int nWeapSel=4;','int nWeapSel=4;unsigned dwFlags[1]={1};int nSrcDam=0,dwMinDam=0,dwMaxDam=0,dwMinLvlDam[5]={},dwMaxLvlDam[5]={},dwDmgSymPerCalc=-1,nToHitShift=8;')
 base+=r'''
constexpr unsigned gdwBitMasks[]={1};constexpr int SKILLSFLAGINDEX_KICK=0,STAT_LEVEL=12,STAT_STRENGTH=0,STAT_DEXTERITY=2,STAT_MINDAMAGE=21,STAT_MAXDAMAGE=22,STAT_ITEM_KICKDAMAGE=159,STAT_DAMAGEPERCENT=25,STAT_ITEM_MAXDAMAGE_PERCENT=18,BODYLOC_RARM=4,BODYLOC_FEET=9;
int strength=150,dexterity=150,kick=0,penalty=0,bootIndex=-1;D2UnitStrc boot;
struct D2ItemsTxt{int nMinDam,nMaxDam,strength,dexterity;};
D2ItemsTxt bootRows[]={__BOOT_ROWS__};
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){return &row;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){if(stat==STAT_LEVEL)return 13;if(stat==STAT_STRENGTH)return strength+(hands&1?11:0)+(hands&2?17:0);if(stat==STAT_DEXTERITY)return dexterity+(hands&1?3:0)+(hands&2?5:0);if(stat==STAT_DAMAGEPERCENT)return penalty+(active&1?37:0)+(active&2?53:0);if(stat==STAT_ITEM_MAXDAMAGE_PERCENT)return 23;std::abort();}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int stat,int){if(stat!=STAT_ITEM_KICKDAMAGE)std::abort();return kick;}
D2UnitStrc* D2Common_10434(D2UnitStrc*,int){std::abort();}
int INVENTORY_GetWieldType(D2UnitStrc*,void*){std::abort();}
int STATLIST_GetMinDamageFromUnit(D2UnitStrc*,int){std::abort();}int STATLIST_GetMaxDamageFromUnit(D2UnitStrc*,int){std::abort();}
int SKILLS_CalculateDamageBonusByLevel(int,int*){std::abort();}int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){std::abort();}
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(void*,int loc){return loc==BODYLOC_FEET?(bootIndex>=0?&boot:nullptr):hand(loc==4?1:2);}
int ITEMS_GetBodyLocation(D2UnitStrc* p){return p->id==1?4:5;}
D2ItemsTxt* DATATBLS_GetItemsTxtRecord(int id){return id>=0&&id<15?&bootRows[id]:nullptr;}
short ITEMS_GetStrengthBonus(D2UnitStrc* p){return bootRows[p->dwClassId].strength;}
short ITEMS_GetDexBonus(D2UnitStrc* p){return bootRows[p->dwClassId].dexterity;}
'''
 base=base.replace('__BOOT_ROWS__',','.join('{'+','.join(str(v.get(k,0)) for k in ['mindam','maxdam','StrBonus','DexBonus'])+'}' for _,v in boots))
 base+='\n'.join(blocks.values())+r'''
int main(){first.id=1;second.id=2;boot.dwUnitType=UNIT_ITEM;bool comma=false;printf("[");
for(int kind:{0,1})for(int s:{0,10,20,150})for(int d:{0,10,50,150})for(int bi=-1;bi<15;++bi)for(int flat:{0,7})for(int pen:{-100,-90,0,100})for(int ed:{0,100})for(int mask:{0,1,3}){
 owner.dwUnitType=kind==0?UNIT_PLAYER:UNIT_MONSTER;strength=s;dexterity=d;bootIndex=bi;boot.dwClassId=bi;kick=flat;penalty=pen;hands=owned=active=mask;primary=1;mergeCalls=trace=0;
 int bodyMin=SKILLS_GetMinPhysDamage(&owner,255,1,0),bodyMax=SKILLS_GetMaxPhysDamage(&owner,255,1,0),lo=0,hi=0,pct=ed;SKILLS_CalculateKickDamage(&owner,&lo,&hi,&pct);
 if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",kind,s,d,bi,flat,pen,ed,mask,bodyMin,bodyMax,lo,hi,pct,active);
}printf("]\n");}
'''
 path=STAGE/'dragon-kick-damage-source-probe.cpp';path.write_text(base);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(path),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==24576
 for kind,s,d,bi,flat,pen,ed,mask,bmin,bmax,lo,hi,pct,active in data:
  sv=s+(11 if mask&1 else 0)+(17 if mask&2 else 0);dv=d+(3 if mask&1 else 0)+(5 if mask&2 else 0);body=max(1,sv+dv-20) if kind==0 else 39
  assert(bmin,bmax)==(body//4*256,body//3*256)
  if bi<0:expected=(flat,flat,ed)
  else:
   b=boots[bi][1];lower=2*flat+b.get('mindam',0);upper=max(lower,2*flat+b.get('maxdam',0));percent=max(-90,b.get('StrBonus',0)*sv//100+b.get('DexBonus',0)*dv//100+pen)
   expected=(lower,upper,ed+23+percent)
  assert(lo,hi,pct,active)==(*expected,mask&1)
 fixture=STAGE/'dragon-kick-damage-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledRoutines={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},currentArmorSha256=hashlib.sha256(armor.read_bytes()).hexdigest(),boots=[dict(code=k,min=v.get('mindam',0),max=v.get('maxdam',0),strength=v.get('StrBonus',0),dexterity=v.get('DexBonus',0)) for k,v in boots],fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(data),sanitizers='ASan+UBSan'),declaredServices=['Reuses hand-context probe declarations, including observed damage-mask merge services. Nondamage weapon strength/dexterity remain present while weapon DamageRelated ED is temporarily removed. Full stat propagation is not compiled here.','Current armor supplies15 boot base endpoint/stat-coefficient records. Nonkick getter branches abort; skill row always has kick flag. NPC level13 and explicit strength/dexterity/flat-kick/ED/maxdamage-stat inputs are fixture services.','Full three unchanged routine bodies compile; current skill formula interpreter, target mitigation/RNG/packet fill, critical/procs and current executable are outside this probe.'],established=['Player body endpoints are floor(max(1,str+dex-20)/4) and /3, then FP256 shift. NPC body endpoints use3*level before division. Low body stats can produce zero endpoints in this source.','Boot contribution uses both flat kick additions plus real boot min/max, stat coefficients, nonweapon ED clamped to minimum-90 and separate maxdamage-percent addition. Without boots only the initial flat kick addition remains.','Primary weapon damage list is restored after the boot helper; secondary damage list remains disabled. Nondamage weapon stat contributions are preserved by the declared DamageRelated service context.'],limits=['Flat kick stat is literally added twice when a valid boot record exists in this old source; current D2R behavior remains uncorroborated and must be flagged.','These are complete source getters/boot arithmetic under explicit services, not full sub_6FCF7CE0, FillDamageValues, modern kick damage/hand/property integration or a native handler certificate.','No guaranteed-hit/block/charge-consumption/timing/payment/input parity. Final effects/icons remain separate.'])
 out=ROOT/'tools/reference/dragon-kick-damage-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Dragon kick damage evidence stale'
 else:out.write_text(encoded)
 print('Dragon kick damage:24576 complete body-getter/boot-helper cases and15 pinned boot bases pass; full damage/native integration pending.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
