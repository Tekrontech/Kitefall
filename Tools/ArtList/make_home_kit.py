import cv2, numpy as np, os, sys
# Cuts the home-screen kit (background without UI, kite, 9-slice skins, white icons) out of docs/design/home_screen_design.jpg.
# Usage: python3 Tools/ArtList/make_home_kit.py docs/design/kit   (then copy the files into Assets/Art/Incoming – Image Mapper installs them).
SRC='/home/user/Kitefall/docs/design/home_screen_design.jpg'
OUT=sys.argv[1]; os.makedirs(OUT, exist_ok=True)
img=cv2.imread(SRC)  # BGR 1600x900
H,W=img.shape[:2]

def rounded_mask(h,w,r):
    m=np.zeros((h,w),np.uint8)
    cv2.rectangle(m,(r,0),(w-1-r,h-1),255,-1); cv2.rectangle(m,(0,r),(w-1,h-1-r),255,-1)
    for cx,cy in [(r,r),(w-1-r,r),(r,h-1-r),(w-1-r,h-1-r)]: cv2.circle(m,(cx,cy),r,255,-1,cv2.LINE_AA)
    return cv2.GaussianBlur(m,(3,3),0)

def skin(name, box, radius, edge=14, keep_rows=7):
    x0,y0,x1,y1=box
    c=img[y0:y1,x0:x1].astype(np.float32); h,w=c.shape[:2]
    hsv=cv2.cvtColor(c.astype(np.uint8),cv2.COLOR_BGR2HSV).astype(np.float32)
    out=c.copy()
    for y in range(keep_rows,h-keep_rows):
        row=c[y,edge:w-edge]; s=hsv[y,edge:w-edge,1]; v=hsv[y,edge:w-edge,2]
        ok=~((v>200)&(s<110))            # drop white / cream text and glyphs
        sel=row[ok] if ok.sum()>w*0.2 else row
        col=np.median(sel,axis=0)
        out[y,edge:w-edge]=col
    # soften the seam between kept edges and the rebuilt middle
    out=cv2.bilateralFilter(out.astype(np.uint8),5,30,5).astype(np.float32)
    a=rounded_mask(h,w,radius)
    rgba=np.dstack([out.astype(np.uint8),a])
    cv2.imwrite(f'{OUT}/{name}.png',rgba)

def panel_skin(name, box, radius, edge, strip):
    x0,y0,x1,y1=box
    c=img[y0:y1,x0:x1].astype(np.float32); h,w=c.shape[:2]
    cols=np.median(c[:,strip[0]:strip[1]],axis=1)            # per-row colour of a clean margin strip
    ys=np.arange(h); inner=(ys>edge)&(ys<h-edge)
    hsv=cv2.cvtColor(cols[None].astype(np.uint8),cv2.COLOR_BGR2HSV)[0]
    clean=inner&(cols[:,2]<cols[:,0]+40)                       # skip rows where an orange button crosses the strip
    if clean.sum()<4: clean=inner
    fit=np.zeros_like(cols)
    for ch in range(3):
        k=np.polyfit(ys[clean],cols[clean,ch],2); fit[:,ch]=np.polyval(k,ys)
    out=c.copy()
    out[edge:h-edge,edge:w-edge]=fit[edge:h-edge,None,:]
    half=w//2
    out[:, :half]=out[:, w-half:][:, ::-1]                    # mirror the clean right half (the left has the star badge)
    out=cv2.bilateralFilter(out.astype(np.uint8),5,30,5)
    cv2.imwrite(f'{OUT}/{name}.png',np.dstack([out,rounded_mask(h,w,radius)]))

def play_skin(name, box):
    x0,y0,x1,y1=box
    c=img[y0:y1,x0:x1]; h,w=c.shape[:2]
    hsv=cv2.cvtColor(c,cv2.COLOR_BGR2HSV)
    btn=((hsv[...,0]<=40)&(hsv[...,1]>90)&(hsv[...,2]>140)).astype(np.uint8)*255
    btn=cv2.morphologyEx(btn,cv2.MORPH_CLOSE,np.ones((9,9),np.uint8))
    ff=btn.copy(); m=np.zeros((h+2,w+2),np.uint8); cv2.floodFill(ff,m,(0,0),255); btn=btn|cv2.bitwise_not(ff)
    btn=cv2.erode(btn,np.ones((3,3),np.uint8)); btn=cv2.GaussianBlur(btn,(3,3),0)
    tmp=OUT+'/_play.png'; skin('_play',box,26,edge=40,keep_rows=14)
    out=cv2.imread(tmp,cv2.IMREAD_UNCHANGED); out[...,3]=btn; os.remove(tmp)
    cv2.imwrite(f'{OUT}/{name}.png',out)

def white_icon(name, cx, cy, bw, bh, lo=150, hi=225):
    x0,y0=int(cx-bw/2),int(cy-bh/2)
    c=img[y0:y0+bh,x0:x0+bw].astype(np.float32)
    mn=c.min(axis=2)
    a=np.clip((mn-lo)/(hi-lo),0,1)
    a=(a*255).astype(np.uint8)
    size=128; pad=12
    s=(size-2*pad)/max(bw,bh)
    a2=cv2.resize(a,(int(bw*s),int(bh*s)),interpolation=cv2.INTER_CUBIC)
    canvas=np.zeros((size,size),np.uint8)
    oy=(size-a2.shape[0])//2; ox=(size-a2.shape[1])//2
    canvas[oy:oy+a2.shape[0],ox:ox+a2.shape[1]]=a2
    rgba=np.dstack([np.full((size,size,3),255,np.uint8),canvas])
    cv2.imwrite(f'{OUT}/ui_icon_{name}.png',rgba)

# ---- 9-slice skins
skin('ui_button_blue',(31,160,327,222),14)
skin('ui_button_selected',(1110,437,1342,503),14)
skin('ui_button_locked',(1350,438,1578,502),14)
skin('ui_button_orange',(1128,356,1560,412),16)
panel_skin('ui_panel',(1108,112,1582,430),18,14,(16,24))
panel_skin('ui_pill',(1455,180,1560,224),12,6,(5,9))
play_skin('ui_button_play',(1116,742,1576,870))

# ---- white glyph icons
rows={'shirt':190,'people':257,'target':325,'hangar':393,'wrench':460,'cart':528,'star':596,'trophy':663}
for k,y in rows.items(): white_icon(k,72,y,54,50)
white_icon('gear',708,53,46,46); white_icon('mail',896,53,48,40); white_icon('pencil',362,48,26,26,lo=105,hi=185)
white_icon('swords',1163,469,44,42); white_icon('lock',1396,468,34,40,lo=100,hi=175); white_icon('door',1395,543,34,40)
white_icon('kite',1397,612,42,42); white_icon('chevron_right',1557,692,22,30); white_icon('chevron_left',1128,692,22,30)
white_icon('clock',1470,145,22,22,lo=110,hi=190)

# ---- kite (animated separately): colour key against the sky
kx0,ky0,kx1,ky1=915,110,1110,285
k=img[ky0:ky1,kx0:kx1]
hsv=cv2.cvtColor(k,cv2.COLOR_BGR2HSV)
hue,sat,val=hsv[...,0].astype(int),hsv[...,1].astype(int),hsv[...,2].astype(int)
warm=((hue<=28)|(hue>=165))&(sat>120)&(val>90)
a=(warm*255).astype(np.uint8)
a=cv2.morphologyEx(a,cv2.MORPH_CLOSE,np.ones((3,3),np.uint8))
n,lab,stats,_=cv2.connectedComponentsWithStats(a)
keep=np.zeros_like(a)
for i in range(1,n):
    if stats[i,cv2.CC_STAT_AREA]>25: keep[lab==i]=255
keep=cv2.GaussianBlur(keep,(3,3),0)
cv2.imwrite(f'{OUT}/home_kite.png',np.dstack([k,keep]))
kite_mask=np.zeros((H,W),np.uint8); kite_mask[ky0:ky1,kx0:kx1]=cv2.dilate(keep,np.ones((7,7),np.uint8))

# ---- clean background: inpaint UI boxes, the kite and its line
mask=kite_mask.copy()
boxes=[(15,4,462,140),(664,18,849,89),(849,18,1024,89),(1088,18,1590,89),(1106,110,1584,432),(1106,434,1582,648),(1106,650,1584,735),(1112,738,1580,872),(338,728,958,870)]
for i in range(9): boxes.append((28,156+i*67.7,330,224+i*67.7))
for b in boxes:
    x0,y0,x1,y1=map(int,b); cv2.rectangle(mask,(x0-2,y0-2),(x1+2,y1+2),255,-1)
pts=np.array([(554,592),(585,560),(669,470),(724,410),(750,380),(777,350),(803,320),(853,260),(878,230),(902,200),(928,170)],np.int32)
cv2.polylines(mask,[pts],False,255,13)   # kite line from the flyer's hand
# Fill hidden areas at low resolution (smooth, frosted look – the UI sits on top of them anyway),
# but the small kite + line areas with a fine inpaint so the open sky stays clean.
fine=np.zeros_like(mask); fine[0:300,880:1130]=kite_mask[0:300,880:1130]
cv2.polylines(fine,[pts],False,255,13)
# The painted flyer is replaced in game by the player's own 3D character (HomeCharacterStage): erase it.
boy=np.zeros_like(mask); cv2.ellipse(boy,(506,600),(64,80),0,0,360,255,-1); cv2.rectangle(boy,(446,560),(570,678),255,-1)
fine|=boy; mask|=boy
bg=cv2.inpaint(img,fine,5,cv2.INPAINT_TELEA)
small=cv2.resize(bg,(W//8,H//8),interpolation=cv2.INTER_AREA)
msmall=cv2.resize(mask,(W//8,H//8),interpolation=cv2.INTER_NEAREST)
small=cv2.inpaint(small,msmall,3,cv2.INPAINT_TELEA)
big=cv2.GaussianBlur(cv2.resize(small,(W,H),interpolation=cv2.INTER_CUBIC),(0,0),6)
coarse=mask.copy(); coarse[fine>0]=0
f=cv2.GaussianBlur(coarse,(0,0),3).astype(np.float32)[...,None]/255.0
bg=(bg*(1-f)+big*f).astype(np.uint8)
# Fill the erased flyer with the sea / roof tiles just to his right (a real texture instead of a smear), feathered in.
sx,sy,sw,sh,shift=440,516,136,166,128
patch=bg[sy:sy+sh,sx+shift:sx+shift+sw].copy()
feather=np.zeros((H,W),np.float32); cv2.ellipse(feather,(506,598),(70,86),0,0,360,1.0,-1); cv2.rectangle(feather,(444,566),(572,680),1.0,-1)
feather=cv2.GaussianBlur(feather,(0,0),6)[sy:sy+sh,sx:sx+sw,None]
bg[sy:sy+sh,sx:sx+sw]=(bg[sy:sy+sh,sx:sx+sw]*(1-feather)+patch*feather).astype(np.uint8)
bg=cv2.resize(bg,(1920,1080),interpolation=cv2.INTER_CUBIC)
cv2.imwrite(f'{OUT}/home_background.jpg',bg,[cv2.IMWRITE_JPEG_QUALITY,90])
print('ok')
