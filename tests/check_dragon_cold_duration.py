#!/usr/bin/env python3
"""Four Dragon finishers' item/skill cold writer with focused native and input proofs."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/dragon-cold-duration'
def configuration():
    c=dict(stage=STAGE,extra_files={'hellslayer-before-readiness.json':ROOT/'tools/reference/named-item-pre-proc-baseline.json'},extra_families=(),extra_inputs=(),extra_manifest_names={},extra_wait_results={})
    for f in ('DragonKick','DragonClawPreparation','DragonClaw','DragonTail','DragonTalon','DragonFlight','IceRelease'):add_fixture(c,f)
    c['extra_families']+=('hellslayer-readiness','dragon-cold-duration')
    c['extra_checks']=tuple(f'dragon-{f}-mechanics-check' for f in ('claw','tail','talon','flight'))
    c['extra_target_input_commands']=tuple(f'dragon-{f}-{mode}' for f in ('claw','tail','talon','flight') for mode in ('left','right','quick'))+tuple(f'dragon-cold-dragon{f}-{mode}' for f in ('claw','tail','talon','flight') for mode in (('right','quick') if f=='claw' else ('right',)))
    c['extra_inputs']=(Path(__file__).resolve(),ROOT/'tools/reference/assassin-weapon-contact-native-evidence.json',ROOT/'tools/reference/dragon-cold-duration-source-review.json',*(ROOT/f'tools/reference/rules/dragon-{f}-action-proposal.md' for f in ('claw','talon','flight')))
    helpers=tuple(Path(m.__file__).resolve() for m in tuple(sys.modules.values()) if getattr(m,'__file__',None) and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
