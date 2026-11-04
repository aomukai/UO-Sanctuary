#!/usr/bin/env python3
"""Bind player-only defense-item NPC admission to reused source and real original actors."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-defense-conditions'
FOCUS=ROOT/'.local/class-skills/npc-defense-conditions-focus'
PARENT='650c3b8739be134c6cbeecd16f23fc7d108ac8b3'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    paths=[STAGE/'birth-inputs.json',FOCUS/'birth-inputs.json',STAGE/'audit-inputs.json']
    tested,focused,audit=[inventory.validate_inventory(p)for p in paths]
    for p in paths:snapshot(STAGE/'pre-build-1.json',p)
    assert tested['artifacts']==focused['artifacts']==audit['artifacts']and tested['copiedData']==focused['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/npc-prevent-heal';prior=json.loads((previous/'birth-inputs.json').read_text())
    assert tested['copiedData']==prior['copiedData']
    sources={'src/Sanctuary.Content/NpcCombatEvents.cs'}
    sources|={'tests/Sanctuary.Integration/'+name+'.cs'for name in ('NpcPreventHealChecks','NpcDefenseConditionsChecks')}
    assert {p for p in set(tested['sourceInputs'])|set(prior['sourceInputs'])if p.endswith('.cs')and tested['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}==sources
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][n]==prior['artifacts'][n]==sha(STAGE/'native/Distribution'/n)
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/npc-prevent-heal-native-evidence.json'],cwd=ROOT))
    for folder,old_folder,total,retained in ((STAGE,previous,553,550),(FOCUS,previous.with_name('npc-prevent-heal-focus'),78,75)):
        old=old_folder/'birth-native-checks.log';assert sha(old)==parent['evidence'][str(old.relative_to(ROOT))]
        expected=markers(old);assert sum(expected.values())==retained
        expected['npc-defense-conditions-check']+=1;expected['npc-defense-conditions-reload-check']+=2
        assert markers(folder/'birth-native-checks.log')==expected and sum(expected.values())==total
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'npc-defense-conditions-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Rows'])==2
        assert {(r['Name'],r['Stat'],r['Value'])for r in saved['Rows']}=={('The Diggler',115,1),('Razoredge',116,33)}
        assert len({r[k]for r in saved['Rows']for k in ('Owner','Revive','Golem')})==6
        for row in saved['Rows']:
            assert all(row[k]>0 for k in ('Owner','Revive','Golem','Material','Weapon','ReviveLife','GolemLife'))and row['Material']!=row['Weapon']and len(row['Binding'])==64
    proof_path=ROOT/'tools/reference/npc-defense-conditions-source-evidence.json';proof=json.loads(proof_path.read_text());assert proof['cases']==6804
    assert proof['currentWriters']==[dict(property=p,stat=n,id=i,function=1,registeredEvents=[])for p,n,i in (('ignore-ac','item_ignoretargetac',115),('reduce-ac','item_fractionaltargetac',116))]
    for section in ('inputs','artifacts'):
        for path,digest in proof[section].items():assert sha(ROOT/path)==digest,path
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-1.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',1012),('first-loop-affix-audit.json',21)):
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
    files=[Path(__file__).resolve(),*(ROOT/p for p in sorted(sources)),proof_path,ROOT/'tools/review_npc_defense_conditions.py',
        *(ROOT/'tests'/n for n in ('check_npc_defense_conditions.py','check_npc_defense_conditions_focus.py')),
        *(ROOT/'docs'/n for n in ('npc-defense-conditions-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-1.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','content-build-1.log','integration-build-1.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','npc-defense-conditions-fixtures.json')),
        *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','npc-defense-conditions-fixtures.json'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='NPC-held ignore-defense115 and percentage-target-defense116 retain raw item stats while leaving the existing nonplayer defense/hit path unchanged',
        verified=dict(nativeMarkers=553,focusedMarkers=78,parentMarkersRetained=550,bootsPerCohort=3,actualReloadsPerCohort=2,connectedWorldSavesPerCohort=3,
            reusedCompleteReferenceHitCases=6804,actualWeapons=['The Diggler','Razoredge'],originalRevives=2,originalIronGolems=2,owners=2,
            learnedQuickCastPayment=True,rawStatsAndItemsPreserved=True,independentCorpseLoot=True,playerDefensePositiveControls=True,
            npcDefenseUnmodified=True,npcHitThresholdDrawsPerBoot=400,ordinaryContactAndWoundRngRetained=True,noOwnerForwarding=True,
            remainingConditionalRefusalsRetained=True,originalBodiesWoundsBindingsAndItemsReloaded=True,pythonTests=71,auditFingerprints=[1012,21],namedDecisionsUnchanged=573,
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),artifacts=tested['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['Defense-item exclusion follows the complete older hit routine and current item mapping; current-executable NPC applicability remains unobserved.',
            'The runtime threshold controls preserve the existing shared hit policy; this is not a new certificate of integer rounding,negative-stat,geometry,blocking or special-recipient equivalence.',
            'The earlier equipped-Revive pending-packet reload failure remains unresolved; its retained assertions and detailed diagnostics remain unchanged.',
            'Other item conditions,procs,unmapped bodies and source/world/recipient/PvP/client paths remain explicit.',
            'No class complete, deployment or final artwork; effects/audio and talent/hotkey icons remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/npc-defense-conditions-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('NPC defense conditions:6804reused source cases;553broader/78focused;550parent markers retained;four real learned original actors and player controls through two reloads;installation preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
