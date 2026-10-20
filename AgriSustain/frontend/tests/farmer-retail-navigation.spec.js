import { test, expect } from '@playwright/test';
import { png, setup, captureDesign } from './helpers/farmerRetailApi.js';

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
