"""Online SQLite snapshot before additive menu expansion; does not change the source DB."""
from pathlib import Path
import json, sqlite3, datetime, shutil, hashlib, sys
root=Path(__file__).resolve().parents[1]
web=root/'src/MellowHouse.Web'
stamp=datetime.datetime.now().strftime('%Y%m%d-%H%M%S')
snapshot=root/'artifacts/backups'/('before-menu-update-'+stamp)
snapshot.mkdir(parents=True)
with sqlite3.connect(web/'App_Data/mellowhouse.db') as source, sqlite3.connect(snapshot/'mellowhouse.db') as target:
    source.backup(target)
    counts={t:target.execute('select count(*) from '+t).fetchone()[0] for t in ['Products','Orders','Customers','PaymentAttempts','CartLines','Favorites','Categories']}
    old_images={p[0]:hashlib.sha256((web/'wwwroot'/p[1].lstrip('/')).read_bytes()).hexdigest() for p in target.execute('select Id,Image from Products')}
(snapshot/'counts.json').write_text(json.dumps(counts,indent=2))
(snapshot/'images.json').write_text(json.dumps(old_images,indent=2))
if '--qa' in sys.argv:
    qa=root/'artifacts/ui-data-v6';qa.mkdir(exist_ok=True)
    assert not (qa/'mellowhouse.db').exists(),'QA database already exists; refusing to overwrite'
    shutil.copy2(snapshot/'mellowhouse.db',qa/'mellowhouse.db')
    shutil.copy2(web/'App_Data/admin-access.json',qa/'admin-access.json')
else:
    (root/'artifacts/menu-completion-update-backup.txt').write_text(str(snapshot),encoding='utf-8')
print(json.dumps({'backup':str(snapshot),'counts':counts}))
