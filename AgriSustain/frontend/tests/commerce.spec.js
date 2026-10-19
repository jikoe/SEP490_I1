import { test, expect } from '@playwright/test';

// Browser interaction tests isolate UI behavior using the documented API contract.
// These do not replace the SQL/API cases and must be reported as UI tests only.
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=', 'base64');
const product = () => ({ id: 1, farmId: 10, farmName: 'Nông trại Xanh', name: 'Cà chua hữu cơ', categoryId: 1, categoryActive: true,
  unit: 'kg', quantityStep: 1, origin: 'Đồng Tháp', description: 'Nông sản chất lượng', retailPrice: 30000, wholesaleMinimum: 10,
  wholesalePrice: 25000, approval: 'Approved', published: true, farmEligible: true, unitLocked: true, revision: 0,
  images: [{ url: '/test-image.png', primary: true }], availableQuantity: 20,
  batches: [{ id: 20, productId: 1, code: 'B20', available: 20, reserved: 0, status: 'Available', harvest: '2026-10-18', expiry: '2099-01-01', stockEntered: true }] });
async function setup(page, route, fixture = 'retail-customer') {
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

test('IT-FAR-03-001/010/014: required fields, image and cancel unsaved form', async ({ page }) => {
  const state = await setup(page, '/farmer/products', 'farmer');
  await page.getByRole('button', { name: '+ Đăng sản phẩm', exact: true }).click();
  await page.getByRole('button', { name: 'Gửi duyệt / Gửi lại', exact: true }).click();
  expect(state.requests.filter(r => r.method === 'POST')).toHaveLength(0);
  await page.getByLabel('Tên sản phẩm *', { exact: true }).fill('Cà chua mới');
  await page.getByRole('button', { name: 'Hủy thay đổi' }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.getByRole('dialog').getByRole('button', { name: 'Hủy', exact: true }).click();
  await expect(page.getByLabel('Tên sản phẩm *', { exact: true })).toHaveValue('Cà chua mới');
});
test('IT-FAR-03-001/015: upload primary image and submit complete form', async ({ page }) => {
  const state = await setup(page, '/farmer/products', 'farmer');
  await page.getByRole('button', { name: '+ Đăng sản phẩm', exact: true }).click();
  await page.getByLabel('Tên sản phẩm *', { exact: true }).fill('Cà chua mới');
  await page.getByLabel('Danh mục đang hoạt động *', { exact: true }).selectOption('1');
  await page.getByLabel('Tỉnh/Thành phố xuất xứ *', { exact: true }).selectOption({ label: 'Đồng Tháp' });
  await page.getByLabel('Giá bán lẻ (đ/đơn vị) *', { exact: true }).fill('30000');
  await page.getByLabel('Ngưỡng bán sỉ *', { exact: true }).fill('10');
  await page.getByLabel('Giá bán sỉ (đ/đơn vị) *', { exact: true }).fill('25000');
  await page.getByLabel('Mô tả sản phẩm', { exact: true }).fill('Mô tả nông sản');
  await page.getByLabel('Tải ảnh').setInputFiles({ name: 'tomato.png', mimeType: 'image/png', buffer: png });
  await page.getByRole('button', { name: 'Gửi duyệt / Gửi lại', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Đã gửi duyệt');
  const posted = state.requests.find(r => r.path === '/api/farmer/products' && r.method === 'POST').body;
  expect(posted.images[0].primary).toBe(true); expect(posted.requestKey).toMatch(/^[a-f0-9-]{36}$/);
});
test('IT-FAR-03-005/006/007: persisted detail and locked unit', async ({ page }) => {
  await setup(page, '/farmer/products', 'farmer');
  await page.getByRole('button', { name: 'Chi tiết / sửa' }).click();
  await expect(page.getByLabel('Đơn vị *', { exact: true })).toHaveAttribute('readonly', '');
  await page.getByLabel('Giá bán lẻ (đ/đơn vị) *', { exact: true }).fill('32000');
  await page.getByRole('button', { name: 'Lưu thay đổi giá', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Đã lưu');
});
test('IT-FAR-03-009/010: hiding requires confirmation and cancellation does not call API', async ({ page }) => {
  const state = await setup(page, '/farmer/products', 'farmer');
  await page.getByRole('button', { name: 'Ẩn', exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Hủy', exact: true }).click();
  expect(state.requests.filter(r => r.path.endsWith('/visibility'))).toHaveLength(0);
  await page.getByRole('button', { name: 'Ẩn', exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Đã ẩn');
});
test('IT-FAR-03-003/004: preserved search and empty state', async ({ page }) => {
  await setup(page, '/farmer/products', 'farmer');
  await page.getByLabel('Tìm tên').fill('không tồn tại');
  await page.getByRole('button', { name: 'Áp dụng', exact: true }).click();
  await expect(page.getByText('MSG124: Không có sản phẩm phù hợp.')).toBeVisible();
  await expect(page.getByLabel('Tìm tên')).toHaveValue('không tồn tại');
});
test('IT-RC-01-001/005/006: quantity controls and threshold feedback', async ({ page }) => {
  await setup(page, '/cart');
  const qty = page.getByLabel('Số lượng Cà chua hữu cơ', { exact: true });
  await qty.fill('10'); await qty.press('Enter');
  await expect(page.getByRole('status')).toContainText('Đã áp dụng giá sỉ');
  await expect(page.getByTestId('cart-subtotal')).toHaveText('250.000 ₫');
  await page.getByLabel('Giảm Cà chua hữu cơ', { exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Đã áp dụng lại giá lẻ');
});
test('IT-RC-01-007/008/009: remove cancel and confirm', async ({ page }) => {
  const state = await setup(page, '/cart');
  await page.getByRole('button', { name: 'Xóa đã chọn' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Hủy', exact: true }).click();
  expect(state.requests.filter(r => r.path.endsWith('/remove'))).toHaveLength(0);
  await page.getByRole('button', { name: 'Xóa đã chọn' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Giỏ hàng đang trống' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Tiến hành thanh toán', exact: true })).toBeDisabled();
});
test('IT-RC-01-011: invalid quantity retains previous persisted value', async ({ page }) => {
  const state = await setup(page, '/cart');
  await page.getByLabel('Số lượng Cà chua hữu cơ', { exact: true }).fill('0');
  await page.getByLabel('Số lượng Cà chua hữu cơ', { exact: true }).press('Enter');
  await expect(page.getByRole('alert')).toContainText('MSG167');
  expect(state.requests.filter(r => r.method === 'PUT')).toHaveLength(0);
  await expect(page.getByRole('button', { name: 'Tiến hành thanh toán', exact: true })).toBeDisabled();
});
test('IT-RC-01-015: checkout without selection remains in cart', async ({ page }) => {
  await setup(page, '/cart');
  await page.getByLabel('Chọn tất cả', { exact: true }).uncheck();
  await expect(page.getByRole('button', { name: 'Xóa đã chọn' })).toBeDisabled();
  await page.getByRole('button', { name: 'Tiến hành thanh toán', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('ít nhất một sản phẩm');
  await expect(page).toHaveURL(/#\/cart$/);
});
for (const type of ['price', 'stock']) test(`IT-RC-01-${type === 'price' ? '017' : '013'}: review required after ${type} change`, async ({ page }) => {
  const state = await setup(page, '/cart'); state[type === 'price' ? 'priceChanged' : 'stockChanged'] = true;
  await page.getByRole('button', { name: 'Tiến hành thanh toán', exact: true }).click();
  await expect(page.getByRole('status')).toContainText(type === 'price' ? 'Giá hoặc chính sách' : 'Số lượng đã điều chỉnh');
  await expect(page).toHaveURL(/#\/cart$/);
  expect(state.requests.filter(r => /checkout\/orders/.test(r.path))).toHaveLength(0);
});
test('IT-FAR-06-002/003: confirm, ready and simulated dispatch', async ({ page }) => {
  const state = await setup(page, '/farmer/orders', 'farmer');
  await page.getByRole('button', { name: 'Xem và xử lý' }).click();
  for (const action of ['Xác nhận đơn', 'Đánh dấu sẵn sàng giao', 'Bắt đầu giao mô phỏng']) {
    await page.getByRole('button', { name: action, exact: true }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  }
  await expect(page.getByRole('status')).toContainText('Đã bắt đầu giao mô phỏng');
  expect(state.order.status).toBe('Shipping');
  await expect(page.getByRole('button', { name: 'Bắt đầu giao mô phỏng', exact: true })).toBeDisabled();
});
test('IT-FAR-06-006/007/016: rejection explanation and confirmation', async ({ page }) => {
  const state = await setup(page, '/farmer/orders', 'farmer');
  await page.getByRole('button', { name: 'Xem và xử lý' }).click();
  await page.getByRole('button', { name: 'Từ chối trước khi giao', exact: true }).click();
  await page.getByLabel('Lý do từ chối *', { exact: true }).selectOption({ label: 'Thiếu sản lượng' });
  await page.getByLabel('Giải thích chi tiết *', { exact: true }).fill('123456789012345');
  await page.getByRole('button', { name: 'Xác nhận từ chối', exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('chờ Finance');
  expect(state.requests.find(r => r.path.endsWith('/reject')).body.confirmed).toBe(true);
});
test('IT-FAR-06-004: buyer receipt and preserved delivery timestamp', async ({ page }) => {
  const state = await setup(page, '/receipts'); state.order.status = 'Shipping';
  await page.getByRole('button', { name: 'Xem và xử lý' }).click();
  await page.getByRole('button', { name: 'Tôi đã nhận hàng' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  await expect(page.getByText(/Hạn gửi khiếu nại của người mua/)).toBeVisible();
  const original = state.order.deliveredAt;
  await page.getByRole('button', { name: 'Chấp nhận hàng', exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Đã ghi nhận'); expect(state.order.deliveredAt).toBe(original);
});
test('IT-FAR-06-019: read failure is shown with retry', async ({ page }) => {
  const state = await setup(page, '/farmer/orders', 'farmer'); await expect(page.getByRole('button', { name: 'Xem và xử lý' })).toBeVisible();
  state.error = true; await page.getByRole('button', { name: 'Áp dụng', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('MSG126'); state.error = false;
  await page.getByRole('button', { name: 'Áp dụng', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveCount(0);
});
test('IT-FAR-06-008: packing print retains snapshots and makes no status mutation', async ({ page }) => {
  await page.addInitScript(() => { window.print = () => { window.__printed = true; }; });
  const state = await setup(page, '/farmer/orders', 'farmer');
  await page.getByRole('button', { name: 'Xem và xử lý' }).click();
  await expect(page.getByText('Nguyễn Minh Hà', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'In phiếu', exact: true }).click();
  await expect.poll(() => page.evaluate(() => window.__printed)).toBe(true);
  expect(state.requests.filter(r => r.path.startsWith('/api/seller-orders') && r.method === 'POST')).toHaveLength(0);
});
test('IT-RC-01-014/016: unavailable line can be removed but cannot be selected', async ({ page }) => {
  const state = await setup(page, '/cart'); state.p.batches[0].available = 0;
  await page.reload();
  await expect(page.getByLabel('Chọn Cà chua hữu cơ', { exact: true })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Xóa Cà chua hữu cơ', exact: true })).toBeEnabled();
  await expect(page.getByTestId('cart-subtotal')).toHaveText('0 ₫');
});

// Design regression checks: dates must reach the API, actions keep their own payloads,
// and the cart continues to the relocated existing offer selector.
const captureDesign = async (page, name) => {
  if (process.env.COMMERCE_SCREENSHOTS) await page.screenshot({ path: `${process.env.COMMERCE_SCREENSHOTS}/${name}.png`, fullPage: true });
};

test('Design FAR03: date filters, combined visibility and table fields', async ({ page }) => {
  const state = await setup(page, '/farmer/products', 'farmer');
  await page.getByLabel('Tìm tên', { exact: true }).fill('Cà chua');
  await page.getByLabel('Từ ngày', { exact: true }).fill('2026-10-01');
  await page.getByLabel('Đến ngày', { exact: true }).fill('2026-10-19');
  await page.getByRole('button', { name: 'Áp dụng', exact: true }).click();
  await expect.poll(() => state.requests.some(r => r.path === '/api/farmer/products' && new URLSearchParams(r.search).get('from') === '2026-10-01' && new URLSearchParams(r.search).get('to') === '2026-10-19' && new URLSearchParams(r.search).get('keyword') === 'Cà chua')).toBe(true);
  await page.getByLabel('Kiểm duyệt / Hiển thị', { exact: true }).selectOption('Approved:false');
  await expect.poll(() => state.requests.some(r => r.path === '/api/farmer/products' && new URLSearchParams(r.search).get('status') === 'Approved' && new URLSearchParams(r.search).get('published') === 'false')).toBe(true);
  await expect(page.getByRole('columnheader', { name: 'Nhóm / Xuất xứ', exact: true })).toBeVisible();
  await expect(page.getByText('Rau củ · Đồng Tháp', { exact: true })).toBeVisible();
  await expect(page.getByText('1 lô thực tế', { exact: false })).toBeVisible();
  await expect(page.getByLabel('Danh mục', { exact: true })).toHaveCount(0);
  await captureDesign(page, 'uc-far-03-list');
});

test('Design FAR03: province select and separate price-only action', async ({ page }) => {
  const state = await setup(page, '/farmer/products', 'farmer');
  await page.getByRole('button', { name: 'Chi tiết / sửa' }).click();
  await expect(page.getByLabel('Tỉnh/Thành phố xuất xứ *', { exact: true })).toHaveValue('Đồng Tháp');
  await expect(page.getByLabel('Bước số lượng *', { exact: true })).toHaveCount(0);
  await page.getByLabel('Tên sản phẩm *', { exact: true }).fill('Đổi nội dung cần duyệt');
  await page.getByRole('button', { name: 'Lưu thay đổi giá', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('chỉ áp dụng');
  expect(state.requests.filter(r => r.method === 'PUT')).toHaveLength(0);
  await page.getByLabel('Tên sản phẩm *', { exact: true }).fill('Cà chua hữu cơ');
  await page.getByRole('button', { name: 'Danh sách sản phẩm', exact: true }).click();
  await page.getByRole('button', { name: 'Chi tiết / sửa', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveCount(0);
  await captureDesign(page, 'uc-far-03-form');
});

test('Design FAR06: list shows goods and retains four screen tabs', async ({ page }) => {
  const state = await setup(page, '/farmer/orders', 'farmer');
  await expect(page.getByRole('columnheader', { name: 'Hàng hóa / Thanh toán' })).toBeVisible();
  await expect(page.getByText('Cà chua hữu cơ · 5 kg', { exact: true })).toBeVisible();
  for (const name of ['Đơn hàng của nông trại', 'Chuẩn bị / giao mô phỏng', 'Từ chối / giao thất bại', 'Khiếu nại và tài chính']) await expect(page.getByRole('button', { name, exact: true })).toBeVisible();
  await expect(page.getByLabel('Loại đơn', { exact: true })).toHaveCount(0);
  await page.getByLabel('Từ ngày', { exact: true }).fill('2026-10-01');
  await page.getByLabel('Đến ngày', { exact: true }).fill('2026-10-19');
  await page.getByRole('button', { name: 'Áp dụng', exact: true }).click();
  await expect.poll(() => state.requests.some(r => r.path === '/api/seller-orders' && new URLSearchParams(r.search).get('from') === '2026-10-01' && new URLSearchParams(r.search).get('to') === '2026-10-19')).toBe(true);
  await captureDesign(page, 'uc-far-06-list');
  await page.getByRole('button', { name: 'Xem và xử lý', exact: true }).click();
  await captureDesign(page, 'uc-far-06-detail');
  expect(state.requests.filter(r => r.method === 'POST')).toHaveLength(0);
});

test('Design FAR06: delivery issue and complaint use separate text and evidence', async ({ page }) => {
  const state = await setup(page, '/farmer/orders', 'farmer');
  state.order.status = 'Shipping';
  state.order.evidenceRequests = [{ id: 7, ticketId: 8, requestedAt: '2026-10-19T03:00:00', respondedAt: null }];
  state.order.complaints = [{ id: 8, status: 'InReview', refundStatuses: [] }];
  await page.getByRole('button', { name: 'Xem và xử lý', exact: true }).click();
  await page.getByRole('button', { name: 'Từ chối / giao thất bại', exact: true }).click();
  await page.getByLabel('Ghi nhận giao thất bại / hàng trả', { exact: true }).fill('Hàng bị hư hỏng trong quá trình giao nhận.');
  await page.getByLabel('Bằng chứng', { exact: true }).setInputFiles({ name: 'delivery.png', mimeType: 'image/png', buffer: png });
  await captureDesign(page, 'uc-far-06-delivery-issue');
  await page.getByRole('button', { name: 'Khiếu nại và tài chính', exact: true }).click();
  await expect(page.getByLabel('Giải trình của nông trại *', { exact: true })).toHaveValue('');
  await page.getByLabel('Giải trình của nông trại *', { exact: true }).fill('Giải trình riêng cho hồ sơ khiếu nại.');
  await page.getByLabel('Bằng chứng', { exact: true }).setInputFiles({ name: 'complaint.png', mimeType: 'image/png', buffer: png });
  await expect(page.getByText('Hồ sơ #8 · Đang xem xét', { exact: true })).toBeVisible();
  await captureDesign(page, 'uc-far-06-complaint');
  await page.getByRole('button', { name: 'Gửi phản hồi', exact: true }).click();
  await expect.poll(() => state.requests.find(r => r.path.endsWith('/evidence'))?.body.requestId).toBe(7);
  const complaint = state.requests.find(r => r.path.endsWith('/evidence')).body;
  expect(complaint.description).toBe('Giải trình riêng cho hồ sơ khiếu nại.');
  expect(complaint.files.map(f => f.name)).toEqual(['complaint.png']);
  await page.getByRole('button', { name: 'Từ chối / giao thất bại', exact: true }).click();
  await expect(page.getByLabel('Ghi nhận giao thất bại / hàng trả', { exact: true })).toHaveValue('Hàng bị hư hỏng trong quá trình giao nhận.');
  await page.getByRole('button', { name: 'Gửi báo cáo giao hàng', exact: true }).click();
  await expect.poll(() => state.requests.find(r => r.path.endsWith('/delivery-issue'))?.body.files[0].name).toBe('delivery.png');
  expect(state.requests.filter(r => r.path.endsWith('/reject'))).toHaveLength(0);
});

test('Design RC01: retail strike-through, wholesale badge and shopping destination', async ({ page }) => {
  const state = await setup(page, '/cart');
  await page.getByLabel('Số lượng Cà chua hữu cơ', { exact: true }).fill('10');
  await page.getByLabel('Số lượng Cà chua hữu cơ', { exact: true }).press('Enter');
  await expect(page.locator('del')).toHaveText('30.000 ₫');
  await expect(page.locator('.commerce-cart-price .commerce-pill')).toHaveText('Đã áp dụng giá sỉ');
  await expect(page.getByText('Nhà cung cấp đã xác minh', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Thêm nông sản vào giỏ' })).toHaveCount(0);
  await captureDesign(page, 'uc-rc-01-cart');
  await page.getByRole('button', { name: '← Tiếp tục mua sắm', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Khám phá nông sản', exact: true })).toBeVisible();
  await page.getByLabel('Lô thu hoạch', { exact: true }).selectOption('20');
  await page.getByRole('button', { name: 'Thêm vào giỏ hàng', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Đã thêm');
  expect(state.requests.find(r => r.path === '/api/cart/items' && r.method === 'POST').body.batchId).toBe(20);
});

test('Design RC01: mobile quantity buttons remain visible without overflow', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await setup(page, '/cart');
  await expect(page.getByLabel('Tăng Cà chua hữu cơ', { exact: true })).toBeVisible();
  await expect(page.getByLabel('Giảm Cà chua hữu cơ', { exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await captureDesign(page, 'uc-rc-01-mobile');
});

test('Navigation: farmer screens retain shared header and can return to other UIs', async ({ page }) => {
  await setup(page, '/farmer/products', 'farmer');
  const navigation = page.getByRole('navigation', { name: 'Mua bán nông sản', exact: true });
  await expect(page.locator('.customer-header')).toBeVisible();
  await expect(navigation.getByRole('button', { name: 'Quản lý sản phẩm', exact: true })).toHaveAttribute('aria-current', 'page');
  await navigation.getByRole('button', { name: 'Xử lý đơn hàng', exact: true }).click();
  await expect(page).toHaveURL(/#\/farmer\/orders$/);
  await expect(page.getByRole('heading', { name: 'Xử lý đơn hàng', exact: true })).toBeVisible();
  await expect(page.locator('.portal-breadcrumb')).toContainText('Xử lý đơn hàng');
  await navigation.getByRole('button', { name: 'Quản lý sản phẩm', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Chi tiết / sửa', exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Trang chủ', exact: true }).first().click();
  await expect(page).toHaveURL(/#\/orders$/);
  await navigation.getByRole('button', { name: 'Quản lý sản phẩm', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Quản lý sản phẩm', exact: true })).toBeVisible();
});

test('Navigation: cart retains shared menu and role switch can open all three UIs', async ({ page }) => {
  await setup(page, '/cart');
  const navigation = page.getByRole('navigation', { name: 'Mua bán nông sản', exact: true });
  await expect(page.locator('.customer-header')).toBeVisible();
  await navigation.getByRole('button', { name: 'Đơn hàng của tôi', exact: true }).click();
  await expect(page).toHaveURL(/#\/orders$/);
  await navigation.getByRole('button', { name: 'Giỏ hàng', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Giỏ hàng của bạn', exact: true })).toBeVisible();
  await page.locator('.user-profile-btn').click();
  await page.getByRole('button', { name: 'Nông dân', exact: true }).click();
  await expect(page).toHaveURL(/#\/farmer\/products$/);
  await expect(page.getByRole('button', { name: 'Chi tiết / sửa', exact: true })).toBeVisible();
  await navigation.getByRole('button', { name: 'Xử lý đơn hàng', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Xem và xử lý', exact: true })).toBeVisible();
  // The common header remains mounted when navigating between feature screens.
  if (!await page.getByRole('button', { name: 'Người mua lẻ', exact: true }).isVisible()) await page.locator('.user-profile-btn').click();
  await page.getByRole('button', { name: 'Người mua lẻ', exact: true }).click();
  await expect(page).toHaveURL(/#\/cart$/);
  await expect(page.getByLabel('Số lượng Cà chua hữu cơ', { exact: true })).toHaveValue('2');
});

for (const role of ['RetailCustomer', 'Farmer']) test(`Merge: restored ${role} session reaches commerce routes with Bearer identity`, async ({ page }) => {
  const farmer = role === 'Farmer';
  const state = await setup(page, farmer ? '/farmer/products' : '/cart', farmer ? 'retail-customer' : 'farmer');
  let imageAuthorization;
  const session = { accountId: 42, email: 'merge@example.test', fullName: 'Người dùng đã đăng nhập', role,
    accessToken: 'merge-session-token', accessTokenExpiresAt: '2099-01-01T00:00:00Z', refreshTokenExpiresAt: '2099-02-01T00:00:00Z' };
  await page.route('**/api/Authentication/refresh', route => route.fulfill({ json: session }));
  await page.route('**/api/product-media/merge-image', route => {
    imageAuthorization = route.request().headers().authorization;
    return route.fulfill({ contentType: 'image/png', body: png });
  });
  state.p.images = [{ url: '/api/product-media/merge-image', primary: true }];
  await page.evaluate(() => sessionStorage.setItem('agrisustain-has-session', 'true'));
  state.requests.length = 0;
  await page.reload();
  await expect(page.locator('.user-name')).toHaveText('Người dùng đã đăng nhập');
  const navigation = page.getByRole('navigation', { name: 'Mua bán nông sản', exact: true });
  await expect(navigation.getByRole('button', { name: farmer ? 'Quản lý sản phẩm' : 'Giỏ hàng', exact: true })).toBeVisible();
  await expect(navigation.getByRole('button', { name: farmer ? 'Giỏ hàng' : 'Quản lý sản phẩm', exact: true })).toHaveCount(0);
  await expect.poll(() => imageAuthorization).toBe('Bearer merge-session-token');
  await page.locator('.user-profile-btn').click();
  await expect(page.getByText('Vai trò demo:', { exact: true })).toHaveCount(0);
});
