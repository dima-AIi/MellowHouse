"""Create delivery-sized WebP assets; originals and alpha are preserved."""
from pathlib import Path
import json
from concurrent.futures import ThreadPoolExecutor
from PIL import Image

root=Path(__file__).resolve().parents[1]/'src/MellowHouse.Web/wwwroot'
paths=[];before=after=0
sources=[p for p in sorted((root/'images/products').iterdir()) if p.suffix.lower() in ('.png','.webp','.jpg','.jpeg')]
def prepare(src):
    with Image.open(src) as original:
        original=original.convert('RGBA')
        for width in (320,480,640,768,960):
            dest=root/'images/optimized/products'/f'{src.name}-{width}.webp'
            dest.parent.mkdir(parents=True,exist_ok=True)
            if dest.exists():
                with Image.open(dest) as existing:existing.verify()
                continue
            temporary=dest.with_suffix('.tmp')
            original.resize((width,max(1,round(original.height*width/original.width))),Image.Resampling.LANCZOS).save(temporary,'WEBP',quality=82,method=3)
            temporary.replace(dest)
    return src
with ThreadPoolExecutor(max_workers=4) as pool:
    for src in pool.map(prepare,sources):
        paths.append('/images/products/'+src.name);before+=src.stat().st_size
        after+=(root/'images/optimized/products'/f'{src.name}-640.webp').stat().st_size
(root/'js/product-images.js').write_text('const optimized=new Set('+json.dumps(paths,ensure_ascii=False)+');\n'+'''export function image(path,width=320){return optimized.has(path)?`/images/optimized/${path.slice(8)}-${width}.webp`:path;}
export function imageSet(path){return optimized.has(path)?[320,480,640,768,960].map(w=>`${image(path,w)} ${w}w`).join(', '):'';}
''',encoding='utf-8')
print(f'{len(paths)} images: originals {before:,} bytes, 640px WebP {after:,} bytes')
