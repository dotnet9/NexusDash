// 固定布局对照原生 Skia 基线；内容自适应宽度遵循布局契约。
const fs=require('node:fs'),path=require('node:path');

function verify(read){
 let minimum=1,count=0,contentCount=0;
 function compare(name,baseline){
  const actual=read(name);
  for(const[key,[x,y,width,height]]of Object.entries(baseline)){
   const a=actual[key];if(!a||![a.x,a.y,a.width,a.height].every(Number.isFinite))throw Error('Missing or invalid '+name+'/'+key);
   const intersection=Math.max(0,Math.min(a.x+a.width,x+width)-Math.max(a.x,x))*Math.max(0,Math.min(a.y+a.height,y+height)-Math.max(a.y,y));
   const score=intersection/(a.width*a.height+width*height-intersection);
   minimum=Math.min(minimum,score);count++;
   if(score<.95)throw Error(`${name}/${key} IoU=${score}`);
  }
 }
 compare('workspace',{titlebar:[1,1,1438,39],navigation:[1,40,220,678],workspace:[226,40,1213,678],cards:[244,94,1177,522],card:[244,94,582.5,255],log:[226,630,1213,88],status:[1,718,1438,41]});
 for(const culture of ['en-US','zh-CN','zh-Hant','ja-JP']){
  compare('appearance-'+culture,{surface:[18,14,1044,656],navigation:[19,15,150,654],mode:[454,41,502,30],palette:[454,89,502,30]});
  const name='about-'+culture,actual=read(name);
  compare(name,{surface:[18,14,1044,656],navigation:[19,15,150,654]});
  // 原生 StackPanel 左对齐且 MaxWidth=720；系统字体决定自然宽度。
  const width=actual.head?.width;
  if(!Number.isFinite(width)||width<1||width>720)throw Error(`${name} invalid content width=${width}`);
  const right=actual.surface.x+actual.surface.width-28;
  for(const[key,y,height]of [['head',41,42],['metadata',101,195],['update',321,132]]){
   const region=actual[key];
   if(!region||![region.x,region.y,region.width,region.height].every(Number.isFinite)||Math.abs(region.x-198)>1||Math.abs(region.y-y)>1||Math.abs(region.height-height)>1||Math.abs(region.width-width)>1||region.x+region.width>right+1)
    throw Error(`${name}/${key} violates content alignment, height or bounds`);
   contentCount++;
  }
 }
 return {minimum,count,contentCount};
}

if(require.main===module){
 const directory=path.resolve(process.argv[2]||process.env.NEXUS_AUDIT_OUTPUT||path.join(__dirname,'../../artifacts/prototype-audit'));
 const result=verify(name=>JSON.parse(fs.readFileSync(path.join(directory,name+'.geometry.json'),'utf8')));
 console.log(`PASS ${result.count} fixed regions; minimum IoU ${(result.minimum*100).toFixed(2)}%; ${result.contentCount} content-sized regions align within bounds. Not a whole-page pixel similarity score.`);
}
module.exports={verify};
