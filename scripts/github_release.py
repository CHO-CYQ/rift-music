"""Publish a prerelease using the standard Git credential helper; never persist tokens."""
import argparse
import json
import os
import subprocess
import tempfile
import urllib.parse
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('--repo',required=True)
parser.add_argument('--commit',required=True)
parser.add_argument('--notes',required=True)
parser.add_argument('--proxy')
parser.add_argument('--asset',action='append',default=[])
args=parser.parse_args()
env=os.environ.copy();env['GIT_TERMINAL_PROMPT']='0';env['GCM_INTERACTIVE']='never'
response=subprocess.run(['git','-c','credential.interactive=never','credential','fill'],input='protocol=https\nhost=github.com\n\n',text=True,capture_output=True,env=env,check=True,timeout=30)
credential=dict(line.split('=',1) for line in response.stdout.splitlines() if '=' in line)
token=credential.get('password')
if not token:raise SystemExit('No GitHub credential available')

def request(method,url,body=None,upload=None):
    # Credentials stay in the stdin pipe, not command arguments, files or output.
    config=['url = '+json.dumps(url),'request = '+json.dumps(method),'header = '+json.dumps('Authorization: Bearer '+token),
            'header = "Accept: application/vnd.github+json"','header = "User-Agent: RiftMusic-release"','silent','show-error','max-time = 1200','http1.1']
    if args.proxy:config.append('proxy = '+json.dumps(args.proxy))
    temp=None
    try:
        if body is not None:
            temp=tempfile.NamedTemporaryFile(mode='w',encoding='utf-8',suffix='.json',delete=False)
            json.dump(body,temp,ensure_ascii=False);temp.close();payload=temp.name
            config.append('header = "Content-Type: application/json"')
        elif upload:
            payload=str(Path(upload).resolve());config.append('header = "Content-Type: application/octet-stream"')
        command=['curl.exe','--config','-','--write-out','\n%{http_code}']
        if body is not None or upload:command+=['--data-binary','@'+payload]
        result=subprocess.run(command,input='\n'.join(config)+'\n',text=True,encoding='utf-8',capture_output=True,timeout=1210)
        if result.returncode:raise RuntimeError('GitHub transport failed: '+result.stderr[:240])
        content,status=result.stdout.rsplit('\n',1)
        data=json.loads(content) if content else {}
        if int(status)>=400:raise RuntimeError('GitHub '+status+': '+str(data.get('message','request failed')))
        return data
    finally:
        if temp:Path(temp.name).unlink(missing_ok=True)

profile=request('GET','https://api.github.com/user')
if profile['login'].casefold()!=args.repo.split('/')[0].casefold():raise SystemExit('Credential owner differs from release owner')
base='https://api.github.com/repos/'+args.repo
repo=request('GET',base)
assert repo['full_name'].casefold()==args.repo.casefold()
existing=request('GET',base+'/releases?per_page=100')
release=next((r for r in existing if r['tag_name']=='v0.1'),None)
if release is None:
    release=request('POST',base+'/releases',{'tag_name':'v0.1','target_commitish':args.commit,'name':'峡谷序曲 v0.1 测试版','body':Path(args.notes).read_text('utf-8'),'draft':True,'prerelease':True})
print(json.dumps({'release_id':release['id'],'draft':release['draft']},ensure_ascii=True),flush=True)
for asset in args.asset:
    name=Path(asset).name
    matched=next((a for a in release['assets'] if a['name']==name),None)
    if matched:
        assert matched['state']=='uploaded' and matched['size']==Path(asset).stat().st_size,'Existing asset mismatch'
        continue
    url=release['upload_url'].split('{')[0]+'?name='+urllib.parse.quote(name)
    uploaded=request('POST',url,upload=asset)
    assert uploaded['state']=='uploaded' and uploaded['size']==Path(asset).stat().st_size
    print(json.dumps({'uploaded':name,'bytes':uploaded['size']},ensure_ascii=True),flush=True)
release=request('PATCH',base+'/releases/'+str(release['id']),{'draft':False,'prerelease':True})
verified=request('GET',base+'/releases/tags/v0.1')
assert not verified['draft'] and verified['prerelease']
print(json.dumps({'url':verified['html_url'],'prerelease':verified['prerelease'],'assets':[{'name':a['name'],'size':a['size'],'state':a['state'],'url':a['browser_download_url']} for a in verified['assets']]},ensure_ascii=True),flush=True)
