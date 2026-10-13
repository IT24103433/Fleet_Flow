import React, { useState, useEffect } from 'react';
import VehicleCard from '../components/common/VehicleCard';
import Button from '../components/common/Button';
import Modal from '../components/common/Modal';
import Alert from '../components/Alert';
import { getVehicles, getCategories } from '../services/vehicleService';

const sorts = [ ['createdAt:desc', 'Newest first'], ['dailyRate:asc', 'Price: low to high'], ['dailyRate:desc', 'Price: high to low'], ['make:asc', 'Make: A–Z'], ['year:desc', 'Model year: newest'] ];
// eslint-disable-next-line react-refresh/only-export-components
export function catalogueFilters(values = {}) {
  return { searchTerm: values.searchTerm || '', category: values.category || '', fuel: ['Electric', 'Hybrid', 'Plug-in Hybrid', 'Gasoline', 'Diesel'].includes(values.fuel) ? values.fuel : '', transmission: ['Automatic', 'Manual'].includes(values.transmission) ? values.transmission : '', sort: sorts.some(([value]) => value === values.sort) ? values.sort : 'createdAt:desc', page: String(Math.max(1, Number.parseInt(values.page, 10) || 1)) };
}

export default function BrowseFleetPage({ onNavigate, initialFilters = {}, onFiltersChange }) {
  const [localFilters, setLocalFilters] = useState(initialFilters);
  const filters = catalogueFilters(onFiltersChange ? initialFilters : localFilters);
  const queryKey = JSON.stringify(filters);
  const [categories, setCategories] = useState([]);
  const [result, setResult] = useState(null);
  const [revision, setRevision] = useState(0);
  const [mobile, setMobile] = useState(() => window.matchMedia?.('(max-width: 767px)').matches || false);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const resultKey = `${queryKey}:${revision}`;
  const current = result?.key === resultKey ? result : null;
  const update = values => {
    const next = { ...filters, ...values, page: values.page || '1' };
    if (onFiltersChange) onFiltersChange(next);
    else setLocalFilters(next);
  };
  useEffect(() => {
    let active = true;
    getCategories().then(response => { if (active && response.success) setCategories(response.data || []); });
    return () => { active = false; };
  }, []);
  useEffect(() => {
    const media = window.matchMedia?.('(max-width: 767px)');
    const resize = event => { setMobile(event.matches); setDrawerOpen(false); };
    media?.addEventListener('change', resize);
    return () => media?.removeEventListener('change', resize);
  }, []);
  useEffect(() => {
    let active = true;
    const values = JSON.parse(queryKey);
    const [sortBy, sortOrder] = values.sort.split(':');
    const timer = window.setTimeout(() => {
      getVehicles({ ...values, searchTerm: values.searchTerm.trim(), page: Number(values.page), pageSize: 9, sortBy, sortOrder }).then(response => { if (active) setResult({ key: resultKey, ...response }); });
    }, 250);
    return () => { active = false; window.clearTimeout(timer); };
  }, [queryKey, resultKey]);
  const reset = () => update({ searchTerm: '', category: '', fuel: '', transmission: '', sort: 'createdAt:desc' });
  const filtered = Boolean(filters.searchTerm.trim() || filters.category || filters.fuel || filters.transmission);
  const select = vehicle => onNavigate('vehicle-details', { vehicle });
  const form = <div className="catalogue-filter-fields"><div className="filter-heading"><h2>Find your fit</h2><button type="button" className="inline-link-btn" onClick={reset}>Reset filters</button></div><label htmlFor="catalogue-search">Make, model, or keyword</label><input id="catalogue-search" type="search" className="form-input" value={filters.searchTerm} placeholder="Search vehicles" onChange={event => update({ searchTerm: event.target.value })} /><label htmlFor="catalogue-category">Vehicle category</label><select id="catalogue-category" className="form-input" value={filters.category} onChange={event => update({ category: event.target.value })}><option value="">All categories</option>{categories.map(category => <option key={category.id || category.name || category} value={category.name || category}>{category.name || category}</option>)}</select><label htmlFor="catalogue-fuel">Fuel type</label><select id="catalogue-fuel" className="form-input" value={filters.fuel} onChange={event => update({ fuel: event.target.value })}><option value="">All fuel types</option>{['Electric', 'Hybrid', 'Plug-in Hybrid', 'Gasoline', 'Diesel'].map(fuel => <option key={fuel}>{fuel}</option>)}</select><label htmlFor="catalogue-transmission">Transmission</label><select id="catalogue-transmission" className="form-input" value={filters.transmission} onChange={event => update({ transmission: event.target.value })}><option value="">All transmissions</option><option>Automatic</option><option>Manual</option></select><p className="filter-note">Choose rental dates on the vehicle page. Availability is checked before booking.</p></div>;
  const page = Number(filters.page);
  const totalPages = current?.pagination?.totalPages || 1;
  return <div className="catalogue-page"><div className="catalogue-heading"><p className="eyebrow">Explore the FleetFlow collection</p><h1>A vehicle for your next chapter.</h1><p>Compare the fleet, find your fit, and choose dates when you’re ready.</p></div><div className="catalogue-layout">{!mobile && <aside className="catalogue-filters" aria-label="Vehicle filters">{form}</aside>}<section className="catalogue-results" aria-label="Vehicle results"><div className="catalogue-toolbar">{mobile && <Button variant="outline" onClick={() => setDrawerOpen(true)}>Filters{filtered ? ' · Active' : ''}</Button>}<p aria-live="polite">{!current ? 'Loading vehicles…' : !current.success ? 'Results unavailable' : `${current.pagination?.totalCount ?? current.data.length} vehicles`}</p><div><label htmlFor="catalogue-sort">Sort by</label><select id="catalogue-sort" className="form-input" value={filters.sort} onChange={event => update({ sort: event.target.value })}>{sorts.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></div></div>{!current ? <div className="state-panel" role="status">Loading vehicles…</div> : !current.success ? <Alert type="error" title="We couldn’t load the fleet" message={current.message || 'Please try again.'} actionText="Try again" onAction={() => setRevision(value => value + 1)} /> : current.data.length ? <><div className="vehicle-grid">{current.data.map(vehicle => <VehicleCard key={vehicle.id} vehicle={vehicle} onSelect={select} />)}</div>{totalPages > 1 && <nav className="catalogue-pagination" aria-label="Fleet pages"><Button variant="outline" disabled={page <= 1} onClick={() => update({ page: String(page - 1) })}>Previous</Button><span>Page {page} of {totalPages}</span><Button variant="outline" disabled={page >= totalPages} onClick={() => update({ page: String(page + 1) })}>Next</Button></nav>}</> : <div className="state-panel"><h2>{filtered ? 'No vehicles match these filters.' : 'The fleet is taking a moment.'}</h2><p>{filtered ? 'Try another category or clear your filters.' : 'No vehicles are listed right now. Please check again later.'}</p>{filtered && <Button variant="outline" onClick={reset}>Clear filters</Button>}</div>}</section></div>{mobile && <Modal isOpen={drawerOpen} onClose={() => setDrawerOpen(false)} title="Vehicle filters" footer={<Button fullWidth onClick={() => setDrawerOpen(false)}>Show results</Button>}>{form}</Modal>}</div>;
}
