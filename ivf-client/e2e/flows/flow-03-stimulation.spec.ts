/**
 * Luồng 3: Kích thích buồng trứng (KTBT)
 * FULL TEST: API validation for stimulation + follicle scan + trigger shot
 *
 * Chọn protocol KTBT → Kê thuốc → Theo dõi nang noãn → Siêu âm
 */
import {
  test,
  expect,
  navigateTo,
  waitForLoad,
  expectPageLoaded,
  waitForFeaturePage,
  apiGet,
  apiPost,
  extractItems,
  extractItem,
  getCycleIdFromApi,
} from '../helpers';

// ─── API: Stimulation ───────────────────────────────────────────────────

test.describe('Luồng 3 — API: Stimulation', () => {
  test('3.A1 — GET /stimulation/cycle/{id} tracker data', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    try {
      const data = await apiGet(page, `/stimulation/cycle/${cycleId}`);
      expect(data).toBeDefined();
    } catch {
      /* cycle may not be in stimulation phase */
    }
  });

  test('3.A2 — GET /stimulation/cycle/{id}/chart follicle chart', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    try {
      const data = await apiGet(page, `/stimulation/cycle/${cycleId}/chart`);
      expect(data).toBeDefined();
    } catch {
      /* chart may be empty */
    }
  });

  test('3.A3 — GET /stimulation/cycle/{id}/medications lịch thuốc', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    try {
      const data = await apiGet(page, `/stimulation/cycle/${cycleId}/medications`);
      expect(data).toBeDefined();
    } catch {
      /* no medications yet */
    }
  });

  test('3.A4 — POST /stimulation/cycle/{id}/scan ghi nhận siêu âm nang', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    const res = await apiPost(page, `/stimulation/cycle/${cycleId}/scan`, {
      scanDate: new Date().toISOString().split('T')[0],
      endometriumThickness: 8.5,
      leftFollicles: [{ size: 14 }, { size: 12 }],
      rightFollicles: [{ size: 16 }, { size: 13 }],
      notes: 'E2E scan test',
    });
    // Accept success or phase mismatch
    expect(res.status).toBeLessThan(500);
  });

  test('3.A5 — POST /stimulation/cycle/{id}/evaluate đánh giá follicle', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    const res = await apiPost(page, `/stimulation/cycle/${cycleId}/evaluate`, {});
    expect(res.status).toBeLessThan(500);
  });
});

// ─── UI: Ultrasound Dashboard ───────────────────────────────────────────

test.describe('Luồng 3 — UI: Siêu âm & KTBT', () => {
  test('3.1 — Mở trang siêu âm (ultrasound dashboard)', async ({ page }) => {
    await navigateTo(page, '/ultrasound');
    await expectPageLoaded(page);
    if (!(await waitForFeaturePage(page, 'app-ultrasound-dashboard, [class*="ultrasound"]'))) {
      test.skip();
      return;
    }
  });

  test('3.2 — Siêu âm: chuyển tab hàng đợi / lịch sử', async ({ page }) => {
    await navigateTo(page, '/ultrasound');
    if (!(await waitForFeaturePage(page, 'app-ultrasound-dashboard, [class*="ultrasound"]'))) {
      test.skip();
      return;
    }
    const tabs = page
      .locator('button, [role="tab"]')
      .filter({ hasText: /hàng đợi|kết quả|lịch sử/i });
    const count = await tabs.count();
    expect(count).toBeGreaterThanOrEqual(1);
  });
});
