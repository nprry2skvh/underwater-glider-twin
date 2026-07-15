const pptxgen = require('pptxgenjs');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const spec = JSON.parse(fs.readFileSync(path.join(root, '.superpowers/sdd/paper-ppt-assets/slide_spec.json'), 'utf8'));
const assetsDir = path.join(root, '.superpowers/sdd/paper-ppt-assets');
const out = path.join(root, '水下滑翔机论文组会汇报_中文.pptx');

const pptx = new pptxgen();
pptx.layout = 'LAYOUT_WIDE'; // 13.333 x 7.5
pptx.author = 'OpenAI';
pptx.subject = 'Underwater glider navigation paper review';
pptx.title = '面向无人测绘的自主水下滑翔机';
pptx.company = 'Academic presentation';
pptx.lang = 'zh-CN';
pptx.theme = { headFontFace: 'Microsoft YaHei', bodyFontFace: 'Microsoft YaHei', lang: 'zh-CN' };
pptx.defineSlideMaster({
  title: 'MASTER',
  background: { color: '081B33' },
  objects: [
    { rect: { x: 0, y: 7.12, w: 13.333, h: 0.38, fill: { color: '061326' }, line: { color: '061326' } } },
    { text: { text: 'Underwater Glider Navigation · IEEE JOE 2025', options: { x: 0.45, y: 7.2, w: 8.6, h: 0.15, fontFace: 'Aptos', fontSize: 8, color: '8BA8C7', margin: 0 } } },
  ],
  slideNumber: { x: 12.55, y: 7.18, color: 'D8B45A', fontFace: 'Aptos', fontSize: 9 }
});

const C = { navy: '081B33', panel: '102D4E', blue: '2D78B8', cyan: '63C7E6', gold: 'D8B45A', white: 'F4F8FC', muted: 'A9BED3', red: 'E56B6F', green: '76C893' };
function addTitle(slide, title, takeaway) {
  slide.addText(title, { x: 0.55, y: 0.34, w: 8.9, h: 0.46, fontFace: 'Microsoft YaHei', fontSize: 24, bold: true, color: C.white, margin: 0, breakLine: false });
  slide.addShape(pptx.ShapeType.line, { x: 0.56, y: 0.94, w: 12.1, h: 0, line: { color: C.blue, width: 1.2 } });
  slide.addText(takeaway, { x: 9.45, y: 0.38, w: 3.25, h: 0.36, fontSize: 11, color: C.gold, bold: true, align: 'right', margin: 0, fit: 'shrink' });
}
function addBullets(slide, bullets, x=0.7, y=1.25, w=5.5, h=4.8) {
  const runs = [];
  bullets.forEach((b, i) => { runs.push({ text: b, options: { bullet: { indent: 16 }, hanging: 4, breakLine: i < bullets.length-1 } }); });
  slide.addText(runs, { x, y, w, h, fontSize: 16, color: C.white, breakLine: false, valign: 'mid', paraSpaceAfterPt: 15, margin: 0.08, fit: 'shrink' });
}
function panel(slide,x,y,w,h,fill=C.panel){ slide.addShape(pptx.ShapeType.roundRect,{x,y,w,h,rectRadius:0.08,fill:{color:fill,transparency:4},line:{color:'2B5B86',width:1}}); }
function chip(slide,text,x,y,w,color=C.blue){ slide.addShape(pptx.ShapeType.roundRect,{x,y,w,h:0.38,rectRadius:0.08,fill:{color},line:{color}}); slide.addText(text,{x:x+0.08,y:y+0.08,w:w-0.16,h:0.2,fontSize:10,bold:true,color:C.white,align:'center',margin:0,fit:'shrink'}); }
function flow(slide, labels, y=3.05){ const start=0.8, gap=0.15, w=(11.8-gap*(labels.length-1))/labels.length; labels.forEach((t,i)=>{ const x=start+i*(w+gap); panel(slide,x,y,w,0.82,'123C63'); slide.addText(t,{x:x+0.1,y:y+0.25,w:w-0.2,h:0.3,fontSize:15,bold:true,color:C.white,align:'center',margin:0,fit:'shrink'}); if(i<labels.length-1) slide.addText('→',{x:x+w+0.01,y:y+0.2,w:gap-0.02,h:0.35,fontSize:22,bold:true,color:C.gold,align:'center',margin:0}); }); }
function addImage(slide,file,x,y,w,h){ slide.addImage({path:path.join(assetsDir,file),x,y,w,h,sizingContain:true}); }

spec.forEach((s, idx) => {
  const slide = pptx.addSlide('MASTER');
  slide.background = { color: C.navy };
  addTitle(slide, s.title, s.takeaway);
  if (idx === 0) {
    slide.addText('An Autonomous Underwater Glider\nWith Improved Onboard Navigation for Unattended Mapping',{x:0.8,y:1.55,w:7.5,h:1.35,fontFace:'Aptos Display',fontSize:25,bold:true,color:C.white,margin:0,breakLine:false,fit:'shrink'});
    slide.addText('IEEE Journal of Oceanic Engineering · 50(3) · July 2025',{x:0.85,y:3.2,w:7.5,h:0.35,fontSize:15,color:C.cyan,margin:0});
    slide.addShape(pptx.ShapeType.arc,{x:8.45,y:1.45,w:3.7,h:3.7,line:{color:C.blue,width:5,transparency:15},adjustPoint:0.25});
    slide.addShape(pptx.ShapeType.arc,{x:8.95,y:1.95,w:2.7,h:2.7,line:{color:C.gold,width:3,transparency:10},adjustPoint:0.25});
    slide.addText('AUG',{x:9.15,y:2.85,w:2.3,h:0.6,fontSize:30,bold:true,color:C.white,align:'center',margin:0});
    chip(slide,'自主导航',0.85,4.55,1.25,C.blue); chip(slide,'无人测绘',2.25,4.55,1.25,C.gold); chip(slide,'低功耗',3.65,4.55,1.25,C.green);
  } else if (idx===1) {
    panel(slide,0.75,1.35,11.85,4.95); addBullets(slide,s.bullets,1.1,1.75,10.9,3.7);
    slide.addText('Paper identity',{x:1.1,y:5.45,w:2,h:0.25,fontSize:11,color:C.cyan,bold:true,margin:0});
  } else if ([5,6,8,11].includes(idx)) {
    addBullets(slide,s.bullets,0.75,1.28,5.15,4.9);
    panel(slide,6.2,1.45,6.3,4.7);
    if(idx===5) { addImage(slide,'fig_system_architecture.png',6.35,1.62,6.0,4.38); }
    if(idx===6) { flow(slide,['DVL 速度','AHRS 姿态','BSD 融合','实时状态'],2.2); slide.addText('< 15 W navigation + sonar',{x:6.65,y:4.35,w:5.35,h:0.45,fontSize:22,bold:true,color:C.gold,align:'center',margin:0}); }
    if(idx===8) { const ss=['Raspberry Pi 4 BSD','Sea-Bird CTD','600-kHz DVL','Sparton M2 AHRS','700-kHz MSIS']; ss.forEach((t,i)=>chip(slide,t,6.75,1.85+i*0.72,4.9,[C.blue,C.cyan,C.gold,C.green,'5A7CC2'][i])); }
    if(idx===11) { slide.addText('11%',{x:6.7,y:2.0,w:2.0,h:1.0,fontSize:42,bold:true,color:C.green,align:'center',margin:0}); slide.addText('BMC 平均位置误差',{x:6.45,y:3.1,w:2.5,h:0.4,fontSize:13,color:C.white,align:'center',margin:0}); slide.addText('44%',{x:9.7,y:2.0,w:2.0,h:1.0,fontSize:42,bold:true,color:C.red,align:'center',margin:0}); slide.addText('Frontseat 内部估计',{x:9.35,y:3.1,w:2.7,h:0.4,fontSize:13,color:C.white,align:'center',margin:0}); slide.addShape(pptx.ShapeType.line,{x:8.75,y:2.1,w:0,h:1.5,line:{color:C.muted,width:2,dash:'dash'}}); addImage(slide,'fig_bathymetry.png',6.15,3.75,6.2,1.95); }
  } else if ([2,3,4,7,9,10,12,14].includes(idx)) {
    addBullets(slide,s.bullets,0.7,1.3,5.0,4.9);
    if(s.assets && s.assets.length){ panel(slide,5.95,1.3,6.7,5.35,'0B223C'); if(idx===14){ addImage(slide,'fig_bathymetry.png',6.1,1.5,6.4,2.25); addImage(slide,'fig_arctic_survey.png',6.1,3.95,6.4,2.25); } else { addImage(slide,s.assets[0],6.1,1.5,6.4,4.8); } slide.addText(s.source_reference,{x:6.15,y:6.28,w:6.2,h:0.22,fontSize:9,color:C.muted,align:'right',margin:0}); }
    else { panel(slide,6.0,1.55,6.35,4.5); if(idx===2) flow(slide,['长航时','低扰动测绘','多场景验证'],2.8); if(idx===3) flow(slide,['无 GPS','误差累积','多传感器融合'],2.8); if(idx===4) flow(slide,['DVL-Odo 里程计','BSD 后座','BMC 航点修正'],2.8); }
  } else if (idx===13) { addBullets(slide,s.bullets,0.75,1.3,6.2,4.9); panel(slide,7.2,1.4,5.2,4.9); flow(slide,['状态记录','误差校准','航点更新','回放闭环'],2.6); slide.addText('应用推断（非论文直接结果）',{x:7.35,y:4.45,w:4.9,h:0.35,fontSize:15,bold:true,color:C.gold,align:'center',margin:0}); }
  slide.addText(`Source: ${s.source_reference}`,{x:0.58,y:6.84,w:10.4,h:0.18,fontSize:8,color:C.muted,margin:0,fit:'shrink'});
});

pptx.writeFile({ fileName: out });
console.log(`wrote ${out}`);
