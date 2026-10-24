#!/usr/bin/env python3
"""Reuse the complete missile router for the pinned Poison Javelin/cloud rows."""
from pathlib import Path
import argparse,hashlib,json,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_immolation_routing_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
shared=json.loads((ROOT/'tools/reference/immolation-routing-engine-evidence.json').read_text());base=(STAGE/'immolation-routing-source-probe.cpp').read_text().split('int main()',1)[0]
pin=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';manifest=json.loads((pin.parent.parent/'sources.json').read_text());sha=hashlib.sha256(pin.read_bytes()).hexdigest();assert sha==manifest['files']['json/missiles.json']['sha256'];rows=json.loads(pin.read_text());parent=rows['poisonjav'];cloud=rows['poisonjavcloud']
assert (parent['*ID'],parent['CollideKill'],parent['ToHit'],parent['LastCollide'],parent['Pierce'])==(38,1,1,1,1)
assert cloud['*ID']==39 and cloud['LastCollide']==1 and not any(cloud.get(k,0) for k in ('CollideKill','ToHit','Pierce'))
for row in (parent,cloud):assert not any(row.get(k,0) for k in ('pSrvHitFunc','pSrvDmgFunc','AlwaysExplode','NextHit','Explosion'))
base+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("{\"routes\":[");
for(int field:{0,1})for(int victim:{0,1})for(int a4:{0,1})for(int success:{0,1})for(int pierce:{0,100})for(int budget:{0,1,4})for(int repeat:{0,1}){
 parent={};owner={};target={};parent.pGame=&game;parent.pMissileData=&parent.md;target.dwUnitType=1;row={};parent.dwClassId=field?39:38;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE]|(field?0:gdwBitMasks[MISSILESFLAGINDEX_PIERCE]);
 row.nCollideKill=field?0:1;row.nToHit=field?0:1;row.nAlwaysExplode=0;row.wSrvHitFunc=row.wSrvDmgFunc=0;
 owner.stats[STAT_SKILL_PIERCE]=pierce;parent.stats[STAT_PIERCE_IDX]=budget;hasRecord=hasSkill=hasOwner=true;hit=success;arCalls=fillCalls=flagCalls=resets=0;
 if(repeat){parent.md.nOwnerType=1;parent.md.dwOwnerGUID=10;}
 int result=MISSMODE_SrvDmgHitHandler(&game,&parent,victim?&target:nullptr,a4);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",field,victim,a4,success,pierce,budget,repeat,result,arCalls,fillCalls,flagCalls,parent.stats[STAT_PIERCE_IDX],parent.md.dwOwnerGUID,resets);
}
std::printf("],\"contacts\":[");first=true;
for(int field:{0,1})for(int success:{0,1}){
 parent={};owner={};target={};parent.pGame=&game;parent.pMissileData=&parent.md;target.dwUnitType=1;row={};parent.dwClassId=field?39:38;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_LASTCOLLIDE]|(field?0:gdwBitMasks[MISSILESFLAGINDEX_PIERCE]);row.nCollideKill=field?0:1;row.nToHit=field?0:1;row.nAlwaysExplode=0;row.wSrvHitFunc=row.wSrvDmgFunc=0;owner.stats[STAT_SKILL_PIERCE]=100;parent.stats[STAT_PIERCE_IDX]=4;hasRecord=hasSkill=hasOwner=true;hit=success;arCalls=fillCalls=flagCalls=0;
 for(int id:{10,10,20,10}){target.dwUnitId=id;int before=fillCalls,arBefore=arCalls;int result=MISSMODE_SrvDmgHitHandler(&game,&parent,&target,0);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",field,success,id,result,fillCalls-before,arCalls-arBefore,parent.md.dwOwnerGUID);}
}std::printf("]}\n");}
'''
path=STAGE/'poison-javelin-routing-source-probe.cpp';path.write_text(base);binary=STAGE/'poison-javelin-routing-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data['routes'])==192 and len(data['contacts'])==16
for field,victim,a4,success,pierce,budget,repeat,result,ar,fill,flags,newBudget,last,resets in data['routes']:
 rejected=bool(victim and repeat);miss=bool(victim and not field and not success and not rejected);direct=bool(victim and not rejected and not miss)
 assert ar==int(bool(not field and victim and not rejected)) and fill==flags==int(direct)
 assert last==(10 if victim or repeat else -1)
 assert newBudget==budget-int(bool(not field and victim and not rejected and pierce and budget))
 expected=1 if rejected else 2 if miss else (2 if a4 else 1) if field else 1 if victim and pierce and budget else 2
 assert result==expected,(field,victim,a4,success,pierce,budget,repeat,result,expected)
for i,(field,success,id,result,fill,ar,last) in enumerate(data['contacts']):
 repeat=i%4==1;assert last==id and fill==int(not repeat and (field or success)) and ar==int(not repeat and not field)
 assert result==(2 if not field and not success and not repeat else 1)
fixture=STAGE/'poison-javelin-routing-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',talentId='amazon.poisonjavelin',handlerEnabled=False,legacyCommit=shared['legacyCommit'],sourceHashes=shared['sourceHashes'],compiledRoutines=shared['compiledRoutines'],blockHashes=shared['blockHashes'],pinnedMissileHash=sha,pinnedRows={'poisonjav':parent,'poisonjavcloud':cloud},probe=dict(routeCases=192,contactCases=16,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),sharedImpactEvidence='tools/reference/weapon-missile-impact-evidence.json',established=['Pinned parent38 and child39 both use the generic complete FillDamageParams/SetDamageFlags route with no skill hit/damage callback. Parent applies AR and current-owner Pierce/saved budget. Child has no AR/Pierce or CollideKill and remains after contact until removal/expiry.','Both rows have LastCollide: the most recent unit type/ID is recorded before AR. Consecutive contact with the same victim is ignored, including after a parent miss. A different victim replaces that record, allowing a later return to the original victim if the missile survives; this is not a permanent per-cloud hit set.','The already compiled shared full impact reader rolls captured poison endpoints and divides aggregate poison length by count only when count>1. The separately compiled source0 cloud has no item poison count; prepared item contributions remain only in the parent packet.'],limits=['Full router and LastCollide getters/setters execute unchanged. FillDamageParams/SetDamageFlags, AR, hostility, stats, rooms and events remain declared services here; their separate component fixtures do not establish complete combined mitigation/defense/proc/order or modern executable equivalence.','Sequential calls deliberately hold the missile alive to isolate LastCollide identity. A parent AR miss returns removal2, so its later hypothetical calls do not prove production survival. Ordinary child repeated contacts can alternate recipients, but exact spatial lookup/selection, path flag cadence/factory/lifetime and current event timing still require native integration.','No Poison Javelin handler or new visual/icon is enabled.'])
out=ROOT/'tools/reference/poison-javelin-routing-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Poison Javelin routing evidence stale'
else:out.write_text(encoded)
print('Poison Javelin:192 complete hit-router and16 sequential LastCollide cases compiled; shared poison impact reader reused.')
