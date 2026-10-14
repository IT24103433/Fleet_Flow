# FleetFlow frontend design rules

- Use `@phosphor-icons/react` for interface icons. Import named icons from their individual `dist/csr` modules; use regular weight consistently.
- Decorative icons must have `aria-hidden="true"`. Icon-only controls need a meaningful accessible label and visible keyboard focus.
- Do not mix icon libraries or use emoji/arrow glyphs as interface icons. Brand artwork and the static favicon are exceptions.
- Use graphite and ivory marketing surfaces, violet accents, and Plus Jakarta Sans / JetBrains Mono typography. Operational workspaces use white panels, clear dark navigation, and compact controls. Preserve existing API contracts, hash routing, role checks, booking guards, and truthful data states.
- Marketing may use a scroll-controlled, pinned hero and section entrances. No draggable panels or pinned booking guides, and no motion on operational controls. Honour reduced-motion preferences and clean up GSAP contexts on route changes.
- Keep logout visible in the staff header. Dashboard charts must describe actual endpoint data; never invent a time series or financial metrics.
- Account photographs must come from the presented account's own profileImageUrl. Use the shared AccountAvatar with a neutral Phosphor User fallback; never initials or another person's photograph. Use readable title-case role labels.
