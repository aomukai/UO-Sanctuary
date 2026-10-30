#!/usr/bin/env python3
"""Bind NPC equipment element acceptance to one tested build and saved originals."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_revive_regeneration import checked_log,markers
from snapshot_skill_build import check as check_snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-equipment-elements'
PREDECESSOR='36014c4'
sha=inventory.sha

def historical(name):
 return json.loads(subprocess.check_output(['git','show',PREDECESSOR+':'+name],cwd=ROOT))

def record(check=False):
 inventory.STAGE=STAGE;paths=[Path(__file__).resolve(),STAGE/'pre-build-attempt-2.json'];scopes=[]
 for name in ['elements-inputs.json','stats-inputs.json','audit-inputs.json']:
  p=STAGE/name;scopes.append(inventory.validate_inventory(p));check_snapshot(STAGE/'pre-build-attempt-2.json',p);paths.append(p)
 focused,retained,audited=scopes
 assert all(x['artifacts']==focused['artifacts'] and x['copiedData']==focused['copiedData'] for x in scopes)
 prior=historical('tools/reference/npc-equipment-damage-evidence.json');old=prior['inputInventory'];assert old['copiedData']==focused['copiedData']
 for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
  assert old['artifacts'][name]==focused['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
 old_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};new_cs={p:h for p,h in focused['sourceInputs'].items() if p.endswith('.cs')}
 assert set(new_cs)-set(old_cs)=={'src/Sanctuary.Content/SourceElementRoll.cs','src/Sanctuary.Content/NpcEquipmentElements.cs','tests/Sanctuary.Integration/NpcEquipmentElementChecks.cs'}
 assert {p for p in old_cs if old_cs[p]!=new_cs[p]}=={'src/Sanctuary.Content/DruidSpiritWolfHit.cs','src/Sanctuary.Content/ItemStatContext.cs','src/Sanctuary.Content/NamedElements.cs','src/Sanctuary.Content/NpcEquipmentDamage.cs','src/Sanctuary.Content/SkillCombat.cs'}
 scope=checked_log(STAGE/'elements-native-checks.log',3)
 assert scope==Counter({'npc-equipment-elements-check':1,'npc-equipment-elements-reload-check':2})
 complete=checked_log(STAGE/'stats-native-checks.log',377)
 previous=ROOT/'.local/class-skills/npc-equipment-damage/stats-native-checks.log';assert sha(previous)==prior['artifacts'][str(previous.relative_to(ROOT))]
 assert complete==markers(previous.read_text())+scope
 source=ROOT/'tools/reference/npc-equipment-elements-source-evidence.json';s=json.loads(source.read_text());paths.append(source)
 assert s['cases']==864 and s['olderEngineCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 for group in ['inputs','artifacts']:
  for n,h in s[group].items():assert sha(Path(n) if Path(n).is_absolute() else ROOT/n)==h,n
 cases=ROOT/'.local/class-skills/npc-equipment-elements-source/cases.json';flat=ROOT/'.local/class-skills/npc-equipment-elements-source-cases.json'
 assert sha(cases)==sha(flat) and len(json.loads(cases.read_text()))==864
 assert focused['sourceInputs'][str(cases.relative_to(ROOT))]==sha(cases) and retained['sourceInputs'][str(flat.relative_to(ROOT))]==sha(flat)
 paths.extend([cases,flat])
 audit_log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count(': passed')==2 and audit_log.count('Shutting down')==1
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());audits.append(a)
  assert a==json.loads((ROOT/'tools/reference'/name).read_text())
  assert a['ContentAssemblySha256']==focused['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  paths.extend([p,ROOT/'tools/reference'/name])
 assert [len(a['InputHashes']) for a in audits]==[789,21]
 assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==historical('tools/reference/named-item-runtime-audit.json')['Definitions']
 old_affixes=historical('tools/reference/first-loop-affix-audit.json')
 for field in ['ImplementedCodes','SpawnableCodes','Items','Pools']:assert audits[1][field]==old_affixes[field],field
 python=STAGE/'python-tests.log';assert 'Ran 71 tests' in python.read_text() and '\nOK\n' in python.read_text();paths.append(python)
 installed=historical('tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
 for n,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/n)==h,n
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build-attempt-2.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
  paths.extend([p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')])
 for name in ['elements-native-checks.log','stats-native-checks.log','audit-native-checks.log','isolated-npc-equipment-elements-fixtures.json','npc-equipment-elements-fixtures.json','run-attempt-2.log','retained-run-attempt-1.log']:
  paths.append(STAGE/name)
 for n in ['tests/check_npc_equipment_elements.py','tests/check_npc_equipment_elements_regression.py','tools/probe_npc_equipment_elements.py','docs/skill-effects.md','docs/skill-icons.md']:paths.append(ROOT/n)
 result=dict(referenceVersion='3.3.93847',component='Original NPC equipment fire/lightning/cold/magic damage',installed=False,goalComplete=False,completedClasses=[],
  verified=dict(sourceRangeContexts=864,focusedMarkers=3,retainedMarkers=377,separateAuditMarkers=2,bootsPerNativeScope=3,actualReloadsPerNativeScope=2,pythonTests=71,inputFingerprints=[789,21],unchangedNamedDefinitions=573,unchangedInstalledArtifacts=106),
  inputInventory=retained,focusedInputInventory=focused,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
  established=['Complete older range/percentage routines match864declared typed contexts per native boot. Existing wolf rolls reuse the same primitive. Fire/lightning/cold/magic ordering and the ordinary-fill threshold are tested separately.',
   'Actual Hellplague,Ghostflame,Baranar,Gleamscythe,Griffon and a genuinely inserted Ruby/Sapphire host supply fixed-point elements. Prepared aura/Enchant leases are combined before recipient item mastery once. Actual health loss,nonimmune resistance,pierce immunity gate and NPC-owned cold state pass.',
   'Three boots/two reloads preserve six original serials/parents/full rolls,exact first/last typed packets,buff state/mastery and inspected cold source/timer. Cold state is held for fixture retention only after its real source duration is checked;natural expiry remains covered by retained cold consumers.'],
  remaining=['Poison,critical,item events/procs,resource/resistance consumers,independent frozen Revive equipment and complete learned Iron Golem remain unfinished. This is a prepared SkeletonGuardian strike certificate,not full learned mode/windup/contact/seed or arbitrary native-attribute acceptance.',
   'Material elemental percentages,negative endpoint projections and socket cold durations not divisible by40ms explicitly require verified adapters. No missing source value is guessed. NPC cold corpse-destruction semantics keep the existing explicit uncertainty.',
   'Older source range composition does not compile complete FillDamageValues or prove modern executable/item-birth parity. Ginther was rejected as nonmetal;the fixture uses eligible Ghostflame without changing gates.',
   'Final weapon glows/elemental impact/audio and talent/hotkey icons are tracked separately with identified placeholders. No final assets,new full1184run,class completion or deployment are claimed.'])
 target=ROOT/'tools/reference/npc-equipment-elements-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==text
 else:target.write_text(text)
 print('NPC elements acceptance:864source/3focused/377retained/2audits/71Python;full item/skill work remains explicit.')

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
