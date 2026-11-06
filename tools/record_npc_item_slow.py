#!/usr/bin/env python3
"""Bind approved shared pet slow and unchanged-build retained acceptance."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation

ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-item-slow';WIDE=BASE/'npc-item-slow-retained'
PARENT='71fcd210cb7bd246696646bb7cc84690368dad88'


def record(check=False):
    snapshot=STAGE/'source-snapshot-7.json';born=e.inventory(STAGE,WIDE,snapshot)
    old=e.load(BASE/'saved-npc-item-recovery/source-snapshot-6.json')['inputs'];now=e.load(snapshot)['inputs']
    assert now['sourceInputs'].keys()-old['sourceInputs'].keys()=={'src/Sanctuary.Content/NpcItemSlow.cs','tests/Sanctuary.Integration/NpcItemSlowChecks.cs'}
    assert old['projectInputs']==now['projectInputs'] and old['copiedData'].keys()==now['copiedData'].keys()
    assert {p for p,h in old['copiedData'].items()if now['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    delta=Counter({'npc-item-slow-check':1,'npc-item-slow-reload-check':2})
    e.cohort(STAGE,base+delta,12)
    prior=BASE/'saved-npc-item-recovery-retained/birth-native-checks.log'
    receipt=e.load(ROOT/'tools/reference/saved-npc-item-recovery-native-evidence.json')
    assert e.sha(prior)==receipt['files'][str(prior.relative_to(ROOT))]
    retained=markers(prior);assert sum(retained.values())==170;e.cohort(WIDE,retained+delta,173)
    clay=BASE/'npc-item-slow-clay';attract=BASE/'npc-item-slow-attract'
    clay_prior=BASE/'clay-golem-world/birth-native-checks.log';expected_clay=markers(clay_prior)
    assert sum(expected_clay.values())==99;e.cohort(clay,expected_clay+delta,102)
    expected_attract=base+Counter({'attract-cast-check':1,'attract-cast-reload-check':2,'attract-cast-reload-result':2})
    for kind in ['right','quick']:expected_attract.update({f'attract-cast-{kind}-check':3,f'attract-cast-{kind}-result':3})
    e.cohort(attract,expected_attract,26)
    for stage,run in [(STAGE,5),(WIDE,3),(clay,2),(attract,1)]:
        inputs=e.load(stage/'birth-inputs.json');assert inputs['artifacts']==born['artifacts'] and inputs['copiedData']==born['copiedData']
        e.snapshot(snapshot,stage/'birth-inputs.json')
        for p,h in inputs['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
        assert 'pass across3boots/2reloads' in(stage/f'run-{run}.log').read_text()
        if stage!=attract:
            saved=e.load(stage/'npc-item-slow-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==4
            assert sum(x['Clay']for x in saved['Actors'])==1
            assert (stage/'birth-native-checks.log').read_text().count('Approved pet slow:70category/amount controls per Iron variant')==3
    for kind,run in [('content',3),('integration',6)]:
        text=(STAGE/f'{kind}-build-{run}.log').read_text();assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
    client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    protected['acceptedClientPatches']=41
    source_path=ROOT/'tools/reference/pet-slow-velocity-source-evidence.json';source=e.load(source_path)
    assert source['contexts']==640 and not source['currentExecutableParity'] and len(source['completeRoutineHashes'])==1
    preflight_path=ROOT/'tools/reference/npc-item-slow-reference-preflight.json';preflight=e.load(preflight_path)
    assert preflight['bytesRead']==0 and not preflight['callbackIdentityVerified'] and not preflight['currentSemanticsVerified']
    assert preflight['approvedAdaptation']=='Approve this shared slow rule'
    for proof in [source,preflight]:
        for group in ['inputs','artifacts']:
            for p,h in proof.get(group,{}).items():assert e.sha(ROOT/p)==h,p
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==8 and all(x['exitCode']==0 for x in commands)
    assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(x['implementation']=='handler-present'for x in status['skills'])==198 and not status['completedClasses']
    failed=(WIDE/'failed-retained-2/birth-native-checks.log').read_text();assert 'attract-cast-quick-result: failed' in failed
    files=e.stage_files(STAGE,WIDE,clay,attract)+[source_path,preflight_path,clay_prior]
    for stage,names in [(STAGE,['failed-build-1','failed-build-4','failed-native-1','prior-focused-2','prior-focused-3','prior-focused-4']),(WIDE,['failed-retained-1','failed-retained-2']),(clay,['failed-clay-1'])]:
        for name in names:
            files += [p for p in(stage/name).rglob('*')if p.is_file()and p.suffix in('.cs','.dll','.log','.json','.py')]
    files+=e.stage_files(BASE/'pet-slow-velocity-source')
    for kind in ['content','integration']:files.append(STAGE/kind/f'Sanctuary.{kind.title()}.dll')
    files += [ROOT/p for p in ['tests/check_npc_item_slow.py','tests/check_clay_golem_world.py','tests/check_iron_golem_birth.py','tools/probe_pet_slow_velocity.py','tools/record_npc_slow_preflight.py','tools/reference/inferno-held-input-client-evidence.json']]
    files += [ROOT/'docs'/p for p in ['approved-npc-slow-rule.md','npc-item-slow-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md','skill-mechanical-reference.md']];files.append(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=198,completedClasses=[],sourceSnapshot=7,contentBuild=3,integrationBuild=6,focusedRun=5,retainedRun=3,clayRun=2,attractRun=1,auditRun=1,
        focusedMarkers=12,retainedMarkers=173,clayMarkers=102,attractMarkers=26,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,originalActors=4,
        velocitySourceContexts=640,pythonTests=71,auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=['Explicit approved melee item/outgoing-Clay rule:30s,90/50category caps,fully absorbed hits,retained active primary slow/refreshed attack slow and expiry. Existing timed/rate services reused.',
               'Original Razortine legacy/graph Iron,Kelpie frozen Revive and learned Clay cover misses/blocks/avoid/dodge,pre-HP slow,absorbed/damaging contacts,resources/RNG and two reloads. Serialization expiry hold follows separate real30s controls.',
               'Complete unchanged older movement routine:640source contexts and native no-haste delay agree on25percent minimum movement rate. Existing global rate code unchanged.',
               'Verified indestructible writer20preserves complete Tyrael as unsupportedRIP108negative. Spiritforge positive original birth/save;Bartucs skilltab remains refused.'],
        limits=['Current D2R callback identity/semantics,haste composition,full path/animation/PvP/global/class acceptance are unverified. Negative/ranged/unmapped Paragon slow remains unsupported;native fallback reports and preserves ordinary damage.',
                'Broad run2Attract acquired its target but dealt no damage before its unchanged deadline. The same-build isolation and unchanged broad rerun pass;root cause untraced,failed inputs/binaries/logs retained.',
                'No new effect/icon identity or final art/audio. Installed build,saves,configs and all accepted41client patches/shared inputs are unchanged. No running D2R client needed for these checks.'])
    e.write('npc-item-slow-native-evidence.json',result,files,check)
    print('Approved pet slow:12focused/173retained/102Clay/26isolated-Attract,two reloads each;640source contexts;71Python;198handlers/no class complete;installed world preserved.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
