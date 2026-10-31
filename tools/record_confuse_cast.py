#!/usr/bin/env python3
"""Bind Confuse neutral-alignment/override verification without certifying full AI parity."""
import argparse
from collections import Counter
import json
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/confuse-cast'
sha=inventory.sha

def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/(n+'-inputs.json')) for n in ['birth','audit']}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(n+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/terror-cast-native-evidence.json';prior=json.loads(parent.read_text());ref=prior['inputInventories']['audit'];assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text());before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    added=set(after)-set(before);changed={p for p in before if after[p]!=before[p]}
    assert added=={'src/Sanctuary.Content/SourceMind.cs','tests/Sanctuary.Integration/ConfuseCastChecks.cs'}
    assert changed=={'src/Sanctuary.Content/NecromancerCurseCasts.cs','src/Sanctuary.Content/NecromancerEffects.cs','src/Sanctuary.Content/PaladinConversion.cs','src/Sanctuary.Content/GraveyardUndead.cs','tests/Sanctuary.Integration/DimVisionCastChecks.cs','tests/Sanctuary.Integration/TerrorCastChecks.cs','tests/Sanctuary.Integration/ClassSkillChecks.cs'}
    assert set(before)<=set(after) and inputs['birth']['copiedData']==old['copiedData']
    for n in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    expected=Counter({'iron-golem-birth-check':3,'class-skills-ai-check':3,'class-skills-ai-result':3,'blind-policy-check':3,'howl-result':3,'taunt-result':3,'blind-world-live-result':3,'mind-blast-result':1,'mind-blast-reload-result':2})
    for n in ['iron-golem-items','npc-resource-callbacks','confuse-cast','terror-cast','dim-vision-cast','blind-world','howl','taunt','mind-blast']:expected[n+'-check']=1;expected[n+'-reload-check']=2
    for n in ['confuse-cast-right','confuse-cast-quick']:expected[n+'-check']=expected[n+'-result']=3
    text=(STAGE/'birth-native-checks.log').read_text();assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==63
    assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for n,count in [('named-item-runtime-audit.json',853),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/n).read_text());b=json.loads((ROOT/'.local/class-skills/terror-cast'/n).read_text());assert a==json.loads((ROOT/'tools/reference'/n).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==count and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==1
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text()
    s=json.loads((STAGE/'confuse-cast-fixtures.json').read_text());assert s['Generation']==2 and s['Curse']['Ability']==87 and s['Curse']['Rank']==21 and s['Mind']['Mode']==3 and s['Mind']['Draws']==0
    assert s['Curse']['Source']==s['Owner'] and s['Mind']['UntilUtc']==s['Curse']['UntilUtc'] and s['Old']['Rank']==1
    review_path=ROOT/'tools/reference/confuse-source-review.json';review=json.loads(review_path.read_text());assert review['referenceVersion']=='3.3.93847' and len(review['reviewedNotExecuted'])==10
    for p,h in review['inputs'].items():assert sha(ROOT/p)==h,p
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    docs=['docs/confuse-cast-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md']
    files=[parent,review_path,Path(__file__),*[ROOT/p for p in added|changed],*[ROOT/p for p in docs],ROOT/'tools/review_confuse_source.py',ROOT/'tests/check_confuse_cast.py',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/export_runtime_audits.py',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/first-loop-affix-audit.json']
    files += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{audit_run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','named-readiness.log','confuse-cast-fixtures.json','terror-cast-fixtures.json','dim-vision-cast-fixtures.json','blind-world-fixtures.json','howl.json','taunt.json','mind-blast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Confuse learned casting, neutral alignment and independent target override',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=63,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[853,21],unchangedInstalledArtifacts=106,connectedRightQuickCasts=6,autonomousNpcCombatChecks=6,naturalTenSecondExpiries=6,mana256=3328,reviewedSourceBodies=10,compiledNewSourceBodies=0,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str((STAGE/(n+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(n+'-inputs.json'))) for n in inputs},changedSourceHashes={p:after[p] for p in sorted(added|changed)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'Confuse has13mana release,source radius6+(rank-1),base250+50*(rank-1)frames,ownerfacet divisor1/2/4beforecurse resistance. Acceptedcurse createsNeutralalignment andindependentmode3override deadline;ordinarycurse replacement restoresalignment butkeepsseparateoverride/RNG.',
            'One source-algorithm lowbit selects temporaryopposingalignment usingnative acquisitionservices;alignmentrestoresafterscan andemptyresult clearsmode3. Bothplayer/monsterfactions,neutralattackability,noplayerownership,independentlease,activereapplication/AIcurse rejection andordinaryreplacement controls pass.',
            'ExistingGraveyardUndead enemyoverride nowuses sharedallegiancehook insteadofbypassingitwithdirectConversion. MindBlastEvil→GoodConversion guard nowrejectsNeutralConfuse;existingMindBlast/Howl/Taunt/Terror/DimVision/blind/classAI/item/resourcechecks pass. OlderDimVision/TerrorConfusereplacement expectations andspawn-difficulty assertion corrected tosourceNeutral/facetcontracts.',
            'Sixrealright/quickcastsautonomouslydamageNPCs whileremainingattackable,thenexpirevisiblecurse/overrideafter10seconds. Theartificial50000lifevictimusesexistingPMHfixturecontroltopreventscaledcustomregenerationmaskinghits;gameplayregenerationunchanged. ExactMindseed/deadline/state andlegacyJSONshapesretainacross2reloads;the reloadfixture advancesRNGarithmetically whileinternal,not viaautonomouspost-loadAI.63markers/3boots/2reloads/3saves,4audits/71Python/573named,853/21fingerprints andcleanbuilds pass;data/kernel/installed106unchanged.'],
        remaining=[
            'The10completelegacybodies arestaticallyreviewed,notnewlycompiled/executed. Current3.3.93847executable,fullsourceVM/factory/node/alignment/target/eventbehaviorremainuncorroborated;no skill/classcompletionclaim.',
            'Nativeacquisitiongate/scan/LOS/threat/ties/mode/path/cadence andper-componentRNGstream remainadapters. Unmappedcandidatealignment,legacyConfuseindependentdeadline migration,sourceTerror/Blind/convertedcoexistence,damage/XPcredit andautonomouspost-loadtargetselection remainunsupported/uncertain.',
            'Attractstillusestheearliercontinuousradiusadapter;do59independentneighboroverrides/minionlinks/factory-sideeffectorderneedimplementationnext. No full521extendedcohortacceptance. Approvedpetupgrades/safeareaparkingremainqueued.',
            'Confusecurseconfuse/cursecast effects/audio andtree/hotkeyicons remainseparateidentifiedplaceholders. Sharedmechanics-firstworkflowisnowdocumentedinroadmap.'])
    dest=ROOT/'tools/reference/confuse-cast-native-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert dest.read_text()==render
    else:dest.write_text(render)
    print('Confuse:63relevant checks/6real NPC-combat casts/2reloads;source/world/credit gaps explicit.')

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--audit-run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
