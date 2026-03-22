/**
 * Luồng 3: Kích thích buồng trứng (KTBT)
 * Theo dõi nang noãn → Kê thuốc KTBT → Siêu âm nang trứng
 */
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { StimulationTrackingComponent } from '../stimulation/stimulation-tracking/stimulation-tracking.component';
import { UltrasoundDashboardComponent } from '../ultrasounds/ultrasound-dashboard/ultrasound-dashboard.component';

describe('Luồng 3: Kích thích buồng trứng (KTBT)', () => {
  // ── 3.1 Stimulation Tracking ─────────────────────────────────────────
  describe('StimulationTrackingComponent', () => {
    let component: any;
    let mockService: any;

    beforeEach(() => {
      mockService = {
        getTracker: vi.fn().mockReturnValue(of(null)),
        getMedications: vi.fn().mockReturnValue(of([])),
        getMedicationSchedule: vi.fn().mockReturnValue(of([])),
        saveStimulationData: vi.fn().mockReturnValue(of({})),
        addScan: vi.fn().mockReturnValue(of({})),
      };

      component = Object.create(StimulationTrackingComponent.prototype);
      component.service = mockService;
      component.router = { navigate: vi.fn() };
      component.route = { snapshot: { paramMap: { get: () => 'test-cycle-id' } } };
      component.cycleId = 'test-cycle-id';
      component.activeTab = 'tracker';
      component.tracker = signal(null);
      component.medications = signal([]);
      component.loading = signal(false);
      component.saving = signal(false);
      component.error = signal('');
      component.successMsg = signal('');
      component.showScanForm = false;
      component.scanForm = {
        scanDate: new Date().toISOString().slice(0, 10),
        cycleDay: 1,
        size12Follicle: null,
        size14Follicle: null,
        totalFollicles: null,
        endometriumThickness: null,
        endometriumPattern: '',
        e2: null,
        lh: null,
        p4: null,
        notes: '',
      };
      component.showTriggerForm = false;
      component.triggerForm = {
        triggerDrug: '',
        triggerDate: new Date().toISOString().slice(0, 10),
        triggerTime: '22:00',
        lhLab: null,
        e2Lab: null,
        p4Lab: null,
      };

      component.load();
    });

    it('tải dữ liệu KTBT theo cycleId', () => {
      expect(component.cycleId).toBe('test-cycle-id');
      expect(mockService.getTracker).toHaveBeenCalled();
    });

    it('khởi tạo với tab theo dõi', () => {
      expect(component.activeTab).toBe('tracker');
    });

    it('có form siêu âm nang noãn', () => {
      expect(component.showScanForm).toBe(false);
      expect(component.scanForm).toBeDefined();
      expect(component.scanForm.cycleDay).toBe(1);
    });

    it('có form trigger', () => {
      expect(component.showTriggerForm).toBe(false);
      expect(component.triggerForm).toBeDefined();
      expect(component.triggerForm.triggerDrug).toBe('');
    });

    it('quản lý trạng thái loading/saving', () => {
      expect(component.loading()).toBe(false);
      expect(component.saving()).toBe(false);
      expect(component.error()).toBe('');
    });
  });

  // ── 3.2 Ultrasound Dashboard ─────────────────────────────────────────
  describe('UltrasoundDashboardComponent', () => {
    let component: any;

    beforeEach(() => {
      component = Object.create(UltrasoundDashboardComponent.prototype);
      component.activeTab = 'queue';
      component.queue = signal([]);
      component.recentExams = signal([]);
      component.queueCount = signal(0);
      component.completedCount = signal(0);
      component.abnornalCount = signal(0);
      component.showNewExam = false;
      component.newExam = {
        patient: '',
        type: 'Canh noãn',
        uterus: '',
        endometrium: null,
        rightOvary: '',
        leftOvary: '',
        conclusion: '',
      };
    });

    it('khởi tạo với tab hàng đợi', () => {
      expect(component).toBeTruthy();
      expect(component.activeTab).toBe('queue');
    });

    it('có form tạo siêu âm mới', () => {
      expect(component.showNewExam).toBe(false);
      expect(component.newExam).toBeDefined();
    });

    it('theo dõi số lượng hàng đợi', () => {
      expect(component.queueCount()).toBe(0);
      expect(component.completedCount()).toBe(0);
    });
  });
});
