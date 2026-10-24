#!/usr/bin/env python3
"""Compile complete Poison Javelin/cloud creator, skill endpoints and stat writer."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_bow_launch_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(i['sha'] for i in tree['tree'] if i['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();text=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
 body=text[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
common='source/D2Common/src/D2Skills.cpp';miss='source/D2Common/src/Units/Missile.cpp'
names=['SKILLS_CalculateDamageBonusByLevel','SKILLS_CalculateMasteryBonus','SKILLS_GetMinElemDamage','SKILLS_GetMaxElemDamage','SKILLS_GetElementalLength'];bodies={name:routine(common,name) for name in names};setter=routine(miss,'MISSILE_SetDamageStats')
base=(STAGE/'bow-launch-source-probe.cpp').read_text().split('int main()',1)[0]
base=base.replace('#define __fastcall','#define D2_MAYBE_UNUSED(x) (void)(x)\n#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))\nusing BOOL=int;\n#define __fastcall')
base=base.replace('int nSkill=6;','int nSkill=6,nLevel=1,fFlags=0;')
base=base.replace('int nSrcDam=128,nEType=0;','int nSrcDam=128,nEType=0,dwEMin=32,dwEMax=48,dwEMinLev[5]={16,32,48,64,96},dwEMaxLev[5]={16,36,52,68,100},nToHitShift=0,dwEDmgSymPerCalc=1,dwELen=200,dwELevLen[3]={50,50,50},dwELenSymPerCalc=-1;')
base=base.replace('bool critical=false;int skillMin=0,skillMax=0;','bool critical=false;int skillMin=0,skillMax=0,hard=0,critCalls=0;')
base=base.replace('bool MISSILE_HasBonusStats(D2UnitStrc*,D2UnitStrc*){return critical;}','bool MISSILE_HasBonusStats(D2UnitStrc*,D2UnitStrc*){critCalls++;return critical;}')
base=base.replace('bool ITEMS_CheckIfThrowable(D2UnitStrc*){return false;}','bool ITEMS_CheckIfThrowable(D2UnitStrc*){return true;}')
for name,params in [('SKILLS_GetMinElemDamage','D2UnitStrc*,int,int,int'),('SKILLS_GetMaxElemDamage','D2UnitStrc*,int,int,int'),('SKILLS_GetElementalLength','D2UnitStrc*,int,int,int')]:
 old='int '+name+'('+params+'){return 0;}';assert old in base;base=base.replace(old,'int '+name+'('+params+');')
extra=set(re.findall(r'\bSTAT_\w+',setter+'\n'+'\n'.join(bodies.values())))
m=re.search(r'enum Stats\{([^}]+)\}',base);old=m.group(1).split(',');base=base[:m.start(1)]+','.join(old+sorted(extra-set(old)))+base[m.end(1):]
base+='\nstruct D2StatStrc{int nValue=0,nLayer=0;};\nvoid STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int){p->stats[stat]=value;}\nint STATLIST_CopyStats(D2UnitStrc*,int,D2StatStrc*,int){return 0;}\nint SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return hard*12;}\nint DATATBLS_ApplyRatio(int value,int percent,int divisor){return (int)((int64_t)value*percent/divisor);}\n'
base+='\n'.join(bodies.values())+'\n'+setter+'\n'
fields=json.loads((STAGE/'bow-launch-source-probe.json').read_text())['fields']
cpp=base+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("[");
for(int cloud:{0,1})for(int rank:{1,2,8,16,20,28,40,255})for(int points:{0,1,20})for(int mastery:{0,25,40})for(int item:{0,257})for(int length:{0,101})for(int count:{0,1,3})for(int crit:{0,1}){
 owner={};weapon={};missile={};skill={};row={};missileData={};weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=cloud?39:38;missileData.nLevel=rank;row.nSkill=15;row.nSrcDamage=cloud?uint8_t(-1):0;row.nSrcMissDmg=128;skill.nEType=ELEMTYPE_POIS;hard=points;critical=crit;critCalls=0;skillMin=skillMax=0;
 owner.stats[STAT_MINDAMAGE]=9;owner.stats[STAT_MAXDAMAGE]=14;owner.stats[STAT_ITEM_THROW_MINDAMAGE]=5;owner.stats[STAT_ITEM_THROW_MAXDAMAGE]=10;owner.stats[STAT_DAMAGEPERCENT]=37;owner.stats[STAT_DEXTERITY]=42;owner.stats[STAT_ITEM_MINDAMAGE_PERCENT]=5;owner.stats[STAT_ITEM_MAXDAMAGE_PERCENT]=15;owner.stats[STAT_ITEM_DEMONDAMAGE_PERCENT]=11;owner.stats[STAT_ITEM_UNDEADDAMAGE_PERCENT]=13;
 owner.stats[STAT_POISONMINDAM]=item;owner.stats[STAT_POISONMAXDAM]=item+19;owner.stats[STAT_POISONLENGTH]=length;owner.stats[STAT_POISON_COUNT]=count;owner.stats[STAT_PASSIVE_POIS_MASTERY]=mastery;owner.stats[STAT_FIREMINDAM]=10;owner.stats[STAT_FIREMAXDAM]=31;owner.stats[STAT_LIFEDRAINMINDAM]=7;
 D2UnitStrc origin;for(int& v:origin.stats)v=99999;D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&d,rank);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,[",cloud,rank,points,mastery,item,length,count,crit);
'''
for i,name in enumerate(fields):cpp+=f'std::printf("{"," if i else ""}%d",d.{name});\n'
cpp+='std::printf("],%d,%d,%d,%d,%d]",missile.stats[STAT_POISONMINDAM],missile.stats[STAT_POISONLENGTH],missile.stats[STAT_POISON_COUNT],missile.stats[STAT_PASSIVE_POIS_MASTERY],critCalls);}\nstd::printf("]\\n");}\n'
path=STAGE/'poison-javelin-damage-source-probe.cpp';path.write_text(cpp);binary=STAGE/'poison-javelin-damage-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==3456
rawPath=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';manifest=json.loads((rawPath.parent.parent/'sources.json').read_text());assert hashlib.sha256(rawPath.read_bytes()).hexdigest()==manifest['files']['json/skills.json']['sha256'];raw=json.loads(rawPath.read_text())['15']
def tier(maximum,rank):
 value=raw['EMax' if maximum else 'EMin'];left=rank-1
 for i,n in enumerate([7,8,6,6,2**30],1):step=min(left,n);value+=step*raw[('EMaxLev' if maximum else 'EMinLev')+str(i)];left-=step
 return value
for cloud,rank,hard,mastery,item,length,count,crit,values,storedRate,storedLen,storedCount,storedMastery,critCalls in data:
 d=dict(zip(fields,values));ends=[]
 for maximum in [False,True]:v=tier(maximum,rank);v+=v*hard*12//100;v+=v*mastery//100;ends.append(v)
 assert(d['nPoisonMinDamage'],d['nPoisonMaxDamage'],d['nPoisonLength'],d['nPoisonCount'])==(ends[0]+(0 if cloud else item),ends[1]+(0 if cloud else item+19),200+(rank-1)*50+(0 if cloud else length),0 if cloud else count)
 assert(d['nMinDamage'],d['nMaxDamage'],d['nFireMinDamage'],d['nLifeDrainMinDamage'])==((0,0,0,0) if cloud else(5*256,10*256,10*256,7))
 assert d['nDamagePercent']==(0 if cloud else 94) and bool(d['nFlags']&2)==bool(not cloud and crit) and critCalls==int(not cloud)
 assert(storedRate,storedLen,storedCount,storedMastery)==(d['nPoisonMinDamage'],d['nPoisonLength'],d['nPoisonCount'],0)
fixture=STAGE/'poison-javelin-damage-source-probe.json';fixture.write_text(json.dumps(dict(fields=fields,cases=data),separators=(',',':'))+'\n')
parent=json.loads((ROOT/'tools/reference/bow-launch-engine-evidence.json').read_text());e=dict(referenceVersion='3.3.93847',talentId='amazon.poisonjavelin',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,pinnedInputHashes={'json/skills.json':hashlib.sha256(rawPath.read_bytes()).hexdigest()},compiledRoutines=list(blocks)+parent['compiledRoutines'],blockHashes=blocks,creatorBlockHashes=parent['blockHashes'],probe=dict(cases=len(data),path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete explicit-Skill15 creator distinguishes parent38 source128 from child39 unsigned SrcDamage255/-1. Child clears both skill/missile weapon sources and does not read the supplied origin damage, owner weapon/item poison, item poison length/count, physical/fire/leech, species or critical. It separately evaluates retained rank and live declared hard synergy/mastery through full skill getters.','Full skill minimum/maximum getters apply hard Plague12percent synergy after HitShift0, then poison mastery. Full length getter gives200+50*(rank-1), independent from skill poison damage synergy. Rank1 unenhanced rate32..48FP256 and200frames; rank28 length1550frames/62seconds; rank255 length12900frames/516seconds.','Full parent creator selects actual throwable stat endpoints5..10 instead of distinct ordinary9..14 endpoints. It adds prepared owner item poison rate and aggregate raw length/count to skill poison without averaging here; the complete setter publishes the resulting packet without copying owner poison mastery into the missile.'],limits=['Formula hard-point lookup, owner/weapon/item metadata and prepared stat projection, ratio helper and empty monster-type stat copy are declared services. Full unchanged skill getters, tier helper, damage creator/add/scale and setter execute; current item stat preparation/mastery RNG and modern executable equivalence remain separate.','Impact getter/poison count division, mitigation/length resistance, reapplication/owner/FP256 stat regeneration and cloud collision/path cadence still require independent source/native acceptance. No handler is enabled.'])
out=ROOT/'tools/reference/poison-javelin-damage-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Poison Javelin damage evidence stale'
else:out.write_text(encoded)
print('Poison Javelin:3456 complete creator/skill poison endpoints/length/mastery/stat-writer cases; cloud weapon source excluded.')
