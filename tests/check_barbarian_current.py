#!/usr/bin/env python3
"""Current Barbarian class composition on the exact accepted Leap Attack build."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_leap_attack import configuration as leap
from check_grim_ward_actor import configuration as ward
from check_frenzy import configuration as frenzy

FAMILIES=('barbarian-passives','barbarian-masteries','throwing-mastery','whirlwind','double-throw',
 'barbarian-attacks','concentrate','berserk','shout','battle-orders','battle-command','howl','battlecry','taunt',
 'find-item','find-potion','double-swing','frenzy','grim-ward-actor','barbarian-binding')
ASYNC=('whirlwind','double-throw','concentrate','berserk','shout','battle-orders','battle-command','howl','battlecry','taunt','find-item')

def snapshots(session,port,boot):
 count=0
 for command in ['find-item-snapshot-check','find-potion-snapshot-check']:
  session.send(command);assert session.expect(command+r': (passed|failed)').group(1)=='passed';count+=1
 return count

def configuration():
 c=leap(False);c['stage']=ROOT/'.local/class-skills/barbarian-current'
 c['extra_families']+=FAMILIES
 c.setdefault('extra_wait_results',{}).update({'barbarian-passives-check':'barbarian-passives-result','barbarian-attacks-check':'barbarian-attacks-result'})
 c.setdefault('extra_connected_checks',())
 for family in ASYNC:
  for ending in ['-check','-reload-check']:
   command=family+ending;c['extra_wait_results'][command]=family+'-result'
   if family in ('whirlwind','double-throw','concentrate','berserk','find-item'):c['extra_connected_checks']+=(command,)
 for source in [ward(False),frenzy(False)]:
  c['extra_files'].update(source['extra_files'])
  for field in ['extra_checks','extra_target_input_commands','extra_connected_checks']:
   c[field]=tuple(dict.fromkeys(c.get(field,())+source.get(field,())))
  c['extra_wait_results'].update(source.get('extra_wait_results',{}))
 c.setdefault('extra_input_timeouts',{})['frenzy-left']=12
 c['extra_files']['barbarian-binding-reference.json']=ROOT/'tools/reference/barbarian-binding-reference.json'
 for name in ['barbarian-mastery','throwing-projectile','leap','leap-collision','find-potion-native']:
  c['extra_files'][name+'-source-probe.json']=ROOT/'.local/class-skills'/(name+'-source-probe.json')
 c['extra_manifest_names'].update({n:n+'.json'for n in FAMILIES if n not in ['whirlwind','double-throw','find-potion','double-swing','frenzy','grim-ward-actor','barbarian-binding']})
 for n in ['howl','taunt','battlecry','battle-orders','shout','battle-command','find-item-loot']:
  p=ROOT/'.local/class-skills'/(n+'-source-probe.json')
  if p.is_file():c['extra_files'][p.name]=p
 c['extra_inputs']+=(ROOT/'tools/reference/barbarian-passive-preflight.json',ROOT/'tools/reference/find-item-engine-evidence.json')
 c['extra_checks']+=('leap-geometry-check','find-item-primitives-check')
 c['extra_wire_callbacks']=(snapshots,)
 c['extra_connection_settle']=0.15
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c

if __name__=='__main__':
 c=configuration()
 for path in c['extra_files'].values():assert path.is_file(),path
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(**c)
