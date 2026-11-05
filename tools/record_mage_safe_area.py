#!/usr/bin/env python3
"""Record legal Mage town/parking fixtures and actual learned delivery."""
import argparse
from collections import Counter
from pathlib import Path
from snapshot_skill_build import check as snapshot
import skill_checkpoint_evidence as e
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/mage-safe-area';PARENT='3d273f055c20737578676103dd9446db34ebbc11'
CHANGED={'tests/Sanctuary.Integration/MagePetBuffChecks.cs','tests/Sanctuary.Integration/SafeAreaMinionChecks.cs'}
def build(check=False):
 born=e.load(STAGE/'birth-inputs.json');audit=e.load(STAGE/'audit-inputs.json');assert born['artifacts']==audit['artifacts']and born['copiedData']==audit['copiedData']
 for path in(STAGE/'birth-inputs.json',STAGE/'audit-inputs.json'):
  snapshot(STAGE/'pre-build-3.json',path)
  for p,h in e.load(path)['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
 for kind in('Content','Integration'):assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 old=e.load(ROOT/'.local/class-skills/generated-npc-live-resources/pre-build-2.json')['inputs'];now=e.load(STAGE/'pre-build-3.json')['inputs']
 assert {p for p in old['sourceInputs'].keys()|now['sourceInputs'].keys()if old['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}==CHANGED
 assert old['projectInputs']==now['projectInputs']and old['copiedData']==now['copiedData']
 parent=e.load(ROOT/'tools/reference/generated-npc-live-resources-native-evidence.json')
 for name in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==parent['artifacts'][name]
 wanted=Counter({'iron-golem-birth-check':3})
 for family in('iron-golem-items','npc-resource-callbacks','safe-area-minion','mage-world'):
  wanted[family+'-check']+=1;wanted[family+'-reload-check']+=2
 for key in('safe-area-minion-live-result','mage-world-live-result','mage-world-right-check','mage-world-right-result','mage-world-quick-check','mage-world-quick-result'):wanted[key]=3
 log=e.cohort(STAGE,wanted,33)
 assert log.count('Safe-area minions:actual region entry/repeated notifications,original wounds/mana/ranks/order/seed')==1
 parked=e.load(STAGE/'safe-area-minion-fixtures.json');mage=e.load(STAGE/'mage-world-fixtures.json');assert parked['Generation']==mage['Generation']==2
 assert parked['Life']>0 and parked['Life']<parked['Maximum']and parked['Mana']==17 and parked['Mage']!=mage['Body']
 diag=(STAGE/'diagnostic-1/birth-native-checks.log').read_text();assert 'available=True,region=False,corpseRoom=False,target=False,spawn=True'in diag
 bad=(STAGE/'failed-native-2/birth-native-checks.log').read_text();assert 'available=True,region=True,corpseRoom=True,target=True,spawn=False'in bad
 for kind in('content','integration'):assert all(t in(STAGE/(kind+'-build-3.log')).read_text()for t in('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=e.audits(STAGE,PARENT,born['artifacts']);commands=e.load(STAGE/'verification-commands.json');assert len(commands)==4 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text();protected=e.protection()
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_mage_safe_area.py']+[ROOT/p for p in CHANGED]
 files += [ROOT/'docs'/n for n in('mage-safe-area-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files += e.stage_files(STAGE)
 for folder in(STAGE/'diagnostic-1',STAGE/'failed-native-2'):files += [p for p in folder.rglob('*')if p.is_file()]
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=3,integrationBuild=3,sourceSnapshot=3,nativeRun=3,nativeMarkers=33,boots=3,actualReloads=2,saves=3,actualRightInputPasses=3,actualQuickInputPasses=3,naturalMagePasses=3,naturalParkingPasses=3,productionSourceChanged=False,pythonTests=71,verificationCommands=4,auditFingerprints=audits,protection=protected,
  scope='Resolve the legacy safe-area Mage fixture failure by respecting pinned InTown0 and distinct wilderness spawn tiles. Explicit town refusal proves no mana/corpse/roster mutation;the full original parking suite and learned Mage world/AI,actual right/quick payment/corpse paths pass through two reloads. Original parked wounds/resources,rank/order/RNG and companion,actual entry/return,healer and owner-death assertions remain. Guard diagnostics improve future failure evidence.',
  limits=['Only Integration fixture C# changed. Production C#/shared code,projects and balance/reference data are identical to the parent source snapshot;rebuilt assembly metadata may differ. No production town permission,corpse guard or approved deviation changed.',
   'Native diagnostic1confirms town rejection;native2then correctly rejects the reused occupied wilderness tile. Both are retained and excluded from accepted reload evidence.',
   'This closes the reproduced Mage/safe-area fixture failure,not all Necromancer mechanics or full current executable/world/room/path/RNG/client parity. Prior prepared resource and other class consumers retain their own bounded evidence.',
   'No new handler/completed class,deployment,installed build/save/config/client change,new skill effect/audio or icon identity.'])
 e.write('mage-safe-area-native-evidence.json',receipt,files,check)
 print('Mage safe-area:33native markers/two reloads;three right/quick,natural AI and parking passes;source InTown0 refusal;unchanged production source;71Python/audits/protection pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
