#!/usr/bin/env python3
"""Bind NPC do17,post-path missile RNG and retained player/Mage consumers."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-charged-item';WIDE=BASE/'npc-charged-item-retained';ISO=BASE/'npc-charged-item-isolate';SHARED=BASE/'npc-charged-item-shared'
PARENT='a39204ecf7ad79798337b996156fa027b4be3db6'
def record(check=False):
    born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-3.json');parent=e.load(ROOT/'tools/reference/npc-item-emission-native-evidence.json');extra=Counter({'npc-charged-item-check':1,'npc-charged-item-reload-check':2})
    for previous,stage,count in [(BASE/'npc-item-emission',STAGE,84),(BASE/'npc-item-emission-retained',WIDE,392)]:
        p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
    e.cohort(ISO,expected({'extra_families':['npc-charged-item']}),12)
    sys.path.insert(0,str(ROOT/'tests'));from check_npc_charged_item import configuration
    e.cohort(SHARED,expected(configuration(shared=True)),255)
    for stage,run in [(STAGE,1),(WIDE,1),(ISO,3),(SHARED,1)]:
        assert 'pass across3boots/2reloads' in (stage/f'native-run-{run}.log').read_text()
        if stage in (ISO,SHARED):
            snapshot(STAGE/'source-snapshot-3.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts'] and other['copiedData']==born['copiedData']
            for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
    phrase='NPC charged item:3780source do17/2160source path contexts;378world rank/flag/failure casts,post-path stream and retained allocation draws,player/Mage triples,self-aim and legacy seed preservation;partial3flights/original items/resources/wounds resume exact natural HP/RNG through two reloads;Chain/full callbacks/game RNG remain separate'
    for stage in [STAGE,WIDE,ISO]:
        assert (stage/'birth-native-checks.log').read_text().count(phrase)==3
        s=e.load(stage/'npc-charged-item-fixtures.json');assert s['Generation']==2 and len(s['Flights'])==3 and len(s['Items'])==7 and s['TargetLife']==65000*256
        b=json.loads(s['Body']);binding=b['EquipmentWeapons']['Identity'];assert b['Owner']==s['Owner'] and s['Random']['Actor']==s['Actor'] and s['Random']['ValkyrieBinding']==binding
        r=json.loads(s['Resources']);assert r['Actor']==s['Actor'] and r['Owner']==s['Owner']
        assert [f['Charged']['Ordinal'] for f in s['Flights']]==[0,2,4]
        for f in s['Flights']:
            n=f['Npc'];p=n['Packet'];assert f['Owner']==s['Owner'] and n['Actor']==s['Actor'] and n['Binding']==binding and p['Skill']==38 and p['Rank']==f['Rank']==3
            assert f['Frame']==1 and f['Frames']==77 and f['Charged']['Next']>=1 and len(f['Charged']['Path'])==39 and p['Emissions']==5
            # Hold target is at source+12tiles;do17's initializer uses its
            # actual absolute subtile X plus source ordinal,then38path draws.
            low=2*(f['Charged']['OriginX']+12)+f['Charged']['Ordinal'];state=(666<<32)|low
            for i in range(38):state=0x6AC690C5*(state&0xffffffff)+(state>>32)
            assert f['Seed']==state and f['Cast']==0 and f['RadialIndex']==-1 and f.get('Fault') is None
    for n in ['content-build-2.log','integration-build-2.log']:assert all(t in(STAGE/n).read_text()for t in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    first=e.load(STAGE/'source-snapshot-1.json')['inputs'];second=e.load(STAGE/'source-snapshot-2.json')['inputs'];third=e.load(STAGE/'source-snapshot-3.json')['inputs']
    assert first['copiedData']==second['copiedData']==third['copiedData'] and first['projectInputs']==second['projectInputs']==third['projectInputs']
    assert {p for p,h in first['sourceInputs'].items()if second['sourceInputs'][p]!=h}=={'src/Sanctuary.Content/SorceressSpellMissiles.cs','src/Sanctuary.Content/MageBasicSkills.cs'}
    assert {p for p,h in second['sourceInputs'].items()if third['sourceInputs'][p]!=h}=={'tests/Sanctuary.Integration/NpcChargedItemChecks.cs'}
    failed=ISO/'failed-corridor-1';assert 'No independent fixture corridor near 6800,3810' in(failed/'birth-native-checks.log').read_text()
    assert e.load(failed/'birth-inputs.json')['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    prior=ISO/'prior-passing-2';assert 'pass across3boots/2reloads'in(prior/'native-run-2.log').read_text();assert e.load(prior/'birth-inputs.json')['artifacts']==born['artifacts']
    metadata=e.load(BASE/'npc-charged-item-source/failed-metadata-1/metadata.json');assert metadata['calc1']=='"min(24,ln12)"'
    sourcepath=ROOT/'tools/reference/npc-charged-item-source-evidence.json';source=e.load(sourcepath);assert source['cases']==3780 and len(source['routineHashes'])==1
    for group in ['inputs','artifacts']:
        for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
    assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text() and '\nOK\n'in(STAGE/'python-tests.log').read_text()
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
    client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
    files=e.stage_files(STAGE,WIDE,ISO,SHARED)+[Path(__file__).resolve(),ROOT/'tests/check_npc_charged_item.py',sourcepath]
    for folder in [STAGE/'prior-build-1',ISO/'failed-corridor-1',ISO/'prior-passing-2',BASE/'npc-charged-item-source/failed-metadata-1']:
        files.extend(p for p in folder.rglob('*') if p.is_file())
    files+=[ROOT/'docs'/n for n in ['npc-charged-item-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,originalProcDispatch=False,currentExecutableParity=False,
        sourceSnapshot=3,contentBuild=2,integrationBuild=2,focusedRun=1,retainedRun=1,isolatedRun=3,sharedRun=1,auditRun=1,focusedMarkers=84,retainedMarkers=392,isolatedMarkers=12,sharedMarkers=255,sourceItemCases=3780,sourcePathCases=2160,worldCastControlsPerBoot=378,savedOriginalChargedFlights=3,originalItems=7,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=['Complete common item execution/do17 retains per-ordinal target reads,skips/failures/continuation,flags33withoutAR and initializer ordinal. World host creates actual original NPC Charged Bolts with explicit per-attempt allocation;all81/389parent markers remain.',
            'Shared path helper returns full final state. New Sorceress/original-NPC/Mage bolts retain post-path damage RNG while allocation still draws once;self-aim correction follows nonzero factory velocity. Mage triples retain concatenated ordinals/independent actor stream. Existing saved seeds are preserved.',
            '378livecast rank/flag/failure combinations and3780/2160source comparisons repeat per boot. Partial ordinals0/2/4 retain original items/resources/wounds/path/seed through actual reloads and resume exact natural HP/RNG. Shared255markers retain player/Mage input and Mage basic live/save paths.'],
        limits=['Source-only tables/initialization/path composition supplements older engine code,not exact current binary initial dispatch/whole-game chronology. Actual world/game seed allocation and full original event subscription/wrapper history remain explicit/unbound;no27proc-loadout admission or learned cast.',
            'Charged Strike retains its separate legacy global damage-roll adapter and needs per-missile state migration. Sentry/martial/Twister/Tornado alreadyadvancepathdraws and were not altered;maker/head contexts have separate child allocation. Chain root/budget and modern traversal/NHD/critical/callback/removal/nonzero-shield/class/client acceptance remain open.',
            'Source-probe literal-quote metadata assertion and first isolated corridor3810 fixture failure are archived. Content2is unchanged by the fixture3440repair. Passing isolate2is archived;isolate3refreshes the source inventory after adding shared Mage regression configuration. No deployment or final asset work.'])
    e.write('npc-charged-item-native-evidence.json',result,files,check)
    print('NPC charged item:84focused/392retained/12isolated/255shared markers;378livecasts and3780/2160source cases,post-path streams and two reloads;202handlers,full proc/cast remain gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
