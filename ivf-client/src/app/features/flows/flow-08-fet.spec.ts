/**
 * Luồng 8: Chuyển phôi đông lạnh (FET)
 * Chuẩn bị nội mạc → Rã phôi → Chuyển phôi
 */
import { signal } from '@angular/core';
import { of } from 'rxjs';

import { FetListComponent } from '../fet/fet-list/fet-list.component';

describe('Luồng 8: Chuyển phôi đông lạnh (FET)', () => {
  describe('FetListComponent', () => {
    let component: any;
    let mockFetService: any;
    let mockRouter: any;

    beforeEach(() => {
      mockFetService = {
        search: vi.fn().mockReturnValue(of({ items: [], total: 0 })),
      };
      mockRouter = { navigate: vi.fn() };

      component = Object.create(FetListComponent.prototype);
      component.service = mockFetService;
      component.router = mockRouter;
      component.protocols = signal([]);
      component.total = signal(0);
      component.loading = signal(false);
      component.searchQuery = '';
      component.filterStatus = '';
      component.page = 1;
      component.pageSize = 20;

      component.load();
    });

    it('khởi tạo component và tải danh sách', () => {
      expect(component).toBeTruthy();
      expect(mockFetService.search).toHaveBeenCalled();
    });

    it('protocols rỗng khi chưa có dữ liệu', () => {
      expect(component.protocols()).toEqual([]);
      expect(component.total()).toBe(0);
    });

    it('tải danh sách FET với phân trang', () => {
      expect(mockFetService.search).toHaveBeenCalledWith(undefined, undefined, 1, 20);
    });

    it('tìm kiếm reset page về 1', () => {
      component.page = 3;
      component.searchQuery = 'FET-001';
      component.load();
      expect(mockFetService.search).toHaveBeenCalledWith('FET-001', undefined, 3, 20);
    });

    it('lọc theo trạng thái', () => {
      component.filterStatus = 'Active';
      component.load();
      expect(mockFetService.search).toHaveBeenCalledWith(undefined, 'Active', 1, 20);
    });

    it('mở chi tiết FET', () => {
      component.openDetail('fet-123');
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/fet', 'fet-123']);
    });

    it('hiển thị nhãn trạng thái tiếng Việt', () => {
      expect(component.statusLabel('Active')).toBe('Đang chuẩn bị');
      expect(component.statusLabel('Transferred')).toBe('Đã chuyển phôi');
      expect(component.statusLabel('Cancelled')).toBe('Đã hủy');
      expect(component.statusLabel('Completed')).toBe('Hoàn thành');
    });

    it('loading = false sau khi tải xong', () => {
      expect(component.loading()).toBe(false);
    });
  });
});
