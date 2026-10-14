import { useRef } from 'react';
import gsap from 'gsap';
import { useGSAP } from '@gsap/react';
// Aceternity Timeline composition adapted to this operational record; licence retained in Credits.
export default function RentalTimeline({ timeline, staff }) {
  const root = useRef(null);
  const items = Array.isArray(timeline?.items) ? timeline.items : [];
  useGSAP((_context, contextSafe) => {
    if (!items.length) return;
    const media = window.matchMedia('(prefers-reduced-motion: reduce)');
    let accent;
    const applyPreference = contextSafe(() => {
      accent?.revert();
      if (!media.matches) accent = gsap.fromTo(root.current.querySelector('[data-timeline-line]'), { scaleY: 0.92 },
        { scaleY: 1, duration: 0.35, ease: 'power2.out', transformOrigin: 'top' });
    });
    applyPreference();
    media.addEventListener('change', applyPreference);
    return () => { media.removeEventListener('change', applyPreference); accent?.revert(); };
  }, { scope: root, dependencies: [items.length], revertOnUpdate: true });
  return <section ref={root} className="rental-timeline" aria-labelledby="rental-timeline-title"><h2 id="rental-timeline-title">Rental timeline</h2><p>Dates and handovers from the saved booking record.</p>{!items.length ? <p className="rental-meta">Lifecycle history is unavailable right now. Refresh the record to try again.</p> : <div className="rental-timeline-body"><div className="rental-timeline-line" data-timeline-line aria-hidden="true" /><ol>{items.map(item => <li key={item.kind}><span className="rental-timeline-dot" aria-hidden="true" /><div className="rental-timeline-date">{item.occurredAt ? <time dateTime={item.occurredAt}>{new Date(item.occurredAt).toLocaleString('en-GB')}</time> : 'Time not recorded'}</div><div><h3>{item.title}</h3>{item.details && <p>{item.details}</p>}{staff && item.actorId && <span className="rental-meta">Recorded account #{item.actorId.slice(0, 8)}</span>}</div></li>)}</ol></div>}</section>;
}
