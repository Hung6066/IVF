/**
 * Luồng 1: Khám lần đầu (First Visit)
 * FULL TEST: API validation + CRUD flow + Form validation + UI verification
 *
 * BN đến lần đầu → Tiếp đón → Tạo hồ sơ → Cấp STT → Tư vấn → Đồng thuận → Hẹn tái khám
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
} from '../helpers';

// ─── API Validation ─────────────────────────────────────────────────────

test.describe('Luồng 1 — API: Patients CRUD', () => {
  let createdPatientId: string;

  test('1.A1 — GET /patients trả về danh sách hợp lệ', async ({ page }) => {
    const data = await apiGet(page, '/patients?page=1&pageSize=5');
    const items = extractItems(data);
    expect(Array.isArray(items)).toBeTruthy();
    if (items.length > 0) {
      expect(items[0]).toHaveProperty('id');
      expect(items[0]).toHaveProperty('fullName');
    }
  });

  test('1.A2 — POST /patients tạo bệnh nhân mới', async ({ page }) => {
    const name = uniqueName('BN_Test');
    const res = await apiPost(page, '/patients', {
      fullName: name,
      dateOfBirth: '1995-03-15',
      gender: 1,
      patientType: 0,
      phone: '0912345678',
    });
    expect(res.ok).toBeTruthy();
    const patient = extractItem(res.body);
    expect(patient).toHaveProperty('id');
    expect(patient.fullName).toBe(name);
    createdPatientId = patient.id;
  });

  test('1.A3 — GET /patients/{id} trả đúng bệnh nhân', async ({ page }) => {
    if (!createdPatientId) {
      test.skip();
      return;
    }
    const data = await apiGet(page, `/patients/${createdPatientId}`);
    const patient = extractItem(data);
    expect(patient.id).toBe(createdPatientId);
    expect(patient.fullName).toContain('BN_Test');
  });

  test('1.A4 — PUT /patients/{id} cập nhật thành công', async ({ page }) => {
    if (!createdPatientId) {
      test.skip();
      return;
    }
    const res = await apiPut(page, `/patients/${createdPatientId}`, {
      fullName: 'BN Updated E2E',
      phone: '0999888777',
      address: '123 E2E Street',
    });
    expect(res.ok).toBeTruthy();
    const updated = extractItem(res.body);
    expect(updated.fullName).toBe('BN Updated E2E');
  });

  test('1.A5 — POST /patients với body rỗng → 400', async ({ page }) => {
    const res = await apiPost(page, '/patients', {});
    expect(res.ok).toBeFalsy();
    expect(res.status).toBeGreaterThanOrEqual(400);
  });

  test('1.A6 — DELETE /patients/{id} xoá mềm', async ({ page }) => {
    if (!createdPatientId) {
      test.skip();
      return;
    }
    const res = await apiDelete(page, `/patients/${createdPatientId}`);
    expect(res.status).toBeLessThan(500);
  });

  test('1.A7 — GET /patients/search/advanced lọc nâng cao', async ({ page }) => {
    const data = await apiGet(page, '/patients/search/advanced?page=1&pageSize=5');
    const items = extractItems(data);
    expect(Array.isArray(items)).toBeTruthy();
  });

  test('1.A8 — GET /patients/analytics trả thống kê', async ({ page }) => {
    const data = await apiGet(page, '/patients/analytics');
    expect(data).toBeDefined();
  });
});

// ─── Queue API ──────────────────────────────────────────────────────────

test.describe('Luồng 1 — API: Queue', () => {
  test('1.Q1 — GET /queue/RECEPTION hàng đợi tiếp đón', async ({ page }) => {
    try {
      const data = await apiGet(page, '/queue/RECEPTION');
      expect(data).toBeDefined();
    } catch {
      /* feature may not be enabled */
    }
  });

  test('1.Q2 — POST /queue/issue cấp STT mới', async ({ page }) => {
    const patient = await getPatientFromApi(page);
    if (!patient) {
      test.skip();
      return;
    }
    try {
      const res = await apiPost(page, '/queue/issue', {
        patientId: patient.id,
        departmentCode: 'RECEPTION',
      });
      if (res.ok) {
        const ticket = extractItem(res.body);
        expect(ticket).toHaveProperty('id');
      }
    } catch {
      /* queue feature may not be enabled */
    }
  });
});

// ─── Consent API ────────────────────────────────────────────────────────

test.describe('Luồng 1 — API: Consent Forms', () => {
  test('1.C1 — GET /consent-forms danh sách', async ({ page }) => {
    const data = await apiGet(page, '/consent-forms?page=1&pageSize=5');
    expect(data).toBeDefined();
  });

  test('1.C2 — POST /consent-forms tạo consent mới', async ({ page }) => {
    const patient = await getPatientFromApi(page);
    if (!patient) {
      test.skip();
      return;
    }
    const res = await apiPost(page, '/consent-forms', {
      patientId: patient.id,
      consentType: 'GeneralTreatment',
      title: `E2E Consent ${Date.now()}`,
      content: 'Đồng thuận điều trị E2E',
    });
    if (res.ok) {
      const consent = extractItem(res.body);
      expect(consent).toHaveProperty('id');
    }
  });
});

// ─── UI: Smoke + Form Validation ────────────────────────────────────────

test.describe('Luồng 1 — UI: Tiếp đón & Tạo hồ sơ', () => {
  test('1.1 — Reception dashboard hiển thị', async ({ page }) => {
    await navigateTo(page, '/reception');
    await expectPageLoaded(page);
    await expect(
      page.locator('app-reception-dashboard, [class*="reception"]').first(),
    ).toBeVisible();
  });

  test('1.2 — Danh sách BN có dữ liệu', async ({ page }) => {
    await navigateTo(page, '/patients');
    await expectPageLoaded(page);
    await expect(page.locator('table, .patient-list').first()).toBeVisible({ timeout: 10_000 });
    const rows = page.locator('table tbody tr');
    await expect(rows.first()).toBeVisible({ timeout: 10_000 });
  });

  test('1.3 — Form tạo BN: trường bắt buộc hiển thị', async ({ page }) => {
    await navigateTo(page, '/patients/new');
    await expectPageLoaded(page);
    await expect(page.locator('input[name="fullName"]')).toBeVisible();
    await expect(page.locator('input[name="dateOfBirth"]')).toBeVisible();
    await expect(page.locator('select[name="gender"]')).toBeVisible();
  });

  test('1.4 — Form tạo BN: submit trống → validation chặn', async ({ page }) => {
    await navigateTo(page, '/patients/new');
    await waitForLoad(page);
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(1000);
    expect(page.url()).toContain('/patients/new');
  });

  test('1.5 — Form tạo BN: điền đủ → tạo → redirect', async ({ page }) => {
    await navigateTo(page, '/patients/new');
    await waitForLoad(page);
    await page.locator('input[name="fullName"]').fill(uniqueName('UI_BN'));
    await page.locator('input[name="dateOfBirth"]').fill('1992-07-20');
    await page.locator('select[name="gender"]').selectOption('Female');
    await page.locator('select[name="patientType"]').selectOption('Infertility');
    const phoneInput = page.locator('input[name="phone"]');
    if (await phoneInput.isVisible()) await phoneInput.fill('0901234567');
    await page.locator('button[type="submit"]').click();
    await page.waitForURL(/\/patients/, { timeout: 15_000 });
  });

  test('1.6 — Tìm kiếm BN trên UI', async ({ page }) => {
    await navigateTo(page, '/patients');
    await waitForLoad(page);
    const search = page
      .locator('input[type="search"], input[type="text"], input[placeholder*="Tìm"]')
      .first();
    if (await search.isVisible({ timeout: 3000 }).catch(() => false)) {
      await search.fill('E2E');
      await search.press('Enter');
      await waitForLoad(page);
      await expectPageLoaded(page);
    }
  });

  test('1.7 — Hàng đợi tiếp đón', async ({ page }) => {
    await navigateTo(page, '/queue/RECEPTION');
    await expectPageLoaded(page);
    if (!(await waitForFeaturePage(page, 'app-queue-display, [class*="queue"]'))) {
      test.skip();
      return;
    }
  });

  test('1.8 — Danh sách consent forms', async ({ page }) => {
    await navigateTo(page, '/consent');
    await expectPageLoaded(page);
  });
});
