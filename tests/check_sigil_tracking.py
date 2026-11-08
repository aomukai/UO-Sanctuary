#!/usr/bin/env python3
"""Current Sigil tracking plus retained launch,using existing disposable host."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/sigil-tracking'
 c['extra_families']=('sigil-tracking','sigil-launch')
 c['extra_files']['sigil-tracking-cases.json']=ROOT/'.local/class-skills/sigil-tracking-source/cases.json'
 c['extra_files']['sigil-launch-cases.json']=ROOT/'.local/class-skills/sigil-launch-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/sigil-tracking-current-evidence.json',ROOT/'tools/probe_sigil_tracking.py')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
