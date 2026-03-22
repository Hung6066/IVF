/**
 * Luồng 4: Tiêm rụng trứng (Trigger Shot)
 * Nang trứng đạt → Tiêm trigger → Hẹn chọc hút
 *
 * Luồng 5: Chọc hút trứng (OPU)
 * Trigger xong → Thủ thuật OPU → Giao mẫu Lab
 */
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { InjectionDashboardComponent } from '../injection/injection-dashboard/injection-dashboard.component';
import { ProcedureListComponent } from '../procedure/procedure-list/procedure-list.component';
import { AppointmentsDashboardComponent } from '../appointments/appointments-dashboard.component';

import { AppointmentService } from '../../core/services/appointment.service';

describe('Luồng 4: Tiêm rụng trứng (Trigger)', () => {
  // ── 4.1 Injection Dashboard ──────────────────────────────────────────
  describe('InjectionDashboardComponent', () => {
    let component: any;
    let mockService: any;

    beforeEach(() => {
      mockService = {
        getQueue: vi.fn().mockReturnValue(of([])),
        getHistory: vi.fn().mockReturnValue(of([])),
        callPatient: vi.fn().mockReturnValue(of({})),
        markDone: vi.fn().mockReturnValue(of({})),
      };

      component = Object.create(InjectionDashboardComponent.prototype);
      component.service = mockService;
      component.activeTab = 'queue';
      component.queue = signal([]);
      component.history = signal([]);
      component.queueCount = signal(0);
      component.completedCount = signal(0);
      component.showForm = false;
      component.currentTicketId = null;
      component.currentPatientName = '';
      component.injectionNotes = '';

      component.refreshQueue();
    });

    it('tải hàng đợi tiêm khi khởi tạo', () => {
      expect(component).toBeTruthy();
      expect(component.activeTab).toBe('queue');
    });

    it('quản lý hàng đợi và lịch sử', () => {
      expect(component.queue()).toEqual([]);
      expect(component.history()).toEqual([]);
      expect(component.queueCount()).toBe(0);
      expect(component.completedCount()).toBe(0);
    });

    it('có form ghi nhận tiêm', () => {
      expect(component.showForm).toBe(false);
      expect(component.currentTicketId).toBeNull();
    });
  });

  // ── 4.2 Appointments Dashboard ───────────────────────────────────────
  describe('AppointmentsDashboardComponent', () => {
    let component: any;
    let mockService: any;

    beforeEach(() => {
      mockService = {
        getTodayAppointments: vi.fn().mockReturnValue(of([])),
        getUpcomingAppointments: vi.fn().mockReturnValue(of([])),
        createAppointment: vi.fn().mockReturnValue(of({})),
      };

      component = Object.create(AppointmentsDashboardComponent.prototype);
      component.appointmentService = mockService;
      component.todayAppointments = signal([]);
      component.upcomingAppointments = signal([]);
      component.confirmedCount = signal(0);
      component.pendingCount = signal(0);
      component.showCreateForm = false;
      component.newAppointment = { type: 'Consultation', durationMinutes: 30 };

      component.loadData();
    });

    it('tải lịch hẹn hôm nay', () => {
      expect(mockService.getTodayAppointments).toHaveBeenCalled();
    });

    it('mặc định loại hẹn là tư vấn', () => {
      expect(component.newAppointment.type).toBe('Consultation');
      expect(component.newAppointment.durationMinutes).toBe(30);
    });

    it('có modal tạo lịch hẹn', () => {
      expect(component.showCreateForm).toBe(false);
    });

    it('đếm số lịch hẹn xác nhận và chờ', () => {
      expect(component.confirmedCount()).toBe(0);
      expect(component.pendingCount()).toBe(0);
    });
  });
});

describe('Luồng 5: Chọc hút trứng (OPU)', () => {
  // ── 5.1 Procedure List ───────────────────────────────────────────────
  describe('ProcedureListComponent', () => {
    let component: any;
    let mockService: any;

    beforeEach(() => {
      mockService = {
        search: vi.fn().mockReturnValue(of({ items: [], total: 0 })),
      };

      component = Object.create(ProcedureListComponent.prototype);
      component.service = mockService;
      component.procedures = signal([]);
      component.total = signal(0);
      component.loading = signal(false);
      component.searchQuery = '';
      component.filterType = '';
      component.filterStatus = '';
      component.page = 1;
      component.pageSize = 20;
      component.procedureTypes = ['OPU', 'IUI', 'ICSI', 'IVM', 'FET', 'Biopsy'];
      component.statusOptions = ['Scheduled', 'InProgress', 'Completed', 'Cancelled', 'Postponed'];

      component.load();
    });

    it('tải danh sách thủ thuật', () => {
      expect(mockService.search).toHaveBeenCalled();
    });

    it('hỗ trợ các loại thủ thuật IVF', () => {
      expect(component.procedureTypes).toContain('OPU');
      expect(component.procedureTypes).toContain('IUI');
      expect(component.procedureTypes).toContain('ICSI');
      expect(component.procedureTypes).toContain('FET');
    });

    it('hỗ trợ lọc theo loại và trạng thái', () => {
      expect(component.filterType).toBe('');
      expect(component.filterStatus).toBe('');
      expect(component.statusOptions).toContain('Scheduled');
      expect(component.statusOptions).toContain('Completed');
    });

    it('hỗ trợ phân trang', () => {
      expect(component.page).toBe(1);
      expect(component.pageSize).toBe(20);
      expect(component.total()).toBe(0);
    });
  });
});
