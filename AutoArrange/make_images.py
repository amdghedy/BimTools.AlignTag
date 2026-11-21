# Regenerates images/options_overview.png and images/before_after.png (needs matplotlib)
import math, random, matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Arc

GREEN="#32aa50"; RED="#cd2d2d"; INK="#3c3c3c"; BLUE="#286ec8"
AX=[150,185,220,255]

def stack(ax,n=4,x0=70,y0=70,step=22,ang=80,style=0,land=0,zig=False,hsep=0,crossed=False,labels=None,hl=False,ox=0,oy=0,sc=1):
    cot=math.cos(math.radians(ang))/math.sin(math.radians(ang)); heads=[]
    for k in range(n):
        ei=n-1-k if crossed else k
        A=(AX[ei],22); H=(x0+(hsep if zig and k%2 else 0), y0+k*step)
        if style==1: E=(H[0]+land,H[1])
        elif style==2: E=((A[0]+H[0])/2,(A[1]+H[1])/2)
        else: E=(A[0]-(H[1]-A[1])*cot,H[1])
        P=[(ox+sc*x,oy+sc*y) for x,y in (A,E,H)]
        ax.plot([p[0] for p in P],[p[1] for p in P],color=INK,lw=1.2); ax.plot(P[0][0],P[0][1],"o",color=GREEN,ms=6)
        w,h=44*sc,13*sc
        ax.add_patch(Rectangle((P[2][0]-w,P[2][1]-h/2),w,h,fc="#d7e6fa" if hl else "#f0f0f0",ec=BLUE if hl else "silver"))
        ax.text(P[2][0]-w+2*sc,P[2][1],(labels[k] if labels else "WP"),fontsize=6*sc+1,va="center",color=INK)
        heads.append(P[2])
    return heads

def panel(ax,title):
    ax.set_xlim(0,300); ax.set_ylim(170,0); ax.set_aspect("equal"); ax.set_xticks([]); ax.set_yticks([])
    ax.set_title(title,fontsize=9,fontweight="bold",loc="left")

def dim(ax,a,b,t,vert=True):
    ax.annotate("",xy=a,xytext=b,arrowprops=dict(arrowstyle="<->",color=RED,lw=1.3))
    ax.text((a[0]+b[0])/2+(5 if vert else -10),(a[1]+b[1])/2-(0 if vert else 8),t,color=RED,fontsize=7,fontweight="bold")

def note(a,t): a.text(8,158,t,fontsize=7,color="gray")

fig,axs=plt.subplots(3,3,figsize=(13,9.2))
a=axs[0,0]; panel(a,"1. Leader shape: Parallel slope"); stack(a); note(a,"Landing lines horizontal, diagonals parallel")
a=axs[0,1]; panel(a,"2. Leader shape: Equal landing"); h=stack(a,style=1,land=34); dim(a,(70,h[1][1]+11),(104,h[1][1]+11),"landing",False); note(a,"Same landing length on every leader")
a=axs[0,2]; panel(a,"3. Leader shape: Straight line"); stack(a,style=2); note(a,"One straight line per tag")
a=axs[1,0]; panel(a,"4. Row spacing"); h=stack(a); dim(a,(14,h[0][1]),(14,h[1][1]),"row spacing"); note(a,"Gap between rows (mm on sheet x view scale)")
a=axs[1,1]; panel(a,"5. Staggered two-column layout"); h=stack(a,x0=60,y0=70,step=15,zig=True,hsep=52); dim(a,(60,h[0][1]-12),(112,h[0][1]-12),"stagger offset",False); note(a,"Alternate tags shift sideways")
a=axs[1,2]; panel(a,"6. Row order: Fewest crossings")
stack(a,labels=["","","",""],crossed=True,oy=15,sc=0.5); stack(a,labels=["","","",""],ox=150,oy=15,sc=0.5)
a.plot([150,150],[10,125],color="silver"); a.text(6,128,"before: leaders cross",color=RED,fontsize=7,fontweight="bold"); a.text(156,128,"after: fewest crossings",color=GREEN,fontsize=7,fontweight="bold")
a=axs[2,0]; panel(a,"7. Leader slope")
for s,ang in enumerate((80,45)):
    ox=s*150; A=(ox+120,30); L=100 if s==0 else 70
    E=(A[0]-L*math.cos(math.radians(ang)),A[1]+L*math.sin(math.radians(ang))); H=(E[0]-20,E[1])
    a.plot([A[0],E[0],H[0]],[A[1],E[1],H[1]],color=INK,lw=1.3); a.plot(*A,"o",color=GREEN,ms=6)
    a.plot([E[0],E[0]+40],[E[1],E[1]],"--",color="gray")
    a.add_patch(Arc(E,40,40,theta1=-ang,theta2=0,color=RED,lw=1.5))   # y axis is inverted -> negative angles are "up"
    a.text(E[0]+12,E[1]-24,f"{ang}\u00b0",color=RED,fontsize=9,fontweight="bold")
    a.add_patch(Rectangle((H[0]-28,H[1]-6.5),28,13,fc="#f0f0f0",ec="silver"))
note(a,"Slope measured from the horizontal landing line")

bx=[4,34,66,98]; by=[22,70,44,100]
def heads(ax,title,mode):
    panel(ax,title); aft=[[bx[i],by[i]] for i in range(4)]
    if mode=="left":
        for i in range(4): aft[i][0]=4
    if mode=="distv":
        for k,i in enumerate([0,2,1,3]): aft[i][1]=22+78*k/3
    for i in range(4):
        ax.add_patch(Rectangle((bx[i],by[i]),28,13,fc="#f0f0f0",ec="silver"))
        ax.add_patch(Rectangle((150+aft[i][0],aft[i][1]),28,13,fc="#d7e6fa",ec=BLUE))
    ax.annotate("",xy=(150,70),xytext=(136,70),arrowprops=dict(arrowstyle="->",color=BLUE,lw=2))
    ax.plot([148,148],[8,140],color="silver"); ax.text(50,134,"before",color=RED,fontsize=7,fontweight="bold"); ax.text(205,134,"after",color=GREEN,fontsize=7,fontweight="bold")
heads(axs[2,1],"8. Align Tag Heads: Align left","left")
heads(axs[2,2],"9. Align Tag Heads: Distribute vertically","distv")
fig.suptitle("BIMTools Auto-Arrange - what each option does (same pictures as the in-app help)",fontsize=12,fontweight="bold")
fig.tight_layout(rect=[0,0,1,0.96]); fig.savefig("images/options_overview.png",dpi=110)

fig,axs=plt.subplots(1,2,figsize=(9,3.4)); random.seed(3)
panel(axs[0],"Before: crossed leaders, ragged tags")
for i in range(6):
    A=(150+i*20,25); H=(random.randint(60,130),random.randint(60,140))
    axs[0].plot([A[0],(A[0]+H[0])/2+random.randint(-20,20),H[0]],[A[1],random.randint(40,120),H[1]],color=INK,lw=1); axs[0].plot(*A,"o",color=GREEN,ms=6)
    axs[0].add_patch(Rectangle((H[0]-44,H[1]-6.5),44,13,fc="#f0f0f0",ec="silver")); axs[0].text(H[0]-42,H[1],"100 WP",fontsize=6.5,va="center")
panel(axs[1],"After: Smart Tag Arrange")
stack(axs[1],labels=["100 WP","100 WP","150 WP","100 WP"])
fig.tight_layout(); fig.savefig("images/before_after.png",dpi=110)
