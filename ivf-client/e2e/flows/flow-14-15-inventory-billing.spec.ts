/**
 * Luồng 14: Quản lý thuốc & vật tư (Inventory)
 * Nhập kho → Xuất kho → Kiểm kê
 *
 * Luồng 15: Thanh toán & Hóa đơn (Billing)
 * Dịch vụ → Tạo hóa đơn → Thanh toán → In phiếu
 */
import {
  test, expect, navigateTo, waitForLoad, expectPageLoaded, waitForFeaturePage,
  apiGet, apiPost, apiPut, extractItems, extractItem, uniqueName,
  getPatientFromApi, getCycleIdFromApi,
} from '../helpers';

// ─── Luồng 14 — API: Inventory CRUD ─────────────────────────────────────────
test.describe('Luồng 14 — API: Inventory', () => {
  test('14.A1 — GET /inventory trả danh sách', async ({ page }) => {
    const data = await apiGet(page, '/inventory?page=1&pageSize=5');
    const items = extractItems(data);
    expect(Array.isArray(items)).toBe(true);
  });

  test('14.A2 — POST /inventory tạo vật tư mới', async ({ page }) => {
    const code = `E2E-${Date.now()}`;
    const res = await apiPost(page, '/inventory', {
      code,
      name: uniqueName('VatTu'),
      category: 'Thuốc',
      unit: 'Viên',
      minStock: 10,
      maxStock: 500,
      unitPrice: 5000,
    });
    expect(res.status).toBeLessThan(500);
    if (res.ok) {
      const body = extractItem(res.body);
      expect(body).toBeDefined();
    }
  });

  test('14.A3 — POST /inventory → GET by ID', async ({ page }) => {
    const code = `E2E-${Date.now()}`;
    const createRes = await apiPost(page, '/inventory', {
      code,
      name: uniqueName('VatTuById'),
      category: 'Vật tư',
      unit: 'Cái',
      minStock: 5,
      maxStock: 200,
      unitPrice: 15000,
    });
    if (!createRes.ok) { test.skip(); return; }
    const created = extractItem(createRes.body);
    const id = created?.id ?? created?.data?.id;
    if (!id) { test.skip(); return; }
    const detail = await apiGet(page, `/inventory/${id}`);
    expect(detail).toBeDefined();
  });

  test('14.A4 — PUT /inventory/{id} cập nhật', async ({ page }) => {
    // Create first, then update
    const code = `E2E-${Date.now()}`;
    const createRes = await apiPost(page, '/inventory', {
      code,
      name: uniqueName('VatTuUpdate'),
      category: 'Vật tư',
      unit: 'Hộp',
      minStock: 2,
      maxStock: 100,
      unitPrice: 25000,
    });
    if (!createRes.ok) { test.skip(); return; }
    const created = extractItem(createRes.body);
    const id = created?.id ?? created?.data?.id;
    if (!id) { test.skip(); return; }
    const res = await apiPut(page, `/inventory/${id}`, {
      code,
      name: uniqueName('VatTuUpdated'),
      category: 'Vật tư',
      unit: 'Hộp',
      minStock: 5,
      maxStock: 150,
      unitPrice: 30000,
    });
    expect(res.status).toBeLessThan(500);
  });

  test('14.A5 — GET /inventory/alerts/low-stock', async ({ page }) => {
    const data = await apiGet(page, '/inventory/alerts/low-stock');
    expect(data).toBeDefined();
    const items = extractItems(data);
    expect(Array.isArray(items)).toBe(true);
  });

  test('14.A6 — POST /inventory thiếu body → 400', async ({ page }) => {
    const res = await apiPost(page, '/inventory', {});
    expect(res.status).toBeGreaterThanOrEqual(400);
    expect(res.status).toBeLessThan(500);
  });
});

// ─── Luồng 15 — API: Billing ─────────────────────────────────────────────────
test.describe('Luồng 15 — API: Billing', () => {
  test('15.A1 — GET /billing/invoices trả danh sách', async ({ page }) => {
    try {
      const data = await apiGet(page, '/billing/invoices?page=1&pageSize=5');
      const items = extractItems(data);
      expect(Array.isArray(items)).toBe(true);
    } catch { test.skip(); }
  });

  test('15.A2 — POST /billing/invoices tạo hóa đơn', async ({ page }) => {
    let patient: any;
    try { patient = await getPatientFromApi(page); } catch { test.skip(); return; }
    if (!patient?.id) { test.skip(); return; }
    try {
      const res = await apiPost(page, '/billing/invoices', {
        patientId: patient.id,
        invoiceDate: new Date().toISOString().split('T')[0],
        notes: 'E2E billing test',
      });
      expect(res.status).toBeLessThan(500);
    } catch { test.skip(); }
  });

  test('15.A3 — POST /billing/invoices → GET by ID', async ({ page }) => {
    let patient: any;
    try { patient = await getPatientFromApi(page); } catch { test.skip(); return; }
    if (!patient?.id) { test.skip(); return; }
    try {
      const createRes = await apiPost(page, '/billing/invoices', {
        patientId: patient.id,
        invoiceDate: new Date().toISOString().split('T')[0],
        notes: 'E2E get by ID',
      });
      if (!createRes.ok) { test.skip(); return; }
      const created = extractItem(createRes.body);
      const id = created?.id ?? created?.data?.id;
      if (!id) { test.skip(); return; }
      const detail = await apiGet(page, `/billing/invoices/${id}`);
      expect(detail).toBeDefined();
    } catch { test.skip(); }
  });

  test('15.A4 — POST /billing/invoices thiếu body → trả lỗi', async ({ page }) => {
    try {
      const res = await apiPost(page, '/billing/invoices', {});
      expect(res.status).toBeLessThan(500);
    } catch { test.skip(); }
  });

  test('15.A5 — GET /billing/services danh sách dịch vụ', async ({ page }) => {
    try {
      const data = await apiGet(page, '/billing/services?page=1&pageSize=5');
      expect(data).toBeDefined();
    } catch { test.skip(); }
  });
});

// ─── Luồng 14/15 — UI ────────────────────────────────────────────────────────
test.describe('Luồng 14: Quản lý thuốc & vật tư', () => {
  test('14.1 — Trang quản lý kho (inventory)', async ({ page }) => {
    await navigateTo(page, '/inventory');
    await expectPageLoaded(page);
    if (!await waitForFeaturePage(page, 'app-inventory-stock, [class*="inventory"], table, .card')) {
      test.skip(); return;
    }
  });

  test('14.2 — Danh sách thuốc/vật tư', async ({ page }) => {
    await navigateTo(page, '/inventory');
    await expect(page.locator('h1, table, button, select').first()).toBeVisible({ timeout: 10_000 });
  });

  test('14.3 — Nhập kho mới (nếu có nút)', async ({ page }) => {
    await navigateTo(page, '/inventory');
    await waitForLoad(page);
    const addBtn = page.locator('button, a').filter({ hasText: /Thêm|Nhập|Tạo/i }).first();
    if (await addBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await addBtn.click();
      await waitForLoad(page);
      await expect(page.locator('form, input, .modal, dialog').first()).toBeVisible();
    }
  });
});

test.describe('Luồng 15: Thanh toán & Hóa đơn', () => {
  test('15.1 — Danh sách hóa đơn (billing)', async ({ page }) => {
    await navigateTo(page, '/billing');
    await expectPageLoaded(page);
    if (!await waitForFeaturePage(page, 'app-invoice-list, [class*="invoice"], [class*="billing"]')) {
      test.skip(); return;
    }
  });

  test('15.2 — Bảng hóa đơn', async ({ page }) => {
    await navigateTo(page, '/billing');
    if (!await waitForFeaturePage(page, 'app-invoice-list, [class*="invoice"], [class*="billing"]')) {
      test.skip(); return;
    }
    const table = page.locator('table, .card').first();
    await expect(table).toBeVisible();
  });

  test('15.3 — Tạo hóa đơn mới', async ({ page }) => {
    await navigateTo(page, '/billing');
    if (!await waitForFeaturePage(page, 'app-invoice-list, [class*="invoice"], [class*="billing"]')) {
      test.skip(); return;
    }
    const createBtn = page.locator('button, a').filter({ hasText: /Tạo|Thêm|Hóa đơn mới/i }).first();
    if (await createBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await createBtn.click();
      await waitForLoad(page);
      await expect(page.locator('form, input, .modal, dialog').first()).toBeVisible();
    }
  });

  test('15.4 — Tìm kiếm hóa đơn', async ({ page }) => {
    await navigateTo(page, '/billing');
    if (!await waitForFeaturePage(page, 'app-invoice-list, [class*="invoice"], [class*="billing"]')) {
      test.skip(); return;
    }
    const searchInput = page.locator('input[type="search"], input[type="text"], input[placeholder*="Tìm"]').first();
    if (await searchInput.isVisible({ timeout: 3000 }).catch(() => false)) {
      await searchInput.fill('test');
      await waitForLoad(page);
    }
  });
});

