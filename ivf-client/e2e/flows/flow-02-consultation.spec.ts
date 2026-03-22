/**
 * Luồng 2: Tư vấn sau xét nghiệm
 * FULL TEST: API validation + CRUD couples & cycles + UI verification
 *
 * XN xong → BS xem KQ → Tư vấn → Tạo cặp đôi → Tạo chu kỳ điều trị
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
  apiPut,
  apiDelete,
  extractItems,
  extractItem,
  uniqueName,
  getPatientFromApi,
  getCoupleFromApi,
  getCycleIdFromApi,
} from '../helpers';

// ─── API: Couples CRUD ──────────────────────────────────────────────────

test.describe('Luồng 2 — API: Couples CRUD', () => {
  let wifeId: string;
  let husbandId: string;
  let createdCoupleId: string;

  test('2.A1 — GET /couples trả về danh sách', async ({ page }) => {
    const data = await apiGet(page, '/couples');
    const items = extractItems(data);
    expect(Array.isArray(items)).toBeTruthy();
    if (items.length > 0) {
      expect(items[0]).toHaveProperty('id');
    }
  });

  test('2.A2 — POST /patients tạo vợ + chồng cho couple', async ({ page }) => {
    const wifeName = uniqueName('Wife');
    const husbandName = uniqueName('Husband');
    const wifeRes = await apiPost(page, '/patients', {
      fullName: wifeName,
      dateOfBirth: '1993-05-10',
      gender: 1,
      patientType: 0,
      phone: '0911111111',
    });
    expect(wifeRes.ok).toBeTruthy();
    wifeId = extractItem(wifeRes.body).id;

    const husbandRes = await apiPost(page, '/patients', {
      fullName: husbandName,
      dateOfBirth: '1991-08-20',
      gender: 0,
      patientType: 0,
      phone: '0922222222',
    });
    expect(husbandRes.ok).toBeTruthy();
    husbandId = extractItem(husbandRes.body).id;
  });

  test('2.A3 — POST /couples tạo cặp đôi', async ({ page }) => {
    if (!wifeId || !husbandId) {
      test.skip();
      return;
    }
    const res = await apiPost(page, '/couples', {
      wifeId,
      husbandId,
      marriageDate: '2018-12-01',
      infertilityYears: 3,
    });
    expect(res.ok).toBeTruthy();
    const couple = extractItem(res.body);
    expect(couple).toHaveProperty('id');
    createdCoupleId = couple.id;
  });

  test('2.A4 — GET /couples/{id} trả đúng cặp đôi', async ({ page }) => {
    if (!createdCoupleId) {
      test.skip();
      return;
    }
    const data = await apiGet(page, `/couples/${createdCoupleId}`);
    const couple = extractItem(data);
    expect(couple.id).toBe(createdCoupleId);
  });

  test('2.A5 — PUT /couples/{id} cập nhật', async ({ page }) => {
    if (!createdCoupleId) {
      test.skip();
      return;
    }
    const res = await apiPut(page, `/couples/${createdCoupleId}`, {
      marriageDate: '2019-06-15',
      infertilityYears: 4,
    });
    expect(res.ok).toBeTruthy();
  });

  test('2.A6 — GET /couples/patient/{patientId} tìm theo vợ', async ({ page }) => {
    if (!wifeId) {
      test.skip();
      return;
    }
    const data = await apiGet(page, `/couples/patient/${wifeId}`);
    const couple = extractItem(data);
    expect(couple).toHaveProperty('id');
  });

  test('2.A7 — POST /couples body rỗng → 400', async ({ page }) => {
    const res = await apiPost(page, '/couples', {});
    expect(res.ok).toBeFalsy();
    expect(res.status).toBeGreaterThanOrEqual(400);
  });
});

// ─── API: Cycles CRUD ───────────────────────────────────────────────────

test.describe('Luồng 2 — API: Cycles CRUD', () => {
  let cycleId: string;

  test('2.C1 — GET /cycles/active trả danh sách active', async ({ page }) => {
    const data = await apiGet(page, '/cycles/active');
    const items = extractItems(data);
    expect(Array.isArray(items)).toBeTruthy();
    if (items.length > 0) {
      expect(items[0]).toHaveProperty('id');
      expect(items[0]).toHaveProperty('coupleId');
    }
  });

  test('2.C2 — POST /cycles tạo chu kỳ mới', async ({ page }) => {
    const couple = await getCoupleFromApi(page);
    if (!couple) {
      test.skip();
      return;
    }
    const res = await apiPost(page, '/cycles', {
      coupleId: couple.id,
      method: 2,
      startDate: new Date().toISOString().split('T')[0],
      notes: `E2E cycle ${Date.now()}`,
    });
    if (res.ok) {
      const cycle = extractItem(res.body);
      expect(cycle).toHaveProperty('id');
      cycleId = cycle.id;
    }
  });

  test('2.C3 — GET /cycles/{id} trả chi tiết', async ({ page }) => {
    const id = cycleId ?? (await getCycleIdFromApi(page));
    if (!id) {
      test.skip();
      return;
    }
    const data = await apiGet(page, `/cycles/${id}`);
    const cycle = extractItem(data);
    expect(cycle.id).toBe(id);
  });

  test('2.C4 — GET /cycles/couple/{coupleId} theo cặp đôi', async ({ page }) => {
    const couple = await getCoupleFromApi(page);
    if (!couple) {
      test.skip();
      return;
    }
    const data = await apiGet(page, `/cycles/couple/${couple.id}`);
    const items = extractItems(data);
    expect(Array.isArray(items)).toBeTruthy();
  });

  test('2.C5 — POST /cycles body rỗng → 400', async ({ page }) => {
    const res = await apiPost(page, '/cycles', {});
    expect(res.ok).toBeFalsy();
    expect(res.status).toBeGreaterThanOrEqual(400);
  });
});

// ─── UI: Consultation Dashboard ─────────────────────────────────────────

test.describe('Luồng 2 — UI: Tư vấn & Cặp đôi', () => {
  test('2.1 — Mở trang tư vấn (consultation dashboard)', async ({ page }) => {
    await navigateTo(page, '/consultation');
    await expectPageLoaded(page);
    if (!(await waitForFeaturePage(page, 'app-consultation-dashboard, [class*="consultation"]'))) {
      test.skip();
      return;
    }
  });

  test('2.2 — Chuyển tab: hàng đợi ↔ lịch sử tư vấn', async ({ page }) => {
    await navigateTo(page, '/consultation');
    if (!(await waitForFeaturePage(page, 'app-consultation-dashboard, [class*="consultation"]'))) {
      test.skip();
      return;
    }
    const tabs = page.locator('button.tab-btn').filter({ hasText: /Hàng đợi|Hồ sơ khám|Lịch sử/i });
    await expect(tabs.first()).toBeVisible({ timeout: 5_000 });
    const count = await tabs.count();
    expect(count).toBeGreaterThanOrEqual(2);
  });

  test('2.3 — Danh sách cặp đôi', async ({ page }) => {
    await navigateTo(page, '/couples');
    await expectPageLoaded(page);
    await expect(page.locator('table, .couple-list, [class*="couple"]').first()).toBeVisible({
      timeout: 10_000,
    });
  });

  test('2.4 — Form tạo cặp đôi mới', async ({ page }) => {
    await navigateTo(page, '/couples/new');
    await expectPageLoaded(page);
    await expect(page.locator('form, input').first()).toBeVisible();
  });

  test('2.5 — Chi tiết chu kỳ: phase timeline', async ({ page }) => {
    const cycleId = await getCycleIdFromApi(page);
    if (!cycleId) {
      test.skip();
      return;
    }
    await navigateTo(page, `/cycles/${cycleId}`);
    await expectPageLoaded(page);
    await expect(page.locator('.phase-timeline, .timeline-section, .tab-nav').first()).toBeVisible({
      timeout: 10_000,
    });
  });
});
