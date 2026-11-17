#!/usr/bin/env python3
"""Qualify the supplied-index primitive and retain actual learned Pierce lifecycle."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/pierce-source-seed'
def configuration():
    c=dict(stage=STAGE,extra_files={'pierce-seed-source-cases.json':STAGE/'source-cases.json','hellslayer-before-readiness.json':ROOT/'tools/reference/named-item-pre-proc-baseline.json'},extra_families=('hellslayer-readiness',),extra_checks=('pierce-seed-check',),extra_manifest_names={},extra_inputs=(Path(__file__).resolve(),ROOT/'tools/probe_pierce_source_seed.py',ROOT/'tools/reference/pierce-source-seed-review.json',ROOT/'tools/reference/amazon-current-acceptance-native-evidence.json'))
    add_fixture(c,'AmazonPierce')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
