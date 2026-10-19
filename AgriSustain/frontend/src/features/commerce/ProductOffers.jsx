import React, { useEffect, useState } from 'react';
import { useCommerce, Pagination, money } from './shared';

// The existing offer selector lives at the shopping destination, outside the cart design.
export default function ProductOffers({ fixture }) {
  const api = useCommerce(fixture), [offers, setOffers] = useState(null), [page, setPage] = useState(1);
  useEffect(() => { api.run(`/cart/offers?page=${page}`).then(x => x && setOffers(x)); }, [fixture, page]);
  return <section className="commerce"><h1>Khám phá nông sản</h1>{api.feedback}<div className="commerce-offers">{offers?.items.map(p => <Offer key={p.id} product={p} busy={api.busy} onAdd={body => api.run('/cart/items', { method: 'POST', body })} />)}</div>{offers && <Pagination page={page} total={offers.total} onChange={setPage} busy={api.busy} />}<a href="#/cart">Xem giỏ hàng</a></section>;
}
function Offer({ product: p, busy, onAdd }) {
  const [batch, setBatch] = useState(''), [qty, setQty] = useState(p.quantityStep);
  return <form className="commerce-card" onSubmit={e => { e.preventDefault(); onAdd({ productId: p.id, batchId: Number(batch), quantity: Number(qty) }); }}><h3>{p.name}</h3><p>{money(p.retailPrice)} / {p.unit}</p><label>Lô thu hoạch<select aria-label="Lô thu hoạch" required value={batch} onChange={e => setBatch(e.target.value)}><option value="">Chọn lô thực tế</option>{p.batches.filter(b => b.harvest && b.stockEntered && b.available > 0).map(b => <option value={b.id} key={b.id}>{b.code} · {b.available} {p.unit} · hạn {b.expiry || '—'}</option>)}</select></label><label>Số lượng<input required type="number" min={p.quantityStep} step={p.quantityStep} value={qty} onChange={e => setQty(e.target.value)} /></label><button disabled={busy} type="submit">Thêm vào giỏ hàng</button></form>;
}
