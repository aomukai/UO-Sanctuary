#!/usr/bin/env python3
"""Bind current target-category rating routing to native acceptance."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-rating-routing'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');artifacts=[]
    _,prior=frozen_receipt('16d4a65','tools/reference/generic-pet-preparation-native-evidence.json');assert prior['verified']['passMarkers']==228
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
    log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==231
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    marker=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(marker,(ROOT/'.local/class-skills/generic-pet-preparation/stats-native-checks.log').read_text()));after=Counter(re.findall(marker,log))
    assert sum(before.values())==228 and not before-after and after-before==Counter({'generic-pet-rating-routing-check':1,'generic-pet-rating-routing-reload-check':2})
    assert log.count('GenericPet rating routing:14088captured-current contexts;exact target prepass,priority,retries,temporary copies and wrapped bonuses pass;per-target scoring remains a mandatory unimplemented service;calls=4255')==3
    sourcepath=ROOT/'tools/reference/generic-pet-rating-routing-source-evidence.json';source=json.loads(sourcepath.read_text());artifacts.append(sourcepath)
    assert source['verified']==dict(contexts=14088,capturedHelperBytes=720,inlineTableBytes=52,scoreCalls=4255)
    assert not source['processMemoryRead'] and source['sourceCodeExecutedInOfflineEmulator'] and not source['fullPerTargetScoringVerified'] and not source['completeAiVerified'] and not source['handlerEnabled']
    for name,h in source['artifacts'].items():assert sha(ROOT/name)==h,name;artifacts.append(ROOT/name)
    lib=ROOT/source['emulatorLibrary']['path'];assert sha(lib)==source['emulatorLibrary']['sha256'];artifacts.append(lib)
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
        audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
    old=json.loads(subprocess.check_output(['git','show','16d4a65:tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[737,21]
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    mainpath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainpath.read_text());assert len(main['installedArtifacts'])==106
    for name,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/name)==h,name
    for name in ['class-skill-status.json','skill-effects.json','skill-icons.json']:
        p=ROOT/'data/Sanctuary/Balance'/name;assert sha(p)==hashlib.sha256(subprocess.check_output(['git','show','16d4a65:'+str(p.relative_to(ROOT))],cwd=ROOT)).hexdigest();artifacts.append(p)
    artifacts += [STAGE/n for n in ['stats-inputs.json','stats-native-checks.log','run.log','source-run.log','source-recheck.log','python-tests.log']]
    artifacts += [p for p in (STAGE/'focused-fixtures').iterdir() if p.is_file()]
    artifacts += [ROOT/n for n in ['tools/record_generic_pet_rating_routing.py','tests/check_generic_pet_rating_routing.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]+[mainpath]
    result=dict(referenceVersion='3.3.93847',component='Current GenericPet target-category rating routing',installed=False,handlerEnabled=False,fullAiVerified=False,fullPerTargetScoringVerified=False,realMetadataAndActorServicesAccepted=False,newActorPersistenceAccepted=False,goalComplete=False,handlers=183,warlockHandlers=1,completedClasses=[],verified=dict(passMarkers=231,retainedMarkers=228,worldBoots=3,actualWorldReloads=2,contextsPerBoot=14088,declaredPerTargetScoreCallsPerBoot=4255,capturedHelperBytes=720,includedInlineTableBytes=52,allByteCategories=True,exactFullDescriptorsAndServiceTraces=True,prepassPriorityCopiesRetriesAndWrap=True,frozenPriorFullClassMarkers=1184,newFullClassRun=False,pythonTests=70,unchangedNamedDefinitions=573,inputFingerprints=[737,21],unchangedMainInstallationArtifacts=106),inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='16d4a65',receipt='tools/reference/generic-pet-preparation-native-evidence.json'),established=[
        'The complete720-byte captured current routing wrapper0x4b5200 executes offline. Its52-byte13-entry inline table is validated and read as data. Native results match14088contexts per boot,all256byte categories,4255declared scorer calls and exact descriptor/context/call order. Source instruction bytes are unchanged.',
        'Routing preserves skill-flag prepass to target slot3,early return on non-minimum rating,priority for existing targets,category-specific slots,and direct scoring of the existing target. It retains mutations made by a failed in-place prepass before fallback. Non-base and ordinary-mode descriptors differ in metadata lookup.',
        'Dual-range category7tries the configured higher-bonus side first,uses fresh temporary descriptors for retries,and commits only a non-minimum result. Category13preserves signed frame100 and unsigned selection-word thresholds,temporary candidate acceptance,wrapped+20bonus and slot0fallback. Distinct declared scorer mutations test full40-byte payload preservation,discard and in-place behavior,including target clearing/replacement.',
        'The per-target scorer is a mandatory service,not an implemented gameplay formula. Frame100 and selection words retain neutral raw names. The3092-byte captured scorer still requires its own implementation and current-code comparison;this wrapper does not claim complete scoring or live AI.',
        'All228prior native markers remain. Both builds have zero warnings/errors;70Python checks,573unchanged named eligibility decisions and106unchanged main installation hashes pass. Existing saved fixtures survive two actual reloads;this policy adds no actor persistence. Frozen417cd39retains1184full-class checks separately;no new full-suite run claimed. No new live capture or failed native run occurred.'
    ],remaining=[
        'Implement the current per-target scorer and target search,then ordering,real world metadata/geometry/state/iterators,approach/follow execution and actual mode/skill effects. Protected event/proc helpers and ordering remain unavailable;continue unaffected work.',
        'Learned Goatman/Tainted/Defiler casting,extended passives/events/Blood Oath/sunder,new Frenzy/Cleave and complete owner/facet/logout/death/respec/save behavior remain gated. Raw-item/Necromancer dependencies continue. Effects/audio and talent/hotkey icons remain separate placeholders;no class is complete.'
    ])
    out=ROOT/'tools/reference/generic-pet-rating-routing-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('GenericPet rating routing:231native/70Python;14088current-code contexts per boot;per-target scoring unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
