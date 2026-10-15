import React, { useEffect, useRef } from 'react';

function JourneyArtwork() {
  return <svg className="auth-route-art customer-journey-art" viewBox="0 0 480 300" fill="none" aria-hidden="true">
    <defs><linearGradient id="journey-route" x1="20" y1="280" x2="450" y2="20" gradientUnits="userSpaceOnUse"><stop stopColor="#BDB5E9" /><stop offset="1" stopColor="#5B1E91" /></linearGradient></defs>
    <path d="M-30 265C100 265 70 170 170 170S240 60 350 60H510" stroke="url(#journey-route)" strokeWidth="44" />
    <path d="M-30 265C100 265 70 170 170 170S240 60 350 60H510" stroke="#fff" strokeWidth="2" strokeDasharray="7 12" />
    <circle cx="170" cy="170" r="25" fill="#fff" /><circle cx="170" cy="170" r="10" fill="#5B1E91" />
    <circle cx="350" cy="60" r="25" fill="#fff" /><circle cx="350" cy="60" r="10" fill="#5B1E91" />
    <path d="M42 76H148M42 88H108" stroke="#8D819A" strokeWidth="2" />
    <text x="312" y="132" fill="#5B1E91" fontSize="12" fontFamily="JetBrains Mono,monospace">YOUR NEXT CHAPTER</text>
  </svg>;
}
function OperationsArtwork() {
  return <svg className="auth-route-art staff-operations-art" viewBox="0 0 480 300" fill="none" aria-hidden="true">
    <defs><pattern id="operations-grid" width="24" height="24" patternUnits="userSpaceOnUse"><path d="M24 0H0V24" stroke="#797184" strokeOpacity=".18" /></pattern></defs>
    <rect width="480" height="300" fill="url(#operations-grid)" />
    <path d="M108 90H218V210H344M218 90H344M218 148H400" stroke="#BDB5E9" strokeWidth="2" />
    <rect x="36" y="58" width="142" height="64" rx="14" fill="#29242F" stroke="#73677F" /><rect x="287" y="58" width="148" height="64" rx="14" fill="#29242F" stroke="#73677F" /><rect x="287" y="178" width="148" height="64" rx="14" fill="#29242F" stroke="#73677F" />
    <circle cx="218" cy="148" r="25" fill="#BDB5E9" /><circle cx="218" cy="148" r="9" fill="#5B1E91" />
    <text x="58" y="86" fill="#fff" fontSize="14" fontFamily="Plus Jakarta Sans,sans-serif">Fleet inventory</text><text x="58" y="106" fill="#BDB5E9" fontSize="10" fontFamily="JetBrains Mono,monospace">VEHICLES &amp; AVAILABILITY</text>
    <text x="309" y="86" fill="#fff" fontSize="14" fontFamily="Plus Jakarta Sans,sans-serif">Rental handovers</text><text x="309" y="106" fill="#BDB5E9" fontSize="10" fontFamily="JetBrains Mono,monospace">PICKUP &amp; RETURN</text>
    <text x="309" y="207" fill="#fff" fontSize="14" fontFamily="Plus Jakarta Sans,sans-serif">Maintenance</text><text x="309" y="227" fill="#BDB5E9" fontSize="10" fontFamily="JetBrains Mono,monospace">PLAN &amp; ASSIGN</text>
  </svg>;
}
export default function AuthLayout({ children, staff = false }) {
  const story = useRef(null);
  useEffect(() => {
    if (typeof window.matchMedia !== 'function') return;
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    let active = true; let media;
    import('gsap').then(({ gsap }) => {
      if (!active) return;
      media = gsap.matchMedia();
      media.add('(prefers-reduced-motion: no-preference)', () => {
        const context = gsap.context(() => {
          gsap.fromTo('.auth-route-art circle', { opacity: .65, scale: .8, transformOrigin: '50% 50%' }, { opacity: 1, scale: 1, duration: .8, stagger: .1, ease: 'power2.out', clearProps: 'all' });
          gsap.fromTo('.customer-journey-art path[stroke-dasharray]', { strokeDashoffset: 96 }, { strokeDashoffset: 0, duration: 1.4, ease: 'power2.out', clearProps: 'strokeDashoffset' });
        }, story);
        return () => context.revert();
      });
    }).catch(() => { /* Decorative motion can fail without blocking account access. */ });
    return () => { active = false; media?.revert(); };
  }, [staff]);
  return <div className="auth-page-layout"><aside ref={story} className={`auth-story ${staff ? 'auth-story-operations' : 'auth-story-journey'}`}>
    <p className="eyebrow">{staff ? 'FleetFlow workspace' : 'Your journey, organised'}</p>
    <h2>{staff ? <>Keep the fleet<br />moving forward.</> : <>Your plans.<br />All in one place.</>}</h2>
    <p>{staff ? 'A focused workspace for vehicles, handovers, maintenance and account access.' : 'Choose your drive. Keep your dates and every booking detail together.'}</p>
    {staff ? <OperationsArtwork /> : <JourneyArtwork />}
    <span className="auth-story-note">{staff ? 'FleetFlow · Operations workspace' : 'FleetFlow · Your next drive starts here'}</span>
  </aside>{children}</div>;
}
