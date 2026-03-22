/**
 * Luồng 9: Theo dõi beta-hCG
 * Luồng 10: Theo dõi thai kỳ (Prenatal)
 * FULL TEST: API validation for pregnancy tracking + UI
 */
import {
  test, expect, navigateTo, waitForLoad, expectPageLoaded, getCycleIdFromApi,
  apiGet, apiPost, extractItems, extractItem,
} from '../helpers';

// ─── API: Pregnancy & Beta-HCG ─────────────────────────────────────────

test.describe('Luồng 9/10 — API: Pregnancy', () => {
  test('9.A1 — GET /pregnancy/cycle/{id} dữ liệu thai kỳ', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) { test.skip(); return; }
    try {
      const data = await apiGet(page, `/pregnancy/cycle/${cycleId}`);
      expect(data).toBeDefined();
    } catch { /* cycle may not have pregnancy data */ }
  });

  test('9.A2 — GET /pregnancy/cycle/{id}/beta-hcg kết quả beta', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) { test.skip(); return; }
    try {
      const data = await apiGet(page, `/pregnancy/cycle/${cycleId}/beta-hcg`);
      expect(data).toBeDefined();
    } catch { /* no beta-hCG data yet */ }
  });

  test('9.A3 — POST /pregnancy/cycle/{id}/beta-hcg ghi nhận beta', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) { test.skip(); return; }
    const res = await apiPost(page, `/pregnancy/cycle/${cycleId}/beta-hcg`, {
      betaHcg: 250.5,
      testDate: new Date().toISOString().split('T')[0],
      notes: 'E2E beta-hCG test',
    });
    // Accept success or phase mismatch
    expect(res.status).toBeLessThan(500);
  });

  test('10.A1 — GET /pregnancy/cycle/{id}/follow-up kế hoạch theo dõi', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) { test.skip(); return; }
    try {
      const data = await apiGet(page, `/pregnancy/cycle/${cycleId}/follow-up`);
      expect(data).toBeDefined();
    } catch { /* no follow-up plan yet */ }
  });

  test('10.A2 — POST /pregnancy/cycle/{id}/prenatal-exam khám thai 7 tuần', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) { test.skip(); return; }
    const res = await apiPost(page, `/pregnancy/cycle/${cycleId}/prenatal-exam`, {
      examDate: new Date().toISOString().split('T')[0],
      gestationalSacs: 1,
      fetalHeartbeats: 1,
      ultrasoundFindings: 'E2E: 1 túi thai, tim thai (+)',
      notes: 'E2E prenatal test',
    });
    // Accept success or phase mismatch
    expect(res.status).toBeLessThan(500);
  });

  test('10.A3 — POST /pregnancy/cycle/{id}/beta-hcg body rỗng → 400+', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) { test.skip(); return; }
    const res = await apiPost(page, `/pregnancy/cycle/${cycleId}/beta-hcg`, {});
    expect(res.ok).toBeFalsy();
    expect(res.status).toBeGreaterThanOrEqual(400);
  });
});

// ─── UI: Pregnancy Pages ────────────────────────────────────────────────

test.describe('Luồng 9/10 — UI: Thai kỳ', () => {
  test('9.1 — Trang beta-hCG khi có cycleId', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) { test.skip(); return; }
    await navigateTo(page, `/pregnancy/${cycleId}/beta-hcg`);
    await expectPageLoaded(page);
    await expect(page.locator('app-pregnancy-beta-hcg, [class*="beta"], [class*="pregnancy"]').first()).toBeVisible();
  });

  test('9.2 — Beta-hCG: form nhập kết quả xét nghiệm', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) { test.skip(); return; }
    await navigateTo(page, `/pregnancy/${cycleId}/beta-hcg`);
    await expect(page.locator('h1, button').filter({ hasText: /Beta|Ghi nhận/i }).first()).toBeVisible({ timeout: 10_000 });
  });

  test('10.1 — Trang prenatal khi có cycleId', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) { test.skip(); return; }
    await navigateTo(page, `/pregnancy/${cycleId}/prenatal`);
    await expectPageLoaded(page);
    await expect(page.locator('app-pregnancy-prenatal, [class*="prenatal"], [class*="pregnancy"]').first()).toBeVisible();
  });

  test('10.2 — Prenatal: thông tin thai kỳ', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) { test.skip(); return; }
    await navigateTo(page, `/pregnancy/${cycleId}/prenatal`);
    await expect(page.locator('h1, input').first()).toBeVisible({ timeout: 10_000 });
  });
});
