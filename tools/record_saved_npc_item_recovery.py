#!/usr/bin/env python3
"""Bind original NPC item/socket/class projection and qualified kill recovery."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'saved-npc-item-recovery';WIDE=BASE/'saved-npc-item-recovery-retained'
PARENT='3b4848eef605cee848bcf88d90cea59a7d7e5466'


def record(check=False):
    born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-6.json')
    old=e.load(BASE/'inferno-held-input/source-snapshot-2.json')['inputs'];now=e.load(STAGE/'source-snapshot-6.json')['inputs']
    assert old['sourceInputs'].keys()<=now['sourceInputs'].keys()
    assert now['sourceInputs'].keys()-old['sourceInputs'].keys()=={'src/Sanctuary.Content/EquipmentSocketProperty.cs','src/Sanctuary.Content/NpcKillRecovery.cs','tests/Sanctuary.Integration/NpcKillRecoveryChecks.cs','tests/Sanctuary.Integration/SavedItemSpecialPropertyChecks.cs'}
    assert old['projectInputs']==now['projectInputs'] and old['copiedData'].keys()==now['copiedData'].keys()
    assert {p for p,h in old['copiedData'].items()if now['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    first=e.load(STAGE/'source-snapshot-1.json')['inputs']
    assert {p:h for p,h in first['sourceInputs'].items()if p.startswith(('src/','shared/'))}=={p:h for p,h in now['sourceInputs'].items()if p.startswith(('src/','shared/'))}
    base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    delta=Counter({'saved-item-special-properties-check':3})
    for family in ['npc-kill-recovery','item-property-writers','iron-golem-birth-graph','item-property-creation']:
        delta.update({family+'-check':1,family+'-reload-check':2})
    e.cohort(STAGE,base+delta,24)
    prior=BASE/'npc-damage-to-mana-retained/birth-native-checks.log';receipt=e.load(ROOT/'tools/reference/npc-damage-to-mana-native-evidence.json')
    assert e.sha(prior)==receipt['files'][str(prior.relative_to(ROOT))]
    retained=markers(prior);assert sum(retained.values())==155
    e.cohort(WIDE,retained+delta,170)
    for stage,run in [(STAGE,6),(WIDE,3)]:
        assert 'pass across3boots/2reloads' in(stage/f'run-{run}.log').read_text()
        saved=e.load(stage/'npc-kill-recovery-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==9 and sum(x['Revive']for x in saved['Actors'])==3
        for actor in saved['Actors']:
            assert actor['OwnerMana']==257 and actor['OwnerLife']>0 and actor['Life']>0
            numeric=__import__('json').loads(actor['Numeric'])
            if actor['Kind']=="Heaven's Light":
                assert any((v['Packed']>>16)==194 and v['Value']>0 for v in numeric['ItemBase'])
                assert any(v['Packed']==(83<<16|3)and v['Value']>0 for v in numeric['Properties'])
        text=(stage/'birth-native-checks.log').read_text();assert text.count('NPC kill recovery:1728source cases')==3 and text.count('Saved socket property:29240complete-source traces/692owned bases')==3
    iso=BASE/'saved-npc-item-recovery-iron';fire=BASE/'saved-npc-item-recovery-inferno'
    e.cohort(iso,base+Counter({'iron-golem-cast-check':1,'iron-golem-cast-reload-check':2}),12)
    expected=markers(BASE/'inferno-held-input/birth-native-checks.log');assert sum(expected.values())==21;e.cohort(fire,expected,21)
    for stage in [iso,fire]:
        inputs=e.load(stage/'birth-inputs.json');assert inputs['artifacts']==born['artifacts'] and inputs['copiedData']==born['copiedData'];e.snapshot(STAGE/'source-snapshot-6.json',stage/'birth-inputs.json')
        for p,h in inputs['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
        assert 'pass across3boots/2reloads' in(stage/'run-1.log').read_text()
    for kind,run in [('content',2),('integration',5)]:
        text=(STAGE/f'{kind}-build-{run}.log').read_text();assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
    client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    sources=[]
    for name,count in [('saved-socket-property-source-evidence.json',29240),('npc-kill-recovery-source-evidence.json',1728)]:
        path=ROOT/'tools/reference'/name;source=e.load(path);assert not source['currentExecutableParity'] and source.get('cases',source.get('contexts'))==count and len(source['completeRoutineHashes'])==3
        for group in ['inputs','artifacts']:
            for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
        sources.append(path)
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==8 and all(x['exitCode']==0 for x in commands)
    assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(x['implementation']=='handler-present'for x in status['skills'])==198 and not status['completedClasses']
    files=e.stage_files(STAGE,WIDE,iso,fire)+sources
    for stage,names in [(STAGE,['failed-native-1','failed-native-2','prior-focused-3','prior-focused-4','prior-focused-5']),(WIDE,['failed-retained-1','failed-retained-2'])]:
        for name in names:
            files += [p for p in(stage/name).rglob('*')if p.is_file()and p.suffix in('.cs','.dll','.log','.json','.py')]
    socket=BASE/'saved-socket-property-source';files+=e.stage_files(socket)
    files += [p for p in(socket/'failed-count-2').rglob('*')if p.is_file()]
    for kind in ['content','integration']:files.append(STAGE/kind/f'Sanctuary.{kind.title()}.dll')
    files += [ROOT/p for p in ['tests/check_saved_npc_item_recovery.py','tests/check_npc_kill_recovery.py','tests/check_iron_golem_birth.py','tools/probe_saved_socket_property.py','tools/probe_npc_kill_recovery.py','tools/reference/item-property-creation-source-evidence.json','tools/reference/inferno-held-input-client-evidence.json']]
    files += [ROOT/'docs'/p for p in ['saved-npc-item-recovery-checkpoint.md','npc-kill-recovery-progress.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md','skill-mechanical-reference.md']];files.append(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',parent=PARENT,restoredWip='07d68de751bc7202a4a99f2dd49a9fb4f57be571',installed=False,handlers=198,completedClasses=[],sourceSnapshot=6,contentBuild=2,integrationBuild=5,focusedRun=6,retainedRun=3,auditRun=1,
        focusedMarkers=24,retainedMarkers=170,learnedIronMarkers=12,infernoMarkers=21,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,originalActors=9,frozenRevives=3,
        socketSourceCases=29240,ownedSocketBases=692,killSourceCases=1728,pythonTests=71,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=['Saved class/element layers reuse function21 without rerolls;property14 stores original empty sockets in item base194 under independently checked owned dimensions/type/level caps. Full original Heavens Light retains pal83/layer3 and sockets.',
               'Ordinary qualified NPC kills deliver life86/28 and demon-life139/18 through legacy/graph/frozen paths;mana138/17 is explicitly player-only. Missing-life fractions/caps/full-life cases and real ordinary melee kills preserve direct credit,owner resources and RNG.',
               'Nine original wounded Deathcleaver/Deathspade/Heavens Light actors retain exact resources/gear/numeric graphs/seeds through two reloads;restored subscriptions execute. All155prior retained markers remain,with15new/source-property markers.',
               'Existing45840numeric and61472creation controls plus original equipped birth remain. Hellplague is now positive actual contact;Bartucs skilltab remains an explicit birth refusal. The accepted held Inferno wire/timer/reload scope is rerun on the same build.'],
        limits=['No current-executable/global/PvP/world geometry/full-class or every-item certificate. Socket contents,fresh/native/tinker socket initialization,invalid saved counts,skilltab/other specialized properties,negative heal amounts and unsupported kill producers remain separate.',
                'The exact-resource fixture now disables native recovery from construction;the failed owners gained3whole HP while pets/mana/seed/gear/numeric graphs matched. Production regeneration and strict equality were not changed.',
                'Archived broad failures preserve the obsolete Hellplague refusal and the replacement display/source-name mismatch. The socket probe count correction changes its arithmetic expectation,not any complete source body.',
                'Healing overlay151uses identified0x376A/hue0x21;final effects/audio and existing talent/hotkey icons remain separate. Installed build,saves,configs and accepted41client patches are unchanged.'])
    e.write('saved-npc-item-recovery-native-evidence.json',result,files,check)
    print('Saved NPC recovery:24focused/170retained/12learned-Iron/21Inferno,two reloads each;9original actors;29240socket/1728kill source cases;71Python;198handlers/no class complete;installed world preserved.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
