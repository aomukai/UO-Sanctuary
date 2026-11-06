#!/usr/bin/env python3
"""Actual learned Coif Iron/original Revive incoming hit routes and two reloads."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/npc-lightning-return',extra_families=('npc-lightning-return',),
          extra_connected_checks=('npc-lightning-return-check','npc-lightning-return-reload-check'),extra_inputs=(Path(__file__).resolve(),))
