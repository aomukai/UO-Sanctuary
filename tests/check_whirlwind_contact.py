#!/usr/bin/env python3
"""Whirlwind contact/poison/proc-state qualification and retained motion/saves."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_hellslayer import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/whirlwind-contact'
def configuration():
    c=prior();c.update(stage=STAGE);c.setdefault('extra_manifest_names',{});c.setdefault('extra_wait_results',{})
    add_fixture(c,'Whirlwind')
    for suffix in ('-check','-reload-check'):
        n='whirlwind'+suffix;c['extra_wait_results'][n]='whirlwind-result';c['extra_connected_checks']=(*c.get('extra_connected_checks',()),n)
    c.update(extra_instant_input_commands=(*c.get('extra_instant_input_commands',()),'whirlwind-contact-right','whirlwind-contact-quick'),extra_families=(*c['extra_families'],'whirlwind-contact'),extra_target_input_commands=(*c['extra_target_input_commands'],'whirlwind-contact-right','whirlwind-contact-quick'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/barbarian-contact-native-evidence.json',ROOT/'tools/reference/whirlwind-contact-source-review.json'))
    helpers=tuple(Path(m.__file__).resolve() for m in tuple(sys.modules.values()) if getattr(m,'__file__',None) and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
