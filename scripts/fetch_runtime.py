"""Fetch Microsoft runtime packages over verified TLS into the local build feed."""
import concurrent.futures
import hashlib
from pathlib import Path
import urllib.request

feed = Path(__file__).resolve().parents[1] / "artifacts" / "nuget"
feed.mkdir(parents=True, exist_ok=True)
def fetch(package):
    name = f"{package}.10.0.5.nupkg"
    if (feed / name).is_file():
        print(f"Using cached {name}", flush=True)
        return
    url = f"https://api.nuget.org/v3-flatcontainer/{package}/10.0.5/{name}"
    data = urllib.request.urlopen(url, timeout=60).read()
    (feed / name).write_bytes(data)
    print(f"Downloaded {name}: {len(data)} bytes; SHA256 {hashlib.sha256(data).hexdigest()}", flush=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    list(pool.map(fetch, ["microsoft.netcore.app.runtime.win-x64", "microsoft.windowsdesktop.app.runtime.win-x64", "microsoft.aspnetcore.app.runtime.win-x64", "microsoft.net.illink.tasks"]))
