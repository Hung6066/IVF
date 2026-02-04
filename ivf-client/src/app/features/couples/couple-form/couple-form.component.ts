import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { Patient } from '../../../core/models/api.models';

@Component({
    selector: 'app-couple-form',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule],
    template: `
    <div class="couple-form">
      <header class="page-header">
        <a routerLink="/couples" class="back-link">← Danh sách cặp đôi</a>
        <h1>Tạo cặp đôi mới</h1>
      </header>

      <form (ngSubmit)="submit()" class="form-card">
        <div class="partner-section">
          <h2>👩 Thông tin vợ</h2>
          <div class="search-box">
            <input 
              type="text" 
              [(ngModel)]="wifeSearch" 
              name="wifeSearch"
              placeholder="Tìm bệnh nhân nữ (mã BN hoặc tên)..."
              (input)="searchWife()"
            />
            @if (wifeResults().length > 0) {
              <div class="search-results">
                @for (p of wifeResults(); track p.id) {
                  <div class="result-item" (click)="selectWife(p)">
                    <span class="code">{{ p.patientCode }}</span>
                    <span class="name">{{ p.fullName }}</span>
                  </div>
                }
              </div>
            }
          </div>
          @if (selectedWife()) {
            <div class="selected-patient">
              <span class="avatar">👩</span>
              <div class="info">
                <strong>{{ selectedWife()?.fullName }}</strong>
                <span>{{ selectedWife()?.patientCode }}</span>
              </div>
              <button type="button" class="btn-clear" (click)="clearWife()">✕</button>
            </div>
          }
        </div>

        <div class="partner-section">
          <h2>👨 Thông tin chồng</h2>
          <div class="search-box">
            <input 
              type="text" 
              [(ngModel)]="husbandSearch" 
              name="husbandSearch"
              placeholder="Tìm bệnh nhân nam (mã BN hoặc tên)..."
              (input)="searchHusband()"
            />
            @if (husbandResults().length > 0) {
              <div class="search-results">
                @for (p of husbandResults(); track p.id) {
                  <div class="result-item" (click)="selectHusband(p)">
                    <span class="code">{{ p.patientCode }}</span>
                    <span class="name">{{ p.fullName }}</span>
                  </div>
                }
              </div>
            }
          </div>
          @if (selectedHusband()) {
            <div class="selected-patient">
              <span class="avatar">👨</span>
              <div class="info">
                <strong>{{ selectedHusband()?.fullName }}</strong>
                <span>{{ selectedHusband()?.patientCode }}</span>
              </div>
              <button type="button" class="btn-clear" (click)="clearHusband()">✕</button>
            </div>
          }
        </div>

        <div class="details-section">
          <h2>💍 Thông tin hôn nhân</h2>
          <div class="form-row">
            <div class="form-group">
              <label>Ngày kết hôn</label>
              <input type="date" [(ngModel)]="formData.marriageDate" name="marriageDate" />
            </div>
            <div class="form-group">
              <label>Số năm hiếm muộn</label>
              <input type="number" [(ngModel)]="formData.infertilityYears" name="infertilityYears" min="0" />
            </div>
          </div>
        </div>

        <div class="form-actions">
          <button type="button" class="btn-cancel" routerLink="/couples">Huỷ</button>
          <button type="submit" class="btn-submit" [disabled]="saving() || !canSubmit()">
            {{ saving() ? 'Đang lưu...' : 'Tạo cặp đôi' }}
          </button>
        </div>
      </form>
    </div>
  `,
    styles: [`
    .couple-form { max-width: 800px; margin: 0 auto; }

    .page-header { margin-bottom: 2rem; }
    .back-link { color: #6b7280; text-decoration: none; font-size: 0.875rem; display: inline-block; margin-bottom: 0.5rem; }
    h1 { font-size: 1.5rem; color: #1e1e2f; margin: 0; }

    .form-card {
      background: white;
      border-radius: 16px;
      padding: 2rem;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
    }

    .partner-section, .details-section { margin-bottom: 2rem; }

    h2 { font-size: 1rem; color: #374151; margin: 0 0 1rem; padding-bottom: 0.5rem; border-bottom: 1px solid #f1f5f9; }

    .search-box { position: relative; margin-bottom: 1rem; }

    .search-box input {
      width: 100%;
      padding: 0.75rem;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      font-size: 1rem;
    }

    .search-box input:focus {
      outline: none;
      border-color: #667eea;
      box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
    }

    .search-results {
      position: absolute;
      top: 100%;
      left: 0;
      right: 0;
      background: white;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      box-shadow: 0 4px 12px rgba(0,0,0,0.1);
      max-height: 200px;
      overflow-y: auto;
      z-index: 10;
    }

    .result-item {
      display: flex;
      gap: 0.75rem;
      padding: 0.75rem;
      cursor: pointer;
      border-bottom: 1px solid #f1f5f9;
    }

    .result-item:hover { background: #f8fafc; }
    .result-item:last-child { border-bottom: none; }

    .result-item .code { color: #6b7280; font-family: monospace; font-size: 0.875rem; }
    .result-item .name { font-weight: 500; }

    .selected-patient {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 1rem;
      background: linear-gradient(135deg, rgba(102,126,234,0.1), rgba(118,75,162,0.1));
      border-radius: 12px;
      border: 2px solid #667eea;
    }

    .selected-patient .avatar { font-size: 2.5rem; }
    .selected-patient .info { flex: 1; display: flex; flex-direction: column; }
    .selected-patient .info strong { font-size: 1rem; }
    .selected-patient .info span { color: #6b7280; font-size: 0.875rem; }

    .btn-clear {
      padding: 0.25rem 0.5rem;
      background: #ef4444;
      color: white;
      border: none;
      border-radius: 4px;
      cursor: pointer;
    }

    .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .form-group { display: flex; flex-direction: column; }
    label { font-size: 0.875rem; color: #374151; margin-bottom: 0.5rem; }

    .details-section input {
      padding: 0.75rem;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      font-size: 1rem;
    }

    .form-actions {
      display: flex;
      gap: 1rem;
      justify-content: flex-end;
      margin-top: 2rem;
      padding-top: 1.5rem;
      border-top: 1px solid #f1f5f9;
    }

    .btn-cancel, .btn-submit {
      padding: 0.75rem 1.5rem;
      border: none;
      border-radius: 8px;
      font-weight: 500;
      cursor: pointer;
      text-decoration: none;
    }

    .btn-cancel { background: #f1f5f9; color: #374151; }
    .btn-submit { background: linear-gradient(135deg, #667eea, #764ba2); color: white; }
    .btn-submit:disabled { opacity: 0.6; cursor: not-allowed; }
  `]
})
export class CoupleFormComponent {
    saving = signal(false);
    selectedWife = signal<Patient | null>(null);
    selectedHusband = signal<Patient | null>(null);
    wifeResults = signal<Patient[]>([]);
    husbandResults = signal<Patient[]>([]);

    wifeSearch = '';
    husbandSearch = '';

    formData = {
        marriageDate: '',
        infertilityYears: null as number | null
    };

    constructor(private api: ApiService, private router: Router) { }

    searchWife(): void {
        if (this.wifeSearch.length < 2) {
            this.wifeResults.set([]);
            return;
        }
        this.api.searchPatients(this.wifeSearch).subscribe(res => {
            this.wifeResults.set(res.items.filter(p => p.gender === 'Female'));
        });
    }

    searchHusband(): void {
        if (this.husbandSearch.length < 2) {
            this.husbandResults.set([]);
            return;
        }
        this.api.searchPatients(this.husbandSearch).subscribe(res => {
            this.husbandResults.set(res.items.filter(p => p.gender === 'Male'));
        });
    }

    selectWife(patient: Patient): void {
        this.selectedWife.set(patient);
        this.wifeSearch = '';
        this.wifeResults.set([]);
    }

    selectHusband(patient: Patient): void {
        this.selectedHusband.set(patient);
        this.husbandSearch = '';
        this.husbandResults.set([]);
    }

    clearWife(): void {
        this.selectedWife.set(null);
    }

    clearHusband(): void {
        this.selectedHusband.set(null);
    }

    canSubmit(): boolean {
        return !!this.selectedWife() && !!this.selectedHusband();
    }

    submit(): void {
        if (!this.canSubmit()) return;

        this.saving.set(true);
        this.api.createCouple({
            wifeId: this.selectedWife()!.id,
            husbandId: this.selectedHusband()!.id,
            marriageDate: this.formData.marriageDate || undefined,
            infertilityYears: this.formData.infertilityYears ?? undefined
        }).subscribe({
            next: () => {
                this.saving.set(false);
                this.router.navigate(['/couples']);
            },
            error: () => this.saving.set(false)
        });
    }
}
