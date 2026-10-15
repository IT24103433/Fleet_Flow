import React, { useEffect, useRef, useState } from 'react';
import { gsap } from 'gsap';
import { UserIcon } from '@phosphor-icons/react/dist/csr/User';
import { ShieldCheckIcon } from '@phosphor-icons/react/dist/csr/ShieldCheck';
import { IdentificationCardIcon } from '@phosphor-icons/react/dist/csr/IdentificationCard';
import { SquaresFourIcon } from '@phosphor-icons/react/dist/csr/SquaresFour';
import { TrashIcon } from '@phosphor-icons/react/dist/csr/Trash';
import { ArrowUpRightIcon } from '@phosphor-icons/react/dist/csr/ArrowUpRight';
import { useAuth } from '../../context/AuthContext';
import Button from '../common/Button';
import InputField from '../InputField';
import CountryPhoneInput from './CountryPhoneInput';
import DriverVerificationPanel from './DriverVerificationPanel';
import useOnboardingConfig from './useOnboardingConfig';
import AccountSecurityPanel from './AccountSecurityPanel';
import AccountDeletionPanel from './AccountDeletionPanel';
import { onboardingRequest } from '../../services/onboardingService';
import '../../onboarding.css';

const sections = [['overview','Overview',SquaresFourIcon],['details','Personal details',UserIcon],['driving','Driving verification',IdentificationCardIcon],['security','Security',ShieldCheckIcon],['deletion','Account deletion',TrashIcon]];
const selected = () => new URLSearchParams(window.location.hash.split('?')[1] || '').get('section') || 'overview';
export default function AccountWorkspace({ customer = true }) {
  const config = useOnboardingConfig(); const { login, updateUser, user } = useAuth();
  const [section, setSection] = useState(selected); const [profile, setProfile] = useState(null); const [driverStatus, setDriverStatus] = useState(null);
  const [error, setError] = useState(''); const [notice, setNotice] = useState(''); const [busy, setBusy] = useState(false); const guard = useRef(false); const root = useRef(null);
  const available = sections.filter(([id]) => customer || !['driving','deletion'].includes(id));
  const hasProfile = profile !== null;
  const active = available.some(([id]) => id === section) ? section : 'overview';
  useEffect(() => { const listener = () => setSection(selected()); window.addEventListener('hashchange',listener); return () => window.removeEventListener('hashchange',listener); }, []);
  useEffect(() => { if (!config.enabled) return; const controller = new AbortController();
    onboardingRequest('profile',{signal:controller.signal}).then(setProfile).catch(e => {if(e.name!=='AbortError')setError(e.message);});
    if(customer)onboardingRequest('driver',{signal:controller.signal}).then(data=>setDriverStatus(data.status)).catch(e=>{if(e.name!=='AbortError')setDriverStatus('Unavailable');});
    return()=>controller.abort();
  },[config.enabled,customer]);
  useEffect(() => { if(!root.current||typeof window.matchMedia!=="function")return; const media=gsap.matchMedia(); media.add('(prefers-reduced-motion: no-preference)',()=>{const context=gsap.context(()=>gsap.fromTo('[data-account-heading]',{opacity:.8,y:4},{opacity:1,y:0,duration:.25,clearProps:'all'}),root);return()=>context.revert();});return()=>media.revert();},[active,config.enabled,hasProfile]);
  const go = id => { const route=window.location.hash.split('?')[0]; const query=new URLSearchParams(window.location.hash.split('?')[1]||'');query.set('section',id);window.location.assign(`${route}?${query}`);setSection(id);setError('');setNotice(''); };
  const refresh = async () => {const data=await onboardingRequest('profile');setProfile(data);updateUser({...user,...data});};
  const save = async event => {event.preventDefault();if(guard.current)return;guard.current=true;setBusy(true);setError('');setNotice('');try{const result=await onboardingRequest('profile',{method:'PUT',body:profile});if(result.session)login(result.session.token,result.session.user);setProfile(result.profile);updateUser({...user,...result.profile});setNotice('Your personal details have been saved.');}catch(e){setError(e.message);}finally{guard.current=false;setBusy(false);}};
  if(config.loading)return <p role="status">Opening your account…</p>;
  if(config.error)return <div className="onboarding-panel" role="alert"><h2>Account settings are unavailable.</h2><p>{config.error}</p><Button variant="outline" onClick={()=>window.location.reload()}>Try again</Button></div>;
  if(!config.enabled)return null;
  return <div className="account-workspace" ref={root}><nav className="account-section-nav" aria-label="Account sections"><p>Your account</p>{available.map(([id,label,Icon])=><button key={id} type="button" aria-current={active===id?'page':undefined} onClick={()=>go(id)}>{React.createElement(Icon,{size:20,weight:'regular','aria-hidden':true})}<span>{label}</span>{active===id&&<span className="account-nav-dot" aria-hidden="true"/>}</button>)}<small>One place for your details, access and driving documents.</small></nav>
    <div className="account-section-content" id="account-section-content">{error&&<p role="alert" className="onboarding-error">{error}</p>}{notice&&<p role="status" className="onboarding-success">{notice}</p>}
      {!profile?<p role="status">{error?'Your account could not load. Refresh to try again.':'Loading account details…'}</p>:<>
      {active==='overview'&&<section className="onboarding-panel account-overview"><div data-account-heading><p className="auth-context">A little more you</p><h2>Your account, at a glance.</h2><p>Choose what you want to manage. You don’t have to complete everything at once.</p></div><div className="account-overview-grid">
        <button type="button" onClick={()=>go('details')}><UserIcon size={25} weight="regular" aria-hidden="true"/><h3>Personal details</h3><p>{profile.fullName}<br/>@{profile.username}</p><span>Edit your details <ArrowUpRightIcon size={16} aria-hidden="true"/></span></button>
        {customer&&<button type="button" onClick={()=>go('driving')}><IdentificationCardIcon size={25} weight="regular" aria-hidden="true"/><h3>Ready for your next drive?</h3><strong className="account-state">{driverStatus||'Checking status…'}</strong><p>{driverStatus==='Approved'?'Your driving details are approved. Expiry is checked for each booking.':'Add your driving details and documents before making a new booking.'}</p><span>Open driving verification <ArrowUpRightIcon size={16} aria-hidden="true"/></span></button>}
        <button type="button" onClick={()=>go('security')}><ShieldCheckIcon size={25} weight="regular" aria-hidden="true"/><h3>Sign-in & security</h3><p>{profile.hasLocalPassword?'Password sign-in is available.':'You sign in securely with Google.'}<br/>Manage your email and sign-in methods.</p><span>Manage access <ArrowUpRightIcon size={16} aria-hidden="true"/></span></button>
      </div></section>}
      {active==='details'&&<section className="onboarding-panel"><div data-account-heading><h2>Personal details</h2><p>Update your everyday details here. Legal driving details are reviewed separately.</p></div><form onSubmit={save}><div className="onboarding-fields"><InputField label="Full name" name="fullName" id="account-fullname" value={profile.fullName} onChange={e=>setProfile(p=>({...p,fullName:e.target.value}))} disabled={busy} required/><InputField label="Username" name="username" id="account-username" value={profile.username} onChange={e=>setProfile(p=>({...p,username:e.target.value}))} disabled={busy} required/><CountryPhoneInput value={profile.phoneNumber} onChange={phoneNumber=>setProfile(p=>({...p,phoneNumber}))} disabled={busy}/><InputField label="Current address" id="account-address" name="address" value={profile.address} onChange={e=>setProfile(p=>({...p,address:e.target.value}))} disabled={busy}/></div><Button type="submit" isLoading={busy}>Save personal details</Button></form><div className="account-email-summary"><span>Email address</span><strong>{profile.email}</strong><Button variant="outline" onClick={()=>go('security')}>Manage email</Button></div></section>}
      {active==='security'&&<AccountSecurityPanel profile={profile} customer={customer} googleClientId={config.googleClientId} onRefresh={refresh}/>}
      {active==='driving'&&customer&&<DriverVerificationPanel/>}
      {active==='deletion'&&customer&&<AccountDeletionPanel/>}
    </> }</div></div>;
}
