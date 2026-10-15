import React, { useEffect, useState } from 'react';
import { ArrowUpRightIcon } from '@phosphor-icons/react/dist/csr/ArrowUpRight';
import { onboardingRequest } from '../../services/onboardingService';
import useOnboardingConfig from '../../components/onboarding/useOnboardingConfig';
import { emailRequest } from '../onboarding/adminWorkspaceService';

export default function AccountAttention(){
  const config=useOnboardingConfig();const [results,setResults]=useState(null);
  useEffect(()=>{if(!config.enabled)return;let active=true;const controller=new AbortController();Promise.allSettled([onboardingRequest('reviews',{signal:controller.signal}),onboardingRequest('accounts/email-status',{signal:controller.signal}),emailRequest('/workspace','GET',controller.signal)]).then(rows=>{if(active)setResults(rows);});return()=>{active=false;controller.abort();};},[config.enabled]);
  if(!config.enabled)return null;
  const cards=[['Driving applications','Review documents','admin-driver-reviews',data=>data.length],['Email verification','Review account access','admin-account-security?view=verification',data=>data.filter(row=>row.isActive&&(row.needsInitialEmailOtp||row.emailVerificationMethod==='Unverified')).length],['Email delivery issues','Open delivery workspace','admin-account-security?view=emails',data=>data.summary.find(row=>row.state==='Failed')?.count||0]];
  return <section aria-label="Accounts needing attention" className="admin-attention-grid">{cards.map(([label,action,route,count],index)=><button type="button" key={route} onClick={()=>window.location.assign(`#/${route}`)}><span>{label}</span>{!results?<strong aria-label="Loading">…</strong>:results[index].status==='rejected'?<strong className="attention-error">Unavailable</strong>:<strong>{count(results[index].value).toLocaleString()}</strong>}<small>{action}<ArrowUpRightIcon size={15} weight="regular" aria-hidden="true"/></small></button>)}</section>;
}
