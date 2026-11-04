#!/usr/bin/env python3
"""Bind NPC allocation-time armor mutation, native A1 observation and persistent target state."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-armor-setup'
FOCUS=ROOT/'.local/class-skills/npc-armor-setup-focus'
PARENT='284a0c6bc6addb11619b0ac51599474025212c15'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    paths=[STAGE/'birth-inputs.json',FOCUS/'birth-inputs.json',STAGE/'audit-inputs.json']
    tested,focused,audit=[inventory.validate_inventory(p)for p in paths]
    for p in paths:snapshot(STAGE/'pre-build-7.json',p)
    assert tested['artifacts']==focused['artifacts']==audit['artifacts']and tested['copiedData']==focused['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/npc-defense-conditions';prior=json.loads((previous/'birth-inputs.json').read_text())
    assert tested['copiedData']==prior['copiedData']
    sources={'src/Sanctuary.Content/'+name+'.cs'for name in ('WeaponStartEffects','NpcArmorOnHit','NpcCombatEvents','ReviveEquipmentEvents','IronGolemContact','NpcEquipmentImpact','ReviveEquipmentImpact','NpcEquipmentDamage')}
    sources|={'tests/Sanctuary.Integration/'+name+'.cs'for name in ('ReviveEventChecks','ReviveNativeSwingChecks','NpcArmorSetupChecks')}
    assert {p for p in set(tested['sourceInputs'])|set(prior['sourceInputs'])if p.endswith('.cs')and tested['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}==sources
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][n]==prior['artifacts'][n]==sha(STAGE/'native/Distribution'/n)
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/npc-defense-conditions-native-evidence.json'],cwd=ROOT))
    for folder,old_folder,total,retained in ((STAGE,previous,559,553),(FOCUS,previous.with_name('npc-defense-conditions-focus'),84,78)):
        old=old_folder/'birth-native-checks.log';assert sha(old)==parent['evidence'][str(old.relative_to(ROOT))]
        expected=markers(old);assert sum(expected.values())==retained
        expected['npc-armor-setup-check']+=1;expected['npc-armor-setup-reload-check']+=2;expected['npc-armor-setup-live-result']+=3
        assert markers(folder/'birth-native-checks.log')==expected and sum(expected.values())==total
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'npc-armor-setup-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Rows'])==2
        assert all(saved[k]>0 for k in ('Owner','Revive','Golem','Material','Weapon'))and saved['Material']!=saved['Weapon']and len(saved['Binding'])==64
        assert {r['Source']for r in saved['Rows']}=={saved['Revive'],saved['Golem']}
        for row in saved['Rows']:
            assert all(row[k]>0 for k in ('Target','Life','SourceLife','Seed'))and row['Packet']['Impact']['Actor']==row['Source']and row['Packet']['Impact']['Target']==row['Target']
    smoke=ROOT/'.local/class-skills/npc-armor-setup-smoke';smoked=inventory.validate_inventory(smoke/'birth-inputs.json')
    snapshot(STAGE/'pre-build-7.json',smoke/'birth-inputs.json');assert smoked['artifacts']==tested['artifacts']and smoked['copiedData']==tested['copiedData']
    assert sum(markers(smoke/'birth-native-checks.log').values())==15 and ': failed'not in(smoke/'birth-native-checks.log').read_text()
    proof_path=ROOT/'tools/reference/npc-armor-setup-source-evidence.json';proof=json.loads(proof_path.read_text());assert proof['cases']==17280
    assert proof['currentWriter']==dict(property='dmg-ac',function=1,stat='item_damagetargetac',id=120,registeredEvents=[])and proof['currentStunPropertyWriters']==[]
    for section in ('inputs','artifacts'):
        for path,digest in proof[section].items():assert sha(ROOT/path)==digest,path
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-7.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',1013),('first-loop-affix-audit.json',21)):
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
    files=[Path(__file__).resolve(),*(ROOT/p for p in sorted(sources)),proof_path,ROOT/'tools/probe_npc_armor_setup.py',
        *(ROOT/'tests'/n for n in ('check_npc_armor_setup.py','check_npc_armor_setup_focus.py','check_npc_armor_setup_smoke.py')),
        *(ROOT/'docs'/n for n in ('npc-armor-setup-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-7.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','content-build-7.log','integration-build-7.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','npc-armor-setup-fixtures.json','content-build-1.log','integration-build-2.log')),
        *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','npc-armor-setup-fixtures.json')),
        *(smoke/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-5.log','npc-armor-setup-fixtures.json','native-run-1.log','failed-2/birth-native-checks.log','failed-3/birth-native-checks.log','failed-4/birth-native-checks.log'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Negative raw120 on qualified owned NPCs mutates persistent base armor during allocation before physical RNG;no registered or release-time effect',
        verified=dict(nativeMarkers=559,focusedMarkers=84,smokeMarkers=15,parentMarkersRetained=553,bootsPerCohort=3,actualReloadsPerCohort=2,connectedWorldSavesPerRetainedCohort=3,
            completeSourceFillCases=17280,actualWeapon='Stone Crusher',originalReviveAndIronMaterial=True,learnedQuickCastPayment=True,
            armorBeforeDamageRng=True,noMissBlockNoFillMutation=True,negativeStacksAndZeroFloor=True,cancelAndEscapeRetainSetup=True,
            releaseNoDuplicateArmor=True,ordinaryDamageAndCbRetained=True,unstagedRolledPacketRefused=True,
            actualNativeA1ArmorBeforeHpPerCohort=3,actualTargetArmorAndHpSaved=True,contactCodecNoReplay=True,
            sourceBodiesWoundsBindingsAndItemsReloaded=True,rawStunPositiveArmorAndLayeredArmorRefused=True,
            pythonTests=71,auditFingerprints=[1013,21],namedDecisionsUnchanged=573,installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        artifacts=tested['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['Runtime armor mutation reuses the existing base-defense loss bridge. Full current-executable/stat-list/derived-target/facet/owner/world equivalence remains unobserved.',
            'Source good alignment is admitted through qualified living player ownership;unmapped/unowned contexts refuse. Positive armor mutation,layered120 and raw66 packet fill remain unsupported.',
            'Saved prepared-contact data is an explicit fixture codec control,not a new live pending-swing serialization claim. The retained native-A1 family separately covers actual pending saves.',
            'The earlier equipped-Revive pending-packet reload failure remains unresolved;its retained assertions and diagnostics remain unchanged.',
            'Legacy typed damage shares the setup helper but is not a complete historical-binary/other-item-event certificate.',
            'No class complete,deployment or final artwork;effects/audio and talent/hotkey icons remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/npc-armor-setup-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('NPC armor setup:17280source contexts;559broader/84focused/15smoke;553parent markers retained;allocation-before-RNG,native A1 and persistent target armor through two reloads;installation preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
