#!/usr/bin/env python3
"""Prepared Slow Missiles release plus retained state/encounter projectile acceptance."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check

def configuration():
 return dict(stage=ROOT/'.local/class-skills/slow-missiles-cast',
  extra_files={name+'-source-probe.json':ROOT/'.local/class-skills'/(name+'-source-probe.json') for name in ('slow-missiles','inner-sight')},
  extra_families=('slow-missiles-cast','slow-missiles-component','slow-missiles-bolts'),
  extra_checks=('slow-missiles-restore-check',),
  extra_wait_results={'slow-missiles-component-check':'slow-missiles-component-result','slow-missiles-bolts-check':'slow-missiles-bolts-result','slow-missiles-bolts-reload-check':'slow-missiles-bolts-result'},
  extra_manifest_names={'slow-missiles-bolts':'slow-missiles-bolt-fixtures.json'},
  extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/slow-missiles-preflight.json',ROOT/'tools/reference/slow-missiles-engine-evidence.json'))

if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
