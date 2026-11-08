#!/usr/bin/env python3
"""Actual NPC-owned Hydra prototype and affected original player Hydra host."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/npc-hydra-world'
 c['extra_families']=('npc-hydra-world','npc-hydra-birth','hydra-world')
 c['extra_files']['hydra-creation-cases.json']=ROOT/'.local/class-skills/hydra-creation-source/cases.json'
 c['extra_connected_checks']=('hydra-world-reload-check',)
 c['extra_wait_results']={'hydra-world-reload-check':'hydra-world-reload-result'}
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/hydra-creation-source-evidence.json',ROOT/'tools/reference/hydra-world-native-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
