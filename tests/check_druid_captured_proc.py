#!/usr/bin/env python3
"""Captured Maul/Feral/Fire Claws contacts with complete items and retained class proofs."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_druid_basic_proc import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/druid-captured-proc'
def configuration():
    c=prior();c.update(stage=STAGE)
    c.setdefault('extra_manifest_names',{});c.setdefault('extra_wait_results',{})
    for f in ('DruidMaul','DruidFeral','DruidFireClaws'):add_fixture(c,f)
    c.update(extra_families=(*c['extra_families'],'druid-captured-proc'),extra_target_input_commands=(*c['extra_target_input_commands'],*(f'druid-captured-proc-{skill}-{mode}' for skill in ('maul','feral','fire-wolf','fire-bear') for mode in ('right','quick'))),extra_pre_save_commands=(*c['extra_pre_save_commands'],'druid-maul-snapshot-check','druid-maul-expiry-stage-check','druid-feral-snapshot-check','druid-fire-claws-snapshot-check'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/druid-basic-proc-native-evidence.json'),between_boot_delay=3)
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
