from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import math
out=Path('src/KhitunGeo/wwwroot/brand')
out.mkdir(parents=True,exist_ok=True)
S=4
N=128*S
im=Image.new('RGBA',(N,N),(0,0,0,0)); d=ImageDraw.Draw(im)
scale=lambda v:int(round(v*S))
d.rounded_rectangle((scale(3),scale(3),scale(125),scale(125)),radius=scale(27),fill='#10243b')
# arc approximates the measuring arc, with its open end facing the staff
arc=[]
for i in range(41):
    t=i/40
    # quadratic Bezier through (48,28), (24,64), (48,100)
    x=(1-t)**2*48+2*(1-t)*t*24+t*t*48
    y=(1-t)**2*28+2*(1-t)*t*64+t*t*100
    arc.append((scale(x),scale(y)))
d.line(arc,fill='#35c9b0',width=scale(6),joint='curve')
for x,y in [(48,28),(48,100)]:
    d.ellipse((scale(x-3),scale(y-3),scale(x+3),scale(y+3)),fill='#35c9b0')
# K arms first, then the survey staff over the center
for end in [(105,30),(105,98)]:
    d.line([(scale(62),scale(64)),(scale(end[0]),scale(end[1]))],fill='#2bc3a4',width=scale(9))
    d.ellipse((scale(end[0]-4.5),scale(end[1]-4.5),scale(end[0]+4.5),scale(end[1]+4.5)),fill='#2bc3a4')
d.line([(scale(61),scale(16)),(scale(61),scale(112))],fill='#f5f8fb',width=scale(6))
for y in (20,108):
    d.line([(scale(53),scale(y)),(scale(69),scale(y))],fill='#f5f8fb',width=scale(6))
d.ellipse((scale(51),scale(53),scale(73),scale(75)),fill='#f5f8fb')
d.ellipse((scale(55),scale(57),scale(69),scale(71)),fill='#10243b')
d.ellipse((scale(58.5),scale(60.5),scale(65.5),scale(67.5)),fill='#2bc3a4')
for size in (32,64,128,256,512):
    im.resize((size,size),Image.Resampling.LANCZOS).save(out/f'khitun_geo_icon_{size}.png',optimize=True)
icon=im.resize((256,256),Image.Resampling.LANCZOS)
icon.save(out/'khitun_geo.ico',format='ICO',sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
# Native About dialog lockup on white, mark + carefully spaced wordmark.
W,H=1252,841
lock=Image.new('RGBA',(W,H),(255,255,255,255))
mark=im.resize((360,360),Image.Resampling.LANCZOS)
lock.alpha_composite(mark,(70,240))
draw=ImageDraw.Draw(lock)
fontpath='/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
font=ImageFont.truetype(fontpath,112)
font2=ImageFont.truetype(fontpath,108)
draw.text((475,326),'Khitun',font=font,fill='#10243b',stroke_width=0)
box=draw.textbbox((0,0),'Khitun',font=font); x=475+box[2]+22
draw.text((x,326),'Geo',font=font2,fill='#13846f',stroke_width=0)
lock.save(out/'khitun_geo_logo.png',optimize=True)
# Keep native desktop icon and embedded web asset byte-identical.
Path('src/KhitunGeo/brand/khitun_geo.ico').write_bytes((out/'khitun_geo.ico').read_bytes())
