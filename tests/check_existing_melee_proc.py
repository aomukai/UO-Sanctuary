#!/usr/bin/env python3
"""Qualify already-shared Sacrifice/Vengeance/Cleave with current item procs."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_hellslayer import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/existing-melee-proc'
def configuration():
    c=prior();c.update(stage=STAGE);c.setdefault('extra_manifest_names',{});c.setdefault('extra_wait_results',{})
    for name in ('Sacrifice','Vengeance'):add_fixture(c,name)
    c['extra_families']+=('cleave-action',)
    c.setdefault('extra_checks',())
    c['extra_checks']+=('sacrifice-allocation-check','sacrifice-contact-check','vengeance-contact-check')
    c.update(extra_families=('existing-melee-proc',*c['extra_families']),extra_target_input_commands=(*c['extra_target_input_commands'],*(f'existing-melee-proc-{a}-{mode}'for a in ('sacrifice','vengeance','cleave')for mode in ('right','quick'))),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/amazon-thrust-contact-native-evidence.json',ROOT/'tools/reference/sacrifice-contact-native-evidence.json',ROOT/'tools/reference/vengeance-contact-native-evidence.json',ROOT/'tools/reference/cleave-action-native-evidence.json',ROOT/'tools/reference/sacrifice-engine-evidence.json',ROOT/'tools/reference/vengeance-engine-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
