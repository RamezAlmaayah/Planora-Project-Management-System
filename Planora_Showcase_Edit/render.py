"""Editable Planora promo. Real source pixels remain unchanged apart from framing/scaling.
Run: python render.py --preview OR python render.py --output Planora_Professional_Showcase.mp4
Edit timeline.json to change ordering, durations, crops, or titles.
"""
from pathlib import Path
import argparse, json, math, time, subprocess, functools
import cv2, numpy as np, imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFont, ImageFilter
cv2.setNumThreads(2)
ROOT=Path(__file__).resolve().parent; ASSETS=ROOT/'assets'; CHECKS=ROOT/'checks'; CHECKS.mkdir(exist_ok=True)
config=json.loads((ROOT/'timeline.json').read_text()); scenes=config['scenes'];W,H=config['width'],config['height'];FPS=config['fps'];D=sum(s['duration'] for s in scenes)
starts=np.cumsum([0]+[s['duration'] for s in scenes[:-1]]).tolist()
@functools.lru_cache()
def font(size,bold=False):
 for p in [Path('C:/Windows/Fonts')/('segoeuib.ttf' if bold else 'segoeui.ttf'),Path('/usr/share/fonts/truetype/dejavu')/('DejaVuSans-Bold.ttf' if bold else 'DejaVuSans.ttf')]:
  if p.exists():return ImageFont.truetype(str(p),size)
 return ImageFont.load_default(size=size)
FG=(32,35,31);MUT=(112,119,109)
y,x=np.mgrid[0:H,0:W];g=np.exp(-((x-1100)/1100)**2-((y-300)/850)**2)
BG=np.stack([np.clip(c+g*7,0,255) for c in [236,238,233]],2).astype('uint8')
imgs={f.stem[-6:]:np.array(Image.open(f).convert('RGB')) for f in ASSETS.glob('*.png') if f.name!='planora-logo.png'}
vids=sorted(ASSETS.glob('*.mp4'))
def video_frame(path,t):
 c=cv2.VideoCapture(str(path));c.set(cv2.CAP_PROP_POS_MSEC,t*1000);ok,fr=c.read();c.release()
 if not ok:raise RuntimeError(f'Cannot decode {path}')
 return cv2.cvtColor(fr,cv2.COLOR_BGR2RGB)
imgs['account']=video_frame(vids[0],1.30)
hero=video_frame(vids[1],1.43)
# Only cache the useful 3-second public-page segment, compressed to conserve memory.
vframes={};c=cv2.VideoCapture(str(vids[1]));idx=0
while True:
 ok,fr=c.read()
 if not ok:break
 if 40<=idx<=120:vframes[idx]=cv2.imencode('.png',fr,[cv2.IMWRITE_PNG_COMPRESSION,1])[1]
 idx+=1
c.release()
logo=Image.open(ASSETS/'planora-logo.png').convert('RGBA');logo=logo.crop(logo.getbbox());logo.thumbnail((112,106),Image.Resampling.LANCZOS)
def ease(t):return (1-math.cos(math.pi*max(0,min(1,t))))*.5

class Browser:
 def __init__(self,box,maxw=1760,maxh=795,center=(960,590)):
  self.box=box;x,y,w,h=box;scale=min(maxw/w,maxh/h)
  self.dw=int(w*scale);self.dh=int(h*scale);self.bar=49
  self.px=round(center[0]-self.dw/2);self.py=round(center[1]-(self.dh+self.bar)/2)
  px,py,dw,dh=self.px,self.py,self.dw,self.dh
  shadow=Image.new('RGBA',(W,H));d=ImageDraw.Draw(shadow)
  d.rounded_rectangle((px+1,py+15,px+dw-1,py+self.bar+dh+15),radius=24,fill=(31,38,28,40));shadow=shadow.filter(ImageFilter.GaussianBlur(20))
  im=Image.alpha_composite(Image.fromarray(BG).convert('RGBA'),shadow);d=ImageDraw.Draw(im)
  d.rounded_rectangle((px-2,py-2,px+dw+2,py+self.bar+dh+2),radius=22,fill=(247,248,245),outline=(193,199,189),width=2)
  for j,col in enumerate([(220,128,120),(222,191,113),(148,178,139)]):d.ellipse((px+21+j*23,py+20,px+32+j*23,py+31),fill=col)
  aw=min(520,int(dw*.43));ax=px+(dw-aw)//2
  d.rounded_rectangle((ax,py+10,ax+aw,py+38),radius=8,fill=(232,235,228),outline=(225,229,220),width=1)
  d.text((ax+aw/2,py+13),'Planora',font=font(15),fill=(100,108,96),anchor='mt')
  d.rounded_rectangle((px+dw-42,py+16,px+dw-24,py+33),radius=3,outline=(140,148,135),width=1)
  d.line((px+1,py+self.bar-1,px+dw-1,py+self.bar-1),fill=(213,218,207),width=1)
  self.base=np.array(im.convert('RGB'))
  mask=Image.new('L',(dw,dh),255);md=ImageDraw.Draw(mask)
  # Only the bottom corners are rounded; the top edge meets the browser toolbar.
  md.rectangle((0,dh-18,dw,dh),fill=0);md.rounded_rectangle((0,dh-38,dw-1,dh-1),radius=18,fill=255)
  self.mask=np.array(mask);self.edge=self.mask<255
 def put(self,base,arr,p,focus=(.5,.25),reverse=False):
  x,y,w,h=self.box;zoom=1+.014*(1-p if reverse else p);cw=w/zoom;ch=h/zoom
  # Camera framing changes only; application pixels are never generated or edited.
  sx=x+(w-cw)*focus[0];sy=y+(h-ch)*focus[1]
  mat=np.array([[cw/self.dw,0,sx],[0,ch/self.dh,sy]],np.float32)
  shot=cv2.warpAffine(arr,mat,(self.dw,self.dh),flags=cv2.INTER_CUBIC|cv2.WARP_INVERSE_MAP,borderMode=cv2.BORDER_REPLICATE)
  px,py=self.px,self.py+self.bar;im=base.copy();region=im[py:py+self.dh,px:px+self.dw]
  # Mask affects the presentation boundary only.
  shot[self.edge]=region[self.edge];region[:]=shot
  return im

browsers=[];bases=[];captions=[]
for i,s in enumerate(scenes):
 kind=s.get('kind','still')
 if kind in ['intro','outro']:
  browsers.append(None);bases.append(None);captions.append(None);continue
 browser=Browser(s['crop']);browsers.append(browser);im=Image.fromarray(browser.base);d=ImageDraw.Draw(im)
 d.text((82,31),s['title'],font=font(34,True),fill=FG)
 d.text((83,77),s['subtitle'],font=font(21),fill=MUT)
 d.text((1836,43),'PLANORA',font=font(20,True),fill=FG,anchor='rt')
 d.line((83,117,1837,117),fill=(208,215,201),width=1)
 d.text((83,1038),'PLANORA  /  PRODUCT SHOWCASE',font=font(14),fill=MUT)
 d.text((1837,1035),f'{i:02d} / 15',font=font(16),fill=MUT,anchor='rt')
 bases.append(np.array(im));captions.append(np.array(im)[25:106,75:1500].copy())
intro_browser=Browser([0,0,1350,710],maxw=1080,maxh=740,center=(1313,572))
intro_base=Image.fromarray(intro_browser.base);d=ImageDraw.Draw(intro_base)
intro_base.paste(logo,(95,253),logo);d.text((90,390),'PLANORA',font=font(86,True),fill=FG)
d.text((95,508),'Intelligent Project',font=font(31),fill=FG);d.text((95,552),'Management',font=font(31),fill=FG)
d.line((96,632,252,632),fill=(151,164,138),width=4);d.text((96,672),'Plan. Manage. Deliver.',font=font(23),fill=MUT)
d.text((96,1010),'THE PLANORA EXPERIENCE',font=font(15),fill=MUT);intro_base=np.array(intro_base)
outro=Image.fromarray(BG);d=ImageDraw.Draw(outro);outro.paste(logo,((W-logo.width)//2,265),logo)
d.text((960,443),'PLANORA',font=font(94,True),fill=FG,anchor='mt');d.text((960,583),'Plan. Manage. Deliver.',font=font(31),fill=MUT,anchor='mt')
d.line((843,675,1077,675),fill=(151,164,138),width=3);d.text((960,725),'SCRUM  •  V-MODEL  •  AI  •  QA',font=font(21),fill=FG,anchor='mt');outro=np.array(outro)

def scene_frame(i,t):
 s=scenes[i];kind=s.get('kind','still');p=ease(t/s['duration'])
 if kind=='intro':
  im=intro_browser.put(intro_base,hero,p)
  return cv2.addWeighted(BG,1-ease(t/.4),im,ease(t/.4),0) if t<.4 else im
 if kind=='outro':
  fade=ease((t-(s['duration']-.7))/.7)
  return cv2.addWeighted(outro,1-fade,BG,fade,0)
 if kind=='video':
  # Keep one purposeful scroll; discard reverse navigation and contact-page pauses.
  source_t=min(3.95,1.43+t*.79);idx=min(120,max(40,round(source_t*30)))
  arr=cv2.cvtColor(cv2.imdecode(vframes[idx],cv2.IMREAD_COLOR),cv2.COLOR_BGR2RGB)
 else:arr=imgs[s['source']]
 focus=(.50,0) if s.get('source')=='211800' else ((.72,.25) if s.get('source')=='211707' else (.50,.20))
 im=browsers[i].put(bases[i],arr,p,focus,reverse=i%3==0)
 if t<.35:
  a=ease(t/.35);im[25:106,75:1500]=cv2.addWeighted(BG[25:106,75:1500],1-a,captions[i],a,0)
 return im
modes=['zoom','fade','push','fade','push','cut','zoom','push','fade','zoom','push','fade','zoom','fade','push','fade']
def get_frame(t):
 i=max(j for j,st in enumerate(starts) if st<=t);local=t-starts[i];im=scene_frame(i,local)
 if i==len(scenes)-1:return im
 mode=modes[i];td=.30 if mode=='push' else .35
 if mode=='cut' or local<scenes[i]['duration']-td:return im
 p=(local-(scenes[i]['duration']-td))/td;q=ease(p);other=scene_frame(i+1,0)
 if mode=='push':
  shift=round(q*W);out=np.empty_like(im)
  if shift<W:out[:,:W-shift]=im[:,shift:]
  if shift:out[:,W-shift:]=other[:,:shift]
  blur=int(9*math.sin(p*math.pi))|1
  return cv2.blur(out,(blur,1)) if blur>1 else out
 if mode=='zoom':
  mat=cv2.getRotationMatrix2D((960,570),0,1+.035*q);im=cv2.warpAffine(im,mat,(W,H),flags=cv2.INTER_CUBIC,borderMode=cv2.BORDER_REPLICATE)
 return cv2.addWeighted(im,1-q,other,q,0)

if __name__=='__main__':
 ap=argparse.ArgumentParser();ap.add_argument('--preview',action='store_true');ap.add_argument('--output',default=str(ROOT.parent/'Planora_Professional_Showcase.mp4'));args=ap.parse_args()
 if args.preview:
  sheet=Image.new('RGB',(1920,math.ceil(len(scenes)/3)*380),(225,229,220));d=ImageDraw.Draw(sheet)
  for i,s in enumerate(scenes):
   t=starts[i]+min(1.8,s['duration']/2);im=Image.fromarray(get_frame(t));im.save(CHECKS/f'scene_{i:02d}.png');im.thumbnail((640,360));x=i%3*640;y=i//3*380;sheet.paste(im,(x,y));d.text((x+5,y+363),f'{t:.1f}s - {s["title"]}',fill='black')
  sheet.save(CHECKS/'preview.jpg')
 else:
  cmd=[imageio_ffmpeg.get_ffmpeg_exe(),'-y','-hide_banner','-loglevel','warning','-f','rawvideo','-pix_fmt','rgb24','-s',f'{W}x{H}','-r',str(FPS),'-i','-','-an','-c:v','libx264','-preset','fast','-crf','17','-pix_fmt','yuv420p','-profile:v','high','-level','4.2','-movflags','+faststart','-threads','6',args.output]
  with open(CHECKS/'render.log','w') as log:
   p=subprocess.Popen(cmd,stdin=subprocess.PIPE,stderr=log);beg=time.time()
   for n in range(round(D*FPS)):
    p.stdin.write(get_frame(n/FPS).tobytes())
    if n%(FPS*5)==0:print(f'{n/FPS:.0f}/{D:.0f}s rendered; elapsed {time.time()-beg:.0f}s',flush=True)
   p.stdin.close();rc=p.wait()
  if rc:raise RuntimeError((CHECKS/'render.log').read_text())
  print('RENDER COMPLETE:',args.output,flush=True)


