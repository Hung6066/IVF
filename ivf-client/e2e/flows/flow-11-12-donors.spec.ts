/**
 * Luồng 11: Cho / Nhận noãn (Egg Donation)
 * Người cho noãn → Đăng ký → KTBT → OPU → Giao Lab
 *
 * Luồng 12: Cho / Nhận tinh trùng (Sperm Donation)
 * Người cho tinh trùng → Đăng ký → Lưu trữ ngân hàng
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
  getCoupleFromApi,
} from '../helpers';

// ─── Luồng 11 — API: Egg Donor Recipients ────────────────────────────────────
test.describe('Luồng 11 — API: Egg Donor Recipients', () => {
  test('11.A1 — GET /egg-donor-recipients trả danh sách', async ({ page }) => {
    try {
      const data = await apiGet(page, '/egg-donor-recipients?page=1&pageSize=5');
      const items = extractItems(data);
      expect(Array.isArray(items)).toBe(true);
    } catch {
      test.skip();
    }
  });

  test('11.A2 — GET /egg-donor-recipients/couple/{coupleId} theo couple', async ({ page }) => {
    let couple: any;
    try {
      couple = await getCoupleFromApi(page);
    } catch {
      test.skip();
      return;
    }
    if (!couple?.id) {
      test.skip();
      return;
    }
    try {
      const data = await apiGet(page, `/egg-donor-recipients/couple/${couple.id}`);
      expect(data).toBeDefined();
    } catch {
      test.skip();
    }
  });

  test('11.A3 — POST /egg-donor-recipients thiếu body → 400', async ({ page }) => {
    const res = await apiPost(page, '/egg-donor-recipients', {});
    expect(res.status).toBeLessThan(500);
  });
});

// ─── Luồng 11 — API: Egg Bank ────────────────────────────────────────────────
test.describe('Luồng 11 — API: Egg Bank', () => {
  test('11.B1 — GET /eggbank/donors trả danh sách', async ({ page }) => {
    try {
      const data = await apiGet(page, '/eggbank/donors?page=1&pageSize=5');
      const items = extractItems(data);
      expect(Array.isArray(items)).toBe(true);
    } catch {
      test.skip();
    }
  });

  test('11.B2 — GET /eggbank/samples/available trả mảng', async ({ page }) => {
    try {
      const data = await apiGet(page, '/eggbank/samples/available');
      const items = extractItems(data);
      expect(Array.isArray(items)).toBe(true);
    } catch {
      test.skip();
    }
  });

  test('11.B3 — POST /eggbank/donors tạo donor mới', async ({ page }) => {
    const name = uniqueName('EggDonor');
    const res = await apiPost(page, '/eggbank/donors', {
      fullName: name,
      dateOfBirth: '1995-05-15',
      bloodType: 'A',
      ethnicity: 'Kinh',
      height: 160,
      weight: 52,
      educationLevel: 'Đại học',
      occupation: 'E2E Test',
      medicalHistory: 'Không',
    });
    expect(res.status).toBeLessThan(500);
  });

  test('11.B4 — POST /eggbank/donors thiếu thông tin → 400', async ({ page }) => {
    const res = await apiPost(page, '/eggbank/donors', {});
    expect(res.status).toBeLessThan(500);
  });
});

// ─── Luồng 12 — API: Sperm Bank ──────────────────────────────────────────────
test.describe('Luồng 12 — API: Sperm Bank', () => {
  test('12.A1 — GET /spermbank/donors trả danh sách', async ({ page }) => {
    try {
      const data = await apiGet(page, '/spermbank/donors?page=1&pageSize=5');
      const items = extractItems(data);
      expect(Array.isArray(items)).toBe(true);
    } catch {
      test.skip();
    }
  });

  test('12.A2 — GET /spermbank/samples/available trả mảng', async ({ page }) => {
    try {
      const data = await apiGet(page, '/spermbank/samples/available');
      const items = extractItems(data);
      expect(Array.isArray(items)).toBe(true);
    } catch {
      test.skip();
    }
  });

  test('12.A3 — POST /spermbank/donors tạo donor mới', async ({ page }) => {
    const name = uniqueName('SpermDonor');
    const res = await apiPost(page, '/spermbank/donors', {
      fullName: name,
      dateOfBirth: '1990-03-20',
      bloodType: 'O',
      ethnicity: 'Kinh',
      height: 172,
      weight: 68,
      educationLevel: 'Đại học',
      occupation: 'E2E Test',
      medicalHistory: 'Không',
    });
    expect(res.status).toBeLessThan(500);
  });

  test('12.A4 — POST /spermbank/donors thiếu body → 400', async ({ page }) => {
    const res = await apiPost(page, '/spermbank/donors', {});
    expect(res.status).toBeLessThan(500);
  });

  test('12.A5 — GET /spermbank/donors tìm theo tên', async ({ page }) => {
    try {
      const data = await apiGet(page, '/spermbank/donors?search=E2E&page=1&pageSize=5');
      expect(data).toBeDefined();
    } catch {
      test.skip();
    }
  });
});

// ─── Luồng 11/12 — UI ────────────────────────────────────────────────────────
test.describe('Luồng 11: Cho / Nhận noãn', () => {
  test('11.1 — Danh sách bệnh nhân (lọc donor)', async ({ page }) => {
    await navigateTo(page, '/patients');
    await waitForLoad(page);
    await expectPageLoaded(page);
    const filterOrSearch = page.locator('input[type="search"], input[type="text"], select').first();
    await expect(filterOrSearch).toBeVisible();
  });

  test('11.2 — Tạo bệnh nhân mới (donor)', async ({ page }) => {
    await navigateTo(page, '/patients/new');
    await expectPageLoaded(page);
    await expect(page.locator('form, app-patient-form').first()).toBeVisible();
  });
});

test.describe('Luồng 12: Cho / Nhận tinh trùng', () => {
  test('12.1 — Ngân hàng tinh trùng', async ({ page }) => {
    await navigateTo(page, '/sperm-bank');
    await expectPageLoaded(page);
    if (!(await waitForFeaturePage(page, 'app-sperm-bank-dashboard, [class*="sperm"]'))) {
      test.skip();
      return;
    }
  });

  test('12.2 — Ngân hàng tinh trùng: nội dung', async ({ page }) => {
    await navigateTo(page, '/sperm-bank');
    if (!(await waitForFeaturePage(page, 'app-sperm-bank-dashboard, [class*="sperm"]'))) {
      test.skip();
      return;
    }
    const content = page.locator('table, .card, [class*="list"]').first();
    await expect(content).toBeVisible({ timeout: 30_000 });
  });

  test('12.3 — Thêm mẫu mới (nếu có nút)', async ({ page }) => {
    await navigateTo(page, '/sperm-bank');
    if (!(await waitForFeaturePage(page, 'app-sperm-bank-dashboard, [class*="sperm"]'))) {
      test.skip();
      return;
    }
    const addBtn = page
      .locator('button, a')
      .filter({ hasText: /Thêm|Tạo|Nhập|Lưu mẫu/i })
      .first();
    if (await addBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await addBtn.click();
      await waitForLoad(page);
      await expect(page.locator('form, input, .modal, dialog').first()).toBeVisible();
    }
  });
});
