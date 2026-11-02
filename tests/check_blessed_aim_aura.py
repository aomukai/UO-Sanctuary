#!/usr/bin/env python3
"""Blessed Aim active/passive AR with the existing Amazon coexistence fixture."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_defiance_aura import configuration as parent

def configuration(full=False):
    c=parent(full);c['stage']=ROOT/'.local/class-skills/blessed-aim-aura'
    c['extra_checks']+=('blessed-aim-aura-check',)
    c['extra_families']+=('amazon-penetrate',)
    c['extra_manifest_names']['amazon-penetrate']='amazon-penetrate-fixtures.json'
    c['extra_files']['amazon-penetrate-source-probe.json']=ROOT/'.local/class-skills/amazon-penetrate-source-probe.json'
    names=tuple('blessed-aim-aura-'+mode for mode in ('right','quick'))
    c['extra_target_input_commands']+=names;c['extra_instant_input_commands']+=names
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/amazon-penetrate-engine-evidence.json')
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
