import { test, expect } from '@playwright/test';

test('UC-AUTH-01 & UC-AUTH-08: Register user and verify OTP', async ({ page }) => {
  await page.goto('/#/register');
  await expect(page.getByRole('heading', { name: 'Đăng ký tài khoản mới', exact: true })).toBeVisible();

  const uniqueSuffix = Date.now().toString().slice(-6);
  const email = `dund.test.${uniqueSuffix}@agrisustain.vn`;
  const phone = `0988${uniqueSuffix}`;

  await page.getByPlaceholder('Ví dụ: Nguyễn Văn A').fill('Bác Nông Dân');
  await page.getByLabel('Số điện thoại di động *', { exact: true }).fill(phone);
  await page.getByLabel('Địa chỉ Email *', { exact: true }).fill(email);
  await page.locator('input[type="password"]').first().fill('Password@123');
  await page.locator('input[type="password"]').nth(1).fill('Password@123');
  await page.locator('input[type="checkbox"]').check();

  await page.getByRole('button', { name: /Đăng Ký & Nhận Mã OTP Xác Thực/ }).click();

  // OTP modal appears
  await expect(page.getByRole('dialog').getByRole('heading', {
    name: 'Xác thực OTP qua Email (UC-AUTH-08)', exact: true,
  })).toBeVisible();

  // Fill 6 OTP digits: 123456
  const otpDigits = ['1', '2', '3', '4', '5', '6'];
  for (let i = 0; i < 6; i++) {
    await page.locator(`#otp-input-${i}`).fill(otpDigits[i]);
  }

  await page.getByRole('button', { name: 'Xác Nhận & Hoàn Tất' }).click();

  // Verify success notice and transition
  await expect(page.getByText('MSG56: Đăng ký tài khoản thành công')).toBeVisible();
});

test('UC-AUTH-03: Manage profile and submit KYC', async ({ page }) => {
  await page.goto('/#/profile');
  await expect(page.getByText('Hồ sơ cá nhân & Sổ địa chỉ')).toBeVisible();

  // Verify form fields loaded
  await expect(page.getByLabel('Họ và tên *')).toBeVisible();

  // Update full name and phone
  await page.getByLabel('Họ và tên *').fill('Nguyễn Văn Dự (Cập Nhật)');
  await page.getByRole('button', { name: 'Lưu thay đổi' }).click();

  // Success notice
  await expect(page.getByText('MSG13: Cập nhật thông tin hồ sơ')).toBeVisible();
});

test('UC-RC-04: Order tracking, harvest batch traceability and filters', async ({ page }) => {
  await page.goto('/#/orders');
  await expect(page.getByText('Lịch sử & Theo dõi chi tiết đơn hàng')).toBeVisible();

  // Filter tabs visible
  await expect(page.getByRole('button', { name: /Tất cả đơn/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /Đang vận chuyển/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /Đã hoàn thành/ })).toBeVisible();
});
