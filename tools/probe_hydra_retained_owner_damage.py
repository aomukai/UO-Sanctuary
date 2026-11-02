#!/usr/bin/env python3
"""Reuse complete sentry registry/flags/HP composition for Hydra fire packets."""
import argparse,hashlib,json,shutil,subprocess,sys
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/hydra-retained-owner-damage-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True)
 subprocess.run([sys.executable,str(ROOT/'tools/probe_sentry_dead_damage_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 parent=ROOT/'tools/reference/sentry-dead-damage-source-evidence.json';prior=json.loads(parent.read_text());ownerpath=ROOT/prior['ownerEvidence'];owners=json.loads(ownerpath.read_text());tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']
 original=ROOT/'.local/class-skills/sentry-dead-damage-source-probe.cpp';cpp=original.read_text();header,main=cpp.split('int main()',1)
 # Keep the complete registry/owner/allegiance,damage structs/tables and
 # canonical functions unchanged. Only the declared fixture packet changes.
 main=main.replace('actor.dwAnimMode=am;','actor.dwAnimMode=am;actor.dwClassId=351;')
 for a,b in [('STAT_LIGHTRESIST','STAT_FIRERESIST'),('STAT_PASSIVE_LTNG_PIERCE','STAT_PASSIVE_FIRE_PIERCE'),('dwLtngDamage','dwFireDamage')]:assert a in main;main=main.replace(a,b)
 cpp=header+'int main()'+main
 sources={**owners['sourceHashes'],**prior['sourceHashes']};routines={**owners['compiledRoutineHashes'],**prior['compiledRoutineHashes']};found={}
 for rel,digest in sources.items():
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert sha(p)==digest and hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel)
  for name,h in routines.items():
   try:b=function(raw.decode(),name)
   except AssertionError:continue
   assert hashlib.sha256(b.encode()).hexdigest()==h and function(cpp,name)==b;found[name]=h
 assert found==routines
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==23040
 for am,pm,store,present,align,dead,block,resist,pierce,raw,suppress,hp,fire,total,result,callbacks,kills,last in cases:
  gate=store and present and not align and not dead and not block;effective=max(-100,min(100,resist-pierce if resist<100 else resist));damage=raw*(100-effective)//100 if gate else 0;remaining=25600-damage
  if remaining<256:remaining=0
  assert hp==remaining and total==damage and fire==(damage if gate else raw) and callbacks==(1+2*int(remaining==0 and not suppress) if gate else 0) and kills==int(gate and remaining==0) and last==int(gate)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 pins={};manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text());tables={}
 for name in ('missiles','monstats'):
  q=ROOT/f'tools/reference/d2r-named-equipment/json/{name}.json';assert sha(q)==manifest['files'][f'json/{name}.json']['sha256'];pins[str(q.relative_to(ROOT))]=sha(q);tables[name]=json.loads(q.read_text())
 row=tables['missiles']['hydra'];assert row['GetHit']==1 and not any(row.get(k) for k in ('SoftHit','KnockBack','pSrvDmgFunc'))
 for n in ('hydra1','hydra2','hydra3'):assert tables['monstats'][n].get('Crit',0)==0
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],cases=len(cases),sourceHashes=sources,routineHashes=routines,currentTable=pins,sanitizers='ASan/UBSan clean',
  established=['Complete typed registry/creator/minion/allegiance chain,damage flags,mitigation/total-damage,ExecuteEvents HP and ExecuteMissileDamage mode dispatch from the sentry composition accept a Hydra fire packet unchanged. Declared registered head modes1/0/12and master modes1/0/17produce the same health loss while inherited stats remain available.',
  'Removing creator storage/registry suppresses damage. A dead or aligned target and declared block also suppress it. Captured fire pierce applies below100resistance without breaking immunity. Raw endpoints are already rolled/mastered;no second caster mastery or player attacking identity is substituted.'],
  declaredServices=['Older reconstructed source with current pinned missile fields,not modern executable proof. Only fixture element/head-class inputs change;canonical registry,allegiance,damage and event bodies remain unchanged. Empty event subscribers,critical0,Normal non-town nonboss/nonhireling recipient,stat storage,block and mode/kill/presentation remain declared services.',
  'Registry/stat retention is supplied. Full Hydra death entry/corpse/GUID removal duration and contemporary firing/owner lifetime are NOT established. This evidence does not authorize an immortal source or keeping a deleted owner alive. Native source/owner credit,typed return,world contacts/persistence/client acceptance remain separate. The probe changes no runtime policy.'],
  artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),parent,ownerpath,ROOT/'tools/probe_sentry_dead_damage_reference.py',original,p,binary,san,log,fixture)})
 target=ROOT/'tools/reference/hydra-retained-owner-damage-source-evidence.json';render=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==render
 else:target.write_text(render)
 print('Hydra retained owner fire:23040complete registry/flags/mitigation/events/HP/mode contexts;registered dead heads retain damage,missing registry suppresses it;ASan/UBSan clean;actual GUID lifetime remains open.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');probe(p.parse_args().check)
