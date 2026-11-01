#!/usr/bin/env python3
"""Shiver typed SOFTHIT and late NPC attempts with retained class contracts."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT, check
from check_chilling_bolt import configuration as chilling


def configuration(full=False):
    config=chilling(full);config['stage']=ROOT/'.local/class-skills/sorceress-shiver-armor'
    config['extra_families']+=('shiver-armor',)
    config['extra_files']['shiver-armor-source-cases.json']=ROOT/'.local/class-skills/shiver-armor-source/cases.json'
    config['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shiver-armor-source-evidence.json')
    return config


if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
