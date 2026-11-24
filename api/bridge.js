'use strict';
const readline = require('node:readline');
const path = require('node:path');
const fs = require('node:fs');
const input = readline.createInterface({input:process.stdin});
const lines = input[Symbol.asyncIterator]();
const emit = data => process.stdout.write(JSON.stringify(data)+'\n');
console.log = () => {};
console.error = () => {};
global.fzuSmsInput = async () => {
  emit({sms:true});
  const next = await lines.next();
  return next.done ? '' : JSON.parse(next.value).code;
};
global.fzuSmsInput.close = () => {};
(async () => {
  const first = await lines.next();
  const request = JSON.parse(first.value);
  const {resolveApi,runOnce} = require('./index');
  const {Store} = require('./lib/store');
  const {createClient} = require('./lib/api');
  fs.mkdirSync(request.dataDir,{recursive:true});
  const store = new Store(request.dataDir,request.account);
  const cfg = {...request,wait:false,retry:0,jitterSeconds:0,restKey:'',caIsNo:'0'};
  if (!request.interactive) delete global.fzuSmsInput;
  const api = await resolveApi(cfg,store,request.interactive);
  if (!request.dry) {
    const result = await runOnce(cfg,store,{interactive:request.interactive,lng:request.lng,lat:request.lat});
    if (result.kind!=='success') {
      emit({success:false,message:result.kind==='not-open'?'未到服务器签到时间。':'接口尚未完成签到，请检查服务器时段、位置与登录状态。'});
      return;
    }
  }
  const status = await createClient(store.token).init();
  const confirmed = status.statusData?.status === 'SUCCESS';
  const recordTime=String(status.statusData?.createTime || '');
  const today=new Date(Date.now()+8*3600000).toISOString().slice(0,10);
  const success=confirmed && recordTime.startsWith(today);
  emit({success:!request.dry&&success,message:request.dry?'接口连接成功，已读取服务器签到状态。':success?'今日签到已确认。':'已提交，服务器尚未确认今日签到成功，稍后重查。'});
})().catch(()=>emit({success:false,message:'接口登录或请求失败。请检查账号、密码、网络，必要时点击检查连接完成短信验证。'})).finally(()=>{input.close();process.exit(0);});
