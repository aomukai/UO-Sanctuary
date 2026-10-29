#!/usr/bin/env python3
"""Bind recursive item stat values to source metadata and scoped native evidence."""
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
STAGE=ROOT/'.local/class-skills/item-stat-values'
FROZEN='4ae424a'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def historical(name):return subprocess.check_output(['git','show',FROZEN+':'+name],cwd=ROOT)
def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
    _,prior=frozen_receipt(FROZEN,'tools/reference/item-stat-attachment-native-evidence.json')
    assert (prior['verified']['passMarkers'],prior['verified']['frozenPriorFullClassMarkers'])==(213,1184)
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==216
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    pattern=r'\] ([A-Za-z0-9-]+): passed'
    before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/item-stat-attachment/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
    assert sum(before.values())==213 and not before-after and after-before==Counter({'item-stat-values-check':1,'item-stat-values-reload-check':2})
    assert log.count('Item stat values:105984complete-source query results/480six-stage updates match numeric arrays,flags,base-set returns and ordered callbacks;actual original armor/sword feed numeric graph and remove cleanly;live NPC setters remain gated')==1
    assert log.count('Item stat values:saved original armor/sword retain identity,private parents,full rolls and numeric suppliers;fresh numeric graphs pass after actual reload without changing live NPC stats')==2
    sourcepath=ROOT/'tools/reference/item-stat-values-source-evidence.json';source=json.loads(sourcepath.read_text());artifacts.append(sourcepath)
    assert source['verified']==dict(ownedComparedFields=4784,ordinalRows=368,commentIdMismatches=['passive_mastery_gethit_rate'],statRows=368,queryContexts=144,queryResults=105984,updateContexts=480,updateStages=6,completeRoutines=23)
    assert len(source['compiledUnchangedRoutineHashes'])==23 and not source['currentExecutableParity'] and not source['nativeConsumerAccepted']
    for group in ['inputs','artifacts']:
        for name,digest in source[group].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
    assert not (ROOT/'.local/class-skills/item-stat-values-source/sanitizer.log').read_bytes()
    assert not (ROOT/'.local/class-skills/item-stat-values-source/compiler.log').read_bytes()
    saved=json.loads((STAGE/'focused-fixtures/item-stat-values-fixtures.json').read_text());assert len(saved)==2
    assert len({x['Owner'] for x in saved})==1 and len({x['Pet'] for x in saved})==len({x['Item'] for x in saved})==2
    for row in saved:
        g=json.loads(row['Gear']);assert g['Identified'] and row['Life256']>0 and row['Defense']>=0
    assert saved[0]['ItemLife256']>0 and saved[0]['ItemLife256']%256==64 and saved[0]['ItemDefense']>0
    assert json.loads(saved[1]['Gear'])['BaseCode']=='bsd' and saved[1]['ItemMaximum']==14
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
        audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
    old=json.loads(historical('tools/reference/named-item-runtime-audit.json'))
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions'] and [len(a['InputHashes']) for a in audits]==[731,21]
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    mainpath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainpath.read_text());assert len(main['installedArtifacts'])==106
    for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
    for name in ['class-skill-status.json','skill-effects.json','skill-icons.json']:
        p=ROOT/'data/Sanctuary/Balance'/name;assert sha(p)==hashlib.sha256(historical(str(p.relative_to(ROOT)))).hexdigest();artifacts.append(p)
    artifacts += [STAGE/n for n in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log']]
    artifacts += list((STAGE/'focused-fixtures').glob('*fixtures.json'))
    artifacts += [ROOT/n for n in ['tools/record_item_stat_values.py','tests/check_item_stat_values.py','tools/build_equipment_stat_costs.py','data/Sanctuary/Balance/equipment-stat-costs.json','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]+[mainpath]
    result=dict(referenceVersion='3.3.93847',component='Recursive original-item numeric graph',installed=False,handlerEnabled=False,currentExecutableParity=False,fullNpcStatGraphAccepted=False,liveNpcStatApplicationAccepted=False,sourceNpcLevelMappingAccepted=False,graphSerialized=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],
        verified=dict(passMarkers=216,retainedMarkers=213,worldBoots=3,actualWorldReloads=2,ownedFields=4784,ordinalRows=368,ignoredCommentIdMismatch='passive_mastery_gethit_rate:comment213/ordinal212',queryContexts=144,queryResults=105984,updateContexts=480,updateStages=6,completeRoutines=23,canonicalPackedKeys=True,nonzeroLayers=True,sourceZeroInitializedUnusedOperations=True,numericArraysFlagsCallbacks=True,actualOriginalItems=2,originalSupplierNumericGraph=True,duplicateMergeNoCallbacks=True,damageToggleAndRemoval=True,liveNpcLifeDefenseUnchanged=True,actualReloadsRetainOriginals=True,frozenPriorFullClassMarkers=1184,newFullClassRun=False,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[731,21],unchangedMainInstallationArtifacts=106),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit=FROZEN,receipt='tools/reference/item-stat-attachment-native-evidence.json'),
        established=[
            'The transient numeric service supplies complete recursive query,dependency recalculation,propagation,base setters and change notification to the existing ownership policy. Base/full arrays remain distinct. Current source-assembled metadata has368rows,4784owned field comparisons and368corroborated row ordinals. The loader uses name-to-index row order;the ignored *ID comment duplicates213for passive_mastery_gethit_rate,whose correct ordinal is212. Unused incoming operation slots retain source compiler zero initialization.',
            'Native comparisons match105984query results and480updates at six stages from23complete unchanged source routines plus the exact dependency-assembly block. Cases include both damage modes,layer0and3,item mutation/repeat,recipient level mutation,removal and exact arrays,flags,base-set returns and ordered callbacks. Canonical keys put stat in high16bits and layer in low16bits. The previous abstract attachment trace fixture remains frozen and translates its keys explicitly;all12288prior policy contexts still execute.',
            'The full by-time numeric routine is compared under declared source day angles. World-clock presence/angles,player character coefficients,body locations and value-change effects remain explicit services. The NPC setter explicitly refuses player ModStats registration. Existing player equipment/rounding and saved item formats remain unchanged. Bounded memory allocation/lookup are fixture services,not current engine memory-layout acceptance.',
            'Actual privately bound original armor and sword feed existing verified life FP256,armor and local maximum suppliers into numeric graphs. Attachment,repeated merge,damage exclusion/restoration and removal retain the expected numeric contributions and callbacks without changing actual creature life/defense or original inventory ownership. Two actual reloads retain complete gear,serials,private parents and suppliers;fresh transient graphs are checked rather than persisted or replayed. Existing persistent trained minions are storage fixtures,not learned golems.',
            'All213previous focused marker counts remain alongside three new checks. Both builds have zero warnings/errors;70Python checks,573unchanged named eligibility decisions and106unchanged main installation hashes pass. The earlier417cd39package separately retains1184full-class checks;this isolated numeric module has no new full-suite claim. Initial compiler binding/initializer issues and corrected harness versions remain in diagnostics;there was no failed native attempt.'
        ],remaining=[
            'Full raw ordinary/named item projection,actual NPC life/defense/resistance/speed/damage consumers,real source recipient-level mapping,actor change effects and automatic graph lifecycle remain unfinished. Multi-item ordering,cycles,reentrant callbacks,extreme/negative/overflowing numeric ranges and modern executable parity are not certified. Scratch lists above16and excessive graph depth are explicitly unsupported.',
            'World-time item mapping and real character/ModStats adapters,base/ethereal initialization,property17zero-parameter generation and modern base-ED rounding remain separate gaps. Complete Iron Golem casting,body/AI,cap/placement and owner/facet/logout/death/respec/bound-item lifecycle remain gated. Skill-dependent item events stay deferred. Effects/audio and talent/hotkey icons remain separate placeholders;no class is complete.'
        ])
    path=ROOT/'tools/reference/item-stat-values-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Item stat values:216focused/70Python;105984queries/480updates;two original-item reloads;live NPC consumers unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
