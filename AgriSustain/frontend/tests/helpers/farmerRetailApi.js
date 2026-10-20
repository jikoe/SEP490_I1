

// Browser interaction tests isolate UI behavior using the documented API contract.
// These do not replace the SQL/API cases and must be reported as UI tests only.
export const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=', 'base64');
const product = () => ({ id: 1, farmId: 10, farmName: 'Nông trại Xanh', name: 'Cà chua hữu cơ', categoryId: 1, categoryActive: true,
  unit: 'kg', quantityStep: 1, origin: 'Đồng Tháp', description: 'Nông sản chất lượng', retailPrice: 30000, wholesaleMinimum: 10,
  wholesalePrice: 25000, approval: 'Approved', published: true, farmEligible: true, unitLocked: true, revision: 0,
  images: [{ url: '/test-image.png', primary: true }], availableQuantity: 20,
  batches: [{ id: 20, productId: 1, code: 'B20', available: 20, reserved: 0, status: 'Available', harvest: '2026-10-18', expiry: '2099-01-01', stockEntered: true }] });
export async function setup(page, route, fixture = 'retail-customer') {
  page.on('pageerror', error => console.error('PAGEERROR', error.message));
  page.on('console', message => { if (message.type() === 'error') console.error('BROWSER', message.text()); });
  const state = { p: product(), items: [], requests: [], error: null, priceChanged: false, stockChanged: false,
    order: { farmName: 'Nông trại Xanh', farmVerified: true, id: 100, code: 'SO-TEST', parentCode: 'ORD-TEST', status: 'PendingConfirmation', type: 'Retail', total: 150000, verifiedPayment: 150000, requiredPayment: 150000,
      recipient: 'Nguyễn Minh Hà', phone: '0901234567', address: 'An Phú', createdAt: '2026-10-19T03:00:00', deliveredAt: null, receivedAt: null, history: [], evidenceRequests: [],
      lines: [{ id: 1, name: 'Cà chua hữu cơ', quantity: 5, unit: 'kg', unitPrice: 30000, allocations: [{ id: 1, quantity: 5, status: 'Reserved', batch: { code: 'B20', expiry: '2099-01-01' } }] }] } };
  const response = code => { state.items.forEach(l => { l.offer = structuredClone(state.p); l.available = state.p.batches[0].available >= 1; l.unitPrice = l.quantity >= 10 ? state.p.wholesalePrice : state.p.retailPrice; if (!l.available) l.selected = false; }); return { items: state.items, selectedSubtotal: state.items.filter(x => x.selected).reduce((s, x) => s + x.quantity * x.unitPrice, 0), reviewToken: 'reviewed', messageCode: code }; };
  state.items = [{ id: 1, productId: 1, batchId: 20, quantity: 2, selected: true }];
  await page.addInitScript(value => sessionStorage.setItem('agrisustain:demo-fixture', value), fixture);
  await page.route('**/test-image.png', r => r.fulfill({ contentType: 'image/png', body: png }));
  await page.route(url => url.pathname.startsWith('/api/'), async r => {
    const req = r.request(), url = new URL(req.url()), path = url.pathname, method = req.method();
    const body = req.postDataJSON?.(); state.requests.push({ path, method, body, search: url.search });
    const send = value => r.fulfill({ contentType: 'application/json', body: JSON.stringify(value) });
    if (state.error) return r.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ code: 'MSG126', title: 'Không thể tải dữ liệu.' }) });
    if (path === '/api/farmer/products/provinces') return send([{ code: '82', name: 'Đồng Tháp' }, { code: '68', name: 'Lâm Đồng' }]);
    if (path === '/api/farmer/products/categories') return send([{ id: 1, name: 'Rau củ' }]);
    if (path === '/api/farmer/products' && method === 'GET') return send({ items: url.searchParams.get('keyword') === 'không tồn tại' ? [] : [state.p], total: 1 });
    if (path === '/api/farmer/products/1' && method === 'GET') return send(state.p);
    if (path.startsWith('/api/farmer/products') && method !== 'GET') {
      if (path.endsWith('/visibility')) { state.p.published = body.published; return send({ messageCode: body.published ? 'MSG164' : 'MSG74' }); }
      return send({ messageCode: method === 'POST' ? 'MSG71' : 'MSG72', id: 1 });
    }
    if (path === '/api/cart/offers') return send({ items: [state.p], total: 1 });
    if (path === '/api/cart' && method === 'GET') return send(response(state.items.length ? null : 'MSG168'));
    if (path === '/api/cart/selection') { state.items.forEach(l => { if (body.ids.includes(l.id)) l.selected = body.selected; }); return send(response()); }
    if (path === '/api/cart/remove') { if (body.confirmed) state.items = state.items.filter(l => !body.ids.includes(l.id)); return send(response('MSG85')); }
    if (path === '/api/cart/items' && method === 'POST') { state.items[0].quantity += body.quantity; return send(response('MSG81')); }
    if (path === '/api/cart/items/1' && method === 'PUT') { const prior = state.items[0].quantity; state.items[0].quantity = body.quantity; return send(response(prior < 10 && body.quantity >= 10 ? 'MSG82' : prior >= 10 && body.quantity < 10 ? 'MSG83' : null)); }
    if (path === '/api/cart/prepare-checkout') {
      if (!state.items.some(l => l.selected)) return send(response('MSG87'));
      if (state.stockChanged) { state.items[0].quantity = 3; state.stockChanged = false; return send(response('MSG86')); }
      if (state.priceChanged) { state.p.retailPrice = 32000; state.priceChanged = false; return send(response('MSG118')); }
      return send({ ...response(), canProceedToCheckout: true });
    }
    if (path === '/api/checkout') return send({ items: [], addresses: [], availablePoints: 0, voucherCodes: [] });
    if (path === '/api/seller-orders' || path === '/api/seller-orders/mine') return send({ items: [state.order], total: 1 });
    if (path === '/api/seller-orders/100' && method === 'GET') return send(state.order);
    if (path.startsWith('/api/seller-orders/100/')) {
      const action = path.split('/').pop(); state.order.status = ({ confirm: 'Confirmed', ready: 'ReadyToShip', dispatch: 'Shipping', reject: 'Cancelled', receive: 'Delivered', accept: 'Received' })[action] || state.order.status;
      if (action === 'receive') state.order.deliveredAt = '2026-10-19T04:00:00';
      if (action === 'accept') state.order.receivedAt = '2026-10-19T05:00:00';
      return send({ id: 100, messageCode: ({ confirm: 'MSG64', ready: 'MSG146', dispatch: 'MSG66', reject: 'MSG67', receive: 'MSG147', accept: 'MSG147', 'delivery-issue': 'MSG149', evidence: 'MSG150' })[action] });
    }
    return send({});
  });
  await page.goto('/#' + route);
  return state;
}


export const captureDesign = async (page, name) => {
  if (process.env.FARMER_RETAIL_SCREENSHOTS) await page.screenshot({ path: `${process.env.FARMER_RETAIL_SCREENSHOTS}/${name}.png`, fullPage: true });
};
