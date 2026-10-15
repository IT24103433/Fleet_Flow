import React, { useMemo, useState } from 'react';
import { getCountries } from 'libphonenumber-js/min';

export default function CountrySelector({ value, onChange, disabled }) {
  const options = useMemo(() => { const names = new Intl.DisplayNames(['en'], { type: 'region' }); return getCountries().map(code => ({ code, label: `${names.of(code)} (${code})` })).sort((a, b) => a.label.localeCompare(b.label)); }, []);
  const selected = options.find(c => c.code === value)?.label || value;
  const [input, setInput] = useState(null);
  return <div><label htmlFor="licence-country">Licence-issuing country</label><input id="licence-country" list="licence-countries" value={input ?? selected} onChange={e => { setInput(e.target.value); const found = options.find(c => c.label === e.target.value); if (found) { onChange(found.code); setInput(null); } }} onBlur={() => setInput(null)} disabled={disabled} autoComplete="off" /><datalist id="licence-countries">{options.map(c => <option key={c.code} value={c.label} />)}</datalist></div>;
}
