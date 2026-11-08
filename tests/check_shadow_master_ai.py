#!/usr/bin/env python3
"""Whole original Master decision policy over its explicit host services."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-master-ai';c['extra_families']=()
 c['extra_checks']=('shadow-master-ai-check',)
 c['extra_files']['shadow-master-ai-cases.json']=ROOT/'.local/class-skills/shadow-master-ai-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/shadow-master-ai-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
