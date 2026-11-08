#!/usr/bin/env python3
"""Shared current Sigil producer and retained Ring launch, no deployment."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/sigil-launch'
 c['extra_families']=('sigil-launch','ring-of-fire-launch')
 c['extra_files']['sigil-launch-cases.json']=ROOT/'.local/class-skills/sigil-launch-source/cases.json'
 c['extra_files']['ring-launch-current-cases.json']=ROOT/'.local/class-skills/ring-of-fire-launch-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/sigil-launch-current-evidence.json',ROOT/'tools/probe_sigil_launch.py')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
