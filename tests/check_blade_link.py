#!/usr/bin/env python3
"""Blade Shield/Sentinel/Fury settled direct-damage compatibility with Health Link."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_blade_cast import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/blade-link'
def configuration():
    c=prior();c['stage']=STAGE;c['extra_files']['hellslayer-before-readiness.json']=ROOT/'tools/reference/named-item-pre-proc-baseline.json'
    c['extra_families']+=('defiler-health-link','hellslayer-readiness');c.setdefault('extra_manifest_names',{})
    for fixture in ('BladeShield','BladeFuryMissile'):add_fixture(c,fixture)
    c['extra_connected_checks']+=('defiler-health-link-check','defiler-health-link-reload-check')
    c['extra_wait_results'].update({'defiler-health-link-check':'defiler-health-link-check-result','defiler-health-link-reload-check':'defiler-health-link-reload-check-result','blade-shield-check':'blade-shield-result','blade-shield-reload-check':'blade-shield-result'})
    c['extra_checks']+=('blade-fury-missile-natural-check',);c['extra_wait_results']['blade-fury-missile-natural-check']='blade-fury-missile-natural-result'
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/defiler-health-link-rule-approval.json',ROOT/'tools/reference/martial-element-link-native-evidence.json',ROOT/'tools/build_skill_roadmap.py')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
