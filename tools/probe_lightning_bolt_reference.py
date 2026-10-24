#!/usr/bin/env python3
"""Execute complete Lightning Bolt damage reset, creator, stable path and TH mode bodies."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args();tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
def source(rel):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(rel,name):
 text=source(rel);m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
 body=text[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
mode='source/D2Game/src/MISSILES/MissMode.cpp';get=routine(mode,'MISSMODE_GetDamageValue');reset=routine(mode,'MISSMODE_ResetDamageParams');hit=routine(mode,'MISSMODE_SrvDmg12_LightningJavelin')
base=(STAGE/'magic-arrow-source-probe.cpp').read_text().split('int main()',1)[0]
base=base.replace('return ELEMTYPE_MAGIC;}','return ELEMTYPE_LTNG;}')
m=re.search(r'struct D2DamageStrc\{([^}]+)\};',base);assert m;existing=set(re.findall(r'int (\w+);',m[1]));extra=set(re.findall(r'pDamage->(\w+)',get+reset+hit))-existing;base=base[:m.end(1)]+''.join('int '+x+';' for x in sorted(extra))+base[m.end(1):]
old='int ITEMS_RollLimitedRandomNumber(int* seed,int limit){return limit<=0?0:std::clamp(*seed,0,limit-1);}';assert old in base;base=base.replace(old,'int rngMode=0,drawCalls=0;int ITEMS_RollLimitedRandomNumber(int*,int limit){int index=drawCalls++;return limit<=0?0:rngMode && index%2?limit-1:0;}')
cpp=base+get+'\n'+reset+'\n'+hit+r'''
int main(){bool first=true;std::printf("[");
for(int raw:{0,257,1024,1048577})for(int bonus:{-90,0,37,100})for(int critical:{0,1})for(int pct:{-1,0,50,100,150})for(int mastery:{0,25})for(int kind:{0,1,2,3})for(int draws:{0,1}){
 missile={};target={};target.dwUnitType=UNIT_MONSTER;target.kind=kind;conversion=pct;rngMode=draws;drawCalls=0;
 missile.stats[STAT_MINDAMAGE]=raw;missile.stats[STAT_MAXDAMAGE]=raw?raw+500:0;missile.stats[STAT_DAMAGEPERCENT]=bonus;missile.stats[STAT_ITEM_DEMONDAMAGE_PERCENT]=31;missile.stats[STAT_ITEM_UNDEADDAMAGE_PERCENT]=47;missile.stats[STAT_ITEM_DEADLYSTRIKE]=critical;
 missile.stats[STAT_FIREMINDAM]=257;missile.stats[STAT_FIREMAXDAM]=513;missile.stats[STAT_MAGICMINDAM]=1281;missile.stats[STAT_MAGICMAXDAM]=1537;missile.stats[STAT_LIGHTMINDAM]=769;missile.stats[STAT_LIGHTMAXDAM]=1025;missile.stats[STAT_COLDMINDAM]=1793;missile.stats[STAT_COLDMAXDAM]=2049;missile.stats[STAT_POISONMINDAM]=257;missile.stats[STAT_POISONMAXDAM]=513;
 missile.stats[STAT_PASSIVE_LTNG_MASTERY]=mastery;missile.stats[STAT_COLDLENGTH]=13;missile.stats[STAT_POISONLENGTH]=101;missile.stats[STAT_POISON_COUNT]=3;missile.stats[STAT_LIFEDRAINMINDAM]=7;missile.stats[STAT_MANADRAINMINDAM]=9;
 D2DamageStrc d={};MISSMODE_FillDamageParams(&missile,kind?&target:nullptr,&d);int initial=d.dwLtngDamage;MISSMODE_SrvDmg12_LightningJavelin(&game,&missile,kind?&target:nullptr,&d);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",raw,bonus,critical,pct,mastery,kind,draws,d.dwPhysDamage,d.dwFireDamage,d.dwMagDamage,d.dwLtngDamage,d.dwColdDamage,d.dwPoisDamage,d.dwPoisLen,d.dwColdLen,d.dwLifeLeech,d.dwManaLeech,drawCalls);
}std::printf("]\n");}
'''
def run(name,cpp):
 path=STAGE/(name+'.cpp');path.write_text(cpp);binary=STAGE/name;subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);return json.loads(subprocess.check_output([str(binary)]))
impact=run('lightning-bolt-impact-source-probe',cpp);assert len(impact)==2560
base=(STAGE/'poison-javelin-damage-source-probe.cpp').read_text().split('int main()',1)[0]
fields=json.loads((STAGE/'poison-javelin-damage-source-probe.json').read_text())['fields']
base=base.replace('dwEMin=32,dwEMax=48,dwEMinLev[5]={16,32,48,64,96},dwEMaxLev[5]={16,36,52,68,100},nToHitShift=0','dwEMin=1,dwEMax=40,dwEMinLev[5]={0,0,0,0,0},dwEMaxLev[5]={12,18,28,48,88},nToHitShift=8')
cpp=base+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("[");
for(int rank:{1,2,8,16,20,28,40,255})for(int points:{0,1,20})for(int mastery:{0,25,40})for(int item:{0,10})for(int crit:{0,1}){
 owner={};weapon={};missile={};skill={};row={};missileData={};weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=205;missileData.nLevel=rank;row.nSkill=20;row.nSrcDamage=0;skill.nSrcDam=96;skill.nEType=ELEMTYPE_LTNG;skill.dwELen=0;skill.dwELevLen[0]=skill.dwELevLen[1]=skill.dwELevLen[2]=0;hard=points;critical=crit;skillMin=skillMax=0;
 owner.stats[STAT_ITEM_THROW_MINDAMAGE]=5;owner.stats[STAT_ITEM_THROW_MAXDAMAGE]=10;owner.stats[STAT_MINDAMAGE]=9;owner.stats[STAT_MAXDAMAGE]=14;owner.stats[STAT_DAMAGEPERCENT]=37;owner.stats[STAT_DEXTERITY]=42;owner.stats[STAT_ITEM_MINDAMAGE_PERCENT]=5;owner.stats[STAT_ITEM_MAXDAMAGE_PERCENT]=15;owner.stats[STAT_ITEM_DEMONDAMAGE_PERCENT]=11;owner.stats[STAT_ITEM_UNDEADDAMAGE_PERCENT]=13;
 owner.stats[STAT_LIGHTMINDAM]=item;owner.stats[STAT_LIGHTMAXDAM]=item+19;owner.stats[STAT_PASSIVE_LTNG_MASTERY]=mastery;owner.stats[STAT_FIREMINDAM]=10;owner.stats[STAT_FIREMAXDAM]=31;owner.stats[STAT_LIFEDRAINMINDAM]=7;
 D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,nullptr,&missile,rank);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,[",rank,points,mastery,item,crit);
'''
for i,name in enumerate(fields):cpp+=f'std::printf("{"," if i else ""}%d",d.{name});\n'
cpp+='std::printf("]]");}std::printf("]\\n");}\n';creator=run('lightning-bolt-creator-source-probe',cpp);assert len(creator)==288
base=(STAGE/'poison-path-source-probe.cpp').read_text().split('int main()',1)[0]
cpp=base+r'''int main(){D2ActiveRoomStrc room;D2UnitStrc missile;D2DynamicPathStrc path;bool first=true;std::printf("[");
for(int fx:{0,8192,32768,57344})for(int fy:{0,32768})for(int vx:{-92160,-65160,-4096,0,4096,65160,92160})for(int vy:{-92160,-65160,-4096,0,4096,65160,92160}){
 path={};path.pUnit=&missile;missile.pDynamicPath=&path;path.pRoom=&room;path.dwFlags=PATH_MISSILE_MASK|PATH_UNKNOWN_FLAG_0x00020|PATH_SAVE_STEPS_MASK;path.tGameCoords.dwPrecisionX=(100<<16)+fx;path.tGameCoords.dwPrecisionY=(100<<16)+fy;path.tVelocityVector={vx,vy};wall=0;D2Common_10226(&missile,1024);int count=(vx||vy)?path.nSavedStepsCount:0;
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,[",fx,fy,vx,vy);for(int i=0;i<count;i++)std::printf("%s[%d,%d]",i?",":"",path.SavedSteps[i].X,path.SavedSteps[i].Y);std::printf("]]");}std::printf("]\n");}
''';steps=run('lightning-bolt-step-source-probe',cpp);assert len(steps)==392
units='source/D2Common/src/Units/Units.cpp';unitText=source(units);table=re.search(r'static D2AnimUnkStrc gaPlayerModesAnimModulators\[NUMBER_OF_PLRMODES\] = \{.*?^\};',unitText,re.M|re.S).group();modeBody=routine(units,'UNITS_CanAnimModeUseAttackRate')
modeCpp=r"""#include <cstdio>
#include <cassert>
#include <initializer_list>
#define D2_ASSERT(x) assert(x)
using BOOL=int;constexpr int TRUE=1,FALSE=0,UNIT_PLAYER=0,UNIT_MONSTER=1,MONSTER_FIRST_EXPANSION=410,SKILLSFLAG_USEATTACKRATE=1,NUMBER_OF_PLRMODES=20;
struct D2AnimUnkStrc{int a,b,bCanUseSkillAttackRate,bCanUseAttackRate,c;};
struct D2SkillsTxt{int nSkillId=20,dwFlags[2]={};} skill;
struct D2SkillStrc{D2SkillsTxt* pSkillsTxt=&skill;} used;
struct D2UnitStrc{} unit;bool hasUsed=true;
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return hasUsed?&used:nullptr;}D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){return &skill;}
D2AnimUnkStrc gaMonModesAnimModulators[20],gaMonModesAnimModulatorsExpansion[20];
"""+table+'\n'+modeBody+r"""int main(){bool first=true;std::printf("[");for(int mode:{10,11,13})for(int flag:{0,1})for(int present:{0,1}){hasUsed=present;skill.dwFlags[0]=flag;int result=UNITS_CanAnimModeUseAttackRate(0,mode,&unit,0);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d]",mode,flag,present,result);}std::printf("]\n");}"""
timing=run('lightning-bolt-mode-source-probe',modeCpp);assert len(timing)==12
for mode,flag,present,result in timing:assert result==int(mode==11 or mode==13 and flag and present)
pin=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((pin/'sources.json').read_text());pins={};data={}
for name in ['skills','missiles']:
 raw=(pin/'json'/(name+'.json')).read_bytes();pins[name]=hashlib.sha256(raw).hexdigest();assert pins[name]==manifest['files']['json/'+name+'.json']['sha256'];data[name]=json.loads(raw)
skill=data['skills']['20'];missile=data['missiles']['lightningjavelin'];assert(skill['SrcDam'],missile['pSrvDmgFunc'],missile['dParam1'],missile['Size'],missile['Vel'])==(96,12,100,2,30);assert not missile.get('ToHit') and not skill.get('UseAttackRate') and not skill.get('localdelay')
fixture=STAGE/'lightning-bolt-source-probe.json';fixture.write_text(json.dumps(dict(fields=fields,impact=impact,creator=creator,steps=steps,timing=timing),separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,pinnedSkill=skill,pinnedMissile=missile,pinnedHashes=pins,sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes=blocks,sharedEvidence=['tools/reference/magic-arrow-engine-evidence.json','tools/reference/poison-javelin-damage-engine-evidence.json','tools/reference/poison-path-engine-evidence.json'],probe=dict(impactCases=2560,creatorCases=288,stepCases=392,timingCases=12,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete impact reader then srvdmg12 rerolls lightning, adds percentage of enhanced/critical physical, resets ALL other damage/status/leech channels and species-bypass flags, then adds lightning only.100percent large-value conversion can lose a raw remainder because the full percentage helper early-divides and the reset discards physical remainder.','Complete creator applies SrcDam96 to the combined skill and owner elemental endpoints, not only weapon damage. Full lightning getters keep its fixed minimum1 unsynergized while four hard synergies each add3percent to maximum; mastery precedes the final96/128 scaling.','Full player-mode rate selector makes TH use attack rate independently of the missing UseAttackRate skill flag. Cast mode does not use that branch; special skill mode consults the flag.','Full stable path step/update produces ordered saved integer-subtile crossings. Current missile size2 uses the already compiled combined footprint finder. Missing ToHit means no AR check; LastCollide, CollideKill/Pierce remain generic router rules.'],limits=['Declared stats/formula/owner/type/RNG/room/collision services and inherited complete source harnesses remain bounded. Current executable/path initialization/global order, native geometry/room order and full item/event/client equivalence remain separate.','Impact draws alternate min/max declared service values to expose the second lightning roll. This is not the original engine seed or complete event pipeline. Native handler, timing/control and input/reload acceptance still need integration.'])
out=ROOT/'tools/reference/lightning-bolt-engine-evidence.json';e['nativeComparisonExclusions']={'creatorCases':8,'reason':'Synthetic rank255 with20 hard points in each of four synergies and mastery25/40 overflows the older signed32-bit final SrcDam96 multiplication. Native shared long arithmetic is retained; exact current executable overflow behavior is unverified. The other280 creator cases compare exactly.'}
encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Lightning Bolt source evidence stale'
else:out.write_text(encoded)
print('Lightning Bolt:2560 complete impact/reset,288 complete creator/getter and392 full stable path-step cases compiled.')
