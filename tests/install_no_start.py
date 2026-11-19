from pathlib import Path
import tempfile, subprocess, shutil
root=Path(tempfile.mkdtemp(prefix='still-install-review-'))
pkg=root/'package'; pkg.mkdir()
for name in ['README.md','LICENSE','THIRD-PARTY-NOTICES.md','uninstall.sh','still.png']:
 (pkg/name).write_text('fixture\n')
(pkg/'licences').mkdir()
(pkg/'still').write_text('#!/bin/sh\nexit 0\n')
(pkg/'still.desktop').write_text('[Desktop Entry]\nExec=@EXEC@\n')
mock=root/'mock'; mock.mkdir()
commands={
 'pgrep':f'if [ ! -f "{root}/seen" ]; then touch "{root}/seen"; exit 0; fi; exit 1',
 'pkill':'exit 0',
 'setsid':f'printf "%s\\n" "$*" > "{root}/started"',
 'update-desktop-database':'exit 0',
 'gtk-update-icon-cache':'exit 0',
}
script=Path('packaging/linux/install.sh').read_text().replace('$HOME',str(root/'home'))
script=script.replace('${XDG_DATA_HOME:-'+str(root/'home')+'/.local/share}',str(root/'data'))
script=script.replace('${XDG_CONFIG_HOME:-'+str(root/'home')+'/.config}',str(root/'config'))
for command,body in commands.items():
 path=mock/command; path.write_text('#!/bin/sh\n'+body+'\n'); path.chmod(0o755)
 script=script.replace(command,str(path))
(pkg/'install.sh').write_text(script)
result=subprocess.run(['/bin/sh',str(pkg/'install.sh'),'--no-start','--no-autostart'],capture_output=True,text=True)
assert result.returncode == 0, result.stderr
assert not (root/'started').exists(), '--no-start launched the clock'
print('PASS installer respects --no-start for a running copy')
print(result.stderr)
shutil.rmtree(root)
