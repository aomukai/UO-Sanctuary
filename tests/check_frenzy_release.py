#!/usr/bin/env python3
"""Frenzy release/charge source comparisons with retained GUID/hand/channel scopes."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_double_swing_phase import configuration as retained
if __name__=='__main__':
    c=retained();c['stage']=ROOT/'.local/class-skills/frenzy-release'
    c['extra_checks']+=('frenzy-release-check',)
    c['extra_files']['frenzy-release-source-probe.json']=ROOT/'.local/class-skills/frenzy-release-source-probe.json'
    c['extra_inputs']+=(Path(__file__).resolve(),)
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
