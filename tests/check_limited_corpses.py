#!/usr/bin/env python3
"""Prepared dead trap ownership, bounded source corpse pool and retained Sentry cohorts."""
import os,re
from pathlib import Path
from check_iron_golem_birth import ROOT,check

def configuration():
 names=('SentryComponentChecks','SentryActorChecks','SentryBoltChecks','SentryAimChecks','SentryEmergenceChecks','SentryCastChecks','SentryTargetChecks','SentryUnitPathChecks')
 files=set()
 for name in names:
  files.update(re.findall(r'"([\w-]+-source-probe\.json)"',(ROOT/'tests/Sanctuary.Integration'/(name+'.cs')).read_text()))
 return dict(stage=ROOT/'.local/class-skills/limited-corpses',
  extra_files={name:ROOT/'.local/class-skills'/name for name in sorted(files)},
  extra_families=('sentry-actor','sentry-bolt','sentry-aim','sentry-emergence','sentry-cast','sentry-unit-path','limited-corpse'),
  extra_checks=('sentry-component-check','sentry-target-check'),
  extra_manifest_names={'sentry-cast':'sentry-cast-fixture.json','sentry-emergence':'sentry-emergence-fixture.json','sentry-aim':'sentry-aim-fixture.json','sentry-unit-path':'sentry-unit-path-fixture.json'},
  extra_connected_checks=('limited-corpse-check','limited-corpse-reload-check'),
  extra_wait_results={'limited-corpse-check':'limited-corpse-result','limited-corpse-reload-check':'limited-corpse-reload-result'},
  extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/d2r-local-dataguide-evidence.json',ROOT/'tools/reference/sentry-death-entry-source-evidence.json',ROOT/'tools/reference/sentry-dead-damage-source-evidence.json'))

if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
