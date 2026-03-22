import { test as setup, expect } from '@playwright/test';
import path from 'path';
import { ensureTestCycleExists } from './helpers';

const AUTH_FILE = path.join(__dirname, '.auth', 'admin.json');

/**
 * Global setup: Đăng nhập 1 lần, lưu session để dùng cho tất cả tests.
 * Credentials từ DatabaseSeeder: admin / Admin@123
 */
setup('authenticate as admin', async ({ page }) => {
  await page.goto('/login');

  // Dismiss cookie consent banner nếu có
  const acceptBtn = page.locator('button').filter({ hasText: /Accept All|Chấp nhận/i }).first();
  if (await acceptBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
    await acceptBtn.click();
  }

  // Điền form đăng nhập
  await page.locator('#username').fill('admin');
  await page.locator('#password').fill('Admin@123');
  await page.locator('button[type="submit"]').click();

  // Chờ redirect về dashboard sau khi login thành công
  await expect(page).toHaveURL(/\/(dashboard|reception|patients)/, { timeout: 15_000 });

  // Lưu storage state (localStorage + cookies) để dùng lại
  await page.context().storageState({ path: AUTH_FILE });

  // Tạo dữ liệu test (patient, couple, cycle) nếu chưa có
  await ensureTestCycleExists(page);
});
