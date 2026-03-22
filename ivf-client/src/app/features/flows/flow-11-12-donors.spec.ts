/**
 * Luồng 11: Hiến trứng (Egg Donor)
 * Đăng ký người hiến → Lấy trứng → Phân phối
 *
 * Luồng 12: Ngân hàng tinh trùng (Sperm Bank)
 * Đăng ký người hiến → Lưu mẫu → Ghép đôi
 */
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { EggDonorListComponent } from '../egg-donor/egg-donor-list/egg-donor-list.component';
import { SpermBankDashboardComponent } from '../sperm-bank/sperm-bank-dashboard/sperm-bank-dashboard.component';

describe('Luồng 11 & 12: Hiến trứng + Ngân hàng tinh trùng', () => {
  // ── 11.1 Egg Donor List ──────────────────────────────────────────────
  describe('EggDonorListComponent', () => {
    let component: any;
    let mockEggBankService: any;

    beforeEach(() => {
      mockEggBankService = {
        searchDonors: vi.fn().mockReturnValue(
          of({
            items: [{ id: 'd1', code: 'ED-001', status: 'Active' }],
            total: 1,
          }),
        ),
      };

      component = Object.create(EggDonorListComponent.prototype);
      component.service = mockEggBankService;
      component.donors = signal([]);
      component.total = signal(0);
      component.loading = signal(false);
      component.searchQuery = '';
      component.page = 1;

      component.load();
    });

    it('khởi tạo và tải danh sách người hiến trứng', () => {
      expect(component).toBeTruthy();
      expect(mockEggBankService.searchDonors).toHaveBeenCalled();
      expect(component.donors().length).toBe(1);
      expect(component.total()).toBe(1);
    });

    it('tìm kiếm reset page về 1', () => {
      component.page = 5;
      component.searchQuery = 'ED-001';
      component.search();
      expect(component.page).toBe(1);
    });

    it('loading = false sau khi tải xong', () => {
      expect(component.loading()).toBe(false);
    });
  });

  // ── 12.1 Sperm Bank Dashboard ────────────────────────────────────────
  describe('SpermBankDashboardComponent', () => {
    let component: any;
    let mockService: any;

    beforeEach(() => {
      mockService = {
        getDonors: vi.fn().mockReturnValue(of([{ id: 'd1', code: 'SD-001', bloodType: 'A+' }])),
        getSamples: vi.fn().mockReturnValue(
          of([
            { id: 's1', code: 'SS-001', status: 'Available' },
            { id: 's2', code: 'SS-002', status: 'Quarantine' },
          ]),
        ),
        getMatches: vi.fn().mockReturnValue(of([])),
        createDonor: vi.fn().mockReturnValue(of({})),
        createSample: vi.fn().mockReturnValue(of({})),
        createMatch: vi.fn().mockReturnValue(of({})),
      };

      component = Object.create(SpermBankDashboardComponent.prototype);
      component.service = mockService;
      component.activeTab = 'donors';
      component.donors = signal([]);
      component.samples = signal([]);
      component.matches = signal([]);
      component.totalDonors = signal(0);
      component.totalSamples = signal(0);
      component.availableSamples = signal(0);
      component.quarantineSamples = signal(0);
      component.showNewDonor = false;
      component.showNewSample = false;
      component.showNewMatch = false;

      component.refreshData();
    });

    it('khởi tạo với tab donors', () => {
      expect(component).toBeTruthy();
      expect(component.activeTab).toBe('donors');
    });

    it('tải danh sách người hiến TT', () => {
      expect(component.donors().length).toBe(1);
      expect(component.totalDonors()).toBe(1);
    });

    it('tải và phân loại mẫu tinh trùng', () => {
      expect(component.samples().length).toBe(2);
      expect(component.totalSamples()).toBe(2);
      expect(component.availableSamples()).toBe(1);
      expect(component.quarantineSamples()).toBe(1);
    });

    it('quản lý trạng thái form tạo mới', () => {
      expect(component.showNewDonor).toBe(false);
      expect(component.showNewSample).toBe(false);
      expect(component.showNewMatch).toBe(false);
    });
  });
});
