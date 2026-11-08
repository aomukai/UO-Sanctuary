#!/usr/bin/env python3
"""Prepared range expression plus retained complete Master policy controls."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-master-range'
 c['extra_families']=('shadow-master-range','shadow-master-world')
 c['extra_checks']=('shadow-master-ai-check',)
 c['extra_files']['shadow-master-ai-cases.json']=ROOT/'.local/class-skills/shadow-master-ai-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shadow-master-ai-source-evidence.json',ROOT/'tools/reference/d2r-local-missile-guide-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
