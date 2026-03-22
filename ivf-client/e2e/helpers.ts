import { test as base, expect, Page, APIRequestContext } from '@playwright/test';
import path from 'path';

/**
 * Custom test fixture: tự động dùng session đã login sẵn.
 */
export const test = base.extend({
  storageState: path.join(__dirname, '.auth', 'admin.json'),
});

export { expect };

// ─── Constants ──────────────────────────────────────────────────────────

/** Backend API base URL (Angular dev server has no proxy) */
export const API = 'http://localhost:5000/api';

// ─── Helpers ────────────────────────────────────────────────────────────

/** Chờ API response xong (spinner biến mất) */
export async function waitForLoad(page: Page) {
  await page.waitForLoadState('networkidle', { timeout: 10_000 }).catch(() => {});
}

/** Navigate tới một route và chờ page load xong */
export async function navigateTo(page: Page, path: string) {
  await page.goto(path);
  await waitForLoad(page);
}

/** Kiểm tra trang không bị lỗi 404 / trắng */
export async function expectPageLoaded(page: Page) {
  const body = page.locator('body');
  await expect(body).not.toContainText('Cannot match any routes', { timeout: 5_000 });
}

/** Click sidebar menu item theo text */
export async function clickSidebarMenu(page: Page, text: string) {
  await page
    .locator('.sidebar a, .sidebar button, nav a')
    .filter({ hasText: text })
    .first()
    .click();
  await waitForLoad(page);
}

/** Đợi bảng dữ liệu hiển thị (table hoặc danh sách) */
export async function expectTableVisible(page: Page) {
  const table = page.locator('table, .list-container, .grid-container, [class*="table"]').first();
  await expect(table).toBeVisible({ timeout: 10_000 });
}

/** Fill và submit form tìm kiếm */
export async function searchFor(page: Page, query: string) {
  const searchInput = page
    .locator(
      'input[type="search"], input[placeholder*="Tìm"], input[placeholder*="tìm"], input[placeholder*="Search"]',
    )
    .first();
  await searchInput.fill(query);
  await searchInput.press('Enter');
  await waitForLoad(page);
}

/** Lấy JWT token từ storageState file */
export function getTokenFromStorageState(): string | null {
  try {
    const fs = require('fs');
    const state = JSON.parse(fs.readFileSync(path.join(__dirname, '.auth', 'admin.json'), 'utf-8'));
    const origins = state.origins ?? [];
    for (const origin of origins) {
      for (const entry of origin.localStorage ?? []) {
        if (entry.name === 'ivf_access_token') return entry.value;
      }
    }
  } catch {}
  return null;
}

/** Auth headers cho API calls */
export function authHeaders(): Record<string, string> {
  const token = getTokenFromStorageState();
  return {
    Authorization: `Bearer ${token}`,
    'Content-Type': 'application/json',
  };
}

// ─── API Helpers ────────────────────────────────────────────────────────

/** Gọi GET API và trả JSON, throw nếu lỗi */
export async function apiGet(page: Page, path: string): Promise<any> {
  const res = await page.request.get(`${API}${path}`, { headers: authHeaders() });
  expect(res.ok(), `GET ${path} failed: ${res.status()}`).toBeTruthy();
  return res.json();
}

/** Gọi POST API và trả JSON, throw nếu lỗi */
export async function apiPost(page: Page, path: string, data: any): Promise<any> {
  const res = await page.request.post(`${API}${path}`, { headers: authHeaders(), data });
  return { status: res.status(), body: await res.json().catch(() => null), ok: res.ok() };
}

/** Gọi PUT API và trả JSON */
export async function apiPut(page: Page, path: string, data: any): Promise<any> {
  const res = await page.request.put(`${API}${path}`, { headers: authHeaders(), data });
  return { status: res.status(), body: await res.json().catch(() => null), ok: res.ok() };
}

/** Gọi DELETE API */
export async function apiDelete(page: Page, path: string): Promise<any> {
  const res = await page.request.delete(`${API}${path}`, { headers: authHeaders() });
  return { status: res.status(), ok: res.ok() };
}

/** Extract items array từ API response (handles Result<T>, PagedResult, array) */
export function extractItems(data: any): any[] {
  if (Array.isArray(data)) return data;
  if (data?.items) return data.items;
  if (data?.data && Array.isArray(data.data)) return data.data;
  if (data?.value?.items) return data.value.items;
  if (data?.value && Array.isArray(data.value)) return data.value;
  return [];
}

/** Extract single item from Result<T> response */
export function extractItem(data: any): any {
  return data?.data ?? data?.value ?? data;
}

/** Generate unique test name với timestamp */
export function uniqueName(prefix: string): string {
  return `${prefix}_E2E_${Date.now()}`;
}

/** Lấy 1 cycleId thực từ API, trả null nếu không có */
export async function getCycleIdFromApi(page: Page): Promise<string | null> {
  const token = getTokenFromStorageState();
  if (!token) return null;
  try {
    const res = await page.request.get(`${API}/cycles/active`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!res.ok()) return null;
    const data = await res.json();
    const items = Array.isArray(data) ? data : (data.items ?? data.data ?? []);
    return items.length > 0 ? items[0].id : null;
  } catch {
    return null;
  }
}

/** Lấy full cycle object từ API */
export async function getCycleFromApi(page: Page): Promise<any | null> {
  const token = getTokenFromStorageState();
  if (!token) return null;
  try {
    const res = await page.request.get(`${API}/cycles/active`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!res.ok()) return null;
    const data = await res.json();
    const items = Array.isArray(data) ? data : (data.items ?? data.data ?? []);
    return items.length > 0 ? items[0] : null;
  } catch {
    return null;
  }
}

/** Lấy 1 patient thực từ API */
export async function getPatientFromApi(page: Page): Promise<any | null> {
  try {
    const data = await apiGet(page, '/patients?page=1&pageSize=1');
    const items = extractItems(data);
    return items.length > 0 ? items[0] : null;
  } catch {
    return null;
  }
}

/** Lấy 1 couple thực từ API */
export async function getCoupleFromApi(page: Page): Promise<any | null> {
  try {
    const data = await apiGet(page, '/couples');
    const items = extractItems(data);
    return items.length > 0 ? items[0] : null;
  } catch {
    return null;
  }
}

/** Chờ trang feature-guarded render xong, trả false nếu bị redirect/trang trắng */
export async function waitForFeaturePage(page: Page, componentSelector: string): Promise<boolean> {
  try {
    await page.locator(componentSelector).first().waitFor({ state: 'visible', timeout: 50_000 });
    return true;
  } catch {
    return false;
  }
}

/** Đảm bảo có ít nhất 1 cycle trong DB (tạo patient + couple + cycle nếu cần). Trả cycleId. */
export async function ensureTestCycleExists(page: Page): Promise<string | null> {
  const token = getTokenFromStorageState();
  if (!token) return null;
  const headers = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };

  // 1. Check existing cycle
  try {
    const cycleRes = await page.request.get(`${API}/cycles/active`, { headers });
    if (cycleRes.ok()) {
      const data = await cycleRes.json();
      const items = Array.isArray(data) ? data : (data.items ?? data.data ?? []);
      if (items.length > 0) return items[0].id as string;
    }
  } catch {}

  // 2. Create wife patient
  const wifeRes = await page.request.post(`${API}/patients`, {
    headers,
    data: {
      fullName: 'E2E Test Wife',
      dateOfBirth: '1990-01-01',
      gender: 1,
      patientType: 0,
      phone: '0900000001',
    },
  });
  if (!wifeRes.ok()) return null;
  const wife = await wifeRes.json();
  const wifeId = wife.data?.id ?? wife.id;

  // 3. Create husband patient
  const husbandRes = await page.request.post(`${API}/patients`, {
    headers,
    data: {
      fullName: 'E2E Test Husband',
      dateOfBirth: '1988-06-15',
      gender: 0,
      patientType: 0,
      phone: '0900000002',
    },
  });
  if (!husbandRes.ok()) return null;
  const husband = await husbandRes.json();
  const husbandId = husband.data?.id ?? husband.id;

  // 4. Create couple
  const coupleRes = await page.request.post(`${API}/couples`, {
    headers,
    data: { wifeId, husbandId, marriageDate: '2015-06-01', infertilityYears: 3 },
  });
  if (!coupleRes.ok()) return null;
  const couple = await coupleRes.json();
  const coupleId = couple.data?.id ?? couple.id;

  // 5. Create cycle
  const cycleRes2 = await page.request.post(`${API}/cycles`, {
    headers,
    data: {
      coupleId,
      method: 2,
      startDate: new Date().toISOString().split('T')[0],
      notes: 'E2E test cycle',
    },
  });
  if (!cycleRes2.ok()) return null;
  const cycle = await cycleRes2.json();
  return (cycle.data?.id ?? cycle.id) as string;
}
