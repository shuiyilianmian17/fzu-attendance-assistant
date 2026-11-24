const assert=require('node:assert');
const cp=require('node:child_process');
const path=require('node:path');
const bridge=path.resolve(__dirname,'../api/bridge.js');
const root=path.dirname(bridge);
for(const scenario of ['today','yesterday','pending','retry','dry']){
 const setup=[
  'const path=require("node:path");',
  'const root='+JSON.stringify(root)+';',
  'const today=new Date(Date.now()+8*3600000).toISOString().slice(0,10);',
  'require.cache[path.join(root,"index.js")]={exports:{resolveApi:async()=>({}),runOnce:async()=>({kind:'+JSON.stringify(scenario==='retry'?'retry':'success')+'})}};',
  'require.cache[path.join(root,"lib/store.js")]={exports:{Store:class{constructor(){this.token="fake"}}}};',
  'require.cache[path.join(root,"lib/api.js")]={exports:{createClient:()=>({init:async()=>({statusData:{status:'+JSON.stringify(scenario==='pending'?'PROCESSING':'SUCCESS')+',createTime:'+(scenario==='yesterday'?'"2000-01-01"':'today+" 21:05:00"')+'}})})}};',
  'require('+JSON.stringify(bridge)+');'
 ].join('\n');
 const out=cp.execFileSync(process.execPath,['-e',setup],{input:JSON.stringify({account:'test',dry:scenario==='dry',dataDir:require('node:os').tmpdir()})+'\n',encoding:'utf8'});
 const result=JSON.parse(out.trim());
 assert.equal(result.success,scenario==='today',scenario);
}
console.log('通过接口适配检查：今日成功、旧记录、处理中、失败与只读检查。');
