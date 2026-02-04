import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';

@Component({
    selector: 'app-patient-form',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule],
    template: `
    <div class="patient-form">
      <header class="page-header">
        <a routerLink="/patients" class="back-link">← Danh sách bệnh nhân</a>
        <h1>Thêm bệnh nhân mới</h1>
      </header>

      <form (ngSubmit)="submit()" class="form-card">
        <div class="form-row">
          <div class="form-group">
            <label>Họ và tên *</label>
            <input type="text" [(ngModel)]="formData.fullName" name="fullName" required />
          </div>
          <div class="form-group">
            <label>Mã bệnh nhân</label>
            <input type="text" [(ngModel)]="formData.patientCode" name="patientCode" placeholder="Tự động tạo nếu để trống" />
          </div>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label>Ngày sinh *</label>
            <input type="date" [(ngModel)]="formData.dateOfBirth" name="dateOfBirth" required />
          </div>
          <div class="form-group">
            <label>Giới tính *</label>
            <select [(ngModel)]="formData.gender" name="gender" required>
              <option value="Female">Nữ</option>
              <option value="Male">Nam</option>
            </select>
          </div>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label>Loại bệnh nhân *</label>
            <select [(ngModel)]="formData.patientType" name="patientType" required>
              <option value="Infertility">Hiếm muộn</option>
              <option value="EggDonor">Người cho trứng</option>
              <option value="SpermDonor">Người cho tinh trùng</option>
            </select>
          </div>
          <div class="form-group">
            <label>CCCD/CMND</label>
            <input type="text" [(ngModel)]="formData.identityNumber" name="identityNumber" />
          </div>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label>Số điện thoại</label>
            <input type="tel" [(ngModel)]="formData.phone" name="phone" placeholder="0901234567" />
          </div>
          <div class="form-group">
            <label>Email</label>
            <input type="email" [(ngModel)]="formData.email" name="email" />
          </div>
        </div>

        <div class="form-group full-width">
          <label>Địa chỉ</label>
          <input type="text" [(ngModel)]="formData.address" name="address" />
        </div>

        <div class="form-actions">
          <button type="button" class="btn-cancel" routerLink="/patients">Huỷ</button>
          <button type="submit" class="btn-submit" [disabled]="saving()">
            {{ saving() ? 'Đang lưu...' : 'Lưu bệnh nhân' }}
          </button>
        </div>
      </form>
    </div>
  `,
    styles: [`
    .patient-form { max-width: 800px; margin: 0 auto; }

    .page-header { margin-bottom: 2rem; }
    .back-link { color: #6b7280; text-decoration: none; font-size: 0.875rem; display: inline-block; margin-bottom: 0.5rem; }
    h1 { font-size: 1.5rem; color: #1e1e2f; margin: 0; }

    .form-card {
      background: white;
      border-radius: 16px;
      padding: 2rem;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
    }

    .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; margin-bottom: 1rem; }

    .form-group { display: flex; flex-direction: column; }
    .form-group.full-width { margin-bottom: 1rem; }

    label { font-size: 0.875rem; color: #374151; margin-bottom: 0.5rem; font-weight: 500; }

    input, select {
      padding: 0.75rem;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      font-size: 1rem;
    }

    input:focus, select:focus {
      outline: none;
      border-color: #667eea;
      box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
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
    }

    .btn-cancel { background: #f1f5f9; color: #374151; text-decoration: none; }
    .btn-submit { background: linear-gradient(135deg, #667eea, #764ba2); color: white; }
    .btn-submit:disabled { opacity: 0.6; cursor: not-allowed; }
  `]
})
export class PatientFormComponent {
    saving = signal(false);

    formData = {
        patientCode: '',
        fullName: '',
        dateOfBirth: '',
        gender: 'Female',
        patientType: 'Infertility',
        identityNumber: '',
        phone: '',
        email: '',
        address: ''
    };

    constructor(private api: ApiService, private router: Router) { }

    submit(): void {
        if (!this.formData.fullName || !this.formData.dateOfBirth) {
            return;
        }

        this.saving.set(true);
        this.api.createPatient(this.formData as any).subscribe({
            next: (patient) => {
                this.saving.set(false);
                this.router.navigate(['/patients', patient.id]);
            },
            error: () => this.saving.set(false)
        });
    }
}
