#!/usr/bin/env python3
"""Compile Plague Javelin's complete impact emitter/factory, route and damage creators."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args();tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();s=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;i=s.index('{',m.start())+1;depth=1
 while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
 body=s[m.start():i];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
mode='source/D2Game/src/MISSILES/MissMode.cpp';emit=routine(mode,'MISSMODE_CreatePoisonCloudHitSubmissiles');hit=routine(mode,'MISSMODE_SrvHit02_PlagueJavelin_PoisonPotion')
def run(name,cpp):
 path=STAGE/(name+'.cpp');path.write_text(cpp);binary=STAGE/name;subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);return json.loads(subprocess.check_output([str(binary)]))
base=(STAGE/'poison-javelin-factory-source-probe.cpp').read_text().split('int main()',1)[0].replace('MISSILES_CreateMissileFromParams(','FullFactory(')
base=base.replace('int dwParam[2]={5,15};','int dwParam[2]={2,4};int wHitSubMissile[1]={221},dwHitPar[3]={1,2,3};')
base+='''\nstruct Emitted{D2MissileStrc params;int life,velocity,x,y;};std::vector<Emitted> emissions;
int MISSILE_GetSkill(D2UnitStrc* u){return u->skill;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc* g,D2MissileStrc* p){auto* u=FullFactory(g,p);emissions.push_back({*p,u?u->total:-1,u?u->path.velocity:-1,u?u->x:-1,u?u->y:-1});return u;}
'''+emit+'\n'+hit
cpp=base+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("[");
for(int rank:{1,2,20,28,255})for(int slowed:{0,1})for(int victim:{0,1})for(int loops:{0,3}){
 reset();row={};row.nVel=row.nMaxVel=0;row.wRange=60;row.nSubLoop=1;row.nSubStart=10;row.nSubStop=19;row.nCollision=1;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_CANSLOW];row.dwParam[0]=2;row.dwParam[1]=4;row.wHitSubMissile[0]=221;row.dwHitPar[0]=1;row.dwHitPar[1]=2;row.dwHitPar[2]=loops;slow=slowed;other.owner=&owner;other.rank=rank;other.skill=25;other.dwClassId=43;other.x=41;other.y=71;target.x=45;target.y=73;emissions.clear();int result=MISSMODE_SrvHit02_PlagueJavelin_PoisonPotion(&game,&other,victim?&target:nullptr);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,[",rank,slowed,victim,loops,result);bool firstEmitted=true;for(auto& e:emissions){auto& p=e.params;if(!firstEmitted)std::printf(",");firstEmitted=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",p.dwFlags,p.nX,p.nY,p.nTargetX,p.nTargetY,p.nVelocity,p.nLoops,p.nSkill,p.nSkillLevel,e.life,e.velocity,e.params.nMissile);}std::printf("]]");}std::printf("]\n");}
''';emissions=run('plague-javelin-emission-source-probe',cpp);assert len(emissions)==40
for rank,slow,victim,loops,result,calls in emissions:
 assert result==3 and len(calls)==23
 for i,(flags,x,y,dx,dy,velocity,nloops,skill,level,life,speed,missile) in enumerate(calls):
  assert(flags,x,y,velocity,nloops,skill,level,life,speed,missile)==(31 if loops else 23,45 if victim else 41,73 if victim else 71,256 if i<8 else 512,loops,25,rank,60+9*loops,(96 if i<8 else 192) if slow else (192 if i<8 else 384),221)
base=(STAGE/'poison-javelin-damage-source-probe.cpp').read_text().split('int main()',1)[0]
base=base.replace('dwEMin=32,dwEMax=48,dwEMinLev[5]={16,32,48,64,96},dwEMaxLev[5]={16,36,52,68,100},nToHitShift=0','dwEMin=12,dwEMax=18,dwEMinLev[5]={8,16,26,55,80},dwEMaxLev[5]={8,16,26,55,80},nToHitShift=3').replace('dwELen=200,dwELevLen[3]={50,50,50}','dwELen=75,dwELevLen[3]={5,5,5}').replace('return hard*12;','return hard*14;')
fields=json.loads((STAGE/'poison-javelin-damage-source-probe.json').read_text())['fields']
old=(STAGE/'poison-javelin-damage-source-probe.cpp').read_text().split('int main()',1)[1]
old=old.replace('cloud?39:38','cloud?221:43').replace('row.nSkill=15','row.nSkill=25')
damage=run('plague-javelin-damage-source-probe',base+'int main()'+old);assert len(damage)==3456
base=(STAGE/'immolation-routing-source-probe.cpp').read_text().split('int main()',1)[0]
base=base.replace('dwHitPar[2]','dwHitPar[3]').replace('int dwHitPar[3]={},','int dwParam[2]={2,4},dwHitPar[3]={},').replace('int dwFlags=0,nSkill=0,','int nVelocity=0,nLoops=0,nTargetX=0,nTargetY=0;D2UnitStrc* pOrigin=nullptr;int dwFlags=0,nSkill=0,')
old='int32_t MISSMODE_SrvHit02_PlagueJavelin_PoisonPotion(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return 3;}'
assert old in base;base=base.replace(old,emit+'\n'+hit)
cpp=base+r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("[");
for(int field:{0,1})for(int victim:{0,1})for(int a4:{0,1})for(int success:{0,1})for(int pierce:{0,100})for(int budget:{0,1,4})for(int repeat:{0,1}){
 parent={};owner={};target={};parent.pGame=&game;parent.pMissileData=&parent.md;target.dwUnitType=1;row={};row.wHitSubMissile[0]=221;row.dwHitPar[0]=1;row.dwHitPar[1]=2;row.dwHitPar[2]=3;row.dwParam[0]=2;row.dwParam[1]=4;parent.dwClassId=field?221:43;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE]|(field?0:gdwBitMasks[MISSILESFLAGINDEX_PIERCE]);row.nCollideKill=field?0:1;row.nToHit=field?0:1;row.nAlwaysExplode=field?0:1;row.wSrvHitFunc=field?0:2;row.wSrvDmgFunc=0;
 owner.stats[STAT_SKILL_PIERCE]=pierce;parent.stats[STAT_PIERCE_IDX]=budget;hasRecord=hasSkill=hasOwner=true;hit=success;arCalls=fillCalls=flagCalls=resets=0;created.clear();if(repeat){parent.md.nOwnerType=1;parent.md.dwOwnerGUID=10;}
 int result=MISSMODE_SrvDmgHitHandler(&game,&parent,victim?&target:nullptr,a4);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%zu,%d,%d]",field,victim,a4,success,pierce,budget,repeat,result,arCalls,fillCalls,flagCalls,created.size(),parent.stats[STAT_PIERCE_IDX],parent.md.dwOwnerGUID);
}std::printf("]\n");}
''';routes=run('plague-javelin-routing-source-probe',cpp);assert len(routes)==192
for field,victim,a4,success,pierce,budget,repeat,result,ar,fill,flags,clouds,newbudget,last in routes:
 rejected=bool(victim and repeat);direct=bool(victim and not rejected and (field or success));callback=bool(not field and not rejected)
 assert clouds==23*callback and ar==int(bool(not field and victim and not rejected)) and fill==flags==int(direct)
 assert newbudget==budget-int(bool(not field and victim and not rejected and pierce and budget))
basePin=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((basePin/'sources.json').read_text());pins={};data={}
for filename in ['skills','missiles']:
 path=basePin/'json'/(filename+'.json');pins[filename]=hashlib.sha256(path.read_bytes()).hexdigest();assert pins[filename]==manifest['files']['json/'+filename+'.json']['sha256'];data[filename]=json.loads(path.read_text())
skill=data['skills']['25'];rows={k:data['missiles'][k] for k in ['plaguejavelin','plaguejavcloud']};assert skill['usemanaondo']==1 and rows['plaguejavelin']['pSrvDoFunc']==3
fixture=STAGE/'plague-javelin-source-probe.json';fixture.write_text(json.dumps(dict(fields=fields,emissions=emissions,damage=damage,routes=routes),separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',talentId='amazon.plaguejavelin',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes=blocks,pinnedHashes=pins,pinnedSkill=skill,pinnedRows=rows,probe=dict(emissionFactoryCases=40,creatorCases=3456,routerCases=192,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),sharedEvidence=['tools/reference/poison-javelin-factory-engine-evidence.json','tools/reference/poison-javelin-damage-engine-evidence.json','tools/reference/immolation-routing-engine-evidence.json','tools/reference/lightning-bolt-engine-evidence.json'],established=['Parent43 current srvdo3 does not emit a poisonjav path trail. Its full hit02 callback chooses victim position when supplied and missile position otherwise, creating23 clouds in ordered8+15 rings; AlwaysExplode triggers the same callback on AR miss and null contact. Full router distinguishes LastCollide rejection, AR failure removal and successful Pierce.','Cloud221 uses fixed override velocities256/512FP8 before factory75percent =>192/384; source flags31 include3loops, extending60 by3*(19-10) to87frames. Slow50percent controls halve each velocity. Rank does not alter cloud movement/lifetime.','Complete shared creator/getters/stat writer execute3456 parent128/cloud0 cases using HitShift3, hard PoisonJavelin14percent and duration75+5*(rank-1). Independent clouds exclude weapon/item/critical/status/leech.','Plague usemanaondo1 differs from PoisonJavelin/LightningBolt. Full older do wrapper checks affordability again, then emits and consumes mana/quantity after success; do not reuse their paid-start path. Player generic missile launcher supplies no skill nAttBonus, unlike monster path; displayed skill30+9/rank versus exact current AR transfer remains acceptance work.'],limits=['Declared factory room/path/stat/seed/damage and router fill/flag/AR/hostility/event services remain bounded, with separate creator and stable path evidence. This is not combined modern executable/client equivalence.','Source damage matrix is compiled but native Plague comparisons, control/resources/input/persistence and ring motion integration are still pending.','Modern generic do resource snapshots, source grid/room collision ordering/initial path, global cloud/parent phase/catch-up, slow/returnfire, exact stat/RNG/item/owner/client and PvP/hireling behavior remain acceptance gaps.'])
compiled=dict(blocks)
reviewed={name:routine('source/D2Game/src/SKILLS/Skills.cpp',name) for name in ['sub_6FD12950','D2GAME_SKILLS_Handler_6FD12BA0','sub_6FD10CE0','sub_6FD11420']}
e['sourceHashes']=hashes;e['compiledRoutines']=list(compiled);e['blockHashes']=compiled;e['reviewedRoutineHashes']={k:hashlib.sha256(v.encode()).hexdigest() for k,v in reviewed.items()}
e['limits'].append('Whole start/do/mana/launch wrappers are hash-pinned reviewed evidence, not compiled by this probe. Cloud initial saved-path chain and exact first-contact timing use the existing retained-origin-cell native adapter and require current executable corroboration; native ring stepping is not proof of that initialization.')
out=ROOT/'tools/reference/plague-javelin-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Plague Javelin evidence stale'
else:out.write_text(encoded)
print('Plague Javelin:40 complete impact emitter/factory,3456 complete creator and192 full route cases compiled.')
