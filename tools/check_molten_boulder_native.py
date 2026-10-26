#!/usr/bin/env python3
"""Verify prepared Molten Boulder and retained Firestorm in a disposable world; never install."""
import hashlib,json,os,shutil,socket,struct,sys,tempfile,threading
from pathlib import Path
import argparse
WORK=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--native-root',type=Path,default=WORK,help='Project containing matching native distribution and installed server assets; nothing there is modified')
MAIN=parser.parse_args().native_root.resolve()
STAGE=WORK/'.local/class-skills/molten-boulder'
os.environ.setdefault('SANCTUARY_TEST_SERVER',str(MAIN/'.local/server'))
sys.path.insert(0,str(WORK/'tests'))
from check_britain import SERVER,Session
os.environ.setdefault('SANCTUARY_PROJECT_ROOT',str(WORK))
def run():
 STAGE.mkdir(parents=True,exist_ok=True)
 source=json.loads((WORK/'tools/reference/molten-boulder-damage-source-evidence.json').read_text())
 assert hashlib.sha256((WORK/source['fixture']['path']).read_bytes()).hexdigest()==source['fixture']['sha256']
 source=json.loads((WORK/'tools/reference/molten-boulder-impact-source-evidence.json').read_text())
 assert hashlib.sha256((WORK/source['fixture']['path']).read_bytes()).hexdigest()==source['fixture']['sha256']
 with tempfile.TemporaryDirectory(prefix='sanctuary-boulder-',dir='/dev/shm') as temporary:
  folder=Path(temporary)
  for entry in SERVER.iterdir():
   if entry.is_file() and entry.suffix in ('.dll','.json','.config'):shutil.copy2(entry,folder/entry.name)
  (folder/'runtimes').symlink_to(SERVER/'runtimes',target_is_directory=True);shutil.copytree(SERVER/'Configuration',folder/'Configuration')
  for name in ['Assemblies','Data']:
   (folder/name).mkdir()
   for entry in (SERVER/name).iterdir():
    if entry.name not in ['Sanctuary.Content.dll','Sanctuary.Integration.dll','Sanctuary','assemblies.json']:(folder/name/entry.name).symlink_to(entry,target_is_directory=entry.is_dir())
  native=MAIN/'upstream/ModernUO/Distribution'
  for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:shutil.copy2(native/name,folder/name)
  for name,kind in [('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')]:(folder/'Assemblies'/name).symlink_to(STAGE/kind/name)
  shutil.copytree(WORK/'data/Sanctuary',folder/'Data/Sanctuary')
  assemblies=json.loads((SERVER/'Data/assemblies.json').read_text())
  for name in ['Sanctuary.Content.dll','Sanctuary.Integration.dll']:
   if name not in assemblies:assemblies.append(name)
  (folder/'Data/assemblies.json').write_text(json.dumps(assemblies))
  configpath=folder/'Configuration/modernuo.json';config=json.loads(configpath.read_text())
  with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
  with socket.socket(type=socket.SOCK_DGRAM) as sock:sock.bind(('127.0.0.1',0));ping=sock.getsockname()[1]
  config['listeners']=[f'127.0.0.1:{port}'];config['settings'].update({'autosave.enabled':'False','pingServer.enabled':'True','pingServer.port':str(ping),'sanctuary.graveyard.enabled':'False','world.savePath':'Saves','crashGuard.restartServer':'False','sanctuary.professions.enabled':'True'});configpath.write_text(json.dumps(config))
  shutil.copy2(WORK/'.local/class-skills/molten-boulder-damage-source-probe.json',folder/'molten-boulder-damage-source-probe.json')
  shutil.copy2(WORK/'.local/class-skills/molten-boulder-impact-source-probe.json',folder/'molten-boulder-impact-source-probe.json')
  try:
   with (STAGE/'molten-boulder-checks.log').open('w') as log:
    for boot in range(3):
     session=Session(folder,log)
     try:
      ready=session.expect(r'create the owner account now\? \(y/n\):|Listening: 127.0.0.1:'+str(port))
      if ready.group(0).startswith('create'):session.send('n');session.expect(f'Listening: 127.0.0.1:{port}')
      for family in ['firestorm','molten-boulder']:
       command=family+('-check' if boot==0 else '-reload-check');session.send(command)
       assert session.expect(command+r': (passed|failed)').group(1)=='passed'
      with socket.create_connection(('127.0.0.1',port),timeout=5) as client:
       def drain():
        try:
         while client.recv(65536):pass
        except OSError:pass
       threading.Thread(target=drain,daemon=True).start()
       session.send('molten-boulder-input-check');assert session.expect('molten-boulder-input-check: (passed|failed)').group(1)=='passed'
       ready=session.expect(r'molten-boulder-input: ready ability=(\d+) x=(\d+) y=(\d+) z=(-?\d+)');ability,x,y,z=map(int,ready.groups())
       client.sendall(struct.pack('>BHHBBIBIHHb',0xBF,21,0xF2,7,3,1,ability,0,x,y,z));assert session.expect('molten-boulder-input-result: (passed|failed)',timeout=12).group(1)=='passed'
      session.send('save');session.expect('Writing world save snapshot done');session.send('shutdown');assert session.process.wait(timeout=20)==0
     finally:session.close()
   text=(STAGE/'molten-boulder-checks.log').read_text();assert ' ERR]' not in text and ': failed' not in text
   shutil.copy2(folder/'molten-boulder-fixtures.json',STAGE/'native-fixtures.json')
   print('Learned Molten Boulder and retained Firestorm pass3boots/2actual reloads/3natural connected Boulder quick casts in disposable world; installed build untouched.')
  except BaseException:
   destination=STAGE/'failed-world';destination.mkdir(exist_ok=True)
   for name in ['Saves','Configuration']:
    if (folder/name).exists():shutil.copytree(folder/name,destination/name,dirs_exist_ok=True)
   for name in folder.glob('*.json'):shutil.copy2(name,destination/name.name)
   raise
if __name__=='__main__':run()
