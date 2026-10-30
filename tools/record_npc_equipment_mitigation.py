#!/usr/bin/env python3
"""Bind NPC item reduction, absorption and legacy coefficient acceptance."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_revive_regeneration import checked_log,markers
from snapshot_skill_build import check as check_snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-equipment-mitigation'
PREDECESSOR='861f110'
sha=inventory.sha

def historical(name):
 return json.loads(subprocess.check_output(['git','show',PREDECESSOR+':'+name],cwd=ROOT))

def record(check=False):
 inventory.STAGE=STAGE;paths=[Path(__file__).resolve(),STAGE/'pre-build-attempt-5.json'];scopes=[]
 for name in ['mitigation-inputs.json','stats-inputs.json','audit-inputs.json','upgrade-inputs.json']:
  p=STAGE/name;scopes.append(inventory.validate_inventory(p));check_snapshot(STAGE/'pre-build-attempt-5.json',p);paths.append(p)
 focused,retained,audited,upgrade=scopes
 assert all(x['artifacts']==focused['artifacts'] and x['copiedData']==focused['copiedData'] for x in scopes)
 prior=historical('tools/reference/npc-equipment-resistances-evidence.json');old=prior['inputInventory'];assert old['copiedData']==focused['copiedData']
 for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
  assert old['artifacts'][name]==focused['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
 old_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};new_cs={p:h for p,h in focused['sourceInputs'].items() if p.endswith('.cs')}
 assert set(new_cs)-set(old_cs)=={'tests/Sanctuary.Integration/NpcEquipmentMitigationChecks.cs'}
 assert {p for p in old_cs if old_cs[p]!=new_cs[p]}=={'src/Sanctuary.Content/NamedElements.cs','src/Sanctuary.Content/GemCombat.cs','src/Sanctuary.Content/NamedPropertyRuntime.cs'}
 scope=checked_log(STAGE/'mitigation-native-checks.log',3)
 assert scope==Counter({'npc-equipment-mitigation-check':1,'npc-equipment-mitigation-reload-check':2})
 complete=checked_log(STAGE/'stats-native-checks.log',389)
 previous=ROOT/'.local/class-skills/npc-equipment-resistances/stats-native-checks.log';assert sha(previous)==prior['artifacts'][str(previous.relative_to(ROOT))]
 assert complete==markers(previous.read_text())+scope
 source=ROOT/'tools/reference/bear-smite-engine-evidence.json';s=json.loads(source.read_text())
 assert s==historical(str(source.relative_to(ROOT))) and s['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 case=ROOT/s['fixture']['path'];assert sha(case)==s['fixture']['sha256'] and s['fixture']['reductionCases']==1248
 assert len(json.loads(case.read_text())['reduction'])==1248
 assert focused['sourceInputs'][str(case.relative_to(ROOT))]==retained['sourceInputs'][str(case.relative_to(ROOT))]==sha(case)
 reduction=ROOT/'tools/reference/damage-rounding-engine-evidence.json';assert json.loads(reduction.read_text())==historical(str(reduction.relative_to(ROOT)))
 assert focused['sourceInputs'][str(reduction.relative_to(ROOT))]==retained['sourceInputs'][str(reduction.relative_to(ROOT))]==sha(reduction)
 writer=ROOT/'tools/reference/item-property-writers-source-evidence.json';assert json.loads(writer.read_text())==historical(str(writer.relative_to(ROOT)))
 paths.extend([source,case,reduction,writer])
 upgraded=checked_log(STAGE/'upgrade-native-checks.log',3)
 assert upgraded==Counter({'npc-equipment-mitigation-legacy-create-check':1,'npc-equipment-mitigation-legacy-upgrade-check':1,'npc-equipment-mitigation-legacy-reload-check':1})
 producer=upgrade['oldProducer'];old_input=ROOT/producer['path'];assert sha(old_input)==producer['sha256']
 previous_producer=json.loads(old_input.read_text());assert previous_producer==producer['inputInventory']
 assert previous_producer['sourceInputs']['src/Sanctuary.Content/NamedPropertyRuntime.cs']==old['sourceInputs']['src/Sanctuary.Content/NamedPropertyRuntime.cs']
 assert previous_producer['artifacts']['Assemblies/Sanctuary.Content.dll']==upgrade['oldArtifacts']['Sanctuary.Content.dll']
 old_content=old_input.parent/'content/Sanctuary.Content.dll';assert sha(old_content)==upgrade['oldArtifacts']['Sanctuary.Content.dll']
 old_snapshot=STAGE/'pre-build-attempt-3.json';frozen=json.loads(old_snapshot.read_text())['inputs']
 assert frozen['sourceInputs']=={n:h for n,h in previous_producer['sourceInputs'].items() if n.endswith('.cs')}
 assert frozen['copiedData']==previous_producer['copiedData']==focused['copiedData']
 assert len(upgrade['boots'])==3
 for i,boot in enumerate(upgrade['boots']):
  expected=dict(focused['artifacts'])
  if i==0:expected['Assemblies/Sanctuary.Content.dll']=sha(old_content)
  assert boot['index']==i and boot['artifacts']==expected and boot['savedFiles']
 paths.extend([old_input,old_snapshot,old_content])
 audit_log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count(': passed')==2 and audit_log.count('Shutting down')==1
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());audits.append(a)
  assert a==json.loads((ROOT/'tools/reference'/name).read_text())
  assert a['ContentAssemblySha256']==focused['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  paths.extend([p,ROOT/'tools/reference'/name])
 assert [len(a['InputHashes']) for a in audits]==[794,21]
 assert next(x for x in audits[0]['Definitions'] if x['Id']=='piece/138')['Available'] is False
 assert next(x for x in audits[0]['Definitions'] if x['Id']=='piece/133')['Available'] is True
 assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==historical('tools/reference/named-item-runtime-audit.json')['Definitions']
 old_affixes=historical('tools/reference/first-loop-affix-audit.json')
 for field in ['ImplementedCodes','SpawnableCodes','Items','Pools']:assert audits[1][field]==old_affixes[field],field
 python=STAGE/'python-tests.log';assert 'Ran 71 tests' in python.read_text() and '\nOK\n' in python.read_text();paths.append(python)
 installed=historical('tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
 for n,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/n)==h,n
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build-attempt-5.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
  paths.extend([p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')])
 for name in ['mitigation-native-checks.log','stats-native-checks.log','audit-native-checks.log','upgrade-native-checks.log','isolated-npc-equipment-mitigation-fixtures.json','npc-equipment-mitigation-fixtures.json','upgrade-npc-equipment-mitigation-legacy-fixtures.json','run-attempt-5.log','retained-run-attempt-1.log','upgrade-run-attempt-1.log']:
  paths.append(STAGE/name)
 for n in ['tests/check_npc_equipment_mitigation.py','tests/check_npc_equipment_mitigation_regression.py','tests/check_npc_equipment_mitigation_upgrade.py','docs/skill-effects.md','docs/skill-icons.md']:paths.append(ROOT/n)
 for name in ['isolated-npc-equipment-mitigation-fixtures.json','npc-equipment-mitigation-fixtures.json']:
  manifest=json.loads((STAGE/name).read_text());assert len(manifest['Actors'])==6 and manifest['Generation']==2
  rows=manifest['Actors'];assert rows[0]['Physical']==rows[0]['Magic']==3 and rows[4]['Lightning']==20
  assert rows[5]['Cold']==11 and rows[5]['Level']==47 and json.loads(rows[5]['Gear'])['NamedProperties'][1]['Value']==2
 legacy=json.loads((STAGE/'upgrade-npc-equipment-mitigation-legacy-fixtures.json').read_text())
 assert legacy['Generation']==2 and legacy['Cold']==11 and json.loads(legacy['Gear'])['NamedProperties'][1]['Value']==0
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
 assert len([x for x in status['skills'] if x['implementation']=='handler-present'])==185 and status['completedClasses']==[]
 result=dict(referenceVersion='3.3.93847',component='Original NPC item flat reductions and absorption;fixed coefficient save compatibility',installed=False,goalComplete=False,completedClasses=[],
  verified=dict(reusedFullReducerContexts=2508,excludedLegacyNegativeMdrContexts=180,focusedMarkers=3,retainedMarkers=389,legacyUpgradeMarkers=3,separateAuditMarkers=2,bootsPerNativeScope=3,actualReloadsPerNativeScope=2,pythonTests=71,inputFingerprints=[794,21],unchangedNamedDefinitions=573,unchangedInstalledArtifacts=106),
  inputInventory=retained,focusedInputInventory=focused,upgradeInputInventory=upgrade,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
  established=['Actual bound original physical/magic flat reductions and fire/cold/lightning absorption feed the shared reducer. Ordinary item percentage absorption uses40cap,not the preexisting blanket NecromancerMinion100cap. No fake player stat owner or second mitigation engine.',
   'Real Patriarch,Gerke,Steel Shade,Nightwing,Blackhorn and Griswold originals verify scalar/range/per-level values,native non-class AOS,poison reduction exclusion,magic,multichannel healing-before-damage at injured/full life,and prepared field-rate MDR scaling. Real owner Dwarf Star MDR/absorb does not leak into NPC stats. Broken/unidentified/removal and original identity/full rolls persist through two reloads.',
   'PropertyFunc17 uses min/max when its parameter is0. Griswold Valor is the only current named scaled definition with a zero/missing parameter;its fixed source coefficient2 now reaches new birth without RNG. Existing zero rolls recover that exact fixed value without rewriting the saved item. An actual equipped player uses24cold absorb at99;an NPC uses11at47.',
   'A real old-content boot creates and saves Griswold coefficient0. Two new-content boots retain the same serials/parent/full saved rolls,still0,but deliver11cold absorb. All three boots use the compatible new integration assembly;old producer content is archived pre-fix candidate code whose NamedPropertyRuntime matches accepted861f110,not an accepted full older mitigation candidate.',
   '2508reused complete reducer contexts cover small/bypass and large-value source percentage behavior. The existing property-writer source evidence establishes function17 fallback. No new older-engine compilation or complete modern executable/item-birth certificate is claimed.'],
  remaining=['Nonzero item percentage absorption on a metal bound original is not verified:Lightsabre/Dragonscale/Flamebellow and other candidates remain gated by unresolved item skill events;available Dwarf Star/Raven Frost/Spirit Keeper are nonmetal. Pure reducer cases do not substitute for that live consumer check.',
   'The existing innate Fire Golem100cap adapter is retained,not certified. A prepared Fire Golem with item absorption is explicitly unsupported until its source composition is audited. Full Fire Golem aura/absorb/combat-death parity remains open.',
   'NPC poison-length/freeze properties,resources,item events/procs,independent frozen Revive equipment and complete learned Iron Golem remain unfinished. No new actor fields or balance data. Existing source negative-MDR overflow remains an explicit modern uncertainty with Sanctuary zero floor retained.',
   'Final summon/item absorption cues and talent/hotkey icons remain separate placeholders. No new full1184run,class completion or deployment is claimed.'])
 target=ROOT/'tools/reference/npc-equipment-mitigation-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==text
 else:target.write_text(text)
 print('NPC mitigation acceptance:2508reused reducer/3focused/389retained/3legacy-upgrade/2audits/71Python;remaining item/skill work explicit.')

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
