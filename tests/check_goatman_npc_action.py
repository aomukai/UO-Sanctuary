#!/usr/bin/env python3
"""Focused NPC action candidate and shared packet/stat-writer regressions."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/goatman-npc-action',
          extra_checks=('goatman-npc-action-check','goatman-npc-melee-check','frenzy-release-check'),
          extra_files={n:ROOT/'.local/class-skills'/n for n in ('goatman-npc-melee-source-probe.json','frenzy-release-source-probe.json')},
          extra_inputs=(Path(__file__).resolve(),))
