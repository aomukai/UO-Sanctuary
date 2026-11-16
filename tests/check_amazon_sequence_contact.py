#!/usr/bin/env python3
"""Jab/Fend event-time contacts with source poison,proc/wear order and sequence controls."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_hellslayer import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/amazon-sequence-contact'
def configuration():
    c=prior();c.update(stage=STAGE);c.setdefault('extra_manifest_names',{});c.setdefault('extra_wait_results',{})
    for f in ('Jab','Fend'):add_fixture(c,f)
    c['extra_wait_results']['fend-loop-check']='fend-loop-result'
    c.update(extra_families=('amazon-sequence-contact',*c['extra_families']),extra_checks=(*c.get('extra_checks',()),'fend-loop-check'),extra_connected_checks=(*c.get('extra_connected_checks',()),'fend-loop-check'),extra_target_input_commands=(*c['extra_target_input_commands'],*(f'amazon-sequence-contact-{skill}-{mode}'for skill in ('jab','fend')for mode in ('right','quick'))),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/review_amazon_sequence_contact.py',ROOT/'tools/reference/double-throw-missile-native-evidence.json',ROOT/'tools/reference/amazon-sequence-contact-source-review.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
