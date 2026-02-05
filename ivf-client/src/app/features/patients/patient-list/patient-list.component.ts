import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { Patient } from '../../../core/models/api.models';

@Component({
  selector: 'app-patient-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './patient-list.component.html',
  styleUrls: ['./patient-list.component.scss']
})
export class PatientListComponent implements OnInit {
  patients = signal<Patient[]>([]);
  total = signal(0);
  page = signal(1);
  loading = signal(false);
  searchQuery = '';
  pageSize = 20;
  showAddModal = false;
  showEditModal = false;
  newPatient: any = { gender: 'Female', patientType: 'Infertility' };
  editingPatient: any = null;

  private searchTimeout?: ReturnType<typeof setTimeout>;

  constructor(private api: ApiService, private router: Router, private route: ActivatedRoute) { }

  ngOnInit(): void {
    this.loadPatients();
    this.route.queryParams.subscribe(params => {
      if (params['action'] === 'new') this.showAddModal = true;
    });
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
    if (!date) return 'N/A';
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

  viewPatient(patient: Patient): void {
    this.router.navigate(['/patients', patient.id]);
  }

  editPatient(patient: Patient): void {
    this.editingPatient = { ...patient };
    this.showEditModal = true;
  }

  submitNewPatient(): void {
    this.api.createPatient(this.newPatient).subscribe({
      next: () => {
        this.showAddModal = false;
        this.newPatient = { gender: 'Female', patientType: 'Infertility' };
        this.loadPatients();
      },
      error: (err) => {
        console.error('Failed to create patient', err);
        alert('Lỗi: ' + (err.error?.message || 'Không thể tạo bệnh nhân'));
      }
    });
  }

  submitEditPatient(): void {
    if (!this.editingPatient) return;
    this.api.updatePatient(this.editingPatient.id, this.editingPatient).subscribe({
      next: () => {
        this.showEditModal = false;
        this.editingPatient = null;
        this.loadPatients();
        alert('Cập nhật bệnh nhân thành công!');
      },
      error: (err) => {
        console.error('Failed to update patient', err);
        alert('Lỗi: ' + (err.error?.message || 'Không thể cập nhật bệnh nhân'));
      }
    });
  }
}
