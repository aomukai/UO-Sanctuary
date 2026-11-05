#!/usr/bin/env python3
"""Strict full-source and original-item NPC blind-on-hit acceptance."""
import argparse,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-item-blind';REPEAT=BASE/'npc-item-blind-repeat';RETAINED=BASE/'npc-item-blind-retained';FREEZE=BASE/'npc-item-blind-freeze';FULL=BASE/'npc-item-blind-full-fill';PARENT='cd0cb15731326984f61644c1b48e4b835d73f831'
sys.path.insert(0,str(ROOT/'tests'))
from check_npc_damage_fill import configuration
from check_npc_item_blind_retained import settings
CHANGED=['src/Sanctuary.Content/'+n for n in ('NpcItemBlind.cs','NpcItemSpellCasts.cs','NecromancerCurseCasts.cs','NpcCombatEvents.cs','NpcItemProcGraph.cs','ReviveEquipmentEvents.cs')]+['tests/Sanctuary.Integration/'+n for n in ('NpcItemBlindChecks.cs','NpcFreezeHitChecks.cs','ReviveEventChecks.cs')]
def families(names,extra=None):
 wanted=Counter(extra or{});wanted['iron-golem-birth-check']+=3
 for n in ('iron-golem-items','npc-resource-callbacks',*names):wanted[n+'-check']+=1;wanted[n+'-reload-check']+=2
 return wanted

def build(check=False):
 born=e.load(STAGE/'birth-inputs.json')
 for stage,file in ((STAGE,'birth-inputs.json'),(REPEAT,'birth-inputs.json'),(RETAINED,'birth-inputs.json'),(FREEZE,'birth-inputs.json'),(FULL,'birth-inputs.json'),(STAGE,'audit-inputs.json')):
  data=e.load(stage/file);assert data['artifacts']==born['artifacts']and data['copiedData']==born['copiedData'];snapshot(STAGE/'pre-build-3.json',stage/file)
  for path,h in data['sourceInputs'].items():assert e.sha(ROOT/path)==h,path
 for kind in ('Content','Integration'):
  assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
  assert all(t in(STAGE/(kind.lower()+'-build-3.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 sourcepath=ROOT/'tools/reference/npc-item-blind-source-evidence.json';source=e.load(sourcepath)
 assert source['contexts']==dict(callback=2560,composition=4800,guards=12)and len(source['completeRoutineHashes'])==7 and not source['currentExecutableParity']
 assert len(source['headerBindings'])==57
 assert [source['headerBindings'][n]for n in ('STAT_ITEM_STUPIDITY','STAT_LEVEL','UNIT_ALIGNMENT_EVIL','UNIT_ALIGNMENT_NEUTRAL','UNITFLAG_CANBEATTACKED','UNITFLAG_ISVALIDTARGET','UNITFLAG_TARGETABLE','EVENTTYPE_REMOVESTATE','PLRMODE_DEAD')]==[113,12,0,1,4,8,2,12,17]
 for group in ('inputs','artifacts'):
  for path,h in source[group].items():assert e.sha(ROOT/path)==h,path
 cases=e.load(BASE/'npc-item-blind-source/cases.json');assert {k:len(v)for k,v in cases.items()}==source['contexts']and cases['guards'][0]['output']==[1,1,11,7,775,1,1,7,0,1]
 for s in (STAGE,REPEAT):
  log=e.cohort(s,families(('npc-item-blind',)),12);assert log.count('NPC item blind:2560callback/4800factory/12guard source contexts;')==3
  saved=e.load(s/'npc-item-blind-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==2 and {a['Revive']for a in saved['Actors']}=={True,False}
  for a in saved['Actors']:
   assert a['Owner']!=a['Source']!=a['Target']and a['Seed']>0 and a['Life']>0 and a['SourceLife']>0
   assert a['State']['Curse']['Source']==a['Source']and a['State']['Curse']['Ability']==83 and a['State']['BlindAi']is not None
 c=settings();extra={'monster-contact-check':3,'iron-golem-ordered-contact-check':3,'npc-flat-drains-timer-check':3,'npc-flat-drains-timer-result':3,'blind-policy-check':3,'venom-result':1,'venom-reload-result':2,'revive-native-swing-result':3,'revive-native-swing-save-result':3,'blind-world-live-result':3,'blood-life-tap-live-result':3}
 e.cohort(RETAINED,families(c['extra_families'],extra),72)
 for n in ('npc-flat-drains','revive-event','revive-native-swing','blind-world','blood-life-tap'):assert e.load(RETAINED/(n+'-fixtures.json'))['Generation']==2
 assert (RETAINED/'birth-native-checks.log').read_text().count('ordinary1025->1281,explicit no-recovery fixture1025->1025;')==3
 e.cohort(FREEZE,families(('npc-freeze-hit',)),12);assert e.load(FREEZE/'npc-freeze-hit-fixtures.json')['Generation']==2
 full=configuration(compact=True);e.cohort(FULL,families(full['extra_families']),60)
 original=e.load(FULL/'generated-npc-stats-fixtures.json');assert original['Generation']==2 and len(original['Holders'])==1566 and sum(len(r['Items'])for r in original['Holders'])==1674
 assert len(e.load(FULL/'generated-npc-live-resources-fixtures.json')['Actors'])==5
 bridge=e.load(FULL/'bridge-evidence.json');assert bridge['contexts']==96 and bridge['generation']==2
 for path,h in bridge['artifacts'].items():assert e.sha(ROOT/path)==h,path
 failed=STAGE/'failed-native-2';assert 'Blind/HP/event/return ordering or RNG replay differs'in(failed/'birth-native-checks.log').read_text()
 assert e.sha(failed/'content/Sanctuary.Content.dll')==born['artifacts']['Assemblies/Sanctuary.Content.dll'] # No production "fix" is claimed for the untraced failure.
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==5 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
 audits=e.audits(STAGE,PARENT,born['artifacts']);protected=e.protection()
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(s['implementation']=='handler-present'for s in status['skills'])==197
 files=[Path(__file__).resolve(),sourcepath,ROOT/'tools/skill_checkpoint_evidence.py']+[ROOT/p for p in CHANGED]
 files += [ROOT/'tests'/n for n in ('check_npc_item_blind.py','check_npc_item_blind_retained.py','check_npc_item_blind_freeze.py','check_npc_item_blind_full_fill.py')]
 files += [ROOT/'docs'/n for n in ('npc-item-blind-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md','pet-adjustment-review.md')]
 for group in ('inputs','artifacts'):files += [ROOT/p for p in source[group]]
 files += [ROOT/p for p in bridge['artifacts']]+e.stage_files(STAGE,REPEAT,RETAINED,FREEZE,FULL)
 for stage in (STAGE,RETAINED,FREEZE,FULL,BASE/'npc-item-blind-source'):
  for directory in stage.iterdir():
   if directory.is_dir()and directory.name.startswith(('failed-','interrupted-','prior-')):files += e.stage_files(directory)
 record=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=3,integrationBuild=3,sourceSnapshot=3,newNativeRun=3,repeatRun=1,retainedRun=2,freezeRun=2,fullFillRun=2,sourceContexts=source['contexts'],completeOriginalRoutines=7,headerBoundIntegerConstants=57,newMarkers=12,repeatMarkers=12,retainedMarkers=72,freezeMarkers=12,fullFillMarkers=60,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,originalNativeHolders=1566,originalNativeItems=1674,originalItemReplayContexts=96,nativePayloadRestoredFromManifest=False,pythonTests=71,verificationCommands=5,auditFingerprints=audits,protection=protected,
 scope='Original Iron/frozen Revive stat113/function09uses shared Raven chance and actor RNG,then the actual direct-target Dim Vision curse/factory/SourceBlind AI receiver. No do30 area traversal,spell execution,mana,extra HP or cast animation. Positive callback chance returns success independently of downstream refusal. Real NPC creator and player allegiance remain distinct. Shared synchronous scope uses existing native ChangingCombatant/CanBeHarmful veto for original Revive as well as guardian;forced nested assignments and exception cleanup preserve ordinary targeting. Seven complete source routines with57independently pinned constants/current and runtime Dim Vision rows cover2560/4800/12contexts. Learned unmodified Patriarch originals/loot,wounds,streams,curse/AI survive two reloads and independent repeat;actual Iron AI/legacy/graph contacts verify pre-health curse and single draw/HP/return. Coif Plan/Release explicitly preserve item/gear/roster/mana while unrelated lightning return remains unsupported. Same-build retentions coveractual Blood Life Tap,item wrappers,live blind AI,combined drain/native Revive A1 and full original generated items/fill.',
 limits=['Bounded older-source model declares formula,metadata,storage,empty subscribers,AI permission/dispatch and scheduler services. Native mapped source/recipient/owner adapters retain their existing refusal limits;no modern executable/global seed/AI/world/PvP/client certification.',
 'Earlier native2contact assertion failed without expanded actor/HP/RNG/controller diagnostics. Identical production DLL passes native3and independent repeat;that isolated failure is not claimed fixed. Failed inputs/binaries/logs remain. Investigate any reproduction with expanded diagnostics.',
 'New blind-equipped Revive fixture covers learned original creation/shared contact;natural pursuit/native A1 with that exact effect is a further combined test. Ordinary native Revive AI/saved A1 and live blind AI are retained separately.',
 'CoifofGlory remains refused for stat128/function10lightning retaliation;modifiers are not stripped. Prior registration fixture now uses still-unsupported Howltusk callbacks,retaining all capability cases. Other unadmitted events,hireling freeze,burn/bypass,Sunder300and unreadable modern Warlock code remain separate.',
 'Initial source-scaffold default alignment became Neutral1after correct header rebinding;explicitEvil0and a composed positive guard baseline correct the fixture. A missing declaration was fixed separately. Failed scaffolds are retained. Source tables/current runtime Dim Vision rows are pinned,not inferred from the old synthetic fixture ordinals.',
 'No handler/class completion,deployment,installed build/save/config/client mutation,new icon identity or completed asset. Target0x375Ais the identified curse placeholder;final effect/audio and talent/hotkey assets stay separate.'])
 e.write('npc-item-blind-native-evidence.json',record,files,check)
 print('NPC blind:source2560/4800/12;native12+12+72+12+60/two reloads;71Python/audits/protection;prior untraced contact explicitly retained.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
