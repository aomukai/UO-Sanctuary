#!/usr/bin/env python3
"""Separate Assassin explicit class inputs, legacy Cast sequences and held Fury."""
import argparse,json,re
from pathlib import Path
from class_input_coverage import ROOT,collect,sha
OUTPUT=ROOT/'tools/reference/assassin-current-input-coverage.json'
LEGACY=(('psychic-hammer','PsychicHammer','psychichammer'),('fade','Fade','fade'),
        ('venom','Venom','venom'),('fire-blast','FireBlast','fireblast'),
        ('shock-web','ShockWeb','shockweb'),('mind-blast','MindBlast','mindblast'),
        ('cloak','Cloak','cloakofshadows'))

def review(check=False):
    stage=ROOT/'.local/class-skills/assassin-lifecycle-acceptance';born=json.loads((stage/'birth-inputs.json').read_text())
    def compiled(path,fragments):
        p=ROOT/path;assert sha(p)==born['sourceInputs'][path],path
        for text in fragments:assert text in p.read_text(),(path,text)
    compiled('tests/Sanctuary.Integration/BladeBodyChecks.cs',['ClassSkillChecks.Player(DiabloClass.Assassin)'])
    compiled('src/Sanctuary.Content/AssassinCombat.cs',['profile.Class==DiabloClass.Assassin'])
    legacy=[]
    for label,fixture,talent in LEGACY:
        fragments=['var owner=Fresh();'if fixture=='Fade'else'var p=Fresh();','ClassCreation.Profiles.Assign(p,']
        fragments.append('DiabloClass characterClass=DiabloClass.Assassin'if fixture in ('FireBlast','ShockWeb','MindBlast')else'ClassCreation.Profiles.Assign(p,DiabloClass.Assassin)')
        legacy.append(dict(label=label+'-input',talentId='assassin.'+talent,ownerSource=f'tests/Sanctuary.Integration/{fixture}Checks.cs',ownerFragments=fragments))
    result=collect('Assassin',[stage],negative_prefixes=tuple(n+'-left'for n in ('blade-cast','sentry-learned','lightning-learned','inferno-learned','death-learned','wake-learned')),legacy=legacy,basic_controls=('assassin-weapon-basic-left','assassin-weapon-basic-right'),held=dict(
        talentId='assassin.bladefury',labels=tuple('fury-held-wire-'+str(n)for n in (0,1,9)),
        ownerSource='tests/Sanctuary.Integration/BladeFuryHeldInputChecks.cs',guardSource='src/Sanctuary.Content/PlayerInfernoInput.cs',
        ownerAssignment='p=BladeBodyChecks.Fresh(6400,30,3550)',wireOwner='var p=Owner();',availabilityGuard='AssassinCombat.Available(owner,ability)'))
    assert (result['activeTalents'],result['passiveTalents'],result['verifiedActiveInputRoutes'],result['heldInputSequences'],result['legacyInputSequences'])==(28,2,28,9,21)
    log=(stage/'birth-native-checks.log').read_text()
    # Count existing legacy scenarios,not heartbeat packets or every phase as
    # another cast. The seven credited above are distinct from explicit class
    # inputs. Four claw scenarios retain their three builder/finisher phases.
    import sys
    sys.path.insert(0,str(ROOT/'tests'))
    from check_assassin_planned_acceptance import LEGACY as labels
    for label in labels:
        for suffix in ('-input-check','-input-result'):
            assert len(re.findall(r' '+re.escape(label+suffix)+r': passed',log))==3
    result.update(totalRetainedLegacySequences=42,legacyLimits='Seven spell/buff routes use actual legacy Cast input. Their class comes from the compiled fixture default/assignment,not a packet class byte. Fourteen legacy scenarios total42sequences;claw phases/heartbeats,Cobra finisher and Cloak refusal/unlock are not inflated into extra scenarios.')
    for p in (Path(__file__).resolve(),ROOT/'tests/Sanctuary.Integration/BladeBodyChecks.cs',ROOT/'src/Sanctuary.Content/AssassinCombat.cs'):result['files'][str(p.relative_to(ROOT))]=sha(p)
    text=json.dumps(result,indent=2)+'\n'
    if check:assert OUTPUT.read_text()==text,'Assassin input audit stale'
    else:OUTPUT.write_text(text)
    print(f"Assassin:28active routes/{result['ordinaryInputCases']} explicit-class inputs;21credited legacy sequences,9held Fury;2passives separate.")

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');review(p.parse_args().check)
