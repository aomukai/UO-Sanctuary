#!/usr/bin/env python3
"""Bind ordinary NPC PMH admission to complete-source and real original-item evidence."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-prevent-heal'
FOCUS=ROOT/'.local/class-skills/npc-prevent-heal-focus'
PARENT='b74d708d1c68c303d1981ca0c5e9b3d4899a0cc3'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    paths=[STAGE/'birth-inputs.json',FOCUS/'birth-inputs.json',STAGE/'audit-inputs.json']
    tested,focused,audit=[inventory.validate_inventory(p)for p in paths]
    for p in paths:snapshot(STAGE/'pre-build-2.json',p)
    assert tested['artifacts']==focused['artifacts']==audit['artifacts']and tested['copiedData']==focused['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/revive-equipped';prior=json.loads((previous/'birth-inputs.json').read_text())
    assert tested['copiedData']==prior['copiedData']
    sources={'src/Sanctuary.Content/NpcCombatEvents.cs'}
    sources|={'tests/Sanctuary.Integration/'+name+'.cs'for name in ('IronGolemOrderedContactChecks','ReviveEquippedChecks','NpcPreventHealChecks')}
    assert {p for p in set(tested['sourceInputs'])|set(prior['sourceInputs'])if p.endswith('.cs')and tested['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}==sources
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][n]==prior['artifacts'][n]==sha(STAGE/'native/Distribution'/n)
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/revive-equipped-evidence.json'],cwd=ROOT))
    for folder,old_folder,total,retained in ((STAGE,previous,550,547),(FOCUS,previous.with_name('revive-equipped-focus'),75,72)):
        old=old_folder/'birth-native-checks.log';assert sha(old)==parent['evidence'][str(old.relative_to(ROOT))]
        expected=markers(old);assert sum(expected.values())==retained
        expected['npc-prevent-heal-check']+=1;expected['npc-prevent-heal-reload-check']+=2
        assert markers(folder/'birth-native-checks.log')==expected and sum(expected.values())==total
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'npc-prevent-heal-fixtures.json').read_text());assert saved['Generation']==2 and all(saved[k]>0 for k in ('Owner','Revive','Golem','Material','Weapon','ReviveLife','GolemLife'))
        assert len({saved['Owner'],saved['Revive'],saved['Golem']})==3 and saved['Material']!=saved['Weapon']and len(saved['Binding'])==64
    proof_path=ROOT/'tools/reference/npc-prevent-heal-source-evidence.json';proof=json.loads(proof_path.read_text())
    assert proof['cases']==3960 and proof['currentWriter']==dict(property='noheal',function=1,stat='item_preventheal',id=117,registeredEvents=[])
    for section in ('inputs','artifacts'):
        for path,digest in proof[section].items():assert sha(ROOT/path)==digest,path
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-2.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
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
    files=[Path(__file__).resolve(),*(ROOT/p for p in sorted(sources)),proof_path,ROOT/'tools/probe_npc_prevent_heal.py',
        *(ROOT/'tests'/n for n in ('check_npc_prevent_heal.py','check_npc_prevent_heal_focus.py')),
        *(ROOT/'docs'/n for n in ('npc-prevent-heal-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-2.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','content-build-2.log','integration-build-2.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','npc-prevent-heal-fixtures.json')),
        *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','npc-prevent-heal-fixtures.json'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='NPC-held Prevent Monster Heal retains raw stat117 and admits ordinary original-item paths without applying the player-only effect',
        verified=dict(nativeMarkers=550,focusedMarkers=75,parentMarkersRetained=547,bootsPerCohort=3,actualReloadsPerCohort=2,connectedWorldSavesPerCohort=3,
            completeReferenceHelperCases=3960,actualWeapon='The Grim Reaper',originalReviveAndIronMaterial=True,learnedQuickCastPayment=True,
            rawStatAndItemPreserved=True,independentCorpseLoot=True,npcContactDamageWithoutNewOrRefreshedPmh=True,noOwnerForwarding=True,
            playerItemPositiveControl=True,noNewSubscriptionsOrReleaseRng=True,otherConditionalRefusalsRetained=True,
            blacktongueContactRetained=True,originalBodiesWoundsAndItemsReloaded=True,pythonTests=71,auditFingerprints=[1012,21],namedDecisionsUnchanged=573,
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),artifacts=tested['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['The PMH interpretation follows the complete older helper and pinned current item mapping; current executable NPC behavior remains unobserved.',
            'The predecessor broader pending-packet reload failure remains unresolved; its assertions and detailed diagnostics remain in the retained suite.',
            'Other item conditions,procs,unmapped bodies and source/world/recipient/PvP/client paths remain explicit.',
            'Bone Prison ground-click acquisition remains deferred because its exact client search policy is not established.',
            'No class complete, deployment or final artwork; effects/audio and talent/hotkey icons remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/npc-prevent-heal-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('NPC PMH:3960source controls;550broader/75focused;547parent markers retained;real learned Revive/Iron originals and player effect through two reloads;installation preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
