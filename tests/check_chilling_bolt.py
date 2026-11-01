#!/usr/bin/env python3
"""Saved Chilling Armor return bolts with retained cold/Sorceress contracts."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT, check
from check_cold_armor import configuration as cold


def configuration(full=False):
    config=cold(full);config['stage']=ROOT/'.local/class-skills/sorceress-chilling-bolt'
    config['extra_families']+=('chilling-bolt',)
    config['extra_connected_checks']+=('chilling-bolt-reload-check',)
    config['extra_wait_results']['chilling-bolt-reload-check']='chilling-bolt-reload-result'
    config['extra_files']['chilling-armor-source-cases.json']=ROOT/'.local/class-skills/chilling-armor-source/cases.json'
    config['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/chilling-armor-source-evidence.json')
    return config


if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
