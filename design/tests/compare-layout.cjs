// 同尺寸原生 Skia 截图边界基线；交并比只适用于列出的布局区域。
const fs=require('node:fs'),path=require('node:path');
const directory=path.resolve(process.argv[2]||process.env.NEXUS_AUDIT_OUTPUT||path.join(__dirname,'../../artifacts/prototype-audit'));
let minimum=1,count=0;
function compare(name,baseline){const actual=JSON.parse(fs.readFileSync(path.join(directory,name+'.geometry.json'),'utf8'));for(const[key,[x,y,width,height]]of Object.entries(baseline)){const a=actual[key];if(!a)throw Error('Missing '+name+'/'+key);const intersection=Math.max(0,Math.min(a.x+a.width,x+width)-Math.max(a.x,x))*Math.max(0,Math.min(a.y+a.height,y+height)-Math.max(a.y,y));const score=intersection/(a.width*a.height+width*height-intersection);minimum=Math.min(minimum,score);count++;if(score<.95)throw Error(`${name}/${key} IoU=${score}`);}}
compare('workspace',{titlebar:[1,1,1438,39],navigation:[1,40,220,678],workspace:[226,40,1213,678],cards:[244,94,1177,522],card:[244,94,582.5,255],log:[226,630,1213,88],status:[1,718,1438,41]});
const widths={'en-US':528,'zh-CN':455,'zh-Hant':467,'ja-JP':647};
for(const culture of Object.keys(widths)){
 compare('appearance-'+culture,{surface:[18,14,1044,656],navigation:[19,15,150,654],mode:[454,41,502,30],palette:[454,89,502,30]});
 compare('about-'+culture,{surface:[18,14,1044,656],navigation:[19,15,150,654],head:[198,41,widths[culture],42],metadata:[198,101,widths[culture],195],update:[198,321,widths[culture],132]});
}
console.log(`PASS ${count} measured regions; minimum IoU ${(minimum*100).toFixed(2)}%. Not a whole-page pixel similarity score.`);
