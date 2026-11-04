#!/usr/bin/env python3
"""Bind original Revive mitigated packet allocation and qualified HP/state tests."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/revive-impact'
FOCUS=ROOT/'.local/class-skills/revive-impact-focus'
PARENT='267cb19a9bdf842609edb507e65d7a3326eea375'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'birth-inputs.json');focus=inventory.validate_inventory(FOCUS/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for folder,name in ((STAGE,'birth-inputs.json'),(FOCUS,'birth-inputs.json'),(STAGE,'audit-inputs.json')):snapshot(STAGE/'pre-build-1.json',folder/name)
    assert tested['artifacts']==focus['artifacts']==audit['artifacts']and tested['copiedData']==focus['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/revive-random';prior=json.loads((previous/'birth-inputs.json').read_text())
    assert tested['copiedData']==prior['copiedData']
    changed={p for p in set(tested['sourceInputs'])|set(prior['sourceInputs'])if p.endswith('.cs')and tested['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}
    sources={'src/Sanctuary.Content/'+p+'.cs'for p in ('ReviveEquipmentImpact','ReviveEquipmentOffense','NpcEquipmentImpact','NpcEquipmentPoison','SkillCombat')}|{'tests/Sanctuary.Integration/ReviveImpactChecks.cs'}
    assert changed==sources
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==prior['artifacts'][name]
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/revive-random-native-evidence.json'],cwd=ROOT))
    baseline=previous/'birth-native-checks.log';assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);assert sum(expected.values())==502
    expected['revive-impact-check']+=1;expected['revive-impact-reload-check']+=2
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==505
    old_focus=previous.with_name('revive-random-focus')/'birth-native-checks.log'
    assert sha(old_focus)==parent['evidence'][str(old_focus.relative_to(ROOT))]
    expected_focus=markers(old_focus);assert sum(expected_focus.values())==27
    expected_focus['revive-impact-check']+=1;expected_focus['revive-impact-reload-check']+=2
    assert markers(FOCUS/'birth-native-checks.log')==expected_focus and sum(expected_focus.values())==30
    for folder in (STAGE,FOCUS):
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'revive-impact-fixtures.json').read_text());assert len(saved['Packets'])==4 and saved['Pierce']>0
        assert saved['State']['Actor']==saved['Source']and saved['State']['Owner']==saved['Owner']and saved['State']['Setup']['EquipmentLifeProfile']=='frozen-equipment-combat-v1'
        for i,impact in enumerate(saved['Packets']):
            packet=impact['Packet'];poison=impact['Poison']
            assert impact['Actor']==saved['Source']and impact['CreditOwner']==saved['Owner']and packet['Total256']>0 and packet['Physical256']>0 and packet['Fire256']>0
            assert packet['Total256']==sum(packet[k]for k in ('Physical256','Fire256','Cold256','Lightning256','Magic256','Poison256'))
            assert packet['Poison256']==poison['Rate256']and packet['ColdFrames']>0 and packet['AbsorbedLife256']==0
            if i==1:assert packet['Lightning256']==0 and poison['Rate256']==0
            else:assert packet['Lightning256']>0 and poison['Rate256']>0 and poison['Frames']>0
        streams=json.loads((folder/'revive-random-fixtures.json').read_text());assert streams['Generation']==2 and len(streams['Streams'])==3
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-1.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',999),('first-loop-affix-audit.json',21)):
        fresh=json.loads((STAGE/name).read_text());assert fresh==json.loads((ROOT/'tools/reference'/name).read_text())and len(fresh['InputHashes'])==count
        assert fresh['ContentAssemblySha256']==tested['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in fresh['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in fresh.items()if k not in skip}=={k:v for k,v in old.items()if k not in skip}
    assert(STAGE/'audit-native-checks.log').read_text().count(': passed')==2
    assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
    for name,text in (('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in(STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    protected=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key,relative,count in (('saves','.local/server/Saves',38),('configuration','.local/server/Configuration',20)):
        folder=Path(installed['runtimeRoot'])/relative;assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses']and sum(s['implementation']=='handler-present'for s in status['skills'])==191
    files=[Path(__file__).resolve(),*(ROOT/p for p in sorted(sources)),ROOT/'src/Sanctuary.Content/SourceRandom.cs',
        *(ROOT/'tests'/name for name in ('check_revive_impact.py','check_revive_impact_focus.py')),
        *(ROOT/'docs'/name for name in ('revive-impact-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md')),
        *(STAGE/name for name in ('pre-build-1.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','content-build-1.log','integration-build-1.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','revive-impact-fixtures.json','revive-random-fixtures.json')),
        *(FOCUS/name for name in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','revive-impact-fixtures.json','revive-random-fixtures.json'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Allocate qualified original-body packets using captured source pierce and shared mitigation;test ordered HP/cold/poison commitment without native replay;production swing/events/public admission remain gated',
        verified=dict(nativeMarkers=505,focusedMarkers=30,parentMarkersRetained=502,bootsPerCohort=3,actualReloadsPerCohort=2,
            savedImpactSourcePerCohort=1,savedPacketsPerCohort=4,positiveOwnerAndNpcPierce=True,unbrokenImmunity=True,fractionalBoneArmor=True,qualifiedHpAndPoisonCommit=True,pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=[999,21],
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        artifacts=tested['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['Qualified release checks use explicit test services;production original-body contact/event registrations and pending native action persistence are not certified.',
            'Native original-body swings,full frozen-recipient resistance/duration/mitigation,registered source/target events,skill/stat attachment and public equipped casting remain unfinished.',
            'Existing attack2/elemental-body/affix/projectile/hand-selection and full executable/world/PvP/client/old-binary/unheld snapshot limits remain.',
            'No new handler,completed class,deployment or assets;effects/audio and talent/hotkey icons remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/revive-impact-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Original Revive impacts:505 broader/30 focused markers;502 parent retained;captured pierce/resistance/immunity/shield/HP/poison and reloads;production swing/events gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
