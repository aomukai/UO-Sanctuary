#!/usr/bin/env python3
"""Complete do70/GUID selection and retained selected-hand/player-channel scopes."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_player_inferno_modes import configuration as retained
if __name__=='__main__':
    c=retained();c['stage']=ROOT/'.local/class-skills/double-swing-phase'
    c['extra_checks']+=('double-swing-phase-check',)
    c['extra_families']+=('weapon-hands',)
    c['extra_manifest_names']['weapon-hands']='weapon-hands.json'
    c['extra_files']['double-swing-phase-source-probe.json']=ROOT/'.local/class-skills/double-swing-phase-source-probe.json'
    c['extra_inputs']+=(Path(__file__).resolve(),)
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
