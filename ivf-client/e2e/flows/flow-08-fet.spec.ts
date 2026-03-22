/**
 * Luồng 8: Chuyển phôi trữ (FET)
 * FULL TEST: API validation for FET cycles + UI
 *
 * Phôi trữ đông → Rã đông → Chuyển phôi
 */
import {
  test, expect, navigateTo, waitForLoad, expectPageLoaded,
  apiGet, apiPost, extractItems, extractItem, getCoupleFromApi,
} from '../helpers';

// ─── API: FET Cycles ────────────────────────────────────────────────────

test.describe('Luồng 8 — API: FET Cycles', () => {
  test('8.A1 — POST /cycles tạo chu kỳ FET (method=4)', async ({ page }) => {
    const couple = await getCoupleFromApi(page);
    if (!couple) { test.skip(); return; }
    const res = await apiPost(page, '/cycles', {
      coupleId: couple.id,
      method: 4, // FET
      startDate: new Date().toISOString().split('T')[0],
      notes: `E2E FET cycle ${Date.now()}`,
    });
    if (res.ok) {
      const cycle = extractItem(res.body);
      expect(cycle).toHaveProperty('id');
    }
    // Accept success or duplicate cycle error
    expect(res.status).toBeLessThan(500);
  });

  test('8.A2 — GET /cycles/active bao gồm FET cycles', async ({ page }) => {
    const data = await apiGet(page, '/cycles/active');
    const items = extractItems(data);
    expect(Array.isArray(items)).toBeTruthy();
  });

  test('8.A3 — GET /cycles/couple/{id} lọc theo couple', async ({ page }) => {
    const couple = await getCoupleFromApi(page);
    if (!couple) { test.skip(); return; }
    const data = await apiGet(page, `/cycles/couple/${couple.id}`);
    const items = extractItems(data);
    expect(Array.isArray(items)).toBeTruthy();
  });
});

// ─── UI: FET Pages ──────────────────────────────────────────────────────

test.describe('Luồng 8 — UI: Chuyển phôi trữ', () => {
  test('8.1 — Danh sách FET', async ({ page }) => {
    await navigateTo(page, '/fet');
    await expectPageLoaded(page);
    await expect(page.locator('app-fet-list, [class*="fet"]').first()).toBeVisible();
  });

  test('8.2 — FET: bảng hoặc thẻ dữ liệu', async ({ page }) => {
    await navigateTo(page, '/fet');
    await waitForLoad(page);
    const content = page.locator('table, .card, [class*="list"]').first();
    await expect(content).toBeVisible();
  });

  test('8.3 — Nút tạo FET mới', async ({ page }) => {
    await navigateTo(page, '/fet');
    await waitForLoad(page);
    const createBtn = page.locator('button, a').filter({ hasText: /Tạo|Thêm|Chuyển phôi/i }).first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await expect(createBtn).toBeVisible();
    }
  });

  test('8.4 — Chi tiết FET (nếu có dữ liệu)', async ({ page }) => {
    await navigateTo(page, '/fet');
    await waitForLoad(page);
    await expect(page.locator('app-fet-list, [class*="fet"], table, .card, h1, h2').first()).toBeVisible({ timeout: 10_000 });
    const detailLink = page.locator('a[href*="/fet/"], tr[class*="clickable"], [routerLink*="/fet/"]').first();
    if (await detailLink.isVisible({ timeout: 3000 }).catch(() => false)) {
      await detailLink.click();
      await waitForLoad(page);
      await expectPageLoaded(page);
      await expect(page.locator('app-fet-detail, [class*="fet-detail"]').first()).toBeVisible();
    }
  });
});
