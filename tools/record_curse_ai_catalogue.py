#!/usr/bin/env python3
"""Bind current-indexed admission and all represented eligible family casts/reloads."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/curse-ai-catalogue'
PARENT='059f0e30bc7acc7a1f16ed38893d4f7f71585913'
sha=inventory.sha
def load(p):return json.loads(p.read_text())
def historical(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))

def record(check=False):
    inventory.STAGE=STAGE
    native,audit=[inventory.validate_inventory(STAGE/n)for n in('birth-inputs.json','audit-inputs.json')]
    for n in('birth-inputs.json','audit-inputs.json'):snapshot(STAGE/'pre-build-1.json',STAGE/n)
    assert native['artifacts']==audit['artifacts']and native['copiedData']==audit['copiedData']
    b=load(STAGE/'pre-build-1.json')['inputs']
    parent=load(ROOT/'.local/class-skills/curse-ai-families/pre-build-2.json')['inputs']
    sources={'src/Sanctuary.Content/SourceTerror.cs','tests/Sanctuary.Integration/CurseAiFamilyChecks.cs'}
    assert {p for p in set(parent['sourceInputs'])|set(b['sourceInputs'])if parent['sourceInputs'].get(p)!=b['sourceInputs'].get(p)}==sources
    assert parent['projectInputs']==b['projectInputs']
    assert {p for p in set(parent['copiedData'])|set(b['copiedData'])if parent['copiedData'].get(p)!=b['copiedData'].get(p)}=={'Data/Sanctuary/Balance/blind-ai-rules.json','Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/curse-ai-families.json'}
    parent_proof=historical('tools/reference/curse-ai-families-native-evidence.json')
    assert sha(ROOT/'.local/class-skills/curse-ai-families/pre-build-2.json')==parent_proof['evidence']['.local/class-skills/curse-ai-families/pre-build-2.json']
    for n in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert native['artifacts'][n]==parent_proof['artifacts'][n]==sha(STAGE/'native/Distribution'/n)
    before=historical('data/Sanctuary/Balance/class-skill-status.json');after=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    assert {x['talentId']:[k for k in x if x[k]!=y[k]]for x,y in zip(before['skills'],after['skills'],strict=True)if x!=y}=={'necromancer.dimvision':['remaining'],'necromancer.terror':['remaining']}
    assert not after['completedClasses']and sum(r['implementation']=='handler-present'for r in after['skills'])==191
    catalogue=load(ROOT/'tools/reference/curse-ai-catalogue-source-evidence.json')
    source=load(ROOT/'tools/reference/curse-ai-catalogue-dispatch-evidence.json')
    assert catalogue['classicDescriptors']==148 and catalogue['callbackNullPatternsChecked']==444 and len(catalogue['selected'])==92
    assert [(r['index'],r['legacy'],r['current'])for r in catalogue['changedFlags']]==[(15,4,5),(23,1,3),(52,5,6)]
    assert source['cases']==4416 and source['families']==92 and len(source['routines'])==2 and source['sourceBodiesUnchanged']
    for proof in(catalogue,source):
        for p,h in proof['evidence'].items():assert sha(ROOT/p)==h,p
    assert not(ROOT/'.local/class-skills/curse-ai-catalogue-source/sanitizer.log').read_text()
    families=load(ROOT/'data/Sanctuary/Balance/curse-ai-families.json')
    assert set(families)=={r['family']for r in catalogue['selected']}and sha(ROOT/catalogue['runtimePath'])==catalogue['runtimeSha256']
    rule_proof=load(ROOT/'tools/reference/blind-ai-rule-evidence.json')
    assert rule_proof['ruleCount']==499 and rule_proof['families']==families
    assert sha(ROOT/rule_proof['runtimePath'])==rule_proof['runtimeSha256']and sha(ROOT/rule_proof['sourcePath'])==rule_proof['sourceSha256']
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'blind-policy-check':3,
        'blind-world-live-result':3,'grim-ward-transport-live-result':3,'howl-result':3,'taunt-result':3})
    for family in('curse-ai-catalogue','curse-ai-families','blind-world','dim-vision-cast','terror-cast','howl','taunt','grim-ward-transport'):
        expected[family+'-check']=1;expected[family+'-reload-check']=2
    for family in('dim-vision-cast','terror-cast'):
        for mode in('right','quick'):
            expected[family+'-'+mode+'-check']=3;expected[family+'-'+mode+'-result']=3
    assert sum(expected.values())==72 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed'not in log and' ERR]'not in log and log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    saved=load(STAGE/'curse-ai-catalogue-fixtures.json')
    assert saved['Generation']==2 and saved['Life']%256!=0 and saved['Mana']>0 and len(saved['Actors'])==114
    assert len({a['Serial']for a in saved['Actors']})==114
    assert Counter(a['Curse']['Ability']for a in saved['Actors'])==Counter({83:57,86:57})
    for actor in saved['Actors']:
        assert actor['Life']%256!=0 and actor['Curse']['Source']==saved['Owner']
        if actor['Blind'] is not None:
            assert actor['Blind']['Thinks']==3 and actor['Blind']['Draws']==(3 if actor['Blind']['Focus']==saved['Owner']else 0)and actor['Blind']['Fault']is None
        else:assert actor['Curse']['Terror']['Started']and actor['Curse']['Terror']['Goal']is not None
    for n in('content-build-1.log','integration-build-1.log'):
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
    files=[Path(__file__).resolve(),*(ROOT/p for p in sources),*(ROOT/'tools'/n for n in('build_blind_ai_rules.py','probe_curse_ai_catalogue.py','build_curse_ai_catalogue.py','build_skill_roadmap.py')),
        *(ROOT/'tools/reference'/n for n in('curse-ai-transitions-source-evidence.json','curse-ai-catalogue-source-evidence.json','curse-ai-catalogue-dispatch-evidence.json','blind-ai-rule-evidence.json')),
        ROOT/'tests/check_curse_ai_catalogue.py',ROOT/'tests/check_iron_golem_birth.py',ROOT/'data/Sanctuary/Balance/curse-ai-families.json',ROOT/'data/Sanctuary/Balance/blind-ai-rules.json',ROOT/'data/Sanctuary/Balance/class-skill-status.json',
        *(ROOT/'docs'/n for n in('curse-ai-catalogue-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(p for p in STAGE.iterdir()if p.is_file()and p.name!='execution-state.json'and not p.name.startswith('receipt-'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,scope='Generated current-indexed ordinary curse AI catalogue and all represented eligible families',
        installed=False,goalComplete=False,completedClasses=[],builds=dict(content=1,integration=1),nativeRun=1,auditRun=1,
        verified=dict(sourceDispatchCases=4416,completeSourceRoutines=2,ordinaryAiFamilies=92,currentTimingRows=499,deferredExitRefused=True,
            nativeMarkers=72,boots=3,actualReloads=2,saves=3,originalNewFamilyRecipients=114,representedEligibleFamilies=57,learnedCastReleasePayment=True,originalWoundsManaCurseStreamsRetained=True,
            retainedBlindFearHowlTauntGrimWard=True,pythonTests=71,auditFingerprints=[1021,21],namedDecisionsUnchanged=573,handlers=191,
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,clientPatchesUnchanged=40),
        artifacts=native['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(files))},
        limitations=['Complete older lookup/transition dispatcher with declared function identity/initializer services;not execution of the current D2R binary or every referenced AI body.',
            'New-family native checks use explicit disposable configured encounter identities,not new native UO taxonomy assignments.',
            'Saved original curse/AI observation deadlines are held. Retained learned input/natural expiry/live movement checks remain separate;no unheld concurrent save or old-binary migration certificate.',
            'Deferred callbacks,unmapped species,escape-state and displacement adapters,source/current room/path/scan/ties/global RNG/events,ownership/conversion and graphical-client equivalence remain open.',
            'No new handler,class completion,native/client patch,deployment or generated asset. Effects/audio and icons remain separate.'])
    target=ROOT/'tools/reference/curse-ai-catalogue-native-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==render
    else:target.write_text(render)
    print('Curse AI catalogue:4416 source contexts,92 families/499 timing rows,114 original recipients,72 native markers/3boots/2reloads;installed build and saves preserved.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
