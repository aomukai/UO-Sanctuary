#!/usr/bin/env python3
"""Captured Impale item/poison/wear delivery with retained special-wear and slow controls."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_hellslayer import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/impale-contact'
def configuration():
    c=prior();c.update(stage=STAGE);c.setdefault('extra_manifest_names',{});c.setdefault('extra_wait_results',{});add_fixture(c,'Impale')
    c['extra_wait_results']['impale-check']='impale-slow-expiry'
    c.update(extra_families=('impale-contact',*c['extra_families']),extra_target_input_commands=(*c['extra_target_input_commands'],'impale-contact-right','impale-contact-quick'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/review_impale_contact.py',ROOT/'tools/reference/amazon-sequence-contact-native-evidence.json',ROOT/'tools/reference/impale-contact-source-review.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
