#!/usr/bin/env python3
"""Bind original NPC resistance consumers to source and native acceptance."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_revive_regeneration import checked_log,markers
from snapshot_skill_build import check as check_snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-equipment-resistances'
PREDECESSOR='4c8fc45'
sha=inventory.sha

def historical(name):
 return json.loads(subprocess.check_output(['git','show',PREDECESSOR+':'+name],cwd=ROOT))

def record(check=False):
 inventory.STAGE=STAGE;paths=[Path(__file__).resolve(),STAGE/'pre-build-attempt-1.json'];scopes=[]
 for name in ['resistances-inputs.json','stats-inputs.json','audit-inputs.json']:
  p=STAGE/name;scopes.append(inventory.validate_inventory(p));check_snapshot(STAGE/'pre-build-attempt-1.json',p);paths.append(p)
 focused,retained,audited=scopes
 assert all(x['artifacts']==focused['artifacts'] and x['copiedData']==focused['copiedData'] for x in scopes)
 prior=historical('tools/reference/npc-equipment-critical-evidence.json');old=prior['inputInventory'];assert old['copiedData']==focused['copiedData']
 for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
  assert old['artifacts'][name]==focused['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
 old_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};new_cs={p:h for p,h in focused['sourceInputs'].items() if p.endswith('.cs')}
 assert set(new_cs)-set(old_cs)=={'src/Sanctuary.Content/NpcEquipmentResistances.cs','tests/Sanctuary.Integration/NpcEquipmentResistanceChecks.cs'}
 assert {p for p in old_cs if old_cs[p]!=new_cs[p]}=={'src/Sanctuary.Content/NamedElements.cs','src/Sanctuary.Content/SkillCombat.cs'}
 scope=checked_log(STAGE/'resistances-native-checks.log',3)
 assert scope==Counter({'npc-equipment-resistances-check':1,'npc-equipment-resistances-reload-check':2})
 complete=checked_log(STAGE/'stats-native-checks.log',386)
 previous=ROOT/'.local/class-skills/npc-equipment-critical/stats-native-checks.log';assert sha(previous)==prior['artifacts'][str(previous.relative_to(ROOT))]
 assert complete==markers(previous.read_text())+scope
 source=ROOT/'tools/reference/npc-equipment-resistances-source-evidence.json';s=json.loads(source.read_text())
 assert s['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and s['cases']==1296
 for n,h in s['inputs'].items():assert sha(ROOT/n)==h,n
 for n,h in s['artifacts'].items():assert sha(ROOT/n)==h,n
 case=ROOT/'.local/class-skills/npc-equipment-resistances-source/cases.json';flat=ROOT/'.local/class-skills/npc-equipment-resistances-source-cases.json'
 assert len(json.loads(case.read_text()))==1296 and sha(case)==sha(flat)
 assert focused['sourceInputs'][str(case.relative_to(ROOT))]==retained['sourceInputs'][str(flat.relative_to(ROOT))]==sha(case)
 assert (case.parent/'sanitizer.log').read_bytes()==b''
 reduction=ROOT/'tools/reference/damage-rounding-engine-evidence.json';assert json.loads(reduction.read_text())==historical(str(reduction.relative_to(ROOT)))
 assert focused['sourceInputs'][str(reduction.relative_to(ROOT))]==retained['sourceInputs'][str(reduction.relative_to(ROOT))]==sha(reduction)
 paths.extend([source,case,flat,reduction,ROOT/'tools/probe_npc_equipment_resistances.py']+[ROOT/n for n in s['artifacts']])
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
  p=STAGE/(kind+'-build-attempt-1.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
  paths.extend([p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')])
 for name in ['resistances-native-checks.log','stats-native-checks.log','audit-native-checks.log','isolated-npc-equipment-resistances-fixtures.json','npc-equipment-resistances-fixtures.json','run-attempt-1.log','retained-run-attempt-1.log']:
  paths.append(STAGE/name)
 for n in ['tests/check_npc_equipment_resistances.py','tests/check_npc_equipment_resistances_regression.py','docs/skill-effects.md','docs/skill-icons.md']:paths.append(ROOT/n)
 for name in ['isolated-npc-equipment-resistances-fixtures.json','npc-equipment-resistances-fixtures.json']:
  manifest=json.loads((STAGE/name).read_text());assert len(manifest['Actors'])==6 and manifest['Generation']==2
  for row in manifest['Actors']:assert len(row['Resistance'])==len(row['Appearance'])==5 and row['Magic']==0
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
 assert len([x for x in status['skills'] if x['implementation']=='handler-present'])==185 and status['completedClasses']==[]
 result=dict(referenceVersion='3.3.93847',component='Original NPC item resistance and immunity consumers',installed=False,goalComplete=False,completedClasses=[],
  verified=dict(sourceStatPoolContexts=1296,reusedReducerContexts=1260,excludedLegacyNegativeMdrContexts=180,focusedMarkers=3,retainedMarkers=386,separateAuditMarkers=2,bootsPerNativeScope=3,actualReloadsPerNativeScope=2,pythonTests=71,inputFingerprints=[794,21],unchangedNamedDefinitions=573,unchangedInstalledArtifacts=106),
  inputInventory=retained,focusedInputInventory=focused,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
  established=['Original item resistance pools add once to the native actor seeds/mods. Physical reduction-percent maps to physical resistance;all-resistance affects fire/cold/lightning/poison only. Unrelated native UO appearance resistances are replaced,not added. Live reads remove broken/unidentified protection without relying on stale item caches.',
   'Six real originals use Wizardspike,Goldskin,Darkglow,Stormshield,Duriel and actual Diamond quote/commit insertion. Prepared native armor resistance controls prove exclusion. Non-class native AOS attacks and actual poison frames consume the resulting pools;owner Goldskin35 does not leak into them. Item max-resistance does not impose player75/95 caps on monsters.',
   'Raw negative and above100 NPC pools remain available to immunity/pierce decisions. Prepared Conviction observes item-created immunity and applies its one-fifth reduction once. Removal drops the item contribution. Six original serials/parents/full gear rolls/native controls and exact resistance values survive two actual reloads.',
   '1296compiled older stat-merge/getter contexts pass ASan/UBSan. The previously verified full reducer contributes1260supported contexts per boot;180legacy negative-MDR cases remain outside acceptance. This does not newly compile the reducer,certify item birth or prove the modern executable.'],
  remaining=['Full learned Iron Golem is still unavailable. Absorb/flat reduction,poison-length resistance,freeze-state properties,resources and item events/procs need their NPC consumers. Independently frozen Revive equipment and complete attack modes/windup/contact/AI seed ownership remain unfinished.',
   'Magic resistance getter is connected to the original numeric reader and retains its baseline,but a nonzero item magic-resistance supplier is not verified:the metal Horazon Legacy piece/138 is unavailable due to oskill,war and other unavailable pieces;the available Bane Wraithskin piece/133 is nonmetal. Do not treat source stat-pool cases as actual nonzero item magic delivery.',
   'No new actor serialization or balance data. No final effects/icons,new full1184run,class completion or deployment. Existing UO summon art and text-label icons remain explicitly identified placeholders in separate registers.'])
 target=ROOT/'tools/reference/npc-equipment-resistances-evidence.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert target.read_text()==text
 else:target.write_text(text)
 print('NPC resistance acceptance:1296source pools/1260reused reducer/3focused/386retained/2audits/71Python;remaining item/skill work explicit.')

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
