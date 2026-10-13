import { useGSAP } from '@gsap/react';
import { gsap } from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';

gsap.registerPlugin(useGSAP, ScrollTrigger);
export default function useAuraMotion(scope, contentReady = false) {
  useGSAP(() => {
    const media = gsap.matchMedia();
    media.add('(prefers-reduced-motion: no-preference)', () => {
      // Essential copy and controls are never hidden or transformed.
      const image = scope.current?.querySelector('.cinematic-media-image');
      if (!image) return;
      let reveal, scroll;
      const start = () => {
        reveal = gsap.fromTo(image, { scale: 1.1 }, { scale: 1, duration: 0.8, ease: 'power3.out' });
        const hero = scope.current?.querySelector('.cinematic-hero');
        const frame = hero?.querySelector('.container-scroll-frame');
        if (hero && frame && window.innerWidth > 900) {
          // Aceternity Container Scroll: rotate/scale progress ported to GSAP.
          // Pin the marketing composition only; text and actions stay usable.
          scroll = gsap.timeline({ scrollTrigger: { trigger: hero, start: 'top 88px', end: '+=580', scrub: 1, pin: true, anticipatePin: 1, invalidateOnRefresh: true } });
          scroll.fromTo(frame, { rotateX: 3, scale: 1 }, { rotateX: 0, scale: 1.035, ease: 'none' }, 0);
          scroll.to(image, { scale: 1.2, xPercent: -3, yPercent: 2, ease: 'none' }, 0);
          reveal.eventCallback('onComplete', () => ScrollTrigger.refresh());
        }
      };
      if (image.complete && image.naturalWidth > 0) start();
      else image.addEventListener('load', start, { once: true });
      return () => { image.removeEventListener('load', start); scroll?.scrollTrigger?.kill(true); scroll?.revert(); reveal?.revert(); };
    });
    return () => media.revert();
  }, { scope });
  useGSAP(() => {
    const media = gsap.matchMedia();
    let active = true;
    media.add('(prefers-reduced-motion: no-preference)', () => {
      for (const section of scope.current?.querySelectorAll('[data-marketing-entrance]') || []) {
        gsap.from(section.children, { y: 40, duration: 0.8, stagger: 0.08, ease: 'power3.out', clearProps: 'transform', scrollTrigger: { trigger: section, start: 'top 88%', once: true } });
      }
    });
    document.fonts?.ready.then(() => { if (active) ScrollTrigger.refresh(); });
    return () => { active = false; media.revert(); };
  }, { scope, dependencies: [contentReady], revertOnUpdate: true });
}
