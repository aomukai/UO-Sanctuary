#!/usr/bin/env python3
"""Bind shared source hand policy to original generated NPC item graphs."""
import argparse,json
from pathlib import Path
from record_generated_npc_resources import expected as previous_expected
from snapshot_skill_build import check as snapshot
import skill_checkpoint_evidence as e
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/generated-npc-weapons';COMPACT=ROOT/'.local/class-skills/generated-npc-weapons-compact';PARENT='99e3df913bd4373288d4f349bc70f58531198af4'
CHANGED={'src/Sanctuary.Content/SourceWeaponSelection.cs','src/Sanctuary.Content/GeneratedNpcWeaponFrame.cs','src/Sanctuary.Content/GeneratedNpcStatGraph.cs','tests/Sanctuary.Integration/GeneratedNpcWeaponChecks.cs'}
def build(check=False):
 born=e.load(COMPACT/'birth-inputs.json');audit=e.load(STAGE/'audit-inputs.json');assert born['artifacts']==audit['artifacts']and born['copiedData']==audit['copiedData']
 for path in(COMPACT/'birth-inputs.json',STAGE/'audit-inputs.json'):
  snapshot(STAGE/'pre-build-1.json',path)
  for p,h in e.load(path)['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
 for kind in('Content','Integration'):assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 old=e.load(ROOT/'.local/class-skills/mage-safe-area/pre-build-3.json')['inputs'];now=e.load(STAGE/'pre-build-1.json')['inputs']
 assert {p for p in old['sourceInputs'].keys()|now['sourceInputs'].keys()if old['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}==CHANGED
 assert old['projectInputs']==now['projectInputs']and old['copiedData']==now['copiedData']
 parent=e.load(ROOT/'tools/reference/mage-safe-area-native-evidence.json')
 for name in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==parent['artifacts'][name]
 sourcepath=ROOT/'tools/reference/generated-npc-weapons-source-evidence.json';source=e.load(sourcepath);cases=e.load(ROOT/'.local/class-skills/generated-npc-weapons-source/cases.json')
 assert len(source['completeRoutineHashes'])==30 and source['numericCompositions']==len(cases['hands'])==len(cases['numericOutputs'])==3276
 assert source['multiItemCompositions']==sum(a['Case']>=1548 for a in cases['hands'])==1728 and source['equippableControls']==len(cases['equippable'])==60
 prior=e.load(ROOT/'tools/reference/dragon-talon-hands-source-evidence.json');priorcases=ROOT/prior['fixture']['path'];assert e.sha(priorcases)==prior['fixture']['sha256']and len(e.load(priorcases))==12288
 for name,h in prior['compiledRoutines'].items():assert source['completeRoutineHashes'][name]==h,name
 wanted=previous_expected()
 for family in('generated-npc-live-resources','generated-npc-weapons'):wanted[family+'-check']+=1;wanted[family+'-reload-check']+=2
 log=e.cohort(COMPACT,wanted,54);assert log.count('Generated NPC weapons:3276source item/hand compositions including1728multi-item;12288shared source selector controls;60equip guards;')==3
 saved=e.load(COMPACT/'generated-npc-weapons-fixtures.json');original=e.load(COMPACT/'generated-npc-stats-fixtures.json');assert saved==original and saved['Generation']==2 and len(saved['Holders'])==1566
 assert len({r['Actor']for r in saved['Holders']})==1566 and sum(len(r['Items'])for r in saved['Holders'])==1674
 live=e.load(COMPACT/'generated-npc-live-resources-fixtures.json');assert live['Generation']==2 and len(live['Actors'])==5
 for kind in('content','integration'):assert all(t in(STAGE/(kind+'-build-1.log')).read_text()for t in('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=e.audits(STAGE,PARENT,born['artifacts']);commands=e.load(STAGE/'verification-commands.json');assert len(commands)==5 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text();protected=e.protection()
 observed=ROOT/'.local/class-skills/warlock-live-2026-11-06-events/event-bodies-36-37-40.json';ref=e.load(observed)
 assert ref['referenceVersion']=='3.3.93847'and ref['immutableCandidateTableMatches']and ref['bytesRead']==1216 and not ref['callbackIdentitiesVerified']
 assert all(not ref[k]for k in('processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'))
 assert [r['slot']for r in ref['records']]==[36,37,40]and all(not f['captured']and f['permissions']=='---s'for r in ref['records']for f in r['fragments'])
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_generated_npc_weapons.py',sourcepath,ROOT/'tools/reference/dragon-talon-hands-source-evidence.json',priorcases,observed]+[ROOT/p for p in CHANGED]
 for group in('inputs','artifacts'):
  for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
  files += [ROOT/p for p in source[group]]
 for p,h in ref['artifacts'].items():assert e.sha(ROOT/p)==h,p
 files += [ROOT/p for p in ref['artifacts']]
 unique=ROOT/'tools/reference/d2r-named-equipment/json/uniqueitems.json';rows=e.load(unique);assert any(r.get('prop1')=='pierce-immunity-poison'and r.get('min1')==r.get('max1')==300 for r in rows.values());files.append(unique)
 files += [ROOT/'docs'/n for n in('generated-npc-weapons-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files += e.stage_files(STAGE,COMPACT)+[p for p in(STAGE/'failed-source-1').rglob('*')if p.is_file()]
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=1,integrationBuild=1,sourceSnapshot=1,compactRun=1,compactMarkers=54,boots=3,actualReloads=2,saves=3,sourceCompositions=3276,multiItemCompositions=1728,completeOriginalRoutines=30,reusedSelectorControls=12288,equipPredicateControls=60,originalNativeHolders=1566,originalNativeItems=1674,nativePayloadRestoredFromManifest=False,pythonTests=71,verificationCommands=5,auditFingerprints=audits,protection=protected,
  scope='Complete older selector,damage-list entry and primary restore now share the original generated NPC item stat graph and existing propagation. Original native identities,selected identity,all arrays/topology/flags and callback chronology match3276compositions,including1728multi-item cases,through two reloads. Independent12288kick selector and60equip predicate controls are reused;retained numeric/resource and five live recipient checks pass.',
  limits=['Primary/secondary identity and used-skill/row/selector/phase/flags are explicit caller services,not durable inventory-primary or actual NPC skill dispatch admission. Selected-hand physical/elemental/poison rolls,full skill/event/rate/resistance consumers,mutable flags/physical transfer and learned summon actors remain separate.',
   'The source is the pinned older complete routines with current raw item packets;current executable/player Warlock hand policy,world/allocation seed/client parity are not claimed. Fresh generated metadata has no BROKEN/NOEQUIP;mutable flags need later admission.',
   'Fresh owned running-reference anchors/immutable event table match,but slots36/37/40are---s;1216anchor/table bytes read,no callback code or game input/write/attach/security change. D2RMM command-line presence does not certify modded runtime tables. Raw Sunder300delivery remains unsupported;historical official pet applicability is not an encoding proof.',
   'Source1compiler inherited a duplicate inventory helper and lacked an equip-predicate declaration;only fixture services/declarations changed before accepted source2andfinalcheck;all complete original bodies remain unchanged.',
   'No new handler/completed class,deployment,installed build/save/config/client change,new skill effect/audio or icon identity.'])
 e.write('generated-npc-weapons-native-evidence.json',receipt,files,check)
 print('Generated NPC weapons:3276compositions/1728multi-item;30complete routines;12288selector/60equip controls;54native markers/two reloads;71Python/audits/protection pass;current callback/Sunder gaps retained.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
