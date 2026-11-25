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
  if (request.dry) {
    const status=await api.init();
    const confirmed=['SUCCESS','PROCESSING'].includes(status.statusData?.status);
    emit({success:false,message:confirmed?'接口连接成功，服务器返回已签到或处理中。':'接口连接成功，已读取服务器签到状态。'});
    return;
  }
  // 保留原项目 runOnce 对 SUCCESS、PROCESSING、提交响应和复查的判断。
  const result=await runOnce(cfg,store,{interactive:request.interactive,lng:request.lng,lat:request.lat});
  const success=result.kind==='success';
  emit({success,message:success?'原项目接口流程返回签到成功。':result.kind==='not-open'?'未到服务器签到时间。':'接口尚未完成签到，请检查服务器时段、位置与登录状态。'});
})().catch(()=>emit({success:false,message:'接口登录或请求失败。请检查账号、密码、网络，必要时点击检查连接完成短信验证。'})).finally(()=>{input.close();process.exit(0);});
