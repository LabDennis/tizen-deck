'use strict';
// Input is scoped to the displayed image; leaving it discards unsent events.
(function(){
const area=document.querySelector('#screen-input'),image=document.querySelector('#tv-screen'),status=document.querySelector('#input-status');
let hovering=false,sending=false,pendingMouse=null,queue=[],lastWheel=0,lastMove=0;
function active(){return hovering&&!document.hidden&&!image.hidden&&location.hash==='#remote'&&!document.querySelector('#shell').hidden;}
function stop(){hovering=false;pendingMouse=null;queue=[];area.classList.remove('input-active');status.textContent='Eingabe inaktiv';}
async function drain(){
 if(sending||!active())return;
 const event=queue.shift()||pendingMouse;if(!event)return;if(event===pendingMouse)pendingMouse=null;
 sending=true;
 try{await api('/api/input',event);}
 catch(e){stop();status.textContent='Eingabe gestoppt: '+e.message;}
 finally{sending=false;if(active()&&(queue.length||pendingMouse))drain();}
}
function send(event){if(!active())return;if(queue.length>=16){stop();status.textContent='Eingabe gestoppt: Verbindung zu langsam';return;}queue.push(event);drain();}
image.addEventListener('mouseenter',()=>{hovering=true;area.classList.add('input-active');status.textContent='Eingabe aktiv · Maus herausbewegen zum Beenden';area.focus({preventScroll:true});});
image.addEventListener('mouseleave',stop);
window.addEventListener('blur',stop);window.addEventListener('hashchange',stop);document.addEventListener('visibilitychange',()=>{if(document.hidden)stop();});
image.addEventListener('mousemove',e=>{if(!active()||(window.deckInputCapabilities&&window.deckInputCapabilities.pointer===false)||Date.now()-lastMove<100)return;lastMove=Date.now();const box=image.getBoundingClientRect();pendingMouse={mouse:{x:Math.max(0,Math.min(image.naturalWidth-1,Math.floor((e.clientX-box.left)*image.naturalWidth/box.width))),y:Math.max(0,Math.min(image.naturalHeight-1,Math.floor((e.clientY-box.top)*image.naturalHeight/box.height)))}};drain();});
image.addEventListener('click',e=>{if(!active())return;e.preventDefault();send({key:'KEY_ENTER'});});
image.addEventListener('contextmenu',e=>{if(!active())return;e.preventDefault();send({key:'KEY_RETURN'});});
image.addEventListener('wheel',e=>{if(!active())return;e.preventDefault();if(Date.now()-lastWheel<180)return;lastWheel=Date.now();send({key:e.deltaY>0?'KEY_DOWN':'KEY_UP'});},{passive:false});
document.addEventListener('keydown',e=>{
 if(!active()||e.altKey||e.ctrlKey||e.metaKey||e.isComposing||document.activeElement!==area)return;
 const keys={ArrowUp:'KEY_UP',ArrowDown:'KEY_DOWN',ArrowLeft:'KEY_LEFT',ArrowRight:'KEY_RIGHT',Enter:'KEY_ENTER',Escape:'KEY_RETURN',Backspace:'KEY_BACKSPACE',Tab:'KEY_TAB'};
 if(keys[e.key]){e.preventDefault();send({key:keys[e.key]});}
 else if(e.key.length===1&&(!window.deckInputCapabilities||window.deckInputCapabilities.text!==false)){e.preventDefault();send({text:e.key});}
},true);
})();
