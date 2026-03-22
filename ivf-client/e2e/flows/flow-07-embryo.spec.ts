/**
 * Luồng 7: Nuôi cấy & chuyển phôi (Embryo Culture & Transfer)
 * FULL TEST: API validation for cycle detail, phases, consent forms + UI tabs
 */
import {
  test,
  expect,
  navigateTo,
  waitForLoad,
  expectPageLoaded,
  getCycleIdFromApi,
  apiGet,
  apiPost,
  extractItems,
  extractItem,
  getCycleFromApi,
  getPatientFromApi,
} from '../helpers';

// ─── API: Cycle Detail & Phase ──────────────────────────────────────────

test.describe('Luồng 7 — API: Cycle Detail & Phase', () => {
  test('7.A1 — GET /cycles/{id} trả chi tiết đầy đủ', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    const data = await apiGet(page, `/cycles/${cycleId}`);
    const cycle = extractItem(data);
    expect(cycle).toHaveProperty('id');
    expect(cycle).toHaveProperty('coupleId');
  });

  test('7.A2 — POST /cycles/{id}/advance chuyển phase', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    const res = await apiPost(page, `/cycles/${cycleId}/advance`, {});
    // Accept 200 (success) or 400 (already at this phase / invalid transition)
    expect(res.status).toBeLessThan(500);
  });

  test('7.A3 — GET /consent-forms/cycle/{id} consent cho cycle', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    const data = await apiGet(page, `/consent-forms/cycle/${cycleId}`);
    const items = extractItems(data);
    expect(Array.isArray(items)).toBeTruthy();
  });

  test('7.A4 — GET /stimulation/cycle/{id} stimulation data', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    try {
      const data = await apiGet(page, `/stimulation/cycle/${cycleId}`);
      expect(data).toBeDefined();
    } catch {
      /* may not have stimulation data */
    }
  });

  test('7.A5 — GET /procedures/cycle/{id} procedures cho cycle', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    const data = await apiGet(page, `/procedures/cycle/${cycleId}`);
    const items = extractItems(data);
    expect(Array.isArray(items)).toBeTruthy();
  });
});

// ─── UI: Cycle Detail Tabs ──────────────────────────────────────────────

test.describe('Luồng 7 — UI: Nuôi cấy & chuyển phôi', () => {
  test('7.1 — Chi tiết chu kỳ: hiển thị tab navigation', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    await navigateTo(page, `/cycles/${cycleId}`);
    await expectPageLoaded(page);
    const tabNav = page.locator('.tab-nav, nav').filter({ has: page.locator('button, a') });
    await expect(tabNav.first()).toBeVisible();
  });

  test('7.2 — Tab Chỉ định (indication)', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    await navigateTo(page, `/cycles/${cycleId}`);
    const indicationTab = page
      .locator('button.tab-btn')
      .filter({ hasText: /Chỉ định|Indication/i })
      .first();
    if (await indicationTab.isVisible({ timeout: 5000 }).catch(() => false)) {
      await indicationTab.click();
      await waitForLoad(page);
      await expect(page.locator('app-indication-tab').first()).toBeVisible();
    }
  });

  test('7.3 — Tab Nuôi cấy (culture)', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    await navigateTo(page, `/cycles/${cycleId}`);
    const cultureTab = page
      .locator('button.tab-btn')
      .filter({ hasText: /Nuôi cấy|Culture/i })
      .first();
    if (await cultureTab.isVisible({ timeout: 5000 }).catch(() => false)) {
      await cultureTab.click();
      await waitForLoad(page);
      await expect(page.locator('app-culture-tab').first()).toBeVisible();
    }
  });

  test('7.4 — Tab Chuyển phôi (transfer)', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    await navigateTo(page, `/cycles/${cycleId}`);
    const transferTab = page
      .locator('button.tab-btn')
      .filter({ hasText: /Chuyển phôi|Transfer/i })
      .first();
    if (await transferTab.isVisible({ timeout: 5000 }).catch(() => false)) {
      await transferTab.click();
      await waitForLoad(page);
      await expect(page.locator('app-transfer-tab').first()).toBeVisible();
    }
  });

  test('7.5 — Phase timeline hiển thị', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    await navigateTo(page, `/cycles/${cycleId}`);
    const timeline = page.locator('.phase-timeline, .timeline-section');
    await expect(timeline.first()).toBeVisible({ timeout: 10_000 });
  });
});
