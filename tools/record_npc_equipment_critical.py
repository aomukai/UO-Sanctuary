#!/usr/bin/env python3
"""Bind NPC critical stages, saved source rolls and old-world upgrade evidence."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_revive_regeneration import checked_log,markers
from snapshot_skill_build import check as check_snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-equipment-critical'
PREDECESSOR='f96dead'
sha=inventory.sha

def historical(name):
 return json.loads(subprocess.check_output(['git','show',PREDECESSOR+':'+name],cwd=ROOT))

def record(check=False):
 inventory.STAGE=STAGE;paths=[Path(__file__).resolve(),STAGE/'pre-build-attempt-2.json'];scopes=[]
 for name in ['critical-inputs.json','stats-inputs.json','audit-inputs.json','upgrade-inputs.json']:
  p=STAGE/name;scopes.append(inventory.validate_inventory(p));check_snapshot(STAGE/'pre-build-attempt-2.json',p);paths.append(p)
 focused,retained,audited,upgrade=scopes
 assert all(x['artifacts']==focused['artifacts'] and x['copiedData']==focused['copiedData'] for x in scopes)
 prior=historical('tools/reference/npc-equipment-poison-evidence.json');old=prior['inputInventory'];assert old['copiedData']==focused['copiedData']
 for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
  assert old['artifacts'][name]==focused['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
 old_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};new_cs={p:h for p,h in focused['sourceInputs'].items() if p.endswith('.cs')}
 assert set(new_cs)-set(old_cs)=={'src/Sanctuary.Content/NpcEquipmentCritical.cs','src/Sanctuary.Content/NpcEquipmentRandom.cs','tests/Sanctuary.Integration/NpcEquipmentCriticalChecks.cs'}
 assert {p for p in old_cs if old_cs[p]!=new_cs[p]}=={'src/Sanctuary.Content/NamedCombat.cs','src/Sanctuary.Content/IronGolemItems.cs','src/Sanctuary.Content/NpcEquipmentDamage.cs','tests/Sanctuary.Integration/NpcEquipmentDamageChecks.cs','tests/Sanctuary.Integration/NpcEquipmentElementChecks.cs','tests/Sanctuary.Integration/NpcEquipmentPoisonChecks.cs'}
 scope=checked_log(STAGE/'critical-native-checks.log',3)
 assert scope==Counter({'npc-equipment-critical-check':1,'npc-equipment-critical-reload-check':2})
 complete=checked_log(STAGE/'stats-native-checks.log',383)
 previous=ROOT/'.local/class-skills/npc-equipment-poison/stats-native-checks.log';assert sha(previous)==prior['artifacts'][str(previous.relative_to(ROOT))]
 assert complete==markers(previous.read_text())+scope
 upgraded=checked_log(STAGE/'upgrade-native-checks.log',3)
 assert upgraded==Counter({'npc-equipment-poison-check':1,'npc-equipment-critical-upgrade-check':1,'npc-equipment-critical-upgrade-reload-check':1})
 assert len(upgrade['boots'])==len(upgrade['randomProfileFiles'])==3 and upgrade['randomProfileFiles'][0]==[]
 assert all(upgrade['randomProfileFiles'][i] for i in [1,2])
 assert upgrade['oldArtifacts']=={n.removeprefix('Assemblies/'):old['artifacts'][n] for n in old['artifacts'] if n.startswith('Assemblies/Sanctuary.')}
 old_receipt=subprocess.check_output(['git','show',PREDECESSOR+':tools/reference/npc-equipment-poison-evidence.json'],cwd=ROOT)
 import hashlib
 assert upgrade['oldReceipt']==dict(commit=PREDECESSOR,sha256=hashlib.sha256(old_receipt).hexdigest())
 for i,boot in enumerate(upgrade['boots']):
  assert boot['index']==i and boot['savedFiles']
  expected=dict(focused['artifacts'])
  if i==0:expected.update({'Assemblies/'+n:h for n,h in upgrade['oldArtifacts'].items()})
  assert boot['artifacts']==expected
  assert any('SanctuaryNpcEquipmentRandom' in p for p in boot['savedFiles'])==(i>0)
 source=ROOT/'tools/reference/npc-equipment-critical-source-evidence.json';s=json.loads(source.read_text())
 assert s['olderEngineCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and s['cases']==dict(critical=3456,seed=150)
 assert len(s['compiledUnchangedRoutineHashes'])==7 and s['unchangedPhysicalCriticalBlockSha256']
 for n,h in s['inputs'].items():assert sha(Path(n))==h,n
 for n,h in s['artifacts'].items():assert sha(ROOT/n)==h,n
 for kind,count in [('critical',3456),('seed',150)]:
  case=ROOT/('.local/class-skills/npc-equipment-critical-source/'+kind+'-cases.json')
  flat=ROOT/('.local/class-skills/npc-equipment-'+kind+'-source-cases.json')
  assert len(json.loads(case.read_text()))==count and sha(case)==sha(flat)
  assert focused['sourceInputs'][str(case.relative_to(ROOT))]==retained['sourceInputs'][str(flat.relative_to(ROOT))]==sha(case)
  assert (case.parent/(kind+'-sanitizer.log')).read_bytes()==b''
  paths.extend([case,flat])
 paths.extend([source,ROOT/'tools/probe_npc_equipment_critical.py']+[ROOT/n for n in s['artifacts']])
 audit_log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count(': passed')==2 and audit_log.count('Shutting down')==1
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());audits.append(a)
  assert a==json.loads((ROOT/'tools/reference'/name).read_text())
  assert a['ContentAssemblySha256']==focused['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  paths.extend([p,ROOT/'tools/reference'/name])
 assert [len(a['InputHashes']) for a in audits]==[793,21]
 assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==historical('tools/reference/named-item-runtime-audit.json')['Definitions']
 old_affixes=historical('tools/reference/first-loop-affix-audit.json')
 for field in ['ImplementedCodes','SpawnableCodes','Items','Pools']:assert audits[1][field]==old_affixes[field],field
 python=STAGE/'python-tests.log';assert 'Ran 71 tests' in python.read_text() and '\nOK\n' in python.read_text();paths.append(python)
 installed=historical('tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
 for n,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/n)==h,n
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build-attempt-2.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
  paths.extend([p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')])
 for name in ['critical-native-checks.log','stats-native-checks.log','audit-native-checks.log','upgrade-native-checks.log','isolated-npc-equipment-critical-fixtures.json','npc-equipment-critical-fixtures.json','upgrade-npc-equipment-critical-upgrade-fixtures.json','upgrade-npc-equipment-poison-fixtures.json','run-attempt-2.log','retained-run-attempt-1.log','upgrade-run-attempt-1.log']:
  paths.append(STAGE/name)
 for n in ['tests/check_npc_equipment_critical.py','tests/check_npc_equipment_critical_regression.py','tests/check_npc_equipment_critical_upgrade.py','docs/skill-effects.md','docs/skill-icons.md']:paths.append(ROOT/n)
 for name in ['isolated-npc-equipment-critical-fixtures.json','npc-equipment-critical-fixtures.json','upgrade-npc-equipment-critical-upgrade-fixtures.json']:
  manifest=json.loads((STAGE/name).read_text());upgrading=name.startswith('upgrade-')
  assert len(manifest['Actors'])==(6 if upgrading else 4) and manifest['Generation']==(1 if upgrading else 2)
  for row in manifest['Actors']:
   assert row['Random']['Actor']==row['Actor'] and row['Random']['Owner']==manifest['Owner'] and row['Random']['Item']==row['Item']
   assert row['Chances']['Mastery']==row['Chances']['Generic']==0 and row['Chances']['Intrinsic']==5
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
 assert len([x for x in status['skills'] if x['implementation']=='handler-present'])==185 and status['completedClasses']==[]
 result=dict(referenceVersion='3.3.93847',component='Original NPC critical stages and persisted source damage rolls',installed=False,goalComplete=False,completedClasses=[],
  verified=dict(sourceCriticalContexts=3456,sourceSeedContexts=150,focusedMarkers=3,retainedMarkers=383,oldWorldUpgradeMarkers=3,separateAuditMarkers=2,bootsPerNativeScope=3,actualReloadsPerNativeScope=2,pythonTests=71,inputFingerprints=[793,21],unchangedNamedDefinitions=573,unchangedInstalledArtifacts=106),
  inputInventory=retained,focusedInputInventory=focused,upgradeInputInventory=upgrade,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
  established=['Unchanged older physical-critical statement block and full intrinsic/hit-class routines compare3456contexts,including mastery/generic/deadly short circuit,ranged/skip flags,draw counts and hit-class/overlay descriptors. Complete seed init/roll/wrappers compare150contexts under ASan/UBSan. Existing player Random critical overload retains values/counts.',
   'Real bound Steelgoad with actual Ruby/Sapphire/Emerald insertion,Hellplague,Ghostflame and Baranar deliver physical-only Deadly and separate all-channel intrinsic criticals. Both stages allow4xphysical while typed damage/poison rate double only intrinsically;poison/cold duration stays unchanged. Real owner Deadly equipment is excluded.',
   'Every default bound-NPC damage draw uses the saved source-algorithm stream in physical,physical-critical,fire/lightning/cold/magic,poison,intrinsic order. Original serials/parents/full gear rolls,chances and exact seed continue across two actual reloads. Removed binding cleanup,nonpositive bounds and atomic duplicate restoration refusal pass.',
   'Accepted old binaries create six original bindings and a real save lacking the new persistence profile. New content initializes each stream once on the first actual strike using the declared adapter seed;the next reload retains exact seeds,originals and full rolls. Actor serialization formats are unchanged.'],
  remaining=['Not complete learned Iron Golem or full NPC equipment acceptance. Item events/procs,resources/defensive resistances,independent frozen Revive equipment and complete attack modes/windup/contact remain unfinished.',
   'Source damage algorithm is verified;initial low seed is a Sanctuary adapter,not historical D2 world-seed identity or full AI seed ownership. Current mapped bodies have no mastery/generic suppliers;item-granted skills remain gated. Older source controls do not prove complete modern dispatcher or mastery lookup parity.',
   'Critical hit-class/overlay54 remain descriptors without final client presentation. Effects/audio and talent/hotkey icons are separately tracked placeholders. No new full1184run,class completion or deployment is claimed.'])
 target=ROOT/'tools/reference/npc-equipment-critical-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==text
 else:target.write_text(text)
 print('NPC critical acceptance:3456stage/150seed/3focused/383retained/3old-world-upgrade/2audits/71Python;remaining item/skill work explicit.')

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
