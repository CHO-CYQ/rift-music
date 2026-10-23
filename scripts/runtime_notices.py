"""Retain the license/notice files from the exact bundled runtime packs."""
from pathlib import Path
import sys
import zipfile

repo = Path(__file__).resolve().parent.parent
target = Path(sys.argv[1]).resolve() / '说明' / 'ThirdParty'
target.mkdir(parents=True, exist_ok=True)
for package, label in [('microsoft.netcore.app.runtime.win-x64', 'NETCore'),
                       ('microsoft.windowsdesktop.app.runtime.win-x64', 'WindowsDesktop')]:
    with zipfile.ZipFile(repo / 'artifacts' / 'nuget' / f'{package}.10.0.5.nupkg') as archive:
        for name in archive.namelist():
            if name.upper() in ('LICENSE', 'LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT'):
                (target / f'{label}-{name}.txt').write_bytes(archive.read(name))
print(f'Runtime notices copied to {target}')
