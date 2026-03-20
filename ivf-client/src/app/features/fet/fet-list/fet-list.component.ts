import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { FetService } from '../../../core/services/fet.service';
import { FetProtocolDto, FetSearchResult } from '../../../core/models/fet.models';

@Component({
  selector: 'app-fet-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './fet-list.component.html',
  styleUrls: ['./fet-list.component.scss'],
})
export class FetListComponent implements OnInit {
  private service = inject(FetService);
  private router = inject(Router);

  protocols = signal<FetProtocolDto[]>([]);
  total = signal(0);
  loading = signal(false);
  searchQuery = '';
  filterStatus = '';
  page = 1;
  pageSize = 20;

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.service
      .search(
        this.searchQuery || undefined,
        this.filterStatus || undefined,
        this.page,
        this.pageSize,
      )
      .subscribe({
        next: (res: FetSearchResult) => {
          this.protocols.set(res.items);
          this.total.set(res.total);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  openDetail(id: string) {
    this.router.navigate(['/fet', id]);
  }

  statusLabel(s: string): string {
    const map: Record<string, string> = {
      Active: 'Đang chuẩn bị',
      Transferred: 'Đã chuyển phôi',
      Cancelled: 'Đã hủy',
      Completed: 'Hoàn thành',
    };
    return map[s] || s;
  }

  statusClass(s: string): string {
    const map: Record<string, string> = {
      Active: 'bg-blue-100 text-blue-800',
      Transferred: 'bg-green-100 text-green-800',
      Cancelled: 'bg-red-100 text-red-800',
      Completed: 'bg-gray-100 text-gray-700',
    };
    return map[s] || 'bg-gray-100 text-gray-700';
  }

  formatDate(d?: string): string {
    if (!d) return '—';
    return new Date(d).toLocaleDateString('vi-VN');
  }
}
