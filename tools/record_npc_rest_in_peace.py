#!/usr/bin/env python3
"""Bind original NPC RIP kills, corpse transfer and retained acceptance."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-rest-in-peace';WIDE=BASE/'npc-rest-in-peace-retained';IRON=BASE/'npc-rest-in-peace-iron'
PARENT='b3d429caaabe89e681ced6290a0d752ebfa30dac'

def record(check=False):
    snapshot=STAGE/'source-snapshot-3.json';born=e.inventory(STAGE,WIDE,snapshot)
    old=e.load(BASE/'npc-item-slow/source-snapshot-7.json')['inputs'];now=e.load(snapshot)['inputs']
    assert now['sourceInputs'].keys()-old['sourceInputs'].keys()=={'src/Sanctuary.Content/NpcRestInPeace.cs','tests/Sanctuary.Integration/NpcRestInPeaceChecks.cs'}
    assert old['projectInputs']==now['projectInputs'] and old['copiedData'].keys()==now['copiedData'].keys()
    assert {p for p,h in old['copiedData'].items()if now['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    first=e.load(STAGE/'source-snapshot-1.json')['inputs']
    assert {p:h for p,h in first['sourceInputs'].items()if p.startswith(('src/','shared/'))}=={p:h for p,h in now['sourceInputs'].items()if p.startswith(('src/','shared/'))}
    base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    delta=Counter({'npc-rest-in-peace-check':1,'npc-rest-in-peace-reload-check':2});e.cohort(STAGE,base+delta,12)
    prior=BASE/'npc-item-slow-retained/birth-native-checks.log';receipt=e.load(ROOT/'tools/reference/npc-item-slow-native-evidence.json')
    assert e.sha(prior)==receipt['files'][str(prior.relative_to(ROOT))]
    retained=markers(prior);assert sum(retained.values())==173;e.cohort(WIDE,retained+delta,176)
    e.cohort(IRON,base+Counter({'iron-golem-cast-check':1,'iron-golem-cast-reload-check':2}),12)
    for stage,run in [(STAGE,3),(WIDE,2),(IRON,2)]:
        inputs=e.load(stage/'birth-inputs.json');assert inputs['artifacts']==born['artifacts'] and inputs['copiedData']==born['copiedData']
        e.snapshot(snapshot,stage/'birth-inputs.json')
        for p,h in inputs['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
        assert 'pass across3boots/2reloads' in(stage/f'run-{run}.log').read_text()
        if stage!=IRON:
            saved=e.load(stage/'npc-rest-in-peace-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==3
            assert len({x['Source']for x in saved['Actors']})==len({x['Pending']for x in saved['Actors']})==3
            assert (stage/'birth-native-checks.log').read_text().count('NPC Rest in Peace:128source controls')==3
    for kind,run in [('content',1),('integration',3)]:
        text=(STAGE/f'{kind}-build-{run}.log').read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
    client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    protected['acceptedClientPatches']=41
    source_path=ROOT/'tools/reference/npc-rest-in-peace-source-evidence.json';source=e.load(source_path)
    assert source['contexts']==128 and not source['currentExecutableParity'] and len(source['completeRoutineHashes'])==1 and source['constants']['STAT_ITEM_RESTINPEACE']==108
    for group in ['inputs','artifacts']:
        for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==8 and all(x['exitCode']==0 for x in commands)
    assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(x['implementation']=='handler-present'for x in status['skills'])==198 and not status['completedClasses']
    assert 'Iceblink learned start refused' in(STAGE/'failed-native-1/birth-native-checks.log').read_text()
    assert 'Unsupported Cutthroat1 callback' in(WIDE/'failed-retained-1/birth-native-checks.log').read_text()
    files=e.stage_files(STAGE,WIDE,IRON)+[source_path]
    for stage,names in [(STAGE,['failed-native-1','prior-focused-2']),(WIDE,['failed-retained-1']),(IRON,['prior-iron-1'])]:
        for name in names:
            folder=stage/name;assert folder.is_dir()
            files += [p for p in folder.rglob('*')if p.is_file()and p.suffix in('.cs','.dll','.log','.json','.py')]
    files+=e.stage_files(BASE/'npc-rest-in-peace-source')
    for kind in ['content','integration']:files.append(STAGE/kind/f'Sanctuary.{kind.title()}.dll')
    files += [ROOT/p for p in ['tests/check_npc_rest_in_peace.py','tests/check_iron_golem_birth.py','tools/probe_npc_rest_in_peace.py','tools/reference/inferno-held-input-client-evidence.json']]
    files += [ROOT/'docs'/p for p in ['npc-rest-in-peace-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md','skill-mechanical-reference.md']];files.append(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=198,completedClasses=[],sourceSnapshot=3,contentBuild=1,integrationBuild=3,focusedRun=3,retainedRun=2,ironRun=2,auditRun=2,
        focusedMarkers=12,retainedMarkers=176,learnedIronMarkers=12,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,originalActors=3,pendingUnits=3,
        callbackSourceContexts=128,pythonTests=71,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=['Complete older RIP callback29 has only nonnull-pointer guards before toggling target state;128sanitized source controls independently bind state/stat IDs. No current-executable parity claim.',
               'Original Tyrael legacy/graph Iron and frozen Revive execute ordinary prepared kills. Pre-death unit marks transfer to the existing saved corpse flag and survive reward processing;owner-only RIP does not forward.',
               'Existing corpse consumers refuse without consuming loot/mana or changing ownership/selection metadata. Flag-only controls prove the same corpse is otherwise usable. Original wounds,resources,gear,numeric graphs,seeds,loot and corpse flags retain across two reloads.',
               'Pending marks survive two reloads,then transfer on later native death. Restored subscriptions execute;malformed state restore is atomic. New additive persistence241does not alter installed saves.'],
        limits=['Other ranged/spell/native-fallback/removed-source kill producers,full naturally delayed combat-mode composition,current-engine/global/PvP/class/client parity remain open.',
                'First focused failure was an uninitialized plain control sword;first broad failure was an obsolete callback refusal string on the replacement skilltab item. Production remains build1;fixtures retain original item,loot,mana,roster and strict resource assertions. Failed inputs,source and binaries are retained.',
                'No new effect/audio or talent/hotkey identity;existing summon art remains pending. Installed build,saves,configuration and accepted41client patches/shared inputs are unchanged.'])
    e.write('npc-rest-in-peace-native-evidence.json',result,files,check)
    print('NPC Rest in Peace:12focused/176retained/12learned-Iron,two reloads each;128source contexts;71Python;198handlers/no class complete;installed world preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
