import React, { useId, useMemo, useState } from 'react';
import { getCountries, getCountryCallingCode, parsePhoneNumberFromString } from 'libphonenumber-js/min';

export default function CountryPhoneInput({ value, onChange, disabled = false }) {
  const countries = useMemo(() => { const names = new Intl.DisplayNames(['en'], { type: 'region' }); return getCountries().map(code => ({ code, label: `${names.of(code)} (+${getCountryCallingCode(code)})` })).sort((a, b) => a.label.localeCompare(b.label)); }, []);
  const id = useId(); const [selectedCountry, setCountry] = useState(null);
  const country = selectedCountry || parsePhoneNumberFromString(value || '')?.country || 'LK';
  const [query, setQuery] = useState(null);
  const selectedLabel = countries.find(c => c.code === country)?.label || 'Sri Lanka (+94)';
  const changeCountry = e => { setQuery(e.target.value); const found = countries.find(c => c.label === e.target.value); if (found) { const parsed = parsePhoneNumberFromString(value || ''); setCountry(found.code); setQuery(null); if (parsed) onChange(`+${getCountryCallingCode(found.code)}${parsed.nationalNumber}`); } };
  const changeNumber = e => {
    const input = e.target.value; const parsed = parsePhoneNumberFromString(input, country);
    if (!input.trim()) { onChange(''); return; }
    onChange(parsed?.isValid() ? parsed.number : input.startsWith('+') ? input : `+${getCountryCallingCode(country)}${input.replace(/^0+/, '').replace(/\D/g, '')}`);
  };
  return <div className="phone-input-grid"><div><label htmlFor={`phone-country-${id}`}>Country calling code</label><input id={`phone-country-${id}`} list={`phone-countries-${id}`} value={query ?? selectedLabel} onChange={changeCountry} onBlur={() => setQuery(null)} disabled={disabled} autoComplete="off" /><datalist id={`phone-countries-${id}`}>{countries.map(c => <option key={c.code} value={c.label} />)}</datalist></div><div><label htmlFor={`international-phone-${id}`}>Phone number</label><input id={`international-phone-${id}`} type="tel" value={value || ''} onChange={changeNumber} autoComplete="tel" placeholder="Your phone number" disabled={disabled} /></div></div>;
}
