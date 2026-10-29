#!/usr/bin/env python3
"""Bind the item maximum-only correction to source, real weapons and saves."""
import argparse
import hashlib
import json
import re
import subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/item-maximum-operations'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def record(check=False,full_class=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
    _,prior=frozen_receipt('f69671a','tools/reference/npc-item-suppliers-native-evidence.json')
    assert prior['verified']['passMarkers']==207 and prior['verified']['combinedRegressionMarkers']==1184
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==210
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    pattern=r'\] ([A-Za-z0-9-]+): passed'
    before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/npc-item-suppliers/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
    assert sum(before.values())==207 and not before-after and after-before==Counter({'item-maximum-operations-check':1,'item-maximum-operations-reload-check':2})
    assert log.count('Maximum item damage:35280complete-source delta comparisons/6480separate-rounding controls;Reaver177-514/Eaglehorn45-313at99,actual player packets/strikes,original held Reaver177-368at declared37,unchanged minimum/general ED and inactive-item controls pass;full NPC graph remains gated')==1
    assert log.count('Maximum item damage:actual saved Reaver/Eaglehorn player weapons and original privately held Reaver retain complete rolls,serials,ownership,local endpoints and FP256physical ranges through reload')==2
    # The obsolete op5 rejection is intentionally replaced; all retained numeric
    # contexts and the original item persistence controls still run.
    assert log.count('and separate op5supplier pass;full NPC stat graph/combat remains gated')==1
    for filename in ['item-maximum-operations-source-evidence.json','item-maximum-bases-source-evidence.json']:
        p=ROOT/'tools/reference'/filename;source=json.loads(p.read_text());artifacts.append(p)
        for group in ['inputs','artifacts']:
            for name,digest in source[group].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
        if filename=='item-maximum-operations-source-evidence.json':
            assert source['verified']['sourceQueryContexts']==35280 and source['verified']['sourceWriterContexts']==48 and source['verified']['ownedComparedFields']==3318
            assert source['verified']['combinedPercentageDifferences']==6480 and source['verified']['integerBasePercentageDifferences']==720
            assert len(source['compiledUnchangedRoutineHashes'])==4 and not source['currentExecutableParity']
        else:assert source['verified']==dict(bases=3,ownedComparedFields=39,unexplainedDifferences=0)
    assert not (ROOT/'.local/class-skills/item-maximum-operations-source/sanitizer.log').read_bytes()
    assert not (ROOT/'.local/class-skills/item-maximum-operations-source/compiler.log').read_bytes()
    saved=json.loads((STAGE/'focused-fixtures/item-maximum-operations-fixtures.json').read_text())
    assert len(saved['Players'])==2 and len({p['Item'] for p in saved['Players']}|{saved['Npc']['Item']})==3
    for s,code,low,high,fpmin,fpmax in zip(saved['Players'],['7ga','6l7'],[177,45],[514,313],[142732,37440],[414489,260416]):
        gear=json.loads(s['Gear']);assert gear['BaseCode']==code and gear['Identified'] and gear['Roll']['ItemLevel']==99
        assert (s['Minimum'],s['Maximum'],s['SkillMinimum256'],s['SkillMaximum256'])==(low,high,fpmin,fpmax) and s['Durability']>0
    held=saved['Npc'];assert held['Owner']==saved['Players'][0]['Owner'] and (held['Minimum'],held['Maximum'])==(177,368) and json.loads(held['Gear'])['BaseCode']=='7ga'
    full=None
    if full_class:
        full=inventory.validate_inventory(STAGE/'class-inputs.json');assert full['artifacts']==inputs['artifacts'] and full['copiedData']==inputs['copiedData']
        text=(STAGE/'class-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==1184
        assert text.count('Shutting down')==3 and text.count('Writing world save snapshot done')==35
        before_full=Counter(re.findall(pattern,(ROOT/'.local/class-skills/npc-item-suppliers/class-native-checks.log').read_text()))
        before_named=Counter(re.findall(pattern,(ROOT/'.local/class-skills/npc-item-suppliers/named-native-checks.log').read_text()))
        assert Counter(re.findall(pattern,text))==before_full+before_named and sum(before_full.values())==1181 and sum(before_named.values())==3
        artifacts += [STAGE/n for n in ['class-inputs.json','class-native-checks.log','full-run.log']]
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        if full_class:
            q=STAGE/('full-'+name);assert a==json.loads(q.read_text());artifacts.append(q)
        assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
        audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
    old=json.loads(subprocess.check_output(['git','show','f69671a:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions'] and [len(a['InputHashes']) for a in audits]==[728,21]
    handpath=ROOT/'tools/reference/weapon-property-hand-evidence.json';hand=json.loads(handpath.read_text());projection=hand['runtimeProjection']
    assert sha(ROOT/projection['path'])==projection['sha256'] and projection['scaledTargets']['dmg%/lvl']=='dmg-max%'
    rules=json.loads((ROOT/'data/Sanctuary/Balance/class-weapon-properties.json').read_text());assert next(r for r in rules if r['code']=='dmg-max%')['damageRelated']
    artifacts += [handpath,ROOT/'data/Sanctuary/Balance/class-weapon-properties.json',ROOT/'tools/build_weapon_hand_rules.py']
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    mainpath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainpath.read_text());assert len(main['installedArtifacts'])==106
    for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
    for name in ['class-skill-status.json','skill-effects.json','skill-icons.json']:
        p=ROOT/'data/Sanctuary/Balance'/name;assert sha(p)==hashlib.sha256(subprocess.check_output(['git','show','f69671a:'+str(p.relative_to(ROOT))],cwd=ROOT)).hexdigest();artifacts.append(p)
    artifacts += [STAGE/n for n in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log']]
    artifacts += list((STAGE/'focused-fixtures').glob('*fixtures.json'))
    artifacts += [ROOT/n for n in ['tools/record_item_maximum_operations.py','tests/check_item_maximum_operations.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]+[mainpath]
    result=dict(referenceVersion='3.3.93847',component='Separate level-scaled item maximum damage',installed=False,handlerEnabled=False,currentExecutableParity=False,fullNpcStatGraphAccepted=False,sourceNpcLevelMappingAccepted=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],
        verified=dict(passMarkers=210,retainedMarkers=207,worldBoots=3,actualWorldReloads=2,sourceMaximumDeltaContexts=35280,sourcePropertyWriterContexts=48,sourceStatRows=368,sourceOwnedFields=3318,realWeaponBases=3,additionalOwnedBaseFields=39,separateRoundingControls=6480,unresolvedLegacyBaseRoundingControls=720,maximumOnlyChannel='dmg-max%',selectedHandClassification=True,minimumDamagePreserved=True,generalDamagePoolExcluded=True,playerWeaponPackets=True,actualNativeWeaponStrikes=True,sourceNpcLevelMapping=False,savedOriginalItems=3,reaverLevel99=[177,514],eaglehornLevel99=[45,313],declaredRecipient37Reaver=[177,368],reaverSkillEndpoints256=[142732,414489],eaglehornSkillEndpoints256=[37440,260416],unchangedOrdinaryBase=[7,14],unidentifiedBrokenUnequippedControls=True,negativeAndOverflowRefused=True,fullClassMarkers=1184 if full_class else None,fullClassBoots=3 if full_class else None,fullClassReloads=2 if full_class else None,fullClassSaveSnapshots=35 if full_class else None,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[728,21],unchangedMainInstallationArtifacts=106),
        inputInventory=inputs,fullInputInventory=full,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='f69671a',receipt='tools/reference/npc-item-suppliers-native-evidence.json'),
        established=[
            'dmg%/lvl now projects to a distinct dmg-max%numeric channel with source damageRelated hand classification. Its integer level-scaled percent applies to the original local maximum in a separate op5step after ordinary ED and flat maximum;it cannot raise minimum damage or enter the general player/skill ED pool. Ordinary integer base-ED behavior is retained. The former entire-item NPC reader rejection is removed for this verified numeric feature only.',
            'Native isolated op5deltas match35280complete-source query compositions across primary,secondary and throwing channels,with6480controls that reject combining percentages. Owned tables corroborate3318stat/property fields plus39base fields for three real weapons. Four complete unchanged source query/writer bodies and the exact dependency-assembly block remain bound through the independent source receipt. Its48writer controls are source evidence,not native property-generation certification.',
            'Actual admitted Reaver and Eaglehorn weapons produce177-514and45-313at level99with constant minima over1/37/99. Native getters,ordinary weapon packets,FP256skill endpoints and actual prepared weapon strikes consume the correction. Identification,equip/removal,broken-state and plain7-14Broadsword controls pass. A separate original Reaver attaches privately to an existing persistent holder and reads177-368at declared recipient37without cloning or rerolling;this does not establish a real golem level or actor.',
            'Two actual reloads preserve all three original item serials,parents,owners,full gear rolls,durability,local damage and the saved player FP256endpoints. All207previous markers remain;the deliberately obsolete op5refusal control now checks separate verified suppliers. Seventy Python checks,573unchanged named eligibility decisions and106unchanged main installation artifacts pass. '+('The identical package additionally passes1184full-class checks over three boots/two reloads/35save snapshots,including the named gate.' if full_class else 'A broader class run is separate;no new full-class acceptance is claimed in this focused receipt.')
        ],remaining=[
            'Full NPC item-stat graph/posting,base/ethereal initialization,recipient-level mapping,callbacks and all actual life/defense/damage/resistance/speed/proc consumers still need integration. Existing legacy double-versus-integer base-ED differences720and current executable rounding remain explicitly unresolved. By-time ops6/7and their asymmetric current channel targets,negative/overflowing ranges and property17parameter-zero fallback generation are outside this native acceptance.',
            'Iron Golem still needs complete body/AI,paid cast/cap/placement,owner/facet/logout/death/respec and bound-item lifecycle. Skill-dependent item events/charges/oskills/auras remain in the approved deferred return ledger. Effects/audio and talent/hotkey icons remain separate identified placeholders;no class is complete.'
        ])
    path=ROOT/'tools/reference/item-maximum-operations-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Maximum item damage:210focused/70Python;full-class='+str(full_class)+';real Reaver/Eaglehorn and original item reloads pass;full NPC graph unfinished.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');p.add_argument('--full-class',action='store_true');a=p.parse_args();record(a.check,a.full_class)
