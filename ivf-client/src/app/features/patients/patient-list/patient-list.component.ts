import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Patient, PatientListResponse } from '../../../core/models/api.models';

@Component({
    selector: 'app-patient-list',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <div class="patient-list">
      <header class="page-header">
        <div>
          <h1>Danh sách bệnh nhân</h1>
          <p>Quản lý thông tin bệnh nhân</p>
        </div>
        <button class="btn-primary">➕ Thêm bệnh nhân</button>
      </header>

      <div class="search-bar">
        <input
          type="text"
          [(ngModel)]="searchQuery"
          (input)="onSearch()"
          placeholder="🔍 Tìm kiếm theo tên, mã bệnh nhân, SĐT..."
        />
      </div>

      <div class="table-container">
        <table class="data-table">
          <thead>
            <tr>
              <th>Mã BN</th>
              <th>Họ tên</th>
              <th>Ngày sinh</th>
              <th>Giới tính</th>
              <th>SĐT</th>
              <th>Loại BN</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            @for (patient of patients(); track patient.id) {
              <tr>
                <td><span class="code">{{ patient.patientCode }}</span></td>
                <td>{{ patient.fullName }}</td>
                <td>{{ formatDate(patient.dateOfBirth) }}</td>
                <td>
                  <span class="badge" [class.male]="patient.gender === 'Male'" [class.female]="patient.gender === 'Female'">
                    {{ patient.gender === 'Male' ? 'Nam' : 'Nữ' }}
                  </span>
                </td>
                <td>{{ patient.phone || '-' }}</td>
                <td>{{ getPatientType(patient.patientType) }}</td>
                <td>
                  <button class="btn-icon" title="Xem">👁️</button>
                  <button class="btn-icon" title="Sửa">✏️</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="7" class="empty-state">
                  {{ loading() ? 'Đang tải...' : 'Không có dữ liệu' }}
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <div class="pagination">
        <span>Tổng: {{ total() }} bệnh nhân</span>
        <div class="page-controls">
          <button [disabled]="page() <= 1" (click)="changePage(page() - 1)">← Trước</button>
          <span>Trang {{ page() }}</span>
          <button [disabled]="patients().length < pageSize" (click)="changePage(page() + 1)">Sau →</button>
        </div>
      </div>
    </div>
  `,
    styles: [`
    .patient-list { max-width: 1400px; }

    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.5rem;
    }

    .page-header h1 { font-size: 1.5rem; color: #1e1e2f; margin: 0 0 0.25rem; }
    .page-header p { color: #6b7280; margin: 0; }

    .btn-primary {
      padding: 0.75rem 1.25rem;
      background: linear-gradient(135deg, #667eea, #764ba2);
      color: white;
      border: none;
      border-radius: 8px;
      font-weight: 500;
      cursor: pointer;
      transition: transform 0.2s;
    }

    .btn-primary:hover { transform: translateY(-1px); }

    .search-bar {
      margin-bottom: 1.5rem;
    }

    .search-bar input {
      width: 100%;
      max-width: 400px;
      padding: 0.75rem 1rem;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      font-size: 0.9375rem;
    }

    .search-bar input:focus {
      outline: none;
      border-color: #667eea;
      box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.15);
    }

    .table-container {
      background: white;
      border-radius: 12px;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
      overflow: hidden;
    }

    .data-table {
      width: 100%;
      border-collapse: collapse;
    }

    .data-table th,
    .data-table td {
      padding: 1rem;
      text-align: left;
      border-bottom: 1px solid #f1f5f9;
    }

    .data-table th {
      background: #f8fafc;
      font-weight: 600;
      font-size: 0.8125rem;
      color: #64748b;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .data-table tbody tr:hover { background: #f8fafc; }

    .code {
      font-family: monospace;
      background: #f1f5f9;
      padding: 0.25rem 0.5rem;
      border-radius: 4px;
      font-size: 0.8125rem;
    }

    .badge {
      display: inline-block;
      padding: 0.25rem 0.75rem;
      border-radius: 999px;
      font-size: 0.75rem;
      font-weight: 500;
    }

    .badge.male { background: #dbeafe; color: #1d4ed8; }
    .badge.female { background: #fce7f3; color: #be185d; }

    .btn-icon {
      background: none;
      border: none;
      cursor: pointer;
      padding: 0.25rem;
      font-size: 1rem;
      opacity: 0.7;
      transition: opacity 0.2s;
    }

    .btn-icon:hover { opacity: 1; }

    .empty-state {
      text-align: center;
      color: #6b7280;
      padding: 3rem 1rem !important;
    }

    .pagination {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-top: 1rem;
      color: #6b7280;
      font-size: 0.875rem;
    }

    .page-controls {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }

    .page-controls button {
      padding: 0.5rem 1rem;
      background: white;
      border: 1px solid #e2e8f0;
      border-radius: 6px;
      cursor: pointer;
    }

    .page-controls button:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
  `]
})
export class PatientListComponent implements OnInit {
    patients = signal<Patient[]>([]);
    total = signal(0);
    page = signal(1);
    loading = signal(false);
    searchQuery = '';
    pageSize = 20;

    private searchTimeout?: ReturnType<typeof setTimeout>;

    constructor(private api: ApiService) { }

    ngOnInit(): void {
        this.loadPatients();
    }

    loadPatients(): void {
        this.loading.set(true);
        this.api.searchPatients(this.searchQuery || undefined, this.page(), this.pageSize).subscribe({
            next: (res) => {
                this.patients.set(res.items);
                this.total.set(res.total);
                this.loading.set(false);
            },
            error: () => this.loading.set(false)
        });
    }

    onSearch(): void {
        clearTimeout(this.searchTimeout);
        this.searchTimeout = setTimeout(() => {
            this.page.set(1);
            this.loadPatients();
        }, 300);
    }

    changePage(newPage: number): void {
        this.page.set(newPage);
        this.loadPatients();
    }

    formatDate(date: string): string {
        return new Date(date).toLocaleDateString('vi-VN');
    }

    getPatientType(type: string): string {
        const types: Record<string, string> = {
            'Infertility': 'Hiếm muộn',
            'EggDonor': 'Cho trứng',
            'SpermDonor': 'Cho tinh'
        };
        return types[type] || type;
    }
}
