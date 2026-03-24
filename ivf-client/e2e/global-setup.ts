import { test as setup, expect } from '@playwright/test';
import path from 'path';
import { ensureTestCycleExists } from './helpers';

const AUTH_FILE = path.join(__dirname, '.auth', 'admin.json');
const API_URL = process.env['API_URL'] || 'http://localhost:5000';

/**
 * Global setup: Đăng nhập qua fetch() trong browser context để bypass 2FA/step-up.
 * Gọi API từ trong Angular page = có đúng Origin headers → không kích hoạt Zero Trust step-up.
 * Credentials từ DatabaseSeeder: admin / Admin@123
 */
setup('authenticate as admin', async ({ page }) => {
  // Bước 1: Mở app trước để browser có Origin context đúng
  await page.goto('/login');

  // Bước 2: Gọi login API từ trong browser (có Origin: http://localhost:4200)
  const result = await page.evaluate(async (apiUrl: string) => {
    const res = await fetch(`${apiUrl}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: 'admin', password: 'Admin@123' }),
    });
    return { status: res.status, body: await res.json() };
  }, API_URL);

  if (!result.body.accessToken) {
    throw new Error(
      `[global-setup] Login thất bại (${result.status}): ${JSON.stringify(result.body)}\n` +
        `Nếu do MFA/Step-Up: tắt bằng lệnh:\n` +
        `docker exec ivf-db psql -U postgres -d ivf_db -c 'DELETE FROM "UserMfaSettings" WHERE "UserId" IN (SELECT "Id" FROM users WHERE "Username" = $1);' --set=1=admin`,
    );
  }

  // Bước 3: Inject token vào localStorage
  await page.evaluate(
    ({ token, refresh, userData }) => {
      localStorage.setItem('ivf_access_token', token);
      localStorage.setItem('ivf_refresh_token', refresh);
      localStorage.setItem('ivf_user', JSON.stringify(userData));
    },
    {
      token: result.body.accessToken,
      refresh: result.body.refreshToken,
      userData: result.body.user,
    },
  );

  // Bước 4: Navigate vào dashboard (Angular nhận token từ localStorage)
  await page.goto('/dashboard');
  await expect(page).toHaveURL(/\/(dashboard|reception|patients|admin)/, { timeout: 15_000 });

  // Bước 5: Lưu storage state để tái sử dụng cho tất cả tests
  await page.context().storageState({ path: AUTH_FILE });

  // Bước 6: Tạo dữ liệu test nếu chưa có
  await ensureTestCycleExists(page);
});
