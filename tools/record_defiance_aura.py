#!/usr/bin/env python3
"""Record Defiance consumer verification without claiming new production behavior."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from record_might_aura import passed
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/defiance-aura';sha=inventory.sha
PARENT='00a96e3d2f4b72ee86ae5f3397d1fafd24ec3850'
def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-1.json',STAGE/'birth-inputs.json')
    log,counts=passed(STAGE/'birth-native-checks.log',481)
    assert 'pass across3boots/2reloads' in (STAGE/'focused-run-1.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'));from check_defiance_aura import configuration
    c=configuration();assert (len(c['extra_families']),len(c['extra_checks']),len(c['extra_target_input_commands']))==(27,10,51)
    for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
    for name in c['extra_checks']:assert counts[name]==3,name
    for name in c['extra_target_input_commands']:
        assert counts[name+'-check']==counts[name+'-result']==3,name
        assert log.count(' '+name+(': acknowledged' if name in c['extra_instant_input_commands'] else ': windup'))==3,name
    assert log.count('Defiance:stat171 non-direct;zero mana;additive Holy Shield/Frozen Armor/Shout;')==3
    old=ROOT/'.local/class-skills/might-aura';parent=json.loads((old/'birth-inputs.json').read_text())
    for path,h in tested['sourceInputs'].items():
        if path.startswith(('src/','shared/')):assert parent['sourceInputs'][path]==h,path
    assert tested['copiedData']==parent['copiedData']
    for path in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][path]==parent['artifacts'][path]
    retained=dict(counts)
    for name in ('defiance-aura-check','defiance-aura-right-check','defiance-aura-right-result','defiance-aura-quick-check','defiance-aura-quick-result'):retained.pop(name)
    assert retained==passed(old/'focused-accepted-1/birth-native-checks.log',466)[1]
    saved=json.loads((STAGE/'paladin-aura-checks.json').read_text());owner=saved['Owners'][1]
    assert any(e['Owner']==owner and e['Ability']==96 for e in saved['State']['Emitters'])
    assert any(l['Source']==owner and l['Target']==saved['Ally'] and l['Ability']==96 and l['Rank']==1 and l['Percent']==70 for l in saved['State']['Leases'])
    assert json.loads((STAGE/'might-aura-checks.json').read_text())['Generation']==2
    for part in ('content','integration'):
        s=(STAGE/(part+'-build-1.log')).read_text();assert all(x in s for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    references=['might-aura-native-evidence.json','might-callback-source-evidence.json','inner-sight-engine-evidence.json','holy-shield-engine-evidence.json']
    for name in references:assert (ROOT/'tools/reference'/name).read_bytes()==historical('tools/reference/'+name)
    for path,h in json.loads((ROOT/'tools/reference/might-callback-source-evidence.json').read_text())['currentTables'].items():assert sha(ROOT/path)==h,path
    tables=ROOT/'tools/reference/d2r-named-equipment/json'
    row=json.loads((tables/'skills.json').read_text())['104'];state=json.loads((tables/'states.json').read_text())['defiance'];stat=json.loads((tables/'itemstatcost.json').read_text())['skill_armor_percent']
    assert (row['srvdofunc'],row['aurafilter'],row['Param3'],row['Param4'],row['mana'],state['*ID'],stat['*ID'],stat.get('direct',0))==(65,73731,70,10,0,37,171,0)
    a=json.loads((STAGE/'audit-inputs.json').read_text());assert a['artifacts']==tested['artifacts'] and a['copiedData']==tested['copiedData']
    from record_poison_dagger_contact import markers
    s=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in s and ' ERR]' not in s and sum(markers(STAGE/'audit-native-checks.log').values())==4
    for name,count in [('named-item-runtime-audit.json',911),('first-loop-affix-audit.json',21)]:
        n=json.loads((STAGE/name).read_text());assert n==json.loads((ROOT/'tools/reference'/name).read_text()) and len(n['InputHashes'])==count
        assert n['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,h in n['InputHashes'].items():assert sha(ROOT/path)==h,path
        o=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in n.items() if k not in ignore}=={k:v for k,v in o.items() if k not in ignore}
    s=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in s and '\nOK\n' in s and 'FAILED' not in s
    s=(STAGE/'catalogue-checks.log').read_text()
    for text in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert text in s
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==h,path
    docs=['paladin-defiance-aura-preflight.md','paladin-defiance-aura-checkpoint.md','paladin-blessed-aim-preflight.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md']
    files=[Path(__file__).resolve(),ROOT/'tests/check_defiance_aura.py',*(ROOT/'tools/reference'/n for n in references),*(ROOT/'docs'/n for n in docs),
        *(STAGE/n for n in ('birth-inputs.json','birth-native-checks.log','pre-build-1.json','focused-run-1.log','content-build-1.log','integration-build-1.log','paladin-aura-checks.json','might-aura-checks.json','audit-inputs.json','audit-native-checks.log','audit-run-1.log','python-tests.log','catalogue-checks.log')),
        *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Defiance shared defense consumers and instant input verification',installed=False,graphicalClientVerified=False,completedClasses=[],
        verified=dict(focusedNativeMarkers=481,retainedFocusedMarkers=466,previousFullMarkers=1121,boots=3,reloads=2,saves=3,newServerWireActions=6,auditMarkers=4,pythonTests=71,unchangedInstalledArtifacts=106),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Production C#/shared sources and copied gameplay data are unchanged from accepted Might. This is dedicated verification with a separately fingerprinted clean build,not a production rewrite or a byte-identical DLL claim across output/commit changes. The prior1121-marker full receipt is retained separately;this run has481focused markers.',
            'Defiance adds to Holy Shield,Frozen Armor and Shout defense percentages. Real Tarnhelm raises active effective aura rank without entering Holy Shield hard-point synergy. Current hard points affect the stored Holy Shield rank immediately;active aura refresh is separate. Stronger recipient leases linger after lower-rank self reselection. Running/Berserk still force final defense zero.',
            'Might instant input fixture is parameterized and reused for Defiance. Both actual right/quick paths pass each boot;all466retained focused markers pass. The existing Paladin aura fixture retains original Defiance emitter/leases through two real reloads and resumes refresh with explicit inspection holds/rearming;no duplicate save fixture or uninterrupted short-clock claim.',
            'Pinned Defiance state37/stat171is non-direct. Reuse complete Might basic-aura/list/modifier evidence,Inner Sight120supplied defense cases and Holy Shield gate/synergy evidence within their declared limits;no new compiled Defiance callback matrix or modern executable certificate is claimed. Fresh audits/71Python/573named/240catalogue/generators pass;106installed artifacts unchanged.'],
        remaining=['Modern callback/search/event/ownership/mode,full stat graph/item-granted aura/species/client equivalence remain uncertain. Native friendliness/geometry/town/candidate/timer rules remain declared host adapters.',
            'Final rings/overlays,audio and talent/right/quick icons remain separate placeholders. No class completion,deployment or save reset.'])
    output=ROOT/'tools/reference/defiance-aura-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Defiance:481focused/466retained markers,6new instant wire actions,2reused original-aura reloads;production sources unchanged;4audits/71Python/106installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
