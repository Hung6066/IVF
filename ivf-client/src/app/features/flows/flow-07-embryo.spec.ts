/**
 * Luồng 7: Nuôi phôi – Chuyển phôi – Đông phôi
 * Component chính: CycleDetailComponent (9 tab sub-components)
 */
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { CycleDetailComponent } from '../cycles/cycle-detail/cycle-detail.component';

describe('Luồng 7: Nuôi phôi – Chuyển phôi – Đông phôi', () => {
  describe('CycleDetailComponent', () => {
    let component: any;
    let mockCycleService: any;
    let mockUltrasoundService: any;
    let mockEmbryoService: any;
    let mockNotification: any;

    const fakeCycle = {
      id: 'cycle-1',
      code: 'CYC-001',
      method: 'ICSI',
      phase: 'EmbryoCulture',
      startDate: '2025-01-01',
    };

    beforeEach(() => {
      mockCycleService = {
        getCycle: vi.fn().mockReturnValue(of(fakeCycle)),
      };
      mockUltrasoundService = {
        getUltrasoundsByCycle: vi.fn().mockReturnValue(of([])),
      };
      mockEmbryoService = {
        getEmbryosByCycle: vi.fn().mockReturnValue(
          of([
            { id: 'e1', code: 'E001', grade: 'AA', day: 5, status: 'Frozen' },
          ]),
        ),
      };
      mockNotification = {
        success: vi.fn(),
        error: vi.fn(),
      };

      component = Object.create(CycleDetailComponent.prototype);
      component.route = {
        params: of({ id: 'cycle-1' }),
        snapshot: { paramMap: { get: () => 'cycle-1' } },
      };
      component.cycleService = mockCycleService;
      component.ultrasoundService = mockUltrasoundService;
      component.embryoService = mockEmbryoService;
      component.notificationService = mockNotification;

      component.cycle = signal(null);
      component.ultrasounds = signal([]);
      component.embryos = signal([]);
      component.cycleId = signal('');
      component.activeTab = signal('indication');

      component.phases = [
        { key: 'Consultation', name: 'Tư vấn' },
        { key: 'OvarianStimulation', name: 'Kích thích' },
        { key: 'TriggerShot', name: 'Trigger' },
        { key: 'EggRetrieval', name: 'Chọc hút' },
        { key: 'EmbryoCulture', name: 'Nuôi phôi' },
        { key: 'EmbryoTransfer', name: 'Chuyển phôi' },
        { key: 'LutealSupport', name: 'Hậu chuyển' },
        { key: 'PregnancyTest', name: 'Thử thai' },
        { key: 'Completed', name: 'Hoàn thành' },
      ];

      component.tabs = [
        { key: 'indication', label: 'Chỉ định', icon: '📋' },
        { key: 'stimulation', label: 'Kích thích', icon: '💉' },
        { key: 'culture', label: 'Nuôi phôi', icon: '🔬' },
        { key: 'transfer', label: 'Chuyển phôi', icon: '🎯' },
        { key: 'luteal', label: 'Hoàng thể', icon: '💊' },
        { key: 'pregnancy', label: 'Thai kỳ', icon: '🤰' },
        { key: 'birth', label: 'Sinh', icon: '👶' },
        { key: 'adverse', label: 'Biến chứng', icon: '⚠️' },
        { key: 'forms', label: 'Biểu mẫu', icon: '📋' },
      ];

      component.ngOnInit();
    });

    it('khởi tạo với tab chỉ định (indication)', () => {
      expect(component).toBeTruthy();
      expect(component.activeTab()).toBe('indication');
    });

    it('tải dữ liệu chu kỳ từ route params', () => {
      expect(mockCycleService.getCycle).toHaveBeenCalledWith('cycle-1');
      expect(component.cycle()).toEqual(fakeCycle);
      expect(component.cycleId()).toBe('cycle-1');
    });

    it('tải danh sách phôi theo chu kỳ', () => {
      expect(mockEmbryoService.getEmbryosByCycle).toHaveBeenCalledWith('cycle-1');
      expect(component.embryos().length).toBe(1);
    });

    it('chuyển tab khi selectTab', () => {
      component.selectTab('culture');
      expect(component.activeTab()).toBe('culture');

      component.selectTab('transfer');
      expect(component.activeTab()).toBe('transfer');
    });

    it('hiển thị thông báo khi onTabSaved', () => {
      component.onTabSaved();
      expect(mockNotification.success).toHaveBeenCalledWith('Thành công', 'Đã lưu thành công!');
      // should reload data
      expect(mockCycleService.getCycle).toHaveBeenCalledTimes(2);
    });

    it('getMethodName trả về tên phương pháp tiếng Việt', () => {
      expect(component.getMethodName('ICSI')).toBe('ICSI');
      expect(component.getMethodName('IUI')).toBe('IUI');
      expect(component.getMethodName('QHTN')).toBe('Quan hệ');
    });

    it('getPhaseName trả về tên giai đoạn tiếng Việt', () => {
      expect(component.getPhaseName('EmbryoCulture')).toBe('Nuôi phôi');
      expect(component.getPhaseName('EmbryoTransfer')).toBe('Chuyển phôi');
      expect(component.getPhaseName('Completed')).toBe('Hoàn thành');
    });

    it('có đầy đủ 9 tab', () => {
      expect(component.tabs.length).toBe(9);
      const tabKeys = component.tabs.map((t: any) => t.key);
      expect(tabKeys).toContain('indication');
      expect(tabKeys).toContain('culture');
      expect(tabKeys).toContain('transfer');
      expect(tabKeys).toContain('pregnancy');
      expect(tabKeys).toContain('forms');
    });

    it('có đầy đủ 9 phases', () => {
      expect(component.phases.length).toBe(9);
      const phaseKeys = component.phases.map((p: any) => p.key);
      expect(phaseKeys).toContain('EmbryoCulture');
      expect(phaseKeys).toContain('EmbryoTransfer');
      expect(phaseKeys).toContain('PregnancyTest');
    });
  });
});
