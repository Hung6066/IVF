import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { Couple } from '../../../core/models/api.models';

@Component({
    selector: 'app-couple-list',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule],
    template: `
    <div class="couple-list">
      <header class="page-header">
        <h1>Danh sách cặp đôi</h1>
        <button class="btn-add" routerLink="/couples/new">➕ Tạo cặp đôi</button>
      </header>

      <div class="couple-grid">
        @for (couple of couples(); track couple.id) {
          <div class="couple-card">
            <div class="couple-header">
              <div class="partner wife">
                <span class="avatar">👩</span>
                <div class="info">
                  <span class="name">{{ couple.wife?.fullName || 'Chưa có' }}</span>
                  <span class="code">{{ couple.wife?.patientCode || '' }}</span>
                </div>
              </div>
              <span class="heart">❤️</span>
              <div class="partner husband">
                <span class="avatar">👨</span>
                <div class="info">
                  <span class="name">{{ couple.husband?.fullName || 'Chưa có' }}</span>
                  <span class="code">{{ couple.husband?.patientCode || '' }}</span>
                </div>
              </div>
            </div>
            <div class="couple-meta">
              <span>Kết hôn: {{ formatDate(couple.marriageDate) || '—' }}</span>
              <span>Hiếm muộn: {{ couple.infertilityYears || '?' }} năm</span>
            </div>
            <div class="couple-actions">
              <a [routerLink]="['/couples', couple.id, 'cycles', 'new']" class="btn-cycle">➕ Tạo chu kỳ</a>
              <a [routerLink]="['/patients', getWifeId(couple)]" class="btn-view">👁️ Vợ</a>
              <a [routerLink]="['/patients', getHusbandId(couple)]" class="btn-view">👁️ Chồng</a>
            </div>
          </div>
        } @empty {
          <div class="empty-state">
            <span class="icon">💑</span>
            <p>Chưa có cặp đôi nào</p>
            <button routerLink="/couples/new">Tạo cặp đôi mới</button>
          </div>
        }
      </div>
    </div>
  `,
    styles: [`
    .couple-list { max-width: 1200px; }

    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 2rem; }
    h1 { font-size: 1.5rem; color: #1e1e2f; margin: 0; }

    .btn-add {
      padding: 0.75rem 1.25rem;
      background: linear-gradient(135deg, #667eea, #764ba2);
      color: white;
      border: none;
      border-radius: 8px;
      cursor: pointer;
      text-decoration: none;
    }

    .couple-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(400px, 1fr)); gap: 1.5rem; }

    .couple-card {
      background: white;
      border-radius: 16px;
      padding: 1.5rem;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
    }

    .couple-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1rem; }

    .partner { display: flex; align-items: center; gap: 0.75rem; flex: 1; }
    .partner.husband { flex-direction: row-reverse; text-align: right; }

    .avatar { font-size: 2rem; }
    .info { display: flex; flex-direction: column; }
    .name { font-weight: 600; color: #1e1e2f; }
    .code { font-size: 0.75rem; color: #6b7280; font-family: monospace; }

    .heart { font-size: 1.5rem; margin: 0 0.5rem; }

    .couple-meta {
      display: flex;
      justify-content: center;
      gap: 2rem;
      color: #6b7280;
      font-size: 0.875rem;
      margin-bottom: 1rem;
      padding: 0.75rem;
      background: #f8fafc;
      border-radius: 8px;
    }

    .couple-actions { display: flex; gap: 0.5rem; }

    .btn-cycle, .btn-view {
      flex: 1;
      padding: 0.5rem;
      border: none;
      border-radius: 6px;
      text-decoration: none;
      text-align: center;
      font-size: 0.875rem;
      cursor: pointer;
    }

    .btn-cycle { background: #d1fae5; color: #065f46; }
    .btn-view { background: #f1f5f9; color: #374151; }

    .empty-state {
      grid-column: 1 / -1;
      text-align: center;
      padding: 4rem;
      background: white;
      border-radius: 16px;
    }

    .empty-state .icon { font-size: 4rem; }
    .empty-state p { color: #6b7280; margin: 1rem 0; }
    .empty-state button {
      padding: 0.75rem 1.5rem;
      background: linear-gradient(135deg, #667eea, #764ba2);
      color: white;
      border: none;
      border-radius: 8px;
      cursor: pointer;
    }
  `]
})
export class CoupleListComponent implements OnInit {
    couples = signal<Couple[]>([]);

    constructor(private api: ApiService) { }

    ngOnInit(): void {
        this.api.getCouples().subscribe(c => this.couples.set(c));
    }

    formatDate(date?: string): string {
        if (!date) return '';
        return new Date(date).toLocaleDateString('vi-VN');
    }

    getWifeId(couple: Couple): string {
        return couple.wife?.id || '';
    }

    getHusbandId(couple: Couple): string {
        return couple.husband?.id || '';
    }
}
