const assert=require('node:assert/strict');
const {verify}=require('./compare-layout.cjs');
const rect=([x,y,width,height])=>({x,y,width,height});
function fixture(width){
 const data={workspace:Object.fromEntries(Object.entries({titlebar:[1,1,1438,39],navigation:[1,40,220,678],workspace:[226,40,1213,678],cards:[244,94,1177,522],card:[244,94,582.5,255],log:[226,630,1213,88],status:[1,718,1438,41]}).map(([key,value])=>[key,rect(value)]))};
 for(const culture of ['en-US','zh-CN','zh-Hant','ja-JP']){
  const surface=rect([18,14,1044,656]),navigation=rect([19,15,150,654]);
  data['appearance-'+culture]={surface,navigation,mode:rect([454,41,502,30]),palette:rect([454,89,502,30])};
  data['about-'+culture]={surface,navigation,head:rect([198,41,width,42]),metadata:rect([198,101,width,195]),update:rect([198,321,width,132])};
 }
 return data;
}
// 覆盖 Linux 窄字体和较宽的多字节字体，并确保真实布局回退仍会被拒绝。
for(const width of [389,528,647,720])assert.equal(verify(name=>fixture(width)[name]).contentCount,12);
for(const damage of [
 data=>data['about-en-US'].metadata.width-=12,
 data=>data['about-en-US'].update.y+=10,
 data=>data['about-en-US'].head.width=721,
 data=>data['about-en-US'].head.x+=20,
 data=>data.workspace.navigation.width+=40,
 data=>data['about-en-US'].head.y=NaN,
]){
 const data=fixture(528);damage(data);assert.throws(()=>verify(name=>data[name]));
}
console.log('PASS font-dependent widths remain valid; misalignment, overflow and fixed-region regressions are rejected.');
