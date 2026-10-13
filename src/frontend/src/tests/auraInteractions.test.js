import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import React, { act, useRef } from 'react';
import { JSDOM } from 'jsdom';
import { createServer } from 'vite';

let server, dom, createRoot, AccountAvatar, AccountPresentation, RoleBadge, useAuraMotion, gsap, ScrollTrigger;
const previous = new Map(); let reduced = true;
const animationTimers = new Set(), intervals = new Set();
const nativeSetInterval = globalThis.setInterval, nativeClearInterval = globalThis.clearInterval;
before(async () => {
  dom = new JSDOM('<!doctype html><div id="root"></div>', { url: 'http://localhost' });
  dom.window.matchMedia = query => ({ matches: query.includes('no-preference') ? !reduced : reduced, media: query, addListener() {}, removeListener() {}, addEventListener() {}, removeEventListener() {} });
  dom.window.scrollTo = () => {};
  for (const [key,value] of Object.entries({ window: dom.window, document: dom.window.document, Node: dom.window.Node, HTMLElement: dom.window.HTMLElement, getComputedStyle: dom.window.getComputedStyle, requestAnimationFrame: callback => { const id=setTimeout(()=>{animationTimers.delete(id);callback();},16);animationTimers.add(id);return id; }, cancelAnimationFrame: id=>{clearTimeout(id);animationTimers.delete(id);}, setInterval: (...args)=>{const id=nativeSetInterval(...args);intervals.add(id);return id;}, clearInterval:id=>{nativeClearInterval(id);intervals.delete(id);}, IS_REACT_ACT_ENVIRONMENT: true })) {
    previous.set(key,Object.getOwnPropertyDescriptor(globalThis,key)); Object.defineProperty(globalThis,key,{ value, writable: true, configurable: true });
  }
  ({ createRoot } = await import('react-dom/client'));
  server = await createServer({ server: { middlewareMode: true, hmr: false }, appType: 'custom' });
  AccountAvatar = (await server.ssrLoadModule('/src/components/common/AccountAvatar.jsx')).default;
  AccountPresentation = (await server.ssrLoadModule('/src/components/common/AccountPresentation.jsx')).default;
  RoleBadge = (await server.ssrLoadModule('/src/components/common/RoleBadge.jsx')).default;
  useAuraMotion = (await server.ssrLoadModule('/src/components/aura/useAuraMotion.js')).default;
  ({ gsap } = await server.ssrLoadModule('gsap'));
  ({ ScrollTrigger } = await server.ssrLoadModule('gsap/ScrollTrigger'));
});
after(async () => { gsap?.ticker.sleep(); await server?.close(); dom?.window.close(); for(const id of animationTimers)clearTimeout(id);for(const id of intervals)nativeClearInterval(id); for(const [key,value] of previous) { if(value)Object.defineProperty(globalThis,key,value);else delete globalThis[key]; } });

test('account photographs stay isolated between signed-in and directory accounts', async () => {
  const root=createRoot(document.getElementById('root'));
  try {
    const accounts=[{id:'self',fullName:'Fleet Manager',username:'manager',profileImageUrl:'/self.webp',roles:['FLEET_MANAGER']},{id:'row',fullName:'Customer One',username:'customer',profileImageUrl:'/customer.webp',role:'CUSTOMER'},{id:'empty',username:'no_photo',role:'CUSTOMER'}];
    await act(async()=>root.render(React.createElement('div',null,accounts.map(account=>React.createElement(AccountPresentation,{key:account.id,account})))));
    const images=[...document.querySelectorAll('img')];
    assert.equal(images.length,2);
    assert.match(images[0].src,/\/self.webp$/);assert.match(images[1].src,/\/customer.webp$/);
    assert.equal(images[1].alt,"Customer One's profile photo");
    assert.ok(document.querySelector('[aria-label="no_photo: profile photo unavailable"] svg'));
    assert.doesNotMatch(document.body.textContent,/undefined/);
  } finally { await act(async()=>root.unmount()); }
});
test('failed photographs use a neutral icon and retry when the account or photo changes', async () => {
  const root=createRoot(document.getElementById('root'));
  try {
    const render=account=>act(async()=>root.render(React.createElement(AccountAvatar,{account})));
    await render({id:'a',username:'alice',profileImageUrl:'/shared.webp'});
    await act(async()=>document.querySelector('img').dispatchEvent(new window.Event('error')));
    assert.equal(document.querySelector('img'),null);assert.ok(document.querySelector('.account-avatar-fallback svg'));
    await render({id:'b',username:'bob',profileImageUrl:'/shared.webp'});
    assert.equal(document.querySelector('img').alt,"bob's profile photo");
    await act(async()=>document.querySelector('img').dispatchEvent(new window.Event('error')));
    await render({id:'b',username:'bob',profileImageUrl:'/updated.webp'});
    assert.match(document.querySelector('img').src,/\/updated.webp$/);
    await render({id:'b',username:'bob'});assert.equal(document.querySelector('img'),null);
  } finally { await act(async()=>root.unmount()); }
});
test('account presentation uses the real full name, username fallback and readable roles', async () => {
  const root=createRoot(document.getElementById('root'));
  try {
    await act(async()=>root.render(React.createElement(AccountPresentation,{account:{id:'a',fullName:'  Nimal Perera  ',username:'nimal'},role:'FLEET_MANAGER'})));
    assert.equal(document.querySelector('strong').textContent,'Nimal Perera');assert.equal(document.querySelector('.role-badge').textContent,'Fleet Manager');assert.equal(document.querySelector('.role-badge-dot'),null);
    await act(async()=>root.render(React.createElement(AccountPresentation,{account:{id:'b',fullName:' ',username:'maintenance'},role:'MAINTENANCE_STAFF'})));
    assert.equal(document.querySelector('strong').textContent,'maintenance');assert.equal(document.querySelector('.role-badge').textContent,'Maintenance Staff');
    await act(async()=>root.render(React.createElement(RoleBadge,{})));assert.equal(document.querySelector('.role-badge').textContent,'User');
  } finally { await act(async()=>root.unmount()); }
});
test('marketing motion keeps essential content visible, respects reduction and cleans up navigation', async () => {
  function Scene() { const scope=useRef(null);useAuraMotion(scope);return React.createElement('div',{ref:scope},React.createElement('h1',null,'Your next drive'),React.createElement('button',null,'Browse vehicles'),React.createElement('img',{className:'cinematic-media-image',alt:'Marketing'}),React.createElement('div',{'data-marketing-entrance':''},React.createElement('article',null,'Choose'),React.createElement('article',null,'Book'))); }
  const render=async()=>{const root=createRoot(document.getElementById('root'));await act(async()=>root.render(React.createElement(Scene)));return root;};
  reduced=true;let root=await render();
  assert.equal(document.querySelector('img').style.transform,'');assert.equal(document.querySelector('article').style.transform,'');assert.equal(ScrollTrigger.getAll().length,0);
  await act(async()=>root.unmount());
  reduced=false;root=await render();
  assert.equal(document.querySelector('h1').style.opacity,'');assert.equal(document.querySelector('button').style.transform,'');assert.equal(document.querySelector('article').style.opacity,'');assert.equal(document.querySelector('.pin-spacer'),null);
  assert.equal(document.querySelector('img').style.transform,'','slow media waits for its own load');
  const image=document.querySelector('img');
  await act(async()=>image.dispatchEvent(new window.Event('load')));
  assert.notEqual(image.style.transform,'','media starts its reveal when it loads');
  await act(async()=>root.unmount());assert.equal(ScrollTrigger.getAll().length,0,'route cleanup removes marketing triggers');
  assert.equal(image.style.transform,'','navigation reverts the media animation');
  image.dispatchEvent(new window.Event('load'));assert.equal(image.style.transform,'','navigation removes late media listeners');
  reduced=true;
});
