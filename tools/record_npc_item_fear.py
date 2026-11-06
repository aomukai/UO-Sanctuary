#!/usr/bin/env python3
"""Bind original NPC item fear, independent saved AI and retained combat acceptance."""
import argparse
from collections import Counter
from pathlib import Path
import sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'npc-item-fear'
WIDE=BASE/'npc-item-fear-retained'
PARENT='5cde73a0ae232053cbe4dbeb64cfaaa4c047605f'
sys.path.insert(0,str(ROOT/'tests'))
from check_npc_item_fear import configuration
from check_npc_lightning_return_retained import settings as previous
from record_poison_dagger_contact import markers


def expected_retained():
    old=previous();new=configuration(True)
    receipt=e.load(ROOT/'tools/reference/npc-lightning-return-native-evidence.json')
    path=BASE/'npc-lightning-return-retained/birth-native-checks.log'
    assert e.sha(path)==receipt['files'][str(path.relative_to(ROOT))]
    expected=markers(path);assert sum(expected.values())==78
    for family in set(new['extra_families'])-set(old['extra_families']):
        expected[family+'-check']+=1;expected[family+'-reload-check']+=2
    for name in set(new['extra_checks'])-set(old['extra_checks']):expected[name]+=3
    for name in set(new['extra_live_commands'])-set(old['extra_live_commands']):expected[name+'-result']+=3
    for name in set(new.get('extra_target_input_commands',()))-set(old.get('extra_target_input_commands',())):
        expected[name+'-check']+=3;expected[name+'-result']+=3
    for name,result in new.get('extra_wait_results',{}).items():
        if name not in old.get('extra_wait_results',{}):expected[result]+=expected[name]
    assert new['save_command']==old['save_command'] and sum(expected.values())==149
    return expected


def record(check=False):
    born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-8.json')
    for stage in (STAGE,WIDE):
        inputs=e.load(stage/'birth-inputs.json')
        for path,digest in inputs['sourceInputs'].items():assert e.sha(ROOT/path)==digest,path
    old=e.load(BASE/'necromancer-current-acceptance/source-snapshot-7.json')['inputs']
    current=e.load(STAGE/'source-snapshot-8.json')['inputs']
    added={'src/Sanctuary.Content/NpcItemFear.cs','tests/Sanctuary.Integration/NpcItemFearChecks.cs'}
    changed={'src/Sanctuary.Content/'+n for n in ('SourceTerror.cs','NecromancerEffects.cs','NamedControl.cs','NpcCombatEvents.cs','NpcItemProcGraph.cs','ReviveEquipmentEvents.cs')}
    changed|={'tests/Sanctuary.Integration/'+n for n in ('ReviveEventChecks.cs','NpcFreezeHitChecks.cs','AttractCastChecks.cs')}
    assert current['sourceInputs'].keys()-old['sourceInputs'].keys()==added
    assert old['sourceInputs'].keys()<=current['sourceInputs'].keys()
    assert {p for p in old['sourceInputs']if old['sourceInputs'][p]!=current['sourceInputs'][p]}==changed
    assert current['copiedData']==old['copiedData'] and current['projectInputs']==old['projectInputs']
    baseline=e.load(BASE/'necromancer-current-acceptance/birth-inputs.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==baseline['artifacts'][name]
    for kind,build in (('content',5),('integration',8)):
        assert all(s in(STAGE/f'{kind}-build-{build}.log').read_text()for s in('Build succeeded.','0 Warning(s)','0 Error(s)'))
    focused=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'npc-item-fear-check':1,'npc-item-fear-reload-check':2,'npc-item-fear-live-result':3})
    logs=[e.cohort(STAGE,focused,15),e.cohort(WIDE,expected_retained(),149)]
    assert all(log.count('NPC item fear:5376callback/1280factory cases')==3 for log in logs)
    for stage,name in ((STAGE,'run-5.log'),(WIDE,'run-3.log')):assert 'pass across3boots/2reloads' in(stage/name).read_text()
    source=e.load(ROOT/'tools/reference/npc-item-fear-source-evidence.json')
    assert source['contexts']==dict(callback=5376,composition=1280) and len(source['completeRoutineHashes'])==3
    for group in ('inputs','artifacts'):
        for path,digest in source[group].items():assert e.sha(ROOT/path)==digest,path
    assert 'ASan/UBSan pass' in(STAGE/'source-check.log').read_text()
    fingerprints=e.audits(STAGE,PARENT,born['artifacts'])
    protection=e.protection()
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==5 and all(c['exitCode']==0 for c in commands)
    assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert sum(r['implementation']=='handler-present' for r in status['skills'])==197 and not status['completedClasses']
    files=e.stage_files(STAGE,WIDE)
    for folder,filename,sourcepath,message in ((STAGE/'failed-native-1','ReviveEquipmentEvents.cs','src/Sanctuary.Content/ReviveEquipmentEvents.cs','Unadmitted frozen Revive event stat: howl'),(WIDE/'failed-retained-1','NpcFreezeHitChecks.cs','tests/Sanctuary.Integration/NpcFreezeHitChecks.cs',"denied by connection filter 'auto-denylist'"),(WIDE/'failed-retained-2','AttractCastChecks.cs','tests/Sanctuary.Integration/AttractCastChecks.cs','Real autonomous neighbor damage/target/RNG'),(STAGE/'failed-native-4','NecromancerEffects.cs','src/Sanctuary.Content/NecromancerEffects.cs','item=False,mind=')):
        inputs=e.load(folder/'birth-inputs.json')
        assert e.sha(folder/filename)==inputs['sourceInputs'][sourcepath]
        assert message in(folder/'birth-native-checks.log').read_text()
        for kind in ('Content','Integration'):
            path=folder/kind.lower()/f'Sanctuary.{kind}.dll';assert e.sha(path)==inputs['artifacts'][f'Assemblies/Sanctuary.{kind}.dll'];files.append(path)
        files+=e.stage_files(folder)+list(folder.glob('*.cs'))
    files += [Path(__file__).resolve(),ROOT/'tests/check_npc_item_fear.py',ROOT/'tools/probe_npc_item_fear.py',ROOT/'tools/reference/npc-item-fear-source-evidence.json',ROOT/'tools/reference/npc-item-blind-source-evidence.json',ROOT/'tools/reference/necromancer-current-acceptance-native-evidence.json',ROOT/'tools/reference/npc-lightning-return-native-evidence.json']
    files += [ROOT/'docs'/name for name in ('npc-item-fear-checkpoint.md','necromancer-coverage-matrix.md','class-skills-resume-checkpoint.md','necromancer-mechanical-audit.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,productionChanged=True,handlers=197,completedClasses=[],
        contentBuild=5,integrationBuild=8,sourceSnapshot=8,focusedRun=5,retainedRun=3,auditRun=3,sourceContexts=6656,completeSourceRoutines=3,
        focusedMarkers=15,retainedMarkers=149,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,originalSavedActors=3,
        savedIronActors=2,savedOriginalRevives=1,retainedFamilies=23,retainedSharedChecks=6,retainedInputActions=9,naturalFearSessions=6,
        pythonTests=71,auditFingerprints=fingerprints,protection=protection,artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=[
            'Actual original Face of Horror learned Iron legacy/live graph and frozen original Revive use NPC stat112/function08 once per qualified damage event. Raw low7chance/fixed20frame Terror,shared resistance/rank/creator/Attract rules and independent target/AI initialization have dedicated coverage.',
            'Optional saved ItemTerror separates AI initializer from the curse list,including stronger/equal creators,transient Attract refusal and a later learned Terror target handoff. Actual natural movement/expiry and original wounded ownership/loot/RNG/effect states survive two reloads per cohort;the held saved phase is also advanced after official pet resume.',
            'The retained cohort preserves all78parent markers and adds affected shared AI/curses,freeze/lightning and actual curse inputs. Freeze reload fixture now authenticates its saved owner;production network security is unchanged. Failed diagnostics remain separate.'],
        limits=[
            'Supplementary source,declared AI eligibility/initializer services and native adapters do not prove full modern executable/class callbacks,global events,target/path geometry,PvP or graphical client parity.',
            'Unmapped/native bard/owned recipient contexts and unknown historical elite metadata remain explicit refusals. Current outgoing original-monster projectile/retired-owner coverage remains separately bounded;the source callback itself has no ranged penalty.',
            'The earlier ordinary Attract damage assertion has no original lane values;the later controlled item/neighbor-lease bug is a separate established issue,not a retroactive explanation. Existing assertions and deadlines are retained with richer diagnostic values.',
            'Howltusk remains unsupported for separate knockback/damage-to-mana events. Existing fear effect/audio placeholder is reused;no new skill or icon identity. No complete class or global goal certificate.'])
    e.write('npc-item-fear-native-evidence.json',result,files,check)
    print('NPC item fear:15focused/149retained markers,2reloads each,6656source contexts;original gear/wounds/AI preserved;installed build untouched.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
