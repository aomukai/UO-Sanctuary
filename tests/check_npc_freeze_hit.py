#!/usr/bin/env python3
"""Learned original Iceblink Iron/frozen Revive,status callbacks and native reloads."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/npc-freeze-hit',extra_families=('npc-freeze-hit',),extra_connected_checks=('npc-freeze-hit-check','npc-freeze-hit-reload-check'),extra_inputs=(Path(__file__).resolve(),),extra_files={'npc-freeze-hit-cases.json':ROOT/'.local/class-skills/npc-freeze-hit-source/cases.json'})
