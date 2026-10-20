import { test, expect } from '@playwright/test';
import { png, setup, captureDesign } from '../helpers/farmerRetailApi.js';

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

test('IT-RC-01-014/016: unavailable line can be removed but cannot be selected', async ({ page }) => {
  const state = await setup(page, '/cart'); state.p.batches[0].available = 0;
  await page.reload();
  await expect(page.getByLabel('Chọn Cà chua hữu cơ', { exact: true })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Xóa Cà chua hữu cơ', exact: true })).toBeEnabled();
  await expect(page.getByTestId('cart-subtotal')).toHaveText('0 ₫');
});

// Design regression checks: dates must reach the API, actions keep their own payloads,
// and the cart continues to the relocated existing offer selector.



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
