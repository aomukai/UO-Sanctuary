#!/usr/bin/env python3
"""Bind original NPC poison delivery, frame clocks and durable credit evidence."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_revive_regeneration import checked_log,markers
from snapshot_skill_build import check as check_snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-equipment-poison'
PREDECESSOR='2dfd5d0'
sha=inventory.sha

def historical(name):
 return json.loads(subprocess.check_output(['git','show',PREDECESSOR+':'+name],cwd=ROOT))

def record(check=False):
 inventory.STAGE=STAGE;paths=[Path(__file__).resolve(),STAGE/'pre-build-attempt-4.json'];scopes=[]
 for name in ['poison-inputs.json','stats-inputs.json','audit-inputs.json']:
  p=STAGE/name;scopes.append(inventory.validate_inventory(p));check_snapshot(STAGE/'pre-build-attempt-4.json',p);paths.append(p)
 focused,retained,audited=scopes
 assert all(x['artifacts']==focused['artifacts'] and x['copiedData']==focused['copiedData'] for x in scopes)
 prior=historical('tools/reference/npc-equipment-elements-evidence.json');old=prior['inputInventory'];assert old['copiedData']==focused['copiedData']
 for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
  assert old['artifacts'][name]==focused['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
 old_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};new_cs={p:h for p,h in focused['sourceInputs'].items() if p.endswith('.cs')}
 assert set(new_cs)-set(old_cs)=={'src/Sanctuary.Content/ItemPoisonProjection.cs','src/Sanctuary.Content/NpcEquipmentPoison.cs','tests/Sanctuary.Integration/NpcEquipmentPoisonChecks.cs'}
 assert {p for p in old_cs if old_cs[p]!=new_cs[p]}=={'src/Sanctuary.Content/ItemStatContext.cs','src/Sanctuary.Content/NpcEquipmentDamage.cs','tests/Sanctuary.Integration/NpcEquipmentElementChecks.cs'}
 scope=checked_log(STAGE/'poison-native-checks.log',3)
 assert scope==Counter({'npc-equipment-poison-check':1,'npc-equipment-poison-reload-check':2})
 complete=checked_log(STAGE/'stats-native-checks.log',380)
 previous=ROOT/'.local/class-skills/npc-equipment-elements/stats-native-checks.log';assert sha(previous)==prior['artifacts'][str(previous.relative_to(ROOT))]
 assert complete==markers(previous.read_text())+scope
 # Reuse the established unchanged poison lane and property writer; no new
 # compilation of the older engine is represented by this increment.
 source=ROOT/'tools/reference/venom-damage-engine-evidence.json';s=json.loads(source.read_text());assert s==historical(str(source.relative_to(ROOT)))
 assert s['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and s['fixture']['cases']==10240
 cases=ROOT/s['fixture']['path'];assert sha(cases)==s['fixture']['sha256'] and len(json.loads(cases.read_text()))==10240
 assert focused['sourceInputs'][str(cases.relative_to(ROOT))]==sha(cases) and retained['sourceInputs'][str(cases.relative_to(ROOT))]==sha(cases)
 paths.extend([source,cases,ROOT/'tools/reference/item-property-writers-source-evidence.json',ROOT/'tools/reference/poison-state-engine-evidence.json'])
 audit_log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count(': passed')==2 and audit_log.count('Shutting down')==1
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());audits.append(a)
  assert a==json.loads((ROOT/'tools/reference'/name).read_text())
  assert a['ContentAssemblySha256']==focused['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  paths.extend([p,ROOT/'tools/reference'/name])
 assert [len(a['InputHashes']) for a in audits]==[791,21]
 assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==historical('tools/reference/named-item-runtime-audit.json')['Definitions']
 old_affixes=historical('tools/reference/first-loop-affix-audit.json')
 for field in ['ImplementedCodes','SpawnableCodes','Items','Pools']:assert audits[1][field]==old_affixes[field],field
 python=STAGE/'python-tests.log';assert 'Ran 71 tests' in python.read_text() and '\nOK\n' in python.read_text();paths.append(python)
 installed=historical('tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
 for n,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/n)==h,n
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build-attempt-4.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
  paths.extend([p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')])
 for name in ['poison-native-checks.log','stats-native-checks.log','audit-native-checks.log','isolated-npc-equipment-poison-fixtures.json','npc-equipment-poison-fixtures.json','run-attempt-4.log','retained-run-attempt-1.log']:
  paths.append(STAGE/name)
 for n in ['tests/check_npc_equipment_poison.py','tests/check_npc_equipment_poison_regression.py','docs/skill-effects.md','docs/skill-icons.md']:paths.append(ROOT/n)
 for name in ['isolated-npc-equipment-poison-fixtures.json','npc-equipment-poison-fixtures.json']:
  manifest=json.loads((STAGE/name).read_text());assert len(manifest['Actors'])==6 and manifest['Generation']==2 and manifest['Poison'] is None
 result=dict(referenceVersion='3.3.93847',component='Original NPC equipment poison delivery and durable source credit',installed=False,goalComplete=False,completedClasses=[],
  verified=dict(reusedPoisonLaneContexts=10240,focusedMarkers=3,retainedMarkers=380,separateAuditMarkers=2,bootsPerNativeScope=3,actualReloadsPerNativeScope=2,pythonTests=71,inputFingerprints=[791,21],unchangedNamedDefinitions=573,unchangedInstalledArtifacts=106),
  inputInventory=retained,focusedInputInventory=focused,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
  established=['The unchanged property writer replays saved poison scalar/range values and supplies count/shift semantics without rerolls. Actual Rakescar,Hellplague,Blacktongue,Jade and real Emerald insertions supply six original raw pools. Combined sources average frame duration only after summing rates,lengths and counts.',
   '10240reused complete older melee poison-lane contexts match the existing weapon poison helper on every boot. Actual strike applications,39/40ms phase,FP256 periodic damage,full expiry,resistance/immunity and absence of ordinary-hit callbacks pass via the shared poison clock.',
   'Real owner poison-weapon and poison-pierce equipment controls do not leak into NPC rates or mitigation. The real owner supplies durable damage credit only. Poison survives explicit source/item removal,two reloads and a lethal tick with the correct LastKiller.',
   'Six originals retain serials/parents/full rolls and exact poison packets through reloads. Retired identity/binding checks allow legitimate serial reuse;observed Fists objects have no retired named state. Existing element fixtures settle/clear old poison before measuring an instantaneous packet.'],
  remaining=['No complete learned Iron Golem or full NPC item certificate. Critical/item events,resource/defensive-resistance consumers,independent frozen Revive equipment and full mode/windup/contact/seed ownership remain unfinished.',
   'Unrecorded randomized poison-duration values,material poison percentages,negative projections and socket durations without an exact40ms mapping explicitly require adapters. Existing poison reapplication/credit rules are reused;no new scheduler or fake player stat source is introduced.',
   'Older source evidence is reused,not newly compiled here. It does not prove current executable or complete item-birth parity. Removal is an explicit prepared fixture,not acceptance of a new learned dismissal/death handler.',
   'Final poison contact/state cues and talent/hotkey icons remain separately tracked placeholders. No final assets,new full1184run,class completion or deployment are claimed.'])
 target=ROOT/'tools/reference/npc-equipment-poison-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==text
 else:target.write_text(text)
 print('NPC poison acceptance:10240reused source/3focused/380retained/2audits/71Python;durable credit and remaining item/skill work explicit.')

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
