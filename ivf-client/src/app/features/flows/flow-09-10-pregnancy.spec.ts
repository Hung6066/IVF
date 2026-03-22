/**
 * Luồng 9: Thử thai (Beta HCG)
 * XN Beta HCG → Đánh giá kết quả → Theo dõi
 *
 * Luồng 10: Theo dõi thai kỳ (Prenatal)
 * Siêu âm thai + theo dõi tiền sản
 */
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { PregnancyBetaHcgComponent } from '../pregnancy/pregnancy-beta-hcg/pregnancy-beta-hcg.component';

describe('Luồng 9 & 10: Thử thai Beta HCG + Theo dõi thai kỳ', () => {
  describe('PregnancyBetaHcgComponent', () => {
    let component: any;
    let mockService: any;

    const fakePregnancy = {
      id: 'preg-1',
      cycleId: 'cycle-1',
      status: 'Ongoing',
      estimatedDueDate: '2025-10-01',
    };

    beforeEach(() => {
      mockService = {
        getByCycle: vi.fn().mockReturnValue(of(fakePregnancy)),
        getBetaHcgResults: vi
          .fn()
          .mockReturnValue(of([{ id: 'r1', betaHcg: 250, testDate: '2025-01-15' }])),
        recordBetaHcg: vi.fn().mockReturnValue(of(fakePregnancy)),
      };

      component = Object.create(PregnancyBetaHcgComponent.prototype);
      component.service = mockService;
      component.route = { snapshot: { paramMap: { get: () => 'cycle-1' } } };
      component.cycleId = 'cycle-1';
      component.pregnancy = signal(null);
      component.results = signal([]);
      component.loading = signal(false);
      component.saving = signal(false);
      component.error = signal('');
      component.successMsg = signal('');
      component.showForm = false;
      component.form = { betaHcg: 0, testDate: '', notes: '' };

      component.load();
    });

    it('khởi tạo với cycleId từ route', () => {
      expect(component).toBeTruthy();
      expect(component.cycleId).toBe('cycle-1');
    });

    it('tải dữ liệu thai kỳ', () => {
      expect(mockService.getByCycle).toHaveBeenCalledWith('cycle-1');
      expect(component.pregnancy()).toEqual(fakePregnancy);
    });

    it('tải danh sách kết quả Beta HCG', () => {
      expect(mockService.getBetaHcgResults).toHaveBeenCalledWith('cycle-1');
      expect(component.results().length).toBe(1);
      expect(component.results()[0].betaHcg).toBe(250);
    });

    it('form mặc định với betaHcg = 0', () => {
      expect(component.form.betaHcg).toBe(0);
      expect(component.showForm).toBe(false);
    });

    it('không submit khi betaHcg âm', () => {
      component.form.betaHcg = -1;
      component.submit();
      expect(component.error()).toBe('Vui lòng nhập giá trị Beta HCG');
      expect(mockService.recordBetaHcg).not.toHaveBeenCalled();
    });

    it('submit ghi kết quả Beta HCG', () => {
      component.showForm = true;
      component.form.betaHcg = 350;
      component.form.testDate = '2025-01-20';
      component.form.notes = 'Kết quả tốt';
      component.submit();

      expect(mockService.recordBetaHcg).toHaveBeenCalledWith(
        'cycle-1',
        350,
        '2025-01-20',
        'Kết quả tốt',
      );
    });

    it('loading = false sau khi tải xong', () => {
      expect(component.loading()).toBe(false);
    });
  });
});
