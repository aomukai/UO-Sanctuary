#!/usr/bin/env python3
"""Intrinsic Shadow Mind Blast and shared player conversion/direct-damage consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-mindblast'
 c['extra_files']['mind-blast-source-probe.json']=ROOT/'.local/class-skills/mind-blast-source-probe.json'
 c['extra_families']=('shadow-mindblast','shadow-psychic','shadow-item-frame','mind-blast')
 c['extra_wait_results']={'mind-blast-check':'mind-blast-result','mind-blast-reload-check':'mind-blast-reload-result'}
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/mind-blast-engine-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
