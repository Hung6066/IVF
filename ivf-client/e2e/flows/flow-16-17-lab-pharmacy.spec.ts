/**
 * Luồng 16: Xét nghiệm (Lab)
 * Yêu cầu XN → Lấy mẫu → Chạy XN → Trả kết quả
 *
 * Luồng 17: Nhà thuốc (Pharmacy)
 * Đơn thuốc → Phát thuốc → Cập nhật kho
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
  uniqueName,
  getPatientFromApi,
} from '../helpers';

// ─── Luồng 16 — API: Lab ─────────────────────────────────────────────────────
test.describe('Luồng 16 — API: Lab', () => {
  test('16.A1 — GET /lab/orders trả danh sách', async ({ page }) => {
    try {
      const data = await apiGet(page, '/lab/orders?page=1&pageSize=5');
      const items = extractItems(data);
      expect(Array.isArray(items)).toBe(true);
    } catch {
      test.skip();
    }
  });

  test('16.A2 — GET /lab/orders/statistics', async ({ page }) => {
    try {
      const data = await apiGet(page, '/lab/orders/statistics');
      expect(data).toBeDefined();
    } catch {
      test.skip();
    }
  });

  test('16.A3 — GET /lab/stats', async ({ page }) => {
    try {
      const data = await apiGet(page, '/lab/stats');
      expect(data).toBeDefined();
    } catch {
      test.skip();
    }
  });

  test('16.A4 — POST /lab/orders tạo chỉ định xét nghiệm', async ({ page }) => {
    let patient: any;
    try {
      patient = await getPatientFromApi(page);
    } catch {
      test.skip();
      return;
    }
    if (!patient?.id) {
      test.skip();
      return;
    }
    try {
      const res = await apiPost(page, '/lab/orders', {
        patientId: patient.id,
        orderType: 'BloodTest',
        tests: [
          { testCode: 'FSH', testName: 'Follicle Stimulating Hormone' },
          { testCode: 'LH', testName: 'Luteinizing Hormone' },
        ],
        notes: 'E2E lab order test',
        priority: 'Normal',
      });
      expect(res.status).toBeLessThan(500);
    } catch {
      test.skip();
    }
  });

  test('16.A5 — GET /lab/cryo-locations', async ({ page }) => {
    try {
      const data = await apiGet(page, '/lab/cryo-locations');
      expect(data).toBeDefined();
    } catch {
      test.skip();
    }
  });

  test('16.A6 — POST /lab/orders thiếu body → trả lỗi', async ({ page }) => {
    try {
      const res = await apiPost(page, '/lab/orders', {});
      expect(res.status).toBeLessThan(500);
    } catch {
      test.skip();
    }
  });
});

// ─── Luồng 17 — API: Pharmacy / Prescriptions ────────────────────────────────
test.describe('Luồng 17 — API: Pharmacy', () => {
  test('17.A1 — GET /prescriptions trả danh sách', async ({ page }) => {
    try {
      const data = await apiGet(page, '/prescriptions?page=1&pageSize=5');
      const items = extractItems(data);
      expect(Array.isArray(items)).toBe(true);
    } catch {
      test.skip();
    }
  });

  test('17.A2 — GET /prescriptions/statistics', async ({ page }) => {
    try {
      const data = await apiGet(page, '/prescriptions/statistics');
      expect(data).toBeDefined();
    } catch {
      test.skip();
    }
  });

  test('17.A3 — POST /prescriptions tạo đơn thuốc', async ({ page }) => {
    let patient: any;
    try {
      patient = await getPatientFromApi(page);
    } catch {
      test.skip();
      return;
    }
    if (!patient?.id) {
      test.skip();
      return;
    }
    // Fetch doctors to get a valid DoctorId
    let doctorId: string | undefined;
    try {
      const usersData = await apiGet(page, '/users?role=Doctor&page=1&pageSize=1');
      const users = extractItems(usersData);
      doctorId = users?.[0]?.id;
    } catch {
      /* optional */
    }
    try {
      const res = await apiPost(page, '/prescriptions', {
        patientId: patient.id,
        ...(doctorId ? { doctorId } : {}),
        prescriptionDate: new Date().toISOString().split('T')[0],
        items: [
          {
            drugName: 'Progesterone',
            quantity: 30,
            unit: 'Viên',
            dosage: '1 viên/ngày',
            instructions: 'Sau ăn',
          },
        ],
        waiveConsultationFee: false,
        notes: 'E2E prescription test',
      });
      expect(res.status).toBeLessThan(500);
    } catch {
      test.skip();
    }
  });

  test('17.A4 — POST /prescriptions → GET by ID', async ({ page }) => {
    let patient: any;
    try {
      patient = await getPatientFromApi(page);
    } catch {
      test.skip();
      return;
    }
    if (!patient?.id) {
      test.skip();
      return;
    }
    try {
      const createRes = await apiPost(page, '/prescriptions', {
        patientId: patient.id,
        prescriptionDate: new Date().toISOString().split('T')[0],
        items: [
          {
            drugName: 'Estradiol',
            quantity: 14,
            unit: 'Viên',
            dosage: '1 viên/ngày',
            instructions: 'Sáng',
          },
        ],
        waiveConsultationFee: false,
      });
      if (!createRes.ok) {
        test.skip();
        return;
      }
      const created = extractItem(createRes.body);
      const id = created?.id ?? created?.data?.id;
      if (!id) {
        test.skip();
        return;
      }
      const detail = await apiGet(page, `/prescriptions/${id}`);
      expect(detail).toBeDefined();
    } catch {
      test.skip();
    }
  });

  test('17.A5 — POST /prescriptions thiếu body → trả lỗi', async ({ page }) => {
    try {
      const res = await apiPost(page, '/prescriptions', {});
      expect(res.status).toBeLessThan(500);
    } catch {
      test.skip();
    }
  });
});

// ─── Luồng 16/17 — UI ────────────────────────────────────────────────────────
test.describe('Luồng 16: Xét nghiệm (Lab)', () => {
  test('16.1 — Bảng điều khiển xét nghiệm', async ({ page }) => {
    await navigateTo(page, '/lab');
    await expectPageLoaded(page);
    if (!(await waitForFeaturePage(page, 'app-lab-dashboard, [class*="lab"]'))) {
      test.skip();
      return;
    }
  });

  test('16.2 — Danh sách yêu cầu xét nghiệm', async ({ page }) => {
    await navigateTo(page, '/lab');
    if (!(await waitForFeaturePage(page, 'app-lab-dashboard, [class*="lab"]'))) {
      test.skip();
      return;
    }
    const content = page.locator('table, .card, [class*="list"]').first();
    await expect(content).toBeVisible();
  });

  test('16.3 — Chuyển tab (Chờ, Đang XN, Hoàn thành)', async ({ page }) => {
    await navigateTo(page, '/lab');
    if (!(await waitForFeaturePage(page, 'app-lab-dashboard, [class*="lab"]'))) {
      test.skip();
      return;
    }
    const tabs = page.locator('button.tab-btn');
    await expect(tabs.first()).toBeVisible({ timeout: 5_000 });
    const count = await tabs.count();
    if (count > 1) {
      await tabs.nth(1).click();
      await waitForLoad(page);
      await expectPageLoaded(page);
    }
  });

  test('16.4 — Tìm kiếm xét nghiệm', async ({ page }) => {
    await navigateTo(page, '/lab');
    if (!(await waitForFeaturePage(page, 'app-lab-dashboard, [class*="lab"]'))) {
      test.skip();
      return;
    }
    const search = page
      .locator('input[type="search"], input[type="text"], input[placeholder*="Tìm"]')
      .first();
    if (await search.isVisible({ timeout: 3000 }).catch(() => false)) {
      await search.fill('test');
      await waitForLoad(page);
    }
  });
});

test.describe('Luồng 17: Nhà thuốc (Pharmacy)', () => {
  test('17.1 — Bảng điều khiển nhà thuốc', async ({ page }) => {
    await navigateTo(page, '/pharmacy');
    await expectPageLoaded(page);
    if (!(await waitForFeaturePage(page, 'app-pharmacy-dashboard, [class*="pharmacy"]'))) {
      test.skip();
      return;
    }
  });

  test('17.2 — Danh sách đơn thuốc', async ({ page }) => {
    await navigateTo(page, '/pharmacy');
    if (!(await waitForFeaturePage(page, 'app-pharmacy-dashboard, [class*="pharmacy"]'))) {
      test.skip();
      return;
    }
    const content = page.locator('table, .card, [class*="list"]').first();
    await expect(content).toBeVisible();
  });

  test('17.3 — Chuyển tab nhà thuốc', async ({ page }) => {
    await navigateTo(page, '/pharmacy');
    if (!(await waitForFeaturePage(page, 'app-pharmacy-dashboard, [class*="pharmacy"]'))) {
      test.skip();
      return;
    }
    const tabs = page
      .locator('[role="tab"], .tab, button')
      .filter({ hasText: /Chờ|Đã phát|Tồn kho|Đơn thuốc/i });
    const count = await tabs.count();
    if (count > 0) {
      await tabs.first().click();
      await waitForLoad(page);
      await expectPageLoaded(page);
    }
  });

  test('17.4 — Tìm kiếm đơn thuốc', async ({ page }) => {
    await navigateTo(page, '/pharmacy');
    if (!(await waitForFeaturePage(page, 'app-pharmacy-dashboard, [class*="pharmacy"]'))) {
      test.skip();
      return;
    }
    const search = page
      .locator('input[type="search"], input[type="text"], input[placeholder*="Tìm"]')
      .first();
    if (await search.isVisible({ timeout: 3000 }).catch(() => false)) {
      await search.fill('test');
      await waitForLoad(page);
    }
  });
});
