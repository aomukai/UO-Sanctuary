#!/usr/bin/env python3
"""Bind actual outgoing original-NPC item flights and partial-ring persistence."""
import argparse
import json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT;BASE=ROOT/'.local/class-skills'
STAGE=BASE/'npc-item-emission';WIDE=BASE/'npc-item-emission-retained';ISOLATE=BASE/'npc-item-emission-isolate'
PARENT='f6b3f02525a38756e1f7f902ca95e441172c3e5e'


def record(check=False):
    born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-2.json')
    parent=e.load(ROOT/'tools/reference/npc-item-spell-origins-native-evidence.json')
    extra=Counter({'npc-item-emission-check':1,'npc-item-emission-reload-check':2})
    for previous,stage,count in [(BASE/'npc-item-spell-origins',STAGE,81),(BASE/'npc-item-spell-origins-retained',WIDE,389)]:
        log=previous/'birth-native-checks.log';assert e.sha(log)==parent['files'][str(log.relative_to(ROOT))]
        e.cohort(stage,markers(log)+extra,count)
    e.cohort(ISOLATE,expected({'extra_families':['npc-item-emission']}),12)
    snapshot(STAGE/'source-snapshot-2.json',ISOLATE/'birth-inputs.json')
    other=e.load(ISOLATE/'birth-inputs.json');assert other['artifacts']==born['artifacts'] and other['copiedData']==born['copiedData']
    for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
    phrase='Original item emission:Fire Bolt/target-centered Nova/Frost Nova use verified requests and shared motion/HP;24incoming/outgoing allocation cases,partial ray survival,source100-subtile guard,exact child/actor RNG and natural contacts;85partial outgoing flights plus original items/resources/wounds through two reloads;Charged/Chain world binding/full callback history/game RNG remain separate'
    for stage in [STAGE,WIDE,ISOLATE]:
        assert 'pass across3boots/2reloads' in (stage/'native-run-1.log').read_text()
        assert (stage/'birth-native-checks.log').read_text().count(phrase)==3
        s=e.load(stage/'npc-item-emission-fixtures.json')
        assert s['Generation']==2 and len(s['Flights'])==85 and len(s['Items'])==7 and len(s['Held'])==3
        body=json.loads(s['Body']);binding=body['EquipmentWeapons']['Identity']
        assert body['Owner']==s['Owner'] and s['Random']['Actor']==s['Actor'] and s['Random']['ValkyrieBinding']==binding
        resources=json.loads(s['Resources']);assert resources['Actor']==s['Actor'] and resources['Owner']==s['Owner']
        assert {h['Skill'] for h in s['Held']}=={36,44,48}
        assert {f['Id'] for f in s['Flights']}=={i for h in s['Held'] for i in h['Ids']}
        for skill in [36,44,48]:
            h=next(h for h in s['Held'] if h['Skill']==skill);flights=[f for f in s['Flights'] if f['Id'] in h['Ids']]
            assert h['Life']==65000*256 and len(flights)==(1 if skill==36 else 42)
            for f in flights:
                n=f['Npc'];p=n['Packet'];assert f['Owner']==s['Owner'] and n['Actor']==s['Actor'] and n['Binding']==binding
                assert p['Skill']==skill and p['Rank']==f['Rank']==3 and f['Frame']==(2 if skill==36 else 1)
                assert p['Emissions']==(1 if skill==36 else 64) and f['Seed']==(666<<32)|(6000+(0 if skill==36 else f['RadialIndex']))
                assert f.get('Charged') is None and len(f['Steps']['Cells'])>=1 and f.get('Fault') is None
                if skill!=36:assert f['Id']==f['Cast']+f['RadialIndex'] and f['LastCollision']==s['Actor']
            if skill!=36:
                assert {f['RadialIndex'] for f in flights}=={i for i in range(64) if i%3!=0}
                assert len({f['Cast'] for f in flights})==1
    for name in ['content-build-1.log','integration-build-1.log']:
        assert all(t in (STAGE/name).read_text() for t in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    before=e.load(STAGE/'source-snapshot-1.json')['inputs'];after=e.load(STAGE/'source-snapshot-2.json')['inputs']
    assert before['copiedData']==after['copiedData'] and before['projectInputs']==after['projectInputs']
    assert {p for p,h in before['sourceInputs'].items() if after['sourceInputs'][p]!=h}=={'tests/Sanctuary.Integration/NpcItemEmissionChecks.cs'}
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
    source=e.load(ROOT/'tools/reference/npc-item-spell-origins-source-evidence.json');assert source['cases']==14112 and source['completeRoutines']==7
    for group in ['inputs','currentTable','artifacts']:
        for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
    client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present' for s in status['skills'])==202 and not status['completedClasses']
    assert next(s for s in status['skills'] if s['talentId']=='amazon.valkyrie')['implementation']=='blocked'
    files=e.stage_files(STAGE,WIDE,ISOLATE)+[Path(__file__).resolve(),ROOT/'tests/check_npc_item_emission.py',ROOT/'tests/Sanctuary.Integration/NpcItemEmissionChecks.cs',ROOT/'src/Sanctuary.Content/SorceressSpellMissiles.cs',ROOT/'tools/reference/npc-item-spell-origins-source-evidence.json']
    files+=[ROOT/'docs'/n for n in ['npc-item-emission-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,originalProcDispatch=False,currentExecutableParity=False,
        sourceSnapshot=2,contentBuild=1,integrationBuild=1,focusedRun=1,retainedRun=1,isolatedRun=1,auditRun=1,focusedMarkers=81,retainedMarkers=389,isolatedMarkers=12,
        bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,savedOutgoingFlights=85,originalItems=7,allocationControlsPerBoot=24,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=['Verified source item requests now create actual outgoing/incoming original-NPC Fire Bolt/Nova/Frost Nova flights through a constructor shared with ordinary emissions. Natural motion and ordered NPC HP preserve own packet/AR/pierce and supplied child seeds;all78/386parent markers remain.',
            'Partial/all-failed allocation preserves source return and surviving ray identities. Source100-subtile/zero-path/missing-target and unqualified spell guards run before seed allocation;per-hit HP/notification/source and separate projectile/actor RNG match exact observed sequences.',
            '85partially moved outgoing flights retain7original items/resources/wounds and RNG through2actualreloads;loaded intervals age without replay,then natural hits resume. Internal attempted-slot IDs preserve existing Cast+RadialIndex and are not world-unit GUIDs or RNG draws.'],
        limits=['Actual world/game-stream seed initialization and full original proc registration/callback/wrapper history are separate;allocator is mandatory and must not reenter the pool. Its external side effects are not transactional. No learned-cast or27proc-loadout admission is enabled.',
            'Charged Bolt per-ordinal initialization/path seed and Chain do151 root/budget/child allocation are still refused by this world host. Current executable target traversal/NHD/critical/callback/source-removal,nonzero shields,full terrain/height and complete AI/initializer/class/client acceptance remain unfinished.',
            'Ordinary prepared emission behavior and V0 persistence fields are retained. The pre-native test review moved control targets beyond maximum ring travel;source snapshot2changesonlythat fixture fromContent1snapshot1. Final effects/audio and talent/hotkey art remain separately tracked placeholders;no deployment.'])
    e.write('npc-item-emission-native-evidence.json',result,files,check)
    print('Original NPC item emission:81focused/389retained/12isolated markers,two reloads;85saved outgoing flights,partial ring allocation and exactHP/RNG;202handlers,full proc/cast still gated.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
