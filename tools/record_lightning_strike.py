#!/usr/bin/env python3
"""Bind learned Lightning Strike's approved opening and shared-chain regression."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/lightning-strike'
PARENT='de793cabd32068a6fdcfaeb70900bcb32e30eae5'
sha=inventory.sha
def load(p):return json.loads(p.read_text())
def historical(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))

def record(check=False):
    inventory.STAGE=STAGE
    native,audit=[inventory.validate_inventory(STAGE/n)for n in('birth-inputs.json','audit-inputs.json')]
    for n in('birth-inputs.json','audit-inputs.json'):snapshot(STAGE/'pre-build-10.json',STAGE/n)
    assert native['artifacts']==audit['artifacts']and native['copiedData']==audit['copiedData']
    b=load(STAGE/'pre-build-10.json')['inputs']
    first=load(STAGE/'pre-build-1.json')['inputs']
    # Only fixture diagnostics/isolation/announcement order changed after content1.
    fixtures={'tests/Sanctuary.Integration/ChainChildSearchChecks.cs','tests/Sanctuary.Integration/LightningStrikeChecks.cs'}
    for number in range(1,11):
        prior=load(STAGE/f'pre-build-{number}.json')['inputs']
        excluded=fixtures|({'src/Sanctuary.Content/ChainLightning.cs'}if number==9 else set())
        assert {k:v for k,v in prior['sourceInputs'].items()if k not in excluded}=={k:v for k,v in b['sourceInputs'].items()if k not in excluded}
        assert prior['projectInputs']==b['projectInputs']and prior['copiedData']==b['copiedData']
    parent=load(ROOT/'.local/class-skills/curse-ai-catalogue/pre-build-1.json')['inputs']
    sources=fixtures|{'src/Sanctuary.Content/ActionTiming.cs','src/Sanctuary.Content/AmazonCombat.cs','src/Sanctuary.Content/AmazonMelee.cs','src/Sanctuary.Content/ChainLightning.cs'}
    assert {p for p in set(parent['sourceInputs'])|set(b['sourceInputs'])if parent['sourceInputs'].get(p)!=b['sourceInputs'].get(p)}==sources
    assert parent['projectInputs']==b['projectInputs']
    assert {p for p in set(parent['copiedData'])|set(b['copiedData'])if parent['copiedData'].get(p)!=b['copiedData'].get(p)}=={'Data/Sanctuary/Balance/necromancer-summons.json','Data/Sanctuary/Balance/class-skill-status.json'}
    parent_proof=historical('tools/reference/curse-ai-catalogue-native-evidence.json')
    assert sha(ROOT/'.local/class-skills/curse-ai-catalogue/pre-build-1.json')==parent_proof['evidence']['.local/class-skills/curse-ai-catalogue/pre-build-1.json']
    for n in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert native['artifacts'][n]==parent_proof['artifacts'][n]==sha(STAGE/'native/Distribution'/n)
    before=historical('data/Sanctuary/Balance/class-skill-status.json');after=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    changes={x['talentId']:[k for k in x if x[k]!=y[k]]for x,y in zip(before['skills'],after['skills'],strict=True)if x!=y}
    assert changes=={'amazon.lightningstrike':['implementation','verification','remaining']}
    assert not after['completedClasses']and sum(r['implementation']=='handler-present'for r in after['skills'])==192
    old_rows=historical('data/Sanctuary/Balance/necromancer-summons.json');new_rows=load(ROOT/'data/Sanctuary/Balance/necromancer-summons.json')
    assert [r for r in new_rows if r['key']!='lightningstrike']==old_rows
    row=next(r['fields']for r in new_rows if r['key']=='lightningstrike')
    assert row['pSrvDoFunc']==1 and row['pSrvHitFunc']==12 and row['NextDelay']==4 and row['Range']==25
    for n,count in(('lightning-strike-damage-source-evidence.json',36720),('chain-child-filter-source-evidence.json',32)):
        proof=load(ROOT/'tools/reference'/n);assert proof['cases']==count
        for p,h in proof['evidence'].items():assert sha(ROOT/p)==h,p
    for name in('lightning-strike-damage-source','chain-child-filter-source'):
        assert not(ROOT/'.local/class-skills'/name/'sanitizer.log').read_text()
    decision=load(ROOT/'tools/reference/lightning-strike-sanctuary-decision.json')
    assert decision['decision']=='approved Sanctuary adaptation'and not decision['isD2RObservation']
    expected=Counter({'iron-golem-birth-check':3,'weapon-sequence-rate-check':3,'chain-child-search-check':3,
        'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
        'charged-strike-native-bolts':1,'charged-strike-expiry-start':1,'charged-strike-expiry':1,'charged-strike-result':1,
        'charged-strike-reload-result':2,'slow-missiles-component-result':1,'player-chain-reload-result':2,
        'mage-chain-live-result':3,'player-chain-offline-result':3})
    for family in('jab','power-strike','charged-strike','slow-missiles-component','charged-strike-velocity','lightning-strike','player-chain','mage-chain'):
        expected[family+'-check']=1;expected[family+'-reload-check']=2
    for family in('lightning-strike','player-chain'):
        for mode in('right','quick'):
            expected[family+'-'+mode+'-check']=3;expected[family+'-'+mode+'-result']=3
    assert sum(expected.values())==78 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed'not in log and' ERR]'not in log and log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    saved=load(STAGE/'lightning-strike-fixtures.json');flight=saved['Flight']
    assert saved['Generation']==2 and saved['Life']%256!=0 and saved['Mana']%256!=0
    assert saved['Owner']==flight['Owner']and saved['Primary']==flight['StrikeContext']['OpeningPrimary']and saved['Secondary']==flight['AimTarget']
    assert flight['StrikeContext']['Profile']=='sanctuary-lightning-strike-opening-v1'and flight['Remaining']==flight['Rank']+1
    assert flight['Frame']>=6 and flight['X']>saved['Point']['X']+1 and flight['PlayerPacket']['Seed']>0
    assert flight['Min256']==256 and flight['NpcCaster']==0 and saved['Weapon']>=0x40000000
    for n in('content-build-3.log','integration-build-10.log'):
        assert all(t in(STAGE/n).read_text()for t in('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for n,count in(('named-item-runtime-audit.json',1021),('first-loop-affix-audit.json',21)):
        fresh=load(STAGE/n);old=historical('tools/reference/'+n);assert fresh==load(ROOT/'tools/reference'/n)and len(fresh['InputHashes'])==count
        for p,h in fresh['InputHashes'].items():assert sha(ROOT/p)==h,p
        skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in fresh.items()if k not in skip}=={k:v for k,v in old.items()if k not in skip}
    assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573
    assert markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    for n,word in(('python-tests.log','Ran 71 tests'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):
        assert word in(STAGE/n).read_text()
    assert '\nOK\n'in(STAGE/'python-tests.log').read_text()
    installed=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for key,relative,count in(('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative
        assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for section in('patchHashes','sharedInputs'):
        for p,h in client[section].items():assert sha(ROOT/p)==h,p
    files=[Path(__file__).resolve(),*(ROOT/p for p in sources),*(ROOT/'tools'/n for n in('build_class_skills.py','build_skill_roadmap.py','probe_chain_child_filter.py','probe_lightning_strike_damage.py','probe_lightning_strike_reference.py')),
        *(ROOT/'tools/reference'/n for n in('lightning-strike-preflight.json','lightning-strike-engine-evidence.json','lightning-strike-damage-source-evidence.json','chain-child-filter-source-evidence.json','lightning-strike-sanctuary-decision.json')),
        ROOT/'tests/check_lightning_strike.py',ROOT/'tests/check_charged_strike_velocity.py',ROOT/'tests/check_iron_golem_birth.py',ROOT/'data/Sanctuary/Balance/necromancer-summons.json',ROOT/'data/Sanctuary/Balance/class-skill-status.json',
        *(ROOT/'docs'/n for n in('lightning-strike-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(p for p in STAGE.iterdir()if p.is_file()and p.name!='execution-state.json'and not p.name.startswith('receipt-'))]
    # Preserve failed-run transcripts, matching fixtures and tested binaries too.
    for number in range(1,10):files.extend(p for p in(STAGE/f'attempt-{number}').rglob('*')if p.is_file())
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Learned Lightning Strike approved opening; shared player/Mage child-search terrain regression',installed=False,goalComplete=False,completedClasses=[],
        builds=dict(content=3,integration=10),nativeRun=10,auditRun=2,
        verified=dict(sourceDamageCases=36720,sourceFilterCases=32,retainedStartCases=2304,retainedChildCases=2560,
            nativeMarkers=78,boots=3,actualReloads=2,saves=3,cornerWorldContextsPerBoot=4,
            originalWoundsManaWeaponBindingAndFlightRetained=True,soloNoChainOrFactoryDraw=True,meleeMissAndLethalEmission=True,
            rightAndQuickLearnedInput=True,pythonTests=71,auditFingerprints=[1021,21],namedDecisionsUnchanged=573,handlers=192,
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,clientPatchesUnchanged=40),
        artifacts=native['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(files))},
        limitations=['The user approved the opening adaptation. Original hidden first-bolt/primary re-hit/Y-Y/solo behavior remains unresolved reference data, not a blocker to this adaptation.',
            'Complete older source routines use explicit formula/stat/equipment/RNG/world services. No execution of the modern D2R damage or collision engine is claimed.',
            'Literal calc1 ED20 overlap, modern geometry/filter/NHD/global RNG/event/owner/PvP and graphical-client equivalence remain open.',
            'Original pre-contact flights use explicitly held inspection deadlines. Two actual reloads advance those same flights without replayed damage; no old-binary Lightning Strike writer or unheld concurrent-save certificate.',
            'Runs1/2 exposed corner-fixture geometry,3/4 exposed nearby independent fixture enemies,5 exposed premature network-ready logging,6-9 exposed the50000life fixture zombie regenerating before observation. A temporary impact observer in build2/run9 confirmed positive actual damage and was removed; final production inputs match content1. Preserved diagnostics are not accepted test runs. Premature Python audit-staleness failures are retained.',
            'No complete class, native/client patch, deployment or generated asset. Final effects/audio and icons are recorded separately.'])
    target=ROOT/'tools/reference/lightning-strike-native-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==render,'Stale Lightning Strike acceptance receipt'
    else:target.write_text(render)
    print('Lightning Strike:approved opening,36720 damage/32 filter source contexts,78 native markers/3boots/2reloads;192 handlers,no class complete,installed build and saves preserved.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
