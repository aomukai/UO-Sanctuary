#!/usr/bin/env python3
"""Bind actual prepared Ward recipients without claiming a learned cast."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/grim-ward-transport'
PARENT='4f169df69fe60d5fe57aad8057486f701a3332c2'
sha=inventory.sha

def record(compare=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json')
    audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ('birth','audit'):snapshot(STAGE/'pre-build-4.json',STAGE/(name+'-inputs.json'))
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {n:h for n,h in native['sourceInputs'].items() if n.endswith('.cs')}==audit['sourceInputs']
    prior=json.loads((ROOT/'.local/class-skills/grim-ward-curse/birth-inputs.json').read_text())
    assert {n for n in set(prior['sourceInputs'])|set(native['sourceInputs']) if n.endswith('.cs') and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}=={
        'src/Sanctuary.Content/GrimWardCurseTransport.cs','src/Sanctuary.Content/NecromancerEffects.cs',
        'src/Sanctuary.Content/GearEffects.cs','tests/Sanctuary.Integration/GrimWardTransportChecks.cs'}
    assert {n for n in prior['copiedData'] if prior['copiedData'][n]!=native['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    baseline=ROOT/'.local/class-skills/grim-ward-curse/birth-native-checks.log'
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/grim-ward-curse-native-evidence.json'],cwd=ROOT))
    assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)
    expected['grim-ward-transport-check']+=1;expected['grim-ward-transport-reload-check']+=2
    expected['grim-ward-transport-live-result']+=3
    log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(expected.values())==158 and markers(STAGE/'birth-native-checks.log')==expected
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    for generation in range(3):assert log.count(f'Ward transport:generation{generation},same saved monster/missile creator/four deltas/AI;')==1
    assert log.count('Ward curse:31134complete legacy list/operation comparisons,5184accepted,1296equal-rank creator/primary retentions,864immune reductions;')==3
    saved=json.loads((STAGE/'grim-ward-transport-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Creator']==saved['Curse']['Source'] and saved['Creator']>=0x40000000
    assert saved['Curse']['Ward']==dict(Velocity=-27,Attack=-27,Other=-27,FullPhysical=-120)
    assert saved['Curse']['PhysicalDelta']==-24 and saved['Curse']['Terror']['Skill']==129
    reconnect=json.loads((STAGE/'pet-reconnect-fixture.json').read_text())
    assert reconnect['Generation']==2 and reconnect['TotalDraws']==10 and reconnect['Life']==69247 and reconnect['Mana']==54272
    world=json.loads((STAGE/'pet-placement-world-fixtures.json').read_text())
    assert world['Generation']==2 and json.loads(world['State'])['Revision']==11
    for name,count in (('named-item-runtime-audit.json',950),('first-loop-affix-audit.json',21)):
        new=json.loads((STAGE/name).read_text());assert new==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(new['InputHashes'])==count and new['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,h in new['InputHashes'].items():assert sha(ROOT/path)==h,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT))
        skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in new.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    a=(STAGE/'audit-native-checks.log').read_text()
    assert a.count(': passed')==2 and ': failed' not in a and ' ERR]' not in a
    for kind,number in (('content',4),('integration',3)):
        text=(STAGE/f'{kind}-build-{number}.log').read_text()
        assert all(t in text for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    for name,text in (('named-readiness.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):
        assert text in (STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for name,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==h,name
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    ward=next(s for s in status['skills'] if s['talentId']=='barbarian.grimward')
    assert ward['implementation']=='blocked' and ward['verification']=='partial-native-coverage' and not ward['complete']
    diagnostic=STAGE/'diagnostic-threat-1'
    assert 'Equal refresh lost primary/creator or failed secondary writes/AI restart' in (diagnostic/'birth-native-checks.log').read_text()
    old=(diagnostic/'GrimWardTransportChecks.cs').read_text()
    assert old.replace('&&curse.Terror.Override==owner.Serial.Value&&target.Combatant==null','&&curse.Terror.Override==0&&target.Combatant==null').replace('FixtureSpace.Corridor(owner,6200,26,3800)','FixtureSpace.Corridor(owner,6200,26,3600)')==(ROOT/'tests/Sanctuary.Integration/GrimWardTransportChecks.cs').read_text()
    assert sha(diagnostic/'content/Sanctuary.Content.dll')==native['artifacts']['Assemblies/Sanctuary.Content.dll']
    assert sha(diagnostic/'GrimWardCurseTransport.cs')==sha(ROOT/'src/Sanctuary.Content/GrimWardCurseTransport.cs')
    corridor=STAGE/'diagnostic-corridor-2'
    assert 'No independent fixture corridor near 6200,3800' in (corridor/'birth-native-checks.log').read_text()
    assert (corridor/'GrimWardTransportChecks.cs').read_text().replace('FixtureSpace.Corridor(owner,6200,26,3800)','FixtureSpace.Corridor(owner,6200,26,3600)')==(ROOT/'tests/Sanctuary.Integration/GrimWardTransportChecks.cs').read_text()
    import hashlib
    unitSource=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Common/src/Units/Units.cpp'
    raw=unitSource.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()=='cac33ac7614f167989129849dedd7e6b9451569d'
    files=[Path(__file__).resolve(),ROOT/'tests/check_grim_ward_transport.py',baseline,unitSource,
        *(ROOT/'docs'/n for n in ('grim-ward-transport-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-1.json','pre-build-2.json','pre-build-3.json','pre-build-4.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-3.log','audit-native-checks.log','audit-run-3.log','python-tests.log','named-readiness.log','catalogue-check.log','roadmap-check.log','content-build-4.log','integration-build-3.log','grim-ward-transport-fixtures.json','pet-placement-world-fixtures.json','pet-reconnect-fixture.json')),
        *(p for p in diagnostic.rglob('*') if p.is_file()),*(p for p in (STAGE/'diagnostic-corridor-2').rglob('*') if p.is_file())]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,goalComplete=False,completedClasses=[],
        component='Prepared actual Grim Ward recipient/curse/AI and save transport with explicit missile formula context',
        movementSource=dict(commit='0c0bbd1e6bef9181a3da1c046523af622a925416',blob='cac33ac7614f167989129849dedd7e6b9451569d',
            routine='UNITS_UpdateRunWalkAnimRateAndVelocity',scope='Legacy source composition inspection plus actual native delay checks;not a new compiled oracle or current executable parity'),
        verified=dict(nativeMarkers=158,retainedNativeMarkers=152,boots=3,reloads=2,saves=3,liveEscapeExpiryRuns=3,
            legacyCurseCasesPerBoot=31134,pythonTests=71,namedDecisions=573,auditFingerprints=[950,21],handlers=191,protectedInstalledArtifactsUnchanged=106),
        limits=['Explicit caller context only;no current missile owner/snapshot lookup,emitter,learned cast,corpse/terrain/global clock proof.',
            'Item serial is the prepared missile-kind creator. Current-combatant capture and supported Skeleton/Zombie/Megademon AI are native adapters,not current executable multiplayer target parity.',
            'Physical,attack and movement deltas feed actual consumers. Other animation rate is persisted/exposed only;nonattack mode consumers remain unfinished.',
            'Legacy run/walk source adds fear velocity and slow before the25percent floor;no new current executable movement proof.',
            'No final assets,deployment,installed-save writes,class completion or fresh full-class regression.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    path=ROOT/'tools/reference/grim-ward-transport-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if compare:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Ward transport:158markers/3boots/2reloads/3live escapes;missile creator,actual deltas/AI and saved wounds retain;other animation and learned emitter remain open.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
