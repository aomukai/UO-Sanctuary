#!/usr/bin/env python3
"""Bind source stat-list ownership policy to its scoped native adapter evidence."""
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
STAGE=ROOT/'.local/class-skills/item-stat-attachment'
FROZEN='417cd39'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def historical(name):return subprocess.check_output(['git','show',FROZEN+':'+name],cwd=ROOT)
def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
    _,prior=frozen_receipt(FROZEN,'tools/reference/item-maximum-operations-native-evidence.json')
    assert (prior['verified']['passMarkers'],prior['verified']['fullClassMarkers'])==(210,1184)
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==213
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    pattern=r'\] ([A-Za-z0-9-]+): passed'
    before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/item-maximum-operations/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
    assert sum(before.values())==210 and not before-after and after-before==Counter({'item-stat-attachment-check':1,'item-stat-attachment-reload-check':2})
    assert log.count('Item stat attachment:12288source contexts/5stages/7routines match topology,owners,flags and ordered service traces;actual original armor/sword pass transient merge,repeat,damage toggle,detach and caller-array isolation;live NPC stat setters remain gated')==1
    assert log.count('Item stat attachment:saved original armor/sword preserve serials,private parents,complete gear and numeric suppliers;fresh transient graphs pass after actual reload without serialized graph replay or live NPC stat application')==2
    sourcepath=ROOT/'tools/reference/item-stat-attachment-source-evidence.json';source=json.loads(sourcepath.read_text());artifacts.append(sourcepath)
    assert source['verified']==dict(contexts=12288,stagesPerContext=5,completeRoutines=7)
    assert len(source['compiledUnchangedRoutineHashes'])==7 and not source['currentExecutableParity'] and not source['nativeConsumerAccepted']
    for group in ['inputs','artifacts']:
        for name,digest in source[group].items():assert sha(ROOT/name)==digest,name;artifacts.append(ROOT/name)
    assert not (ROOT/'.local/class-skills/item-stat-attachment-source/sanitizer.log').read_bytes()
    assert not (ROOT/'.local/class-skills/item-stat-attachment-source/compiler.log').read_bytes()
    saved=json.loads((STAGE/'focused-fixtures/item-stat-attachment-fixtures.json').read_text());assert len(saved)==2
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
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==old['Definitions'] and [len(a['InputHashes']) for a in audits]==[729,21]
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    mainpath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainpath.read_text());assert len(main['installedArtifacts'])==106
    for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
    for name in ['class-skill-status.json','skill-effects.json','skill-icons.json']:
        p=ROOT/'data/Sanctuary/Balance'/name;assert sha(p)==hashlib.sha256(historical(str(p.relative_to(ROOT)))).hexdigest();artifacts.append(p)
    artifacts += [STAGE/n for n in ['stats-inputs.json','stats-native-checks.log','run.log','python-tests.log']]
    artifacts += list((STAGE/'focused-fixtures').glob('*fixtures.json'))
    artifacts += [ROOT/n for n in ['tools/record_item_stat_attachment.py','tests/check_item_stat_attachment.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]+[mainpath]
    result=dict(referenceVersion='3.3.93847',component='Original-item stat attachment and explicit transient native adapter',installed=False,handlerEnabled=False,currentExecutableParity=False,fullNpcStatGraphAccepted=False,liveNpcStatApplicationAccepted=False,sourceNpcLevelMappingAccepted=False,graphSerialized=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],
        verified=dict(passMarkers=213,retainedMarkers=210,worldBoots=3,actualWorldReloads=2,sourceContexts=12288,stagesPerContext=5,completeRoutines=7,exactOrderedServices=True,realOriginalItems=2,callerArrayIsolation=True,repeatPostingNoDuplicate=True,damageToggle=True,clearedLinksBeforeRemoval=True,liveNpcLifeDefenseUnchanged=True,numericRecalculationService=False,fullPropagationService=False,actualReloadsRetainOriginals=True,frozenPriorFullClassMarkers=1184,newFullClassRun=False,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[729,21],unchangedMainInstallationArtifacts=106),
        inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit=FROZEN,receipt='tools/reference/item-maximum-operations-native-evidence.json'),
        established=[
            'The prepared C# policy matches all12288source-derived contexts at five stages,including complete topology,owner lookup,stored recipient,flags,delta aggregates and exact ordered service traces from seven unchanged complete source routines. Both initial damage modes,repeat posting,toggle,prior parents,alternate body slots and nullable/nonextended inputs retain their source behavior. Internal SET flags are not named set-item quality.',
            'Recalculation,propagation and body-location metadata remain required services. Source fixture IDs0/1/2and their metadata are abstract. Recalculation output is not invented;the service trace is verified. The explicit native adapter validates the original bound object and copies caller-supplied stat arrays. It does not infer a full NPC stat projection or source recipient level.',
            'Actual original Spirit Forge armor and an ordinary sword exercise transient merge,repeat without duplicate deltas,damage exclusion/restoration and detach with cleared callback links. The explicit fixture aggregate uses existing item life FP256,local armor and local maximum suppliers. Original inventory ownership,serials,complete gear and live creature life/defense remain unchanged. Two actual reloads retain both originals and their numeric suppliers;fresh transient graphs are reconstructed for checks rather than serialized or automatically replayed.',
            'All210prior focused marker counts are retained alongside the three new markers. Both builds have zero warnings/errors;70Python checks,573unchanged named eligibility decisions and106unchanged main installation hashes pass. Frozen417cd39separately retains1184full-class checks;no new full-suite acceptance is claimed for this isolated prepared module.'
        ],remaining=[
            'Complete numeric stat-operation recalculation and propagation,live NPC life/defense/resistance/speed/damage consumers,real recipient-level mapping,automatic graph lifecycle,multi-item ordering,cycles and callback reentrancy remain unfinished. More than16applying-to-item scratch entries are explicitly unsupported. No current executable parity or complete stat graph is certified.',
            'Base/ethereal initialization,property17parameter-zero fallback,by-time operations6/7and modern base-ED rounding remain separate gaps. Full Iron Golem casting,body/AI,cap/placement and owner/facet/logout/death/respec/bound-item lifecycle remain gated. Skill-dependent item events stay deferred. Effects/audio and talent/hotkey icons remain separate placeholders;no class is complete.'
        ])
    path=ROOT/'tools/reference/item-stat-attachment-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Item stat attachment:213focused/70Python;12288source contexts;two actual original-item reloads;full NPC graph unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
