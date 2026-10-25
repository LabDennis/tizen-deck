const {chromium}=require('playwright'),assert=require('node:assert/strict'),fs=require('fs'),path=require('path');
(async()=>{const b=await chromium.launch({headless:true});try{
 const p=await b.newPage({viewport:{width:1440,height:1000}});let native=false,saved=null,wakes=0;
 const config={mac:'02:00:00:00:00:01',broadcast:'192.0.2.255'};
 await p.route('http://deck.test/**',async route=>{
  const req=route.request(),url=new URL(req.url());let status=200,body;
  if(url.pathname==='/api/session')body={csrf:'test',host:'192.0.2.10',username:'admin',mode:native?'tv':'external',apps:{}};
  else if(url.pathname==='/api/device'){status=503;body={error:'Monitor schläft'};}
  else if(url.pathname==='/api/wake-config'){if(req.method()==='POST')saved=req.postDataJSON();body=saved||config;}
  else if(url.pathname==='/api/wake'){assert.equal(req.headers()['x-csrf-token'],'test');wakes++;body={job:'wake'};}
  else if(url.pathname==='/api/jobs/wake')body={status:'done',result:{awake:true,sent:true,message:'Monitor ist eingeschaltet und antwortet.'}};
  else{const name=url.pathname==='/'?'index.html':url.pathname.slice(1);if(!['index.html','app.js','style.css','input.js'].includes(name))return route.fulfill({status:404,body:''});return route.fulfill({contentType:name.endsWith('.js')?'text/javascript':name.endsWith('.css')?'text/css':'text/html',body:fs.readFileSync(path.join(__dirname,'../static',name))});}
  await route.fulfill({status,contentType:'application/json',body:JSON.stringify(body)});
 });
 await p.goto('http://deck.test/');await p.waitForFunction(()=>document.querySelector('#connection').textContent==='Status nicht vollständig');
 assert.equal(await p.locator('#wake-tv').isEnabled(),true,'wake must work when device API is asleep');
 await p.locator('#wake-tv').click();await p.waitForFunction(()=>document.querySelector('#wake-result').textContent.includes('antwortet'));
 assert.equal(wakes,1);
 await p.locator('[data-page=settings]').click();await p.locator('#wake-form button').click();assert.deepEqual(saved,config);
 native=true;await p.goto('http://deck.test/');await p.waitForFunction(()=>document.querySelector('#wake-hint').textContent.includes('Dieser Dienst'));
 assert.equal(await p.locator('#wake-tv').isVisible(),false);assert.equal(await p.locator('#wake-settings').isVisible(),false);
 console.log('Wake browser: usable during standby, configured address, CSRF and native fallback explanation passed');
}finally{await b.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
