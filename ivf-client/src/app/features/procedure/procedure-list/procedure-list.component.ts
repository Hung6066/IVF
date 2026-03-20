import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ProcedureService } from '../../../core/services/procedure.service';
import { ProcedureDto } from '../../../core/models/procedure.models';

@Component({
  selector: 'app-procedure-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './procedure-list.component.html',
  styleUrls: ['./procedure-list.component.scss'],
})
export class ProcedureListComponent implements OnInit {
  private service = inject(ProcedureService);

  procedures = signal<ProcedureDto[]>([]);
  total = signal(0);
  loading = signal(false);
  searchQuery = '';
  filterType = '';
  filterStatus = '';
  page = 1;
  pageSize = 20;

  procedureTypes = ['OPU', 'IUI', 'ICSI', 'IVM', 'FET', 'Biopsy'];
  statusOptions = ['Scheduled', 'InProgress', 'Completed', 'Cancelled', 'Postponed'];

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.service
      .search(
        this.searchQuery || undefined,
        this.filterType || undefined,
        this.filterStatus || undefined,
        this.page,
        this.pageSize,
      )
      .subscribe({
        next: (res) => {
          this.procedures.set(res.items);
          this.total.set(res.total);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  search() {
    this.page = 1;
    this.load();
  }

  statusClass(s: string): string {
    const m: Record<string, string> = {
      Scheduled: 'bg-blue-100 text-blue-700',
      InProgress: 'bg-yellow-100 text-yellow-700',
      Completed: 'bg-green-100 text-green-700',
      Cancelled: 'bg-red-100 text-red-600',
      Postponed: 'bg-gray-100 text-gray-600',
    };
    return m[s] || 'bg-gray-100 text-gray-600';
  }

  formatDate(d?: string) {
    return d ? new Date(d).toLocaleDateString('vi-VN') : '—';
  }
  formatTime(d?: string) {
    return d
      ? new Date(d).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
      : '—';
  }
}
