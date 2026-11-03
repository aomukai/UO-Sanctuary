#!/usr/bin/env python3
"""Bind approved Grizzly stun/CB, real NPC item CB and retained regression."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/grizzly-upgrades'
PARENT='455aa8c7f34d5e1a4f15321a997113418c5eea48'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE;native=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-4.json',STAGE/'birth-inputs.json')
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==42
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    assert log.count('grizzly-upgrades-live-check: passed')==log.count('grizzly-upgrades-live-result: passed')==3
    for name in ('iron-golem-ordered-contact','grizzly-sequence','bear-smite'):
        assert log.count(name+'-check: passed')==3
    for name in ('grizzly-upgrades','grizzly-world','npc-crushing','weapon-crushing','npc-open-wounds','spirit-wolf-upgrades'):
        assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2
    saved=json.loads((STAGE/'grizzly-upgrades-fixtures.json').read_text())
    assert all(saved[k]>0 for k in ('Owner','Bear','Target','Life','TargetLife','Seed')) and saved['StunUntil']
    item=json.loads((STAGE/'npc-crushing-fixtures.json').read_text())
    assert all(item[k]>0 for k in ('Owner','Source','Item','Target','Life','Seed')) and item['Life']==1025 and item['Gear']
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {p:h for p,h in native['sourceInputs'].items() if p.endswith('.cs')}==audit['sourceInputs']
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==2
    for name,count in [('named-item-runtime-audit.json',920),('first-loop-affix-audit.json',21)]:
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-4.log')).read_text();assert all(word in text for word in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    text=(STAGE/'catalogue-checks.log').read_text()
    for word in ('573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert word in text
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    old=json.loads(subprocess.check_output(['git','show',PARENT+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
    assert len(old['skills'])==len(status['skills'])==240 and [a['talentId'] for a,b in zip(old['skills'],status['skills']) if a!=b]==['druid.summongrizzly']
    previous=json.loads((ROOT/'.local/class-skills/dire-wolf-upgrades/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==previous['artifacts'][name]
    assert {p for p,h in native['copiedData'].items() if previous['copiedData'].get(p)!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    production=lambda inputs:{p:h for p,h in inputs['sourceInputs'].items() if p.startswith(('src/','shared/'))}
    assert {p for p,h in production(native).items() if production(previous).get(p)!=h}=={
        'src/Sanctuary.Content/'+name+'.cs' for name in ('NamedCombat','NpcCombatEvents','NpcCrushing','GrizzlyUpgrades','DruidGrizzlyHit','DruidGrizzlyWorld')}
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    source=(ROOT/'src/Sanctuary.Content/DruidGrizzlyHit.cs').read_text()
    line='        try{GrizzlyUpgrades.Stun(body,target,smite,Limited);}finally{seed=current;}\n'
    thorns='        PaladinThorns.Prepared(body,target,packet.Physical256,false,clipped);\n'
    assert source.count(line)==source.count(thorns)==1
    before=source.replace(line,'').replace(thorns,thorns+line)
    files=[Path(__file__).resolve(),ROOT/'tests/check_grizzly_upgrades.py',ROOT/'tools/build_skill_roadmap.py',
        ROOT/'tools/reference/weapon-crushing-engine-evidence.json',
        *(ROOT/'docs'/name for name in ('grizzly-upgrades-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','pet-adjustment-review.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-4.json','birth-inputs.json','birth-native-checks.log','native-run-3.log','content-build-4.log','integration-build-4.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','python-tests.log','catalogue-checks.log','grizzly-upgrades-fixtures.json','grizzly-world-fixtures.json','npc-crushing-fixtures.json','weapon-crushing-fixtures.json','npc-open-wounds-fixtures.json','spirit-wolf-upgrades-fixtures.json'))]
    diagnostic_dll=None
    for name in ('diagnostic-live-stream-1','diagnostic-live-suspension-2'):
        diagnostic=STAGE/name;old=json.loads((diagnostic/'birth-inputs.json').read_text())
        assert {p for p,h in production(native).items() if production(old).get(p)!=h}=={'src/Sanctuary.Content/DruidGrizzlyHit.cs'}
        assert hashlib.sha256(before.encode()).hexdigest()==old['sourceInputs']['src/Sanctuary.Content/DruidGrizzlyHit.cs']
        if diagnostic_dll is None:diagnostic_dll=sha(diagnostic/'content/Sanctuary.Content.dll')
        else:assert sha(diagnostic/'content/Sanctuary.Content.dll')==diagnostic_dll
        files.extend(p for p in diagnostic.rglob('*') if p.is_file())
    data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Approved Grizzly hard10ordinary stun/hard20CB and original NPC item CB verified; no new handler/class certificate.',
        verified=dict(nativeMarkers=42,boots=3,reloads=2,saves=3,liveLearnedCastAndNaturalStunExpiryScenes=3,retainedPlayerCbSourceCases=1152,retainedGrizzlyClockStepsPerBoot=5742,retainedGrizzlyRateContextsPerBoot=576,retainedGrizzlySequenceContextsPerBoot=576,auditMarkers=2,pythonTests=71,handlerCount=191,unchangedInstalledArtifacts=106),
        limits=['Sanctuary-approved stun uses existing boss flags:50percent/75frames ordinary recipients,10percent/25frames bosses,ordinary A1only; source baseline remains distinct.',
            'Saved original bear/stun inspection clocks are held explicitly; this does not certify arbitrary uninterrupted short stuns through downtime.',
            'Existing published CB elite grouping is retained despite its documented older-source conflict; special recipients, nonzero player-count HP bonus, full modern event/world/PvP/client and final assets remain separate.',
            'Two failed live fixture runs are preserved. Accepted production differs from them only by moving stun from after Thorns to the proper pre-return phase.',
            'No fresh full1180class run is claimed; dedicated Grizzly, shared player/NPC CB, ordered Iron, world/sequence and Blood/Spirit wound scopes are recorded.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/grizzly-upgrades-native-evidence.json';encoded=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Grizzly upgrades:42native markers/3boots/2reloads,3paid learned/natural stun-expiry scenes,shared original-item CB and wound regression;71Python;191handlers;106installed unchanged.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
