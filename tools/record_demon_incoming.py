#!/usr/bin/env python3
"""Bind prepared demon incoming metadata/guard and original-body reloads."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/demon-incoming';FOCUS=ROOT/'.local/class-skills/demon-incoming-focus'
PARENT='444ba0e6b1205d52394912701edadc89a6f8d3ba';sha=inventory.sha

def record(check=False):
 inventory.STAGE=STAGE;native=inventory.validate_inventory(STAGE/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json');focused=inventory.validate_inventory(FOCUS/'birth-inputs.json')
 for folder,name in ((STAGE,'birth-inputs.json'),(STAGE,'audit-inputs.json'),(FOCUS,'birth-inputs.json')):snapshot(STAGE/'pre-build-2.json',folder/name)
 assert native['artifacts']==audit['artifacts']==focused['artifacts'] and native['copiedData']==audit['copiedData']==focused['copiedData']
 prior=json.loads((ROOT/'.local/class-skills/tainted-aura/birth-inputs.json').read_text())
 assert {n for n in set(prior['sourceInputs'])|set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}=={
  'src/Sanctuary.Content/CreatureProfiles.cs','src/Sanctuary.Content/PreparedMonsterNativeDamage.cs','src/Sanctuary.Content/NpcFireSpellHit.cs','tests/Sanctuary.Integration/DemonIncomingChecks.cs'}
 assert {n for n in set(prior['copiedData'])|set(native['copiedData']) if prior['copiedData'].get(n)!=native['copiedData'].get(n)}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][n]==prior['artifacts'][n]==sha(STAGE/'native/Distribution'/n)
 proofpath=ROOT/'tools/reference/demon-incoming-source-evidence.json';proof=json.loads(proofpath.read_text())
 assert not any(proof[k] for k in ('handlerEnabled','currentExecutableParity','newCompiledOracle'))
 assert [(p['Class'],p['Base'],p['ModeFlags'],p['Velocity']) for p in proof['profiles']]==[(742,742,8192,8),(743,743,8192,6)]
 assert not proof['bloodBoil']['executionVerified'] and not proof['bloodBoil']['observation']['processMemoryRead']
 for name,digest in proof['inputs'].items():assert sha(ROOT/name)==digest,name
 parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/tainted-aura-native-evidence.json'],cwd=ROOT));baseline=ROOT/'.local/class-skills/tainted-aura/birth-native-checks.log'
 assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
 for folder,old,count in ((STAGE,ROOT/'.local/class-skills/tainted-aura',390),(FOCUS,ROOT/'.local/class-skills/tainted-aura-focus',58)):
  expected=markers(old/'birth-native-checks.log');expected['demon-incoming-check']+=1;expected['demon-incoming-reload-check']+=2
  log=(folder/'birth-native-checks.log').read_text();assert sum(expected.values())==count and markers(folder/'birth-native-checks.log')==expected
  assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
  saved=json.loads((folder/'demon-incoming-fixtures.json').read_text());assert saved['Generation']==2 and saved['Life']==116274 and saved['Mana']==22784
  assert len({saved[n] for n in ('Owner','Body','Enemy','Helm')})==4 and min(saved[n] for n in ('Owner','Body','Enemy','Helm'))>0
  body=json.loads(saved['BodyState']);assert body['Owner']==saved['Owner'] and body['Profile']=='prepared-tainted-life-owner-v1'
 for kind in ('content','integration'):assert all(s in (STAGE/f'{kind}-build-2.log').read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 for name,count in (('named-item-runtime-audit.json',980),('first-loop-affix-audit.json',21)):
  a=json.loads((STAGE/name).read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text()) and len(a['InputHashes'])==count and a['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,digest in a['InputHashes'].items():assert sha(ROOT/n)==digest,n
  b=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in a.items() if k not in skip}=={k:v for k,v in b.items() if k not in skip}
 assert (STAGE/'audit-native-checks.log').read_text().count(': passed')==2
 assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
 for name,text in (('source-check.log','two owned return identities reused'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in (STAGE/name).read_text()
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
 protected=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
 for key,rel in (('saves','.local/server/Saves'),('configuration','.local/server/Configuration')):
  p=Path(installed['runtimeRoot'])/rel;assert {str(f.relative_to(p)):sha(f) for f in p.rglob('*') if f.is_file()}==protected[key]
 state=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not state['completedClasses'] and sum(s['implementation']=='handler-present' for s in state['skills'])==191
 files=[Path(__file__).resolve(),proofpath,ROOT/'tools/review_demon_incoming.py',ROOT/'tools/build_skill_roadmap.py',baseline,
  ROOT/'tests/check_demon_incoming.py',ROOT/'tests/check_demon_incoming_focus.py',
  *(ROOT/'docs'/n for n in ('demon-incoming-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
  *(STAGE/n for n in ('pre-build-2.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-1.log','audit-native-checks.log','audit-run-1.log','content-build-2.log','integration-build-2.log','python-tests.log','source-check.log','readiness-check.log','catalogue-check.log','roadmap-check.log','demon-incoming-fixtures.json')),
  *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-2.log','demon-incoming-fixtures.json'))]
 for folder in ('native-diagnostic-1','source-diagnostic-1','source-diagnostic-2'):
  files.extend(sorted(p for p in (STAGE/folder).rglob('*') if p.is_file()))
 result=dict(referenceVersion='3.3.93847',parent=PARENT,component='Shared prepared demon incoming return metadata and friendship guard',handlerEnabled=False,goalComplete=False,completedClasses=[],
  verified=dict(nativeMarkers=390,retainedNativeMarkers=387,focusedNativeMarkers=58,boots=3,reloads=2,saves=3,pinnedDemonIdentities=2,originalActorIncomingReloadPackets=2,pythonTests=71,namedDecisions=573,auditFingerprints=[980,21],handlers=191,protectedInstalledArtifactsUnchanged=106),tested=native,
  limitations=['Receiver coverage only:successful incoming NPC fire,native resisted-fire entry,friendly refusal and lethal source return. Full incoming events/reactions,automatic hostile producers and learned summon/lifecycle remain unfinished.',
   'Existing return proof and unchanged original bodies are bound;historical ignored dependencies are not all present and no new complete source oracle or current executable parity is claimed.',
   'Blood Boil new do160 candidate linkage/semantics remain unverified and its observed mapping unreadable. Do not invent health cost,pet iteration,proc cadence or targeting from formulas alone.',
   'One native diagnostic correctly refused the inherited friendship override;the final adapter explicitly preserves it. Two source-review diagnostics record an absent historical ignored dependency and raw *Id versus cached Id header. Earlier logs,inputs,source and binaries remain preserved.',
   'Saved recovery/aura clocks are held;new reload packets act on the original actor/owner/helm/enemy with wounds and mana retained. No deployment,installed-save write,learned handler,class completion or final assets.'],evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
 p=ROOT/'tools/reference/demon-incoming-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Demon incoming:390 retained/58 focused checks;two pinned identities and original-body reload packets;Blood Boil execution remains unverified;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
