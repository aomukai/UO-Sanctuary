#!/usr/bin/env python3
"""Telekinesis item/mobile serial routing, world operations and retained scopes."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT, check
from check_storm_pulse import configuration as storm


def configuration(full=False):
    config=storm(full)
    config['stage']=ROOT/'.local/class-skills/sorceress-telekinesis-world'
    config['extra_families']+=('telekinesis-world',)
    config['extra_wait_results']['telekinesis-world-reload-check']='telekinesis-world-reload-result'
    config['extra_connected_checks']+=('telekinesis-world-reload-check',)
    config['extra_target_input_commands']+=tuple('telekinesis-'+kind+'-'+mode for kind in ('item','mobile') for mode in ('right','quick'))
    config['extra_files']['telekinesis-source-cases.json']=ROOT/'.local/class-skills/telekinesis-source/cases.json'
    config['extra_inputs']+=(Path(__file__).resolve(),)
    return config


if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
