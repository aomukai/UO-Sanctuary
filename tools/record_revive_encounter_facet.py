#!/usr/bin/env python3
"""Bind original encounter Revive casting,facet projection and reload evidence."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/revive-encounter-facet'
FOCUS=ROOT/'.local/class-skills/revive-encounter-facet-focus'
PARENT='69b1dc7ffde85e8b71b90e8224cd74adc6eb3445'
sha=inventory.sha
PRODUCTION=('EncounterCreature','CreatureProfiles','ReviveFacets','ReviveOriginals','ReviveCasts')

def record(check=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json');focus=inventory.validate_inventory(FOCUS/'birth-inputs.json')
    audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for folder,name in ((STAGE,'birth-inputs.json'),(FOCUS,'birth-inputs.json'),(STAGE,'audit-inputs.json')):
        snapshot(STAGE/'pre-build-6.json',folder/name)
    assert native['artifacts']==focus['artifacts']==audit['artifacts']
    assert native['copiedData']==focus['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/elite-aura-winner';prior=json.loads((previous/'birth-inputs.json').read_text())
    changed={n for n in set(prior['sourceInputs'])|set(native['sourceInputs'])if n.endswith('.cs')and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}
    assert changed=={'src/Sanctuary.Content/'+n+'.cs'for n in PRODUCTION}|{'tests/Sanctuary.Integration/ReviveEncounterFacetChecks.cs'}
    assert native['copiedData']==prior['copiedData']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][n]==prior['artifacts'][n]
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/elite-aura-winner-native-evidence.json'],cwd=ROOT))
    baseline=previous/'birth-native-checks.log';assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);assert sum(expected.values())==457
    for family in ('revive-encounter-facet','revive-facet','revive-cast','revive-native-activation','revive-before-death'):
        expected[family+'-check']+=1;expected[family+'-reload-check']+=2
    for family in ('revive-encounter-facet-live','revive-facet-live'):
        expected[family+'-check']+=3;expected[family+'-result']+=3
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==484
    focused={'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
             'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
             'revive-encounter-facet-check':1,'revive-encounter-facet-reload-check':2,'revive-facet-check':1,'revive-facet-reload-check':2,
             'revive-encounter-facet-live-check':3,'revive-encounter-facet-live-result':3,'revive-facet-live-check':3,'revive-facet-live-result':3}
    assert markers(FOCUS/'birth-native-checks.log')==focused
    for folder in (STAGE,FOCUS):
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log
        assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'revive-encounter-facet-fixtures.json').read_text())
        assert len(saved['Actors'])==len(saved['Spawns'])==len(saved['Life'])==len(saved['Corpses'])==6 and saved['Mana']==22784
        assert len({s['Grade']for s in saved['Spawns']})==2 and {s['Difficulty']for s in saved['Spawns']}=={0,1,2}
        assert len({s['Actor']for s in saved['Actors']})==len(set(saved['Corpses']))==6
        for row,spawn,life,corpse in zip(saved['Actors'],saved['Spawns'],saved['Life'],saved['Corpses']):
            assert row['Owner']==saved['Owner']and row['Difficulty']==2 and row['Original']['Corpse']==corpse and life>0
            assert row['Setup']['Rank']==20 and row['Setup']['LifePlan']['MasteryRank']==20
            assert row['Setup']['LifePlan']['Difficulty']==spawn['Difficulty']
            assert row['FacetPlan']['Definition']=='Sanctuary.EncounterCreature'and row['FacetPlan']['Life']['Source']=='skeleton1'
            assert row['FacetPlan']['Life']['Difficulty']==2 and row['FacetPlan']['Life']['OriginalLevel']==row['Setup']['LifePlan']['OriginalLevel']
    before=FOCUS/'before-fix';old=json.loads((before/'birth-inputs.json').read_text())
    assert 'Encounter original death/capture missing'in(before/'birth-native-checks.log').read_text()
    for name in PRODUCTION:
        path='src/Sanctuary.Content/'+name+'.cs';raw=subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
        assert hashlib.sha256(raw).hexdigest()==old['sourceInputs'][path]==prior['sourceInputs'][path]
    assert sha(before/'ReviveEncounterFacetChecks.cs')==old['sourceInputs']['tests/Sanctuary.Integration/ReviveEncounterFacetChecks.cs']
    diagnostics={'capture-path-diagnostic':'Encounter original death/capture missing',
                 'preview-diagnostic':'Explicit encounter preview contaminated active profile',
                 'live-handshake-diagnostic':"Server did not reach 'revive-encounter-facet-live-result: (passed|failed)'",
                 'owner-clock-diagnostic':'Original owner/mana changed on reload'}
    for folder,message in [(before,'Encounter original death/capture missing'),*((STAGE/n,m)for n,m in diagnostics.items())]:
        inputs=json.loads((folder/'birth-inputs.json').read_text())
        assert message in '\n'.join(p.read_text()for p in folder.glob('*.log'))
        assert sha(folder/'integration/Sanctuary.Integration.dll')==inputs['artifacts']['Assemblies/Sanctuary.Integration.dll']
        if(folder/'content').is_dir():assert sha(folder/'content/Sanctuary.Content.dll')==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
    # The two later diagnostics changed fixture scheduling only.
    for name in ('live-handshake-diagnostic','owner-clock-diagnostic'):
        assert sha(STAGE/name/'content/Sanctuary.Content.dll')==native['artifacts']['Assemblies/Sanctuary.Content.dll']
    for kind in ('content','integration'):
        assert all(t in(STAGE/(kind+'-build-6.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',992),('first-loop-affix-audit.json',21)):
        new=json.loads((STAGE/name).read_text());assert new==json.loads((ROOT/'tools/reference'/name).read_text())and len(new['InputHashes'])==count
        assert new['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for filename,digest in new['InputHashes'].items():assert sha(ROOT/filename)==digest,filename
        oldaudit=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in new.items()if k not in skip}=={k:v for k,v in oldaudit.items()if k not in skip}
    assert(STAGE/'audit-native-checks.log').read_text().count(': passed')==2
    assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
    for name,text in (('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):
        assert text in(STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
    protected=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key,relative,count in (('saves','.local/server/Saves',38),('configuration','.local/server/Configuration',20)):
        folder=Path(installed['runtimeRoot'])/relative;assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    state=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not state['completedClasses']and sum(s['implementation']=='handler-present'for s in state['skills'])==191
    files=[Path(__file__).resolve(),*(ROOT/'src/Sanctuary.Content'/(n+'.cs')for n in PRODUCTION),ROOT/'tests/Sanctuary.Integration/ReviveEncounterFacetChecks.cs',
           *(ROOT/'tests'/n for n in ('check_revive_encounter_facet.py','check_revive_encounter_facet_focus.py')),
           *(ROOT/'docs'/n for n in ('revive-encounter-facet-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md')),
           *(STAGE/n for n in ('pre-build-1.json','pre-build-2.json','pre-build-3.json','pre-build-4.json','pre-build-5.json','pre-build-6.json',
               'birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log',
               'content-build-6.log','integration-build-6.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','revive-encounter-facet-fixtures.json')),
           *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-6.log','revive-encounter-facet-fixtures.json')),
           *(p for folder in (before,*(STAGE/n for n in diagnostics))for p in sorted(folder.rglob('*'))if p.is_file())]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Configured source identity and reviewed native death path;original encounter Revive/facet projection under approved difficulty and relative-health rules',
        verified=dict(nativeMarkers=484,focusedMarkers=27,parentMarkersRetained=457,bootsPerCohort=3,actualReloadsPerCohort=2,
            savedOriginalsPerCohort=6,spawnDifficulties=['Normal','Nightmare','Hell'],grades=['Ordinary','ExtraStrongMinion'],
            connectedEncounterSessionsPerCohort=3,retainedNativeFacetSessionsPerCohort=3,pythonTests=71,namedDecisionsUnchanged=573,
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        artifacts=native['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['Configured encounter source references and current native archetype combat are reused;unmapped native monsters are not assigned guessed species.',
            'Champion eligibility is explicitly uncorroborated/refused;unique/superunique gates remain;unsupported equipment and other death overrides remain explicit.',
            'Historical corpses without their original source/death data are not reconstructed;actual old-binary migration and concurrent/unheld snapshot cases remain separate.',
            'Full species/AI/item/event/room/PvP/current-executable/client parity and the global monster spell conversion remain unfinished.',
            'No new handler,completed class,deployment or final assets;effects/audio and talent/hotkey icons remain separate.'])
    target=ROOT/'tools/reference/revive-encounter-facet-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Encounter Revive:484broader/27focused markers;457parent retained;6originals/2reloads;connected facet travel and protected build/saves pass.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
