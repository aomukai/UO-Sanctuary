#!/usr/bin/env python3
"""Bind owned A1 metadata, source clocks and retained-original input/reload checks."""
import argparse,json,subprocess
from pathlib import Path
import build_revive_attack_clocks as source
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/revive-swing-clock'
FOCUS=ROOT/'.local/class-skills/revive-swing-clock-focus'
PARENT='5349dd194a7d26831f377e2b0e41b537cf02caa6'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    paths=[STAGE/'birth-inputs.json',FOCUS/'birth-inputs.json',STAGE/'audit-inputs.json']
    tested,focused,audit=[inventory.validate_inventory(p)for p in paths]
    for path in paths:snapshot(STAGE/'pre-build-2.json',path)
    assert tested['artifacts']==focused['artifacts']==audit['artifacts']and tested['copiedData']==focused['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/revive-melee-geometry';prior=json.loads((previous/'birth-inputs.json').read_text())
    added='Data/Sanctuary/Balance/revive-attack-clocks.json'
    assert {k:v for k,v in tested['copiedData'].items()if k!=added}==prior['copiedData']and set(tested['copiedData'])-set(prior['copiedData'])=={added}
    sources={'src/Sanctuary.Content/FrozenEquipmentCombat.cs','src/Sanctuary.Content/ReviveSwingClock.cs',
        'tests/Sanctuary.Integration/FrozenEquipmentCombatChecks.cs','tests/Sanctuary.Integration/ReviveMeleeGeometryChecks.cs','tests/Sanctuary.Integration/ReviveSwingClockChecks.cs'}
    assert {p for p in set(tested['sourceInputs'])|set(prior['sourceInputs'])if p.endswith('.cs')and tested['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}==sources
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/revive-melee-geometry-native-evidence.json'],cwd=ROOT))
    for folder,old_folder,count,retained in ((STAGE,previous,523,520),(FOCUS,previous.with_name('revive-melee-geometry-focus'),48,45)):
        old=old_folder/'birth-native-checks.log';assert sha(old)==parent['evidence'][str(old.relative_to(ROOT))]
        expected=markers(old);assert sum(expected.values())==retained
        expected['revive-swing-clock-check']+=1;expected['revive-swing-clock-reload-check']+=2
        assert markers(folder/'birth-native-checks.log')==expected and sum(expected.values())==count
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert '97664accepted/0explicit skipped-action refusals'in log
        saved=json.loads((folder/'revive-swing-clock-fixtures.json').read_text())
        assert saved['Generation']==2 and all(saved[k]>0 for k in ('Owner','Actor','Life','Ias','WeaponSpeed'))
        assert saved['Plan']['Source']=='skeleton1'and saved['Plan']['Animation']=='SKA11HS'and saved['Plan']['FasterAttackRate']==saved['Ias']and saved['Plan']['WeaponSpeed']==saved['WeaponSpeed']and saved['Plan']['ActorRate']==100
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-2.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    source.build(check=True)
    meta=json.loads((ROOT/'tools/reference/revive-attack-clock-source-evidence.json').read_text());assert (meta['sourceRows'],meta['supported'],meta['parsedAnimationRecords'],meta['uniqueAnimationCodes'])==(751,436,3661,3631)
    assert sum(meta['refusals'].values())==315
    engine=json.loads((ROOT/'tools/reference/revive-attack-clock-engine-evidence.json').read_text());assert engine['cases']==97664
    rate_parent=json.loads((ROOT/'tools/reference/spirit-wolf-rate-source-evidence.json').read_text())
    original=Path('/home/aomukai/.codex/worktrees/death-sentry-damage/Sanctuary/.local/class-skills/spirit-wolf-rate-source-probe.cpp')
    assert sha(original)==engine['parentGeneratedSourceSha256']==rate_parent['generatedSourceSha256']
    generated=(ROOT/'.local/class-skills/revive-attack-clock-source/source.cpp').read_text()
    assert generated.split('int main(){',1)[0]=='#include <iostream>\n'+original.read_text().split('int main(){',1)[0]+'\n'
    assert engine['unchangedRoutineHashes']==rate_parent['unchangedRoutineHashes']
    for path,digest in {**engine['inputs'],**engine['artifacts']}.items():assert sha(ROOT/path)==digest,path
    probe='.local/class-skills/revive-attack-clock-source-probe.json'
    assert tested['sourceInputs'][probe]==focused['sourceInputs'][probe]==engine['artifacts'][probe]
    assert not(ROOT/'.local/class-skills/revive-attack-clock-source/sanitizer.log').read_text()
    assert '97664 complete rate/predicate/scheduler contexts'in(ROOT/'.local/class-skills/revive-attack-clock-source-run-2.log').read_text()
    for name,count in (('named-item-runtime-audit.json',1011),('first-loop-affix-audit.json',21)):
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
    files=[Path(__file__).resolve(),*(ROOT/p for p in sorted(sources)),ROOT/'tools/build_revive_attack_clocks.py',ROOT/'tools/probe_revive_attack_clocks.py',
        ROOT/'data/Sanctuary/Balance/revive-attack-clocks.json',ROOT/'tools/reference/revive-attack-clock-source-evidence.json',ROOT/'tools/reference/revive-attack-clock-engine-evidence.json',
        *(ROOT/'tests'/n for n in ('check_revive_swing_clock.py','check_revive_swing_clock_focus.py')),
        *(ROOT/'docs'/n for n in ('revive-swing-clock-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-2.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','content-build-2.log','integration-build-2.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','source-check.log','revive-swing-clock-fixtures.json')),
        *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-2.log','revive-swing-clock-fixtures.json','failed-1/birth-native-checks.log')),
        *(ROOT/'.local/class-skills/revive-attack-clock-source'/n for n in ('source.cpp','probe','inputs.txt','compiler.log','sanitizer.log')),
        ROOT/'.local/class-skills/revive-attack-clock-source-run-2.log']
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Owned ordinary A1 metadata and source-event clock with captured original weapon speed/frozen IAS;native swing scheduling/public equipped casting unchanged',
        verified=dict(nativeMarkers=523,focusedMarkers=48,parentMarkersRetained=520,bootsPerCohort=3,actualReloadsPerCohort=2,
            sourceRows=751,ownedAnimationRecords=3661,uniqueAnimationCodes=3631,qualifiedA1Rows=436,explicitMetadataRefusals=315,completeSourceContexts=97664,
            nativeCSharpComparisons=97664,sourceSanitizersClean=True,independentLootDeletion=True,historicalMissingSpeedRefused=True,oldStatProfileRetained=True,multipleHandRefusal=True,
            clockQueryNoRngOrHealing=True,partialWoundsReloaded=True,pythonTests=71,auditFingerprints=[1011,21],namedDecisionsUnchanged=573,
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),artifacts=tested['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['Native swing entry,action/movement ownership,pending actions/cancellation/save behavior and public equipped casting remain unfinished.',
            'Missing,multiple/nonmelee,dual-wield and ambiguous-record paths remain explicit;no lookup/duration is guessed.',
            'Legacy algorithm plus owned metadata is not a modern dynamic lookup,mode-conversion,melee-only alternate-clock or full AI certificate.',
            'Current subscription history and unsupported item/hand/element/world/PvP/client paths remain explicit;no class complete,deployment or final artwork.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/revive-swing-clock-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Revive A1 clocks:523 broader/48 focused,520 parent retained;97664source/C# contexts,436qualified rows,315explicit gaps;installation preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
