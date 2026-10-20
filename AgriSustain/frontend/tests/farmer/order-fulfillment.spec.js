import { test, expect } from '@playwright/test';
import { png, setup, captureDesign } from '../helpers/farmerRetailApi.js';

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
