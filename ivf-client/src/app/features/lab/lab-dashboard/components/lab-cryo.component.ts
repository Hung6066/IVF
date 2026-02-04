import { Component, Input, Output, EventEmitter, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CryoLocation, LabStats } from '../lab-dashboard.models';

@Component({
    selector: 'app-lab-cryo',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <section class="content-section card">
      <div class="section-header">
        <h2>Quản lý kho đông lạnh</h2>
        <button class="btn btn-primary" (click)="showModal = true">➕ Thêm vị trí</button>
      </div>
      <div class="cryo-grid">
        @for (loc of locations; track loc.tank) {
          <div class="cryo-tank">
            <div class="tank-header">
              <span class="tank-name">🧊 {{ loc.tank }}</span>
              <span class="tank-capacity">{{ loc.used }}/{{ loc.available + loc.used }}</span>
            </div>
            <div class="tank-bar">
              <div class="tank-fill" [style.width.%]="(loc.used / (loc.available + loc.used)) * 100"></div>
            </div>
            <div class="tank-details">
              <span>{{ loc.canister }} canisters</span>
              <span>{{ loc.cane }} canes</span>
              <span>{{ loc.goblet }} goblets</span>
            </div>
          </div>
        }
      </div>
      <div class="cryo-summary">
        <div class="summary-item"><span class="label">Tổng phôi đông lạnh</span><span class="value">{{ stats.totalFrozenEmbryos }}</span></div>
        <div class="summary-item"><span class="label">Tổng trứng đông lạnh</span><span class="value">{{ stats.totalFrozenEggs }}</span></div>
        <div class="summary-item"><span class="label">Tổng tinh trùng đông lạnh</span><span class="value">{{ stats.totalFrozenSperm }}</span></div>
      </div>
    </section>

    <!-- Modal for adding new location -->
    @if (showModal) {
      <div class="modal-overlay" (click)="closeModal()">
        <div class="modal-content" (click)="$event.stopPropagation()">
          <h3>➕ Thêm vị trí đông lạnh</h3>
          <form (ngSubmit)="onSubmit()">
            <div class="form-group">
                <label class="form-label">Tên bình (Tank) *</label>
                <input type="text" [(ngModel)]="newCryo.tank" name="tank" required placeholder="VD: Tank A1" class="form-control" />
            </div>
            <div class="form-row">
                <div class="form-group"><label class="form-label">Số canister</label><input type="number" [(ngModel)]="newCryo.canister" name="canister" min="0" class="form-control" /></div>
                <div class="form-group"><label class="form-label">Số cane</label><input type="number" [(ngModel)]="newCryo.cane" name="cane" min="0" class="form-control" /></div>
                <div class="form-group"><label class="form-label">Số goblet</label><input type="number" [(ngModel)]="newCryo.goblet" name="goblet" min="0" class="form-control" /></div>
            </div>
            <div class="form-row">
                <div class="form-group"><label class="form-label">Sức chứa (available)</label><input type="number" [(ngModel)]="newCryo.available" name="avail" min="0" class="form-control" /></div>
                <div class="form-group"><label class="form-label">Đang sử dụng (used)</label><input type="number" [(ngModel)]="newCryo.used" name="used" min="0" class="form-control" /></div>
            </div>
            <div class="modal-actions">
                <button type="button" class="btn btn-secondary" (click)="closeModal()">Huỷ</button>
                <button type="submit" class="btn btn-primary">Thêm vị trí</button>
            </div>
          </form>
        </div>
      </div>
    }
  `,
    styles: [`
    .cryo-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: 1rem; margin-bottom: 1.5rem; }
    .cryo-tank { background: var(--bg-card); padding: 1rem; border-radius: var(--radius-lg); }
    .tank-header { display: flex; justify-content: space-between; margin-bottom: 0.5rem; }
    .tank-name { font-weight: 600; }
    .tank-capacity { font-size: 0.8rem; color: var(--text-secondary); }
    .tank-bar { height: 8px; background: var(--border-color); border-radius: 4px; overflow: hidden; }
    .tank-fill { height: 100%; background: linear-gradient(90deg, var(--info), var(--primary)); }
    .tank-details { display: flex; justify-content: space-between; margin-top: 0.5rem; font-size: 0.75rem; color: var(--text-secondary); }
    
    .cryo-summary { display: flex; gap: 1rem; padding: 1rem; background: var(--bg-card); border-radius: var(--radius-lg); }
    .summary-item { flex: 1; text-align: center; }
    .summary-item .label { display: block; font-size: 0.75rem; color: var(--text-secondary); }
    .summary-item .value { font-size: 1.5rem; font-weight: 700; color: var(--primary); }
    .form-row { display: flex; gap: 1rem; }
  `]
})
export class LabCryoComponent {
    @Input() locations: CryoLocation[] = [];
    @Input() stats: LabStats = { eggRetrievalCount: 0, cultureCount: 0, transferCount: 0, freezeCount: 0, totalFrozenEmbryos: 0, totalFrozenEggs: 0, totalFrozenSperm: 0 };
    @Output() addLocation = new EventEmitter<CryoLocation>();

    showModal = false;
    newCryo: any = { tank: '', canister: 0, cane: 0, goblet: 0, available: 50, used: 0 };

    closeModal() {
        this.showModal = false;
    }

    onSubmit() {
        this.addLocation.emit({
            tank: this.newCryo.tank,
            canister: this.newCryo.canister,
            cane: this.newCryo.cane,
            goblet: this.newCryo.goblet,
            available: this.newCryo.available,
            used: this.newCryo.used
        });
        this.showModal = false;
        this.newCryo = { tank: '', canister: 0, cane: 0, goblet: 0, available: 50, used: 0 };
    }
}
