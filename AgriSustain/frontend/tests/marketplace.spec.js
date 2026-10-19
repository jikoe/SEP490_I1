import { test, expect } from '@playwright/test';

const products = [
  { id: 1, name: 'Rau xà lách thủy canh', categoryId: 1, categoryName: 'Rau củ quả', supplierId: 1,
    supplierName: 'HTX Nông nghiệp Xanh', region: 'Đà Lạt, Lâm Đồng', price: 25000, unit: 'kg',
    imageUrl: '/marketplace/catalog-reference.png#lettuce', availableQuantity: 40, rating: 4.8, reviewCount: 120, certifications: ['VietGAP'] },
  { id: 2, name: 'Xoài cát Hòa Lộc', categoryId: 2, categoryName: 'Trái cây', supplierId: 2,
    supplierName: 'Trang trại Hòa Lộc', region: 'Tiền Giang', price: 45000, unit: 'kg',
    imageUrl: '/marketplace/catalog-reference.png#mango', availableQuantity: 50, rating: 4.9, reviewCount: 89, certifications: ['OCOP'] },
  { id: 3, name: 'Gạo ST25', categoryId: 3, categoryName: 'Gạo & ngũ cốc', supplierId: 1,
    supplierName: 'HTX Nông nghiệp Xanh', region: 'Sóc Trăng', price: 32000, unit: 'kg',
    imageUrl: '/marketplace/gao_st25_main.png', availableQuantity: 100, rating: 4.8, reviewCount: 120, certifications: ['VietGAP'] },
  { id: 4, name: 'Tôm sú tươi', categoryId: 5, categoryName: 'Thủy hải sản', supplierId: 3,
    supplierName: 'Trang trại Thủy sản Bình Minh', region: 'Bến Tre', price: 180000, unit: 'kg',
    imageUrl: '/marketplace/catalog-reference.png#shrimp', availableQuantity: 20, rating: 4.8, reviewCount: 72, certifications: ['VietGAP'] },
];
const filters = {
  categories: ['Rau củ quả', 'Trái cây', 'Gạo & ngũ cốc', 'Thực phẩm chế biến', 'Thủy hải sản', 'Gia súc, gia cầm', 'Sản phẩm khác']
    .map((name, index) => ({ id: index + 1, name, count: [342, 216, 185, 98, 76, 58, 273][index] })),
  suppliers: [{ id: 1, name: 'HTX Nông nghiệp Xanh', count: 2 }],
  seasons: [{ id: 1, name: 'Đông Xuân 2026', count: 1 }], regions: ['Sóc Trăng', 'Tiền Giang'],
  certifications: ['VietGAP', 'OCOP', 'Hữu cơ'],
};
const login = { accountId: 42, fullName: 'Nguyễn Văn A', email: 'buyer@example.test', role: 'RetailCustomer',
  accessToken: 'browser-test-token', accessTokenExpiresAt: '2099-01-01T00:00:00Z', refreshTokenExpiresAt: '2099-02-01T00:00:00Z' };

async function mockApi(page) {
  await page.route('**/api/products/filters', route => route.fulfill({ json: filters }));
  await page.route(/\/api\/products\?.*/, route => {
    const params = new URL(route.request().url()).searchParams;
    let items = products;
    if (params.get('keyword')) items = items.filter(item => item.name.includes(params.get('keyword')));
    if (params.get('categoryId')) items = items.filter(item => item.categoryId === Number(params.get('categoryId')));
    route.fulfill({ json: { items, total: items.length, page: Number(params.get('page') || 1), pageSize: Number(params.get('pageSize') || 12) } });
  });
  await page.route('**/api/products/3', route => route.fulfill({ json: {
    product: products[2], description: 'Đặc sản Sóc Trăng – Hương thơm tự nhiên, vị ngọt thanh\n\nGạo ST25 là giống gạo nổi tiếng của Việt Nam, được trồng tại vùng đất Sóc Trăng màu mỡ. Hạt gạo dài, trong và có mùi thơm tự nhiên đặc trưng, khi nấu lên dẻo mềm, vị ngọt thanh, rất được ưa chuộng trên thị trường.',
    farmVerification: 'Approved', farmRating: 4.8, wholesalePrice: null, wholesaleMinQuantity: null,
    images: ['gao_st25_main.png', 'gao_st25_thumb_bowl.png', 'ruong_lua_thumb.png', 'bao_bi_gao_thumb.png']
      .map(file => ({ url: `/marketplace/${file}`, altText: 'Gạo ST25' })),
    certificates: [{ type: 'VietGAP', number: 'TEST-CERT', issuedBy: 'Tổ chức chứng nhận (dữ liệu kiểm thử)', expiresOn: '2027-01-01' }],
    batches: [{ id: 1, code: 'ST25-DX-2026', season: 'Đông Xuân 2026', availableQuantity: 100, harvestedOn: '2026-10-01', expiresOn: '2027-10-01' }],
    reviews: [{ rating: 5, comment: 'Gạo thơm, hạt dài, cơm dẻo ngon.', createdAt: '2026-10-18T03:00:00Z' }],
    canAddToCart: true, canStartB2B: false,
  } }));
  await page.route('**/api/Authentication/login', route => route.fulfill({ json: login }));
  await page.route('**/api/Authentication/refresh', route => route.fulfill({ json: login }));
}

async function signIn(page, returnTo) {
  await page.goto(`/#/login?returnTo=${encodeURIComponent(returnTo)}`);
  await page.getByLabel('Email', { exact: true }).fill('buyer@example.test');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('OldPass123');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
}

test.beforeEach(async ({ page }) => { await mockApi(page); });

test('UC-MKT-01: search, filters, empty state and reset', async ({ page }) => {
  await page.goto('/#/marketplace');
  await expect(page.locator('.market-product-card')).toHaveCount(4);
  await page.screenshot({ path: 'test-results/marketplace-desktop.png', fullPage: true });
  await page.getByLabel('Tìm kiếm sản phẩm, danh mục').fill('Gạo');
  await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
  await expect(page.locator('.market-product-card')).toHaveCount(1);
  await page.getByLabel('Tìm kiếm sản phẩm, danh mục').fill('Không tồn tại');
  await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click();
  await expect(page.getByText('Không tìm thấy sản phẩm phù hợp')).toBeVisible();
  await page.getByRole('button', { name: 'Xóa bộ lọc', exact: true }).click();
  await expect(page.locator('.market-product-card')).toHaveCount(4);
  await expect(page.getByLabel('Tìm kiếm sản phẩm, danh mục')).toHaveValue('');
  const request = page.waitForRequest(request => request.url().includes('minRating=4'));
  await page.getByLabel('Đánh giá', { exact: true }).selectOption('4');
  await request;
  await page.getByRole('button', { name: 'Xóa tất cả bộ lọc' }).click();
  await expect(page.getByLabel('Đánh giá', { exact: true })).toHaveValue('');
});

test('UC-MKT-02: gallery, tabs, stock validation and cart integration', async ({ page }) => {
  await signIn(page, '/products/3');
  await expect(page.getByRole('heading', { name: 'Gạo ST25', exact: true })).toBeVisible();
  await page.screenshot({ path: 'test-results/product-detail-desktop.png', fullPage: true });
  await page.getByRole('button', { name: 'Xem ảnh 2', exact: true }).click();
  await expect(page.locator('.market-main-photo img')).toHaveAttribute('src', /thumb_bowl/);
  await page.getByRole('tab', { name: 'Đánh giá (120)', exact: true }).click();
  await expect(page.getByText('Gạo thơm, hạt dài, cơm dẻo ngon.')).toBeVisible();
  await page.getByRole('tab', { name: 'Thông tin sản phẩm', exact: true }).click();
  await page.getByLabel('Số lượng', { exact: true }).fill('101');
  await expect(page.getByRole('button', { name: 'Thêm vào giỏ hàng', exact: true })).toBeDisabled();
  await page.getByLabel('Số lượng', { exact: true }).fill('1.001');
  await expect(page.getByRole('button', { name: 'Thêm vào giỏ hàng', exact: true })).toBeEnabled();
  await page.getByLabel('Số lượng', { exact: true }).fill('2');
  await page.route('**/api/carts/current/items', route => {
    expect(route.request().postDataJSON()).toEqual({ productId: 3, quantity: 2 });
    return route.fulfill({ json: 2 });
  });
  await page.getByRole('button', { name: 'Thêm vào giỏ hàng', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Đã thêm 2 kg Gạo ST25');
});

test('UC-AUTH-06: visibility, validation, server errors, success and cancel', async ({ page }) => {
  await signIn(page, '/change-password');
  await page.screenshot({ path: 'test-results/change-password-desktop.png', fullPage: true });
  await page.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveCount(3);
  await page.getByLabel('Mật khẩu hiện tại', { exact: true }).fill('OldPass123');
  await page.getByRole('button', { name: 'Hiện mật khẩu hiện tại', exact: true }).click();
  await expect(page.getByLabel('Mật khẩu hiện tại', { exact: true })).toHaveAttribute('type', 'text');
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill('NewPass123');
  await page.getByLabel('Xác nhận mật khẩu mới', { exact: true }).fill('NewPass123');
  await page.route('**/api/passwords/change', route => route.fulfill({
    status: 400, json: { code: 'MSG04', title: 'Mật khẩu hiện tại không chính xác.' },
  }));
  await page.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveText('Mật khẩu hiện tại không chính xác.');
  await page.route('**/api/passwords/change', route => {
    expect(route.request().postDataJSON()).toEqual({ currentPassword: 'OldPass123', newPassword: 'NewPass123', confirmNewPassword: 'NewPass123' });
    return route.fulfill({ json: 'Đổi mật khẩu thành công.' });
  });
  await page.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).click();
  await expect(page.getByRole('status')).toHaveText('Đổi mật khẩu thành công.');
  await expect(page.getByLabel('Mật khẩu mới', { exact: true })).toHaveValue('');
  await page.getByRole('button', { name: 'Hủy', exact: true }).click();
  await expect(page).toHaveURL(/marketplace/);
});

test('mobile: marketplace, detail and password fit the viewport', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  for (const route of ['/marketplace', '/products/3']) {
    await page.goto(`/#${route}`);
    await expect(page.locator('h1')).toBeVisible();
    if (route === '/marketplace') {
      await expect(page.locator('.market-category-tiles button').first()).toContainText('Rau củ quả');
      await expect(page.locator('.market-category-tiles button span').first()).toBeVisible();
    }
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
    await page.screenshot({ path: `test-results/${route.includes('products') ? 'detail' : 'marketplace'}-mobile.png`, fullPage: true });
  }
  await signIn(page, '/change-password');
  await expect(page.getByRole('heading', { name: 'Đổi mật khẩu' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
  await page.screenshot({ path: 'test-results/password-mobile.png', fullPage: true });
  expect(errors).toEqual([]);
});
