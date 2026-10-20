import { test, expect } from '@playwright/test';
import { png, setup, captureDesign } from '../helpers/farmerRetailApi.js';

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
