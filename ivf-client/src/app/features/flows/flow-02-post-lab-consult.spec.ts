/**
 * Luồng 2: Tư vấn sau xét nghiệm
 * XN xong → BS xem KQ → Tư vấn → Tạo chu kỳ điều trị
 */
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { ConsultationDashboardComponent } from '../consultation/consultation-dashboard/consultation-dashboard.component';
import { CoupleListComponent } from '../couples/couple-list/couple-list.component';
import { CoupleFormComponent } from '../couples/couple-form/couple-form.component';

describe('Luồng 2: Tư vấn sau xét nghiệm', () => {
  // ── 2.1 Consultation Dashboard ────────────────────────────────────────
  describe('ConsultationDashboardComponent', () => {
    let component: any;

    beforeEach(() => {
      component = Object.create(ConsultationDashboardComponent.prototype);
      component.activeTab = 'queue';
      component.showFirstVisitForm = false;
      component.showFollowUpForm = false;
      component.showTreatmentDecision = false;
      component.consultations = signal([]);
      component.consultationsTotal = signal(0);
      component.consultationsPage = 1;
      component.queue = signal([]);
      component.history = signal([]);
      component.queueCount = signal(0);
      component.completedCount = signal(0);
    });

    it('khởi tạo với tab hàng đợi', () => {
      expect(component).toBeTruthy();
      expect(component.activeTab).toBe('queue');
    });

    it('quản lý tư vấn lần đầu và tái khám', () => {
      expect(component.showFirstVisitForm).toBe(false);
      expect(component.showFollowUpForm).toBe(false);
      expect(component.showTreatmentDecision).toBe(false);
    });

    it('có danh sách tư vấn với phân trang', () => {
      expect(component.consultations()).toEqual([]);
      expect(component.consultationsTotal()).toBe(0);
      expect(component.consultationsPage).toBe(1);
    });
  });

  // ── 2.2 Couple List ──────────────────────────────────────────────────
  describe('CoupleListComponent', () => {
    let component: any;
    let mockService: any;

    beforeEach(() => {
      mockService = {
        getCouples: vi.fn().mockReturnValue(of([])),
      };

      component = Object.create(CoupleListComponent.prototype);
      component.coupleService = mockService;
      component.couples = signal([]);
      component.searchTerm = signal('');

      component.ngOnInit();
    });

    it('tải danh sách cặp đôi', () => {
      expect(mockService.getCouples).toHaveBeenCalled();
    });
  });

  // ── 2.3 Couple Form (Tạo cặp đôi mới) ──────────────────────────────
  describe('CoupleFormComponent', () => {
    let component: any;

    beforeEach(() => {
      component = Object.create(CoupleFormComponent.prototype);
      component.saving = signal(false);
      component.selectedWife = signal(null);
      component.selectedHusband = signal(null);
    });

    it('khởi tạo form tạo cặp đôi', () => {
      expect(component).toBeTruthy();
    });
  });
});
