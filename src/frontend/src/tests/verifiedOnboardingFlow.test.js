import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import React, { act } from 'react';
import { JSDOM } from 'jsdom';
import { createServer } from 'vite';
let dom, server, createRoot, AvailabilityInput, Register, AuthProvider, AccountWorkspace, AccountSecurity, GoogleCompletion, GoogleButton;
const previous = new Map();
before(async () => {
  dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', { url: 'http://localhost' });
  for (const [key, value] of Object.entries({ window: dom.window, document: dom.window.document, HTMLElement: dom.window.HTMLElement, sessionStorage: dom.window.sessionStorage, localStorage: dom.window.localStorage, IS_REACT_ACT_ENVIRONMENT: true })) {
    previous.set(key, Object.getOwnPropertyDescriptor(globalThis, key)); Object.defineProperty(globalThis, key, { configurable: true, writable: true, value });
  }
  previous.set('fetch', Object.getOwnPropertyDescriptor(globalThis, 'fetch'));
  ({ createRoot } = await import('react-dom/client'));
  dom.window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {}, addEventListener() {}, removeEventListener() {} });
  server = await createServer({ server: { middlewareMode: true, hmr: false }, appType: 'custom' });
  AvailabilityInput = (await server.ssrLoadModule('/src/components/onboarding/AvailabilityInput.jsx')).default;
  Register = (await server.ssrLoadModule('/src/pages/onboarding/VerifiedRegisterPage.jsx')).default;
  ({ AuthProvider } = await server.ssrLoadModule('/src/context/AuthContext.jsx'));
  AccountWorkspace = (await server.ssrLoadModule('/src/components/onboarding/AccountWorkspace.jsx')).default;
  AccountSecurity = (await server.ssrLoadModule('/src/components/onboarding/AccountSecurityPanel.jsx')).default;
  GoogleCompletion = (await server.ssrLoadModule('/src/components/onboarding/GoogleSignupCompletion.jsx')).default;
  GoogleButton = (await server.ssrLoadModule('/src/components/onboarding/GoogleButton.jsx')).default;
});
after(async () => { await server.close(); dom.window.close(); for (const [key, descriptor] of previous) { if (descriptor) Object.defineProperty(globalThis, key, descriptor); else delete globalThis[key]; } });
const response = data => new Response(JSON.stringify(data), { headers: { 'Content-Type': 'application/json' } });

test('a Google callback after navigation cannot link an account or finish authentication',async()=>{
  const oldGoogle=window.google;let callback;const calls=[];window.google={accounts:{id:{initialize(options){callback=options.callback;},renderButton(){}}}};
  globalThis.fetch=async url=>{calls.push(String(url));return response({challengeId:'nonce-id',nonce:'synthetic-nonce'});};
  const root=createRoot(document.getElementById('root'));
  try{await act(async()=>root.render(React.createElement(GoogleButton,{clientId:'test-client',link:true,password:'Synthetic@123',onComplete(){throw new Error('Callback must be discarded');}})));assert.equal(typeof callback,'function');await act(async()=>root.unmount());await callback({credential:'synthetic-token'});assert.equal(calls.length,1);assert.match(calls[0],/google\/link\/begin$/);}
  finally{if(oldGoogle===undefined)delete window.google;else window.google=oldGoogle;}
});
test('availability debounce aborts and discards obsolete answers even after returning to the same username', async () => {
  const pending = []; globalThis.fetch = (url, options) => new Promise(resolve => pending.push({ url, options, resolve }));
  const root = createRoot(document.getElementById('root'));
  const render = value => act(async () => root.render(React.createElement(AvailabilityInput, { kind: 'username', value, id: 'availability', label: 'Username', onChange() {} })));
  try {
    await render('first_user'); assert.match(document.body.textContent,/Checking availability/);assert.ok(document.querySelector('.availability-feedback.checking'));
    await act(async () => new Promise(resolve => setTimeout(resolve, 450))); assert.equal(pending.length, 1);
    await render('second_user'); assert.equal(pending[0].options.signal.aborted, true);
    await act(async () => new Promise(resolve => setTimeout(resolve, 450)));
    await render('first_user'); await act(async () => new Promise(resolve => setTimeout(resolve, 450)));
    await act(async () => pending[0].resolve(response({ available: false })));
    assert.doesNotMatch(document.body.textContent, /not available/);
    await act(async () => pending[2].resolve(response({ available: true }))); assert.match(document.body.textContent, /Available/);assert.ok(document.querySelector('.availability-feedback.available'));
    await render('a');await render('first_user');assert.ok(document.querySelector('.availability-feedback.checking'));assert.equal(document.querySelector('.availability-feedback.available'),null);
  } finally { await act(async () => root.unmount()); }
});

test('Google profile completion follows verified identity and never requests a local password', async()=>{
  const root=createRoot(document.getElementById('root'));
  try{await act(async()=>root.render(React.createElement(GoogleCompletion,{request:{signupId:'proof-id',fullName:'Google Name',email:'synthetic@example.test'},onComplete(){},onCancel(){}})));
    assert.equal(document.querySelectorAll('input').length,2);assert.equal(document.querySelector('[type=password]'),null);assert.match(document.body.textContent,/Google account confirmed/);assert.match(document.body.textContent,/synthetic@example.test/);assert.equal(document.querySelector('#google-fullname').value,'Google Name');
  }finally{await act(async()=>root.unmount());}
});
test('Google-only customer security has no current-password requirement or new MFA enrolment control',async()=>{
  const root=createRoot(document.getElementById('root'));
  try{await act(async()=>root.render(React.createElement(AuthProvider,null,React.createElement(AccountSecurity,{profile:{email:'synthetic@example.test',hasLocalPassword:false,emailVerificationMethod:'Google',emailSecondStepEnabled:false},customer:true,onRefresh(){}}))));
    assert.equal(document.querySelector('[type=password]'),null);assert.doesNotMatch(document.body.textContent,/Enable email login verification|Current FleetFlow password/);assert.match(document.body.textContent,/Confirm it’s you/);assert.match(document.body.textContent,/Set a password/);
  }finally{await act(async()=>root.unmount());}
});
test('account navigation renders one subsection and restores section context from the hash',async()=>{
  window.location.hash='#/customer-profile';sessionStorage.clear();globalThis.fetch=async url=>response(String(url).endsWith('/config')?{enabled:true}:String(url).endsWith('/driver')?{status:'Not submitted'}:{fullName:'Synthetic Driver',username:'synthetic',email:'synthetic@example.test',phoneNumber:'',address:'',hasLocalPassword:false,emailVerificationMethod:'Google'});
  const root=createRoot(document.getElementById('root'));
  try{await act(async()=>root.render(React.createElement(AuthProvider,null,React.createElement(AccountWorkspace))));assert.match(document.body.textContent,/Your account, at a glance/);assert.equal(document.querySelector('#account-fullname'),null);assert.equal(document.querySelector('#legalName'),null);
    await act(async()=>document.querySelector('nav[aria-label="Account sections"] button:nth-of-type(2)').click());assert.ok(document.querySelector('#account-fullname'));assert.equal(document.querySelector('#account-new-email'),null);assert.match(window.location.hash,/section=details/);
    await act(async()=>{window.location.hash='#/customer-profile?section=security';window.dispatchEvent(new dom.window.HashChangeEvent('hashchange'));});assert.ok(document.querySelector('#account-new-email'));assert.equal(document.querySelector('#account-fullname'),null);
  }finally{await act(async()=>root.unmount());}
});
test('signup collects five account fields and does not request driver documents or store an early session', async () => {
  sessionStorage.clear(); globalThis.fetch = async () => response({ enabled: true });
  const root = createRoot(document.getElementById('root'));
  try {
    await act(async () => root.render(React.createElement(AuthProvider, null, React.createElement(Register, { config: { enabled: true, googleClientId: null }, onNavigateToLogin() {} }))));
    assert.equal(document.querySelectorAll('input').length, 5);
    assert.doesNotMatch(document.body.textContent, /NIC number|Current address|Driving licence number|Phone number/);
    assert.equal(sessionStorage.getItem('token'), null);
    assert.match(document.body.textContent, /Verify email and create account/);
  } finally { await act(async () => root.unmount()); }
});
