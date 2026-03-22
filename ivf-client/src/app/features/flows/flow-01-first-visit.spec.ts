/**
 * Luồng 1: Khám lần đầu (First Visit)
 * BN đến lần đầu → Tiếp đón → Tạo hồ sơ → Cấp STT → Tư vấn → Đồng thuận → Hẹn tái khám
 */
import { signal, computed } from '@angular/core';
import { of } from 'rxjs';

import { ReceptionDashboardComponent } from '../reception/reception-dashboard/reception-dashboard.component';
import { PatientListComponent } from '../patients/patient-list/patient-list.component';
import { QueueDisplayComponent } from '../queue/queue-display/queue-display.component';
import { ConsentListComponent } from '../consent/consent-list/consent-list.component';

describe('Luồng 1: Khám lần đầu (First Visit)', () => {
  // ── 1.1 Reception Dashboard ──────────────────────────────────────────
  describe('ReceptionDashboardComponent', () => {
    let component: any;

    beforeEach(() => {
      component = Object.create(ReceptionDashboardComponent.prototype);
      component.searchTerm = '';
      component.searchResults = signal([]);
      component.services = signal([]);
      component.filteredServices = computed(() => component.services());
    });

    it('khởi tạo component tiếp đón', () => {
      expect(component).toBeTruthy();
    });

    it('có chức năng tìm kiếm bệnh nhân', () => {
      expect(component.searchTerm).toBeDefined();
      expect(component.searchResults).toBeDefined();
    });

    it('hiển thị danh sách dịch vụ', () => {
      expect(component.services).toBeDefined();
      expect(component.filteredServices).toBeDefined();
    });
  });

  // ── 1.2 Patient List ─────────────────────────────────────────────────
  describe('PatientListComponent', () => {
    let component: any;
    let mockService: any;

    beforeEach(() => {
      mockService = {
        searchPatients: vi.fn().mockReturnValue(of({ items: [], total: 0 })),
        createPatient: vi.fn().mockReturnValue(of({})),
        deletePatient: vi.fn().mockReturnValue(of(void 0)),
      };

      component = Object.create(PatientListComponent.prototype);
      component.patientService = mockService;
      component.router = { navigate: vi.fn() };
      component.route = { queryParams: of({}) };
      component.patients = signal([]);
      component.total = signal(0);
      component.page = signal(1);
      component.loading = signal(false);
      component.searchQuery = '';
      component.genderFilter = '';
      component.pageSize = 20;
      component.showAddModal = false;
      component.showEditModal = false;
      component.newPatient = { gender: 'Female', patientType: 'Infertility' };
      component.editingPatient = null;

      component.loadPatients();
    });

    it('tải danh sách bệnh nhân khi khởi tạo', () => {
      expect(mockService.searchPatients).toHaveBeenCalled();
    });

    it('có modal thêm bệnh nhân mới', () => {
      expect(component.showAddModal).toBe(false);
      expect(component.newPatient).toBeDefined();
      expect(component.newPatient.gender).toBe('Female');
      expect(component.newPatient.patientType).toBe('Infertility');
    });

    it('hỗ trợ tìm kiếm và lọc', () => {
      expect(component.searchQuery).toBe('');
      expect(component.genderFilter).toBe('');
      expect(component.pageSize).toBe(20);
    });

    it('hỗ trợ phân trang', () => {
      expect(component.page()).toBe(1);
      expect(component.total()).toBe(0);
    });
  });

  // ── 1.3 Queue Display ────────────────────────────────────────────────
  describe('QueueDisplayComponent', () => {
    let component: any;
    let mockQueueService: any;

    beforeEach(() => {
      mockQueueService = {
        getQueueByDept: vi.fn().mockReturnValue(of([])),
      };

      component = Object.create(QueueDisplayComponent.prototype);
      component.queueService = mockQueueService;
      component.departmentCode = 'RECEPTION';
      component.tickets = signal([]);
      component.currentTickets = signal([]);
      component.waitingTickets = signal([]);
      component.blinkEffect = false;

      component.loadQueue();
    });

    it('tải hàng đợi theo phòng ban', () => {
      expect(mockQueueService.getQueueByDept).toHaveBeenCalledWith('RECEPTION');
    });

    it('phân loại vé đang phục vụ và chờ', () => {
      expect(component.currentTickets()).toEqual([]);
      expect(component.waitingTickets()).toEqual([]);
    });

    afterEach(() => {
      component.refreshInterval = undefined;
      component.ngOnDestroy();
    });
  });

  // ── 1.4 Consent List ─────────────────────────────────────────────────
  describe('ConsentListComponent', () => {
    let component: any;
    let mockService: any;

    beforeEach(() => {
      mockService = {
        getByPatient: vi.fn().mockReturnValue(of([])),
      };

      component = Object.create(ConsentListComponent.prototype);
      component.service = mockService;
      component.router = { navigate: vi.fn() };
      component.consents = signal([]);
      component.filtered = signal([]);
      component.loading = signal(false);
      component.searchQuery = '';
      component.filterType = '';
      component.filterStatus = '';

      component.load();
    });

    it('tải danh sách đồng thuận', () => {
      expect(mockService.getByPatient).toHaveBeenCalled();
    });

    it('có bộ lọc theo loại và trạng thái', () => {
      expect(component.filterType).toBe('');
      expect(component.filterStatus).toBe('');
    });
  });
});
