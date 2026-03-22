/**
 * Luồng 14: Quản lý kho vật tư (Inventory)
 * Nhập kho → Xuất kho → Cảnh báo tồn kho
 *
 * Luồng 15: Thanh toán – Viện phí (Billing)
 * Tạo hóa đơn → Thanh toán → Theo dõi doanh thu
 */
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { InventoryStockComponent } from '../inventory/inventory-stock/inventory-stock.component';
import { InvoiceListComponent } from '../billing/invoice-list/invoice-list.component';

describe('Luồng 14 & 15: Kho vật tư + Thanh toán', () => {
  // ── 14.1 Inventory Stock ─────────────────────────────────────────────
  describe('InventoryStockComponent', () => {
    let component: any;
    let mockService: any;
    let mockRouter: any;

    beforeEach(() => {
      mockService = {
        search: vi.fn().mockReturnValue(
          of({
            items: [
              { id: 'i1', name: 'Progesterone', quantity: 50, isLowStock: false, isExpired: false },
              { id: 'i2', name: 'Folic acid', quantity: 3, isLowStock: true, isExpired: false },
            ],
            total: 2,
          }),
        ),
      };
      mockRouter = { navigate: vi.fn() };

      component = Object.create(InventoryStockComponent.prototype);
      component.service = mockService;
      component.router = mockRouter;
      component.items = signal([]);
      component.total = signal(0);
      component.loading = signal(false);
      component.searchQuery = '';
      component.filterCategory = '';
      component.lowStockOnly = false;
      component.page = 1;
      component.pageSize = 20;

      component.load();
    });

    it('khởi tạo và tải danh sách tồn kho', () => {
      expect(component).toBeTruthy();
      expect(component.items().length).toBe(2);
      expect(component.total()).toBe(2);
    });

    it('tìm kiếm theo tên vật tư', () => {
      component.searchQuery = 'Progesterone';
      component.load();
      expect(mockService.search).toHaveBeenCalledWith(
        'Progesterone', undefined, false, 1, 20,
      );
    });

    it('lọc theo danh mục', () => {
      component.filterCategory = 'Thuốc';
      component.load();
      expect(mockService.search).toHaveBeenCalledWith(
        undefined, 'Thuốc', false, 1, 20,
      );
    });

    it('lọc tồn kho thấp', () => {
      component.lowStockOnly = true;
      component.load();
      expect(mockService.search).toHaveBeenCalledWith(
        undefined, undefined, true, 1, 20,
      );
    });

    it('điều hướng đến trang nhập kho', () => {
      component.goImport();
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/inventory/import']);
    });

    it('stockClass trả về class phù hợp', () => {
      const normal = { isExpired: false, isLowStock: false } as any;
      const low = { isExpired: false, isLowStock: true } as any;
      const expired = { isExpired: true, isLowStock: false } as any;

      expect(component.stockClass(normal)).toContain('green');
      expect(component.stockClass(low)).toContain('orange');
      expect(component.stockClass(expired)).toContain('red');
    });
  });

  // ── 15.1 Invoice List (Billing) ──────────────────────────────────────
  describe('InvoiceListComponent', () => {
    let component: any;
    let mockBillingService: any;

    beforeEach(() => {
      mockBillingService = {
        getInvoices: vi.fn().mockReturnValue(
          of([{ id: 'inv1', code: 'HD-001', status: 'Paid', date: '15/01/2025', total: 5000000 }]),
        ),
        getPayments: vi.fn().mockReturnValue(of([])),
        getRevenueChartData: vi.fn().mockReturnValue(of([])),
        getStats: vi.fn().mockReturnValue({
          todayInvoices: 5, todayRevenue: 25000000,
          pendingPayments: 2, weekRevenue: 100000000,
          monthRevenue: 400000000, quarterRevenue: 1200000000,
        }),
      };

      component = Object.create(InvoiceListComponent.prototype);
      component.service = mockBillingService;
      component.catalogService = { getServices: vi.fn().mockReturnValue(of({ items: [] })) };
      component.queueService = { getByDepartment: vi.fn().mockReturnValue(of([])) };
      component.services = signal([]);
      component.activeTab = 'invoices';
      component.invoices = signal([]);
      component.payments = signal([]);
      component.chartData = signal([]);
      component.todayInvoices = signal(0);
      component.todayRevenue = signal(0);
      component.pendingPayments = signal(0);
      component.weekRevenue = signal(0);
      component.monthRevenue = signal(0);
      component.quarterRevenue = signal(0);
      component.filterDate = '';
      component.filterStatus = '';
      component.searchTerm = '';
      component.showCreateInvoice = false;

      component.refreshData();
    });

    it('khởi tạo với tab invoices', () => {
      expect(component).toBeTruthy();
      expect(component.activeTab).toBe('invoices');
    });

    it('tải danh sách hóa đơn', () => {
      expect(component.invoices().length).toBe(1);
    });

    it('thống kê doanh thu hôm nay', () => {
      expect(component.todayInvoices()).toBe(5);
      expect(component.todayRevenue()).toBe(25000000);
      expect(component.pendingPayments()).toBe(2);
    });

    it('lọc hóa đơn theo trạng thái', () => {
      component.filterStatus = 'Paid';
      const filtered = component.filteredInvoices();
      expect(filtered.every((i: any) => i.status === 'Paid')).toBe(true);
    });

    it('tìm kiếm hóa đơn theo mã', () => {
      component.searchTerm = 'HD-001';
      const filtered = component.filteredInvoices();
      expect(filtered.length).toBe(1);
    });

    it('quản lý trạng thái form tạo hóa đơn', () => {
      expect(component.showCreateInvoice).toBe(false);
    });
  });
});
