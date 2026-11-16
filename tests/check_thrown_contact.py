#!/usr/bin/env python3
"""Ordinary Double Throw ordered missile contact and actual hit-proc delivery."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_hellslayer import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/thrown-contact'
def configuration():
    c=prior();c.update(stage=STAGE);c.setdefault('extra_manifest_names',{});c.setdefault('extra_wait_results',{})
    add_fixture(c,'DoubleThrow')
    c['extra_families']+=('double-throw-missile',)
    c['extra_connected_checks']=(*c.get('extra_connected_checks',()),'double-throw-check','double-throw-reload-check')
    c['extra_wait_results'].update({'double-throw-check':'double-throw-result','double-throw-reload-check':'double-throw-result'})
    c['extra_connection_settle']=1.1
    c.update(extra_families=('thrown-contact',*c['extra_families']),extra_target_input_commands=(*c['extra_target_input_commands'],'thrown-contact-right','thrown-contact-quick','double-throw-missile-right','double-throw-missile-quick'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/review_thrown_contact.py',ROOT/'tools/prepare_double_throw_missiles.py',ROOT/'tools/reference/existing-melee-proc-native-evidence.json',ROOT/'tools/reference/double-throw-missile-native-evidence.json',ROOT/'tools/reference/thrown-contact-source-review.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
