#!/usr/bin/env python3
"""Approved shared sentry placement with retained five-trap/paid cast consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/sentry-placement'
 c['extra_families']=('sentry-placement','shadow-sentry','sentry-cast')
 c['extra_manifest_names']={'sentry-cast':'sentry-cast-fixture.json'}
 c['extra_checks']=('sentry-learned-mechanics-check',)
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'docs/sentry-occupied-placement-proposal.md')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
