/**
 * Luồng 16: Xét nghiệm (Lab)
 * Hàng đợi → Nuôi phôi → Lịch OPU → Trữ đông
 *
 * Luồng 17: Nhà thuốc (Pharmacy)
 * Tiếp nhận toa → Phát thuốc → Quản lý kho thuốc
 */
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { LabDashboardComponent } from '../lab/lab-dashboard/lab-dashboard.component';
import { PharmacyDashboardComponent } from '../pharmacy/pharmacy-dashboard/pharmacy-dashboard.component';

describe('Luồng 16 & 17: Xét nghiệm + Nhà thuốc', () => {
  // ── 16.1 Lab Dashboard ───────────────────────────────────────────────
  describe('LabDashboardComponent', () => {
    let component: any;
    let mockLabService: any;

    beforeEach(() => {
      mockLabService = {
        getQueue: vi.fn().mockReturnValue(of([])),
        getEmbryos: vi.fn().mockReturnValue(of([])),
        getSchedule: vi.fn().mockReturnValue(of([])),
        getCryoLocations: vi.fn().mockReturnValue(of([])),
        getStats: vi.fn().mockReturnValue(
          of({
            eggRetrievalCount: 3,
            cultureCount: 5,
            transferCount: 2,
            freezeCount: 4,
            totalFrozenEmbryos: 120,
            totalFrozenEggs: 80,
            totalFrozenSperm: 60,
          }),
        ),
        getActiveCycles: vi.fn().mockReturnValue(of([])),
        getDoctors: vi.fn().mockReturnValue(of([])),
        getEmbryoReport: vi.fn().mockReturnValue(of([])),
      };

      component = Object.create(LabDashboardComponent.prototype);
      component.labService = mockLabService;
      component.activeTab = 'queue';
      component.currentDate = new Date();
      component.queue = signal([]);
      component.embryos = signal([]);
      component.schedule = signal([]);
      component.cryoLocations = signal([]);
      component.activeCycles = signal([]);
      component.doctors = signal([]);
      component.stats = signal({
        eggRetrievalCount: 0,
        cultureCount: 0,
        transferCount: 0,
        freezeCount: 0,
        totalFrozenEmbryos: 0,
        totalFrozenEggs: 0,
        totalFrozenSperm: 0,
      });
      component.embryoReport = signal([]);

      component.refreshData();
    });

    it('khởi tạo với tab hàng đợi', () => {
      expect(component).toBeTruthy();
      expect(component.activeTab).toBe('queue');
    });

    it('tải thống kê phòng lab', () => {
      const stats = component.stats();
      expect(stats.eggRetrievalCount).toBe(3);
      expect(stats.cultureCount).toBe(5);
      expect(stats.transferCount).toBe(2);
      expect(stats.freezeCount).toBe(4);
      expect(stats.totalFrozenEmbryos).toBe(120);
    });

    it('chuyển tab', () => {
      component.setActiveTab('embryos');
      expect(component.activeTab).toBe('embryos');

      component.setActiveTab('schedule');
      expect(component.activeTab).toBe('schedule');

      component.setActiveTab('cryo');
      expect(component.activeTab).toBe('cryo');
    });

    it('thay đổi ngày lịch OPU', () => {
      const today = new Date(component.currentDate);
      component.changeDay(1);
      const tomorrow = new Date(today);
      tomorrow.setDate(tomorrow.getDate() + 1);
      expect(component.currentDate.getDate()).toBe(tomorrow.getDate());
    });

    it('quay về hôm nay', () => {
      component.changeDay(5); // di chuyển trước
      component.goToday();
      const now = new Date();
      expect(component.currentDate.getDate()).toBe(now.getDate());
    });
  });

  // ── 17.1 Pharmacy Dashboard ──────────────────────────────────────────
  describe('PharmacyDashboardComponent', () => {
    let component: any;
    let mockPrescriptionService: any;
    let mockAuthService: any;

    beforeEach(() => {
      const mockPharmacyService = {
        getDrugs: vi.fn().mockReturnValue(
          of([
            { id: 'd1', name: 'Progesterone', stock: 50, minStock: 10 },
            { id: 'd2', name: 'Gonal-F', stock: 3, minStock: 10 },
          ]),
        ),
        getImports: vi.fn().mockReturnValue(of([])),
      };
      mockPrescriptionService = {
        search: vi.fn().mockReturnValue(
          of({
            items: [
              { id: 'rx1', code: 'RX-001', status: 'Pending' },
              { id: 'rx2', code: 'RX-002', status: 'Dispensed' },
            ],
          }),
        ),
        enter: vi.fn().mockReturnValue(of({})),
        dispense: vi.fn().mockReturnValue(of({})),
        cancel: vi.fn().mockReturnValue(of({})),
      };
      mockAuthService = {
        user: vi.fn().mockReturnValue({ id: 'user-1', name: 'Dược sĩ A' }),
      };

      component = Object.create(PharmacyDashboardComponent.prototype);
      component.service = mockPharmacyService;
      component.prescriptionService = mockPrescriptionService;
      component.authService = mockAuthService;
      component.router = { navigate: vi.fn() };
      component.activeTab = 'prescriptions';
      component.prescriptions = signal([]);
      component.drugs = signal([]);
      component.imports = signal([]);
      component.pendingRx = signal(0);
      component.completedRx = signal(0);
      component.lowStockCount = signal(0);
      component.totalItems = signal(0);
      component.drugSearch = '';
      component.prescriptionSearch = '';
      component.statusFilter = '';
      component.showNewImport = false;

      component.refreshData();
    });

    it('khởi tạo với tab prescriptions', () => {
      expect(component).toBeTruthy();
      expect(component.activeTab).toBe('prescriptions');
    });

    it('tải danh sách thuốc và thống kê tồn kho', () => {
      expect(component.drugs().length).toBe(2);
      expect(component.totalItems()).toBe(2);
      expect(component.lowStockCount()).toBe(1); // Gonal-F stock < minStock
    });

    it('tải toa thuốc và phân loại trạng thái', () => {
      expect(component.prescriptions().length).toBe(2);
      expect(component.pendingRx()).toBe(1);
      expect(component.completedRx()).toBe(1);
    });

    it('lọc thuốc theo tên', () => {
      component.drugSearch = 'Progesterone';
      const filtered = component.filteredDrugs();
      expect(filtered.length).toBe(1);
      expect(filtered[0].name).toBe('Progesterone');
    });

    it('hiển thị nhãn trạng thái tiếng Việt', () => {
      expect(component.getStatusLabel('Pending')).toBe('Chờ xử lý');
      expect(component.getStatusLabel('Dispensed')).toBe('Đã phát');
      expect(component.getStatusLabel('Cancelled')).toBe('Đã hủy');
    });

    it('quản lý trạng thái form nhập kho', () => {
      expect(component.showNewImport).toBe(false);
    });
  });
});
