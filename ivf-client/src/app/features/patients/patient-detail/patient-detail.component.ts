import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule, Router } from '@angular/router';
import { PatientService } from '../../../core/services/patient.service';
import { CoupleService } from '../../../core/services/couple.service';
import { CycleService } from '../../../core/services/cycle.service';
import { FormsService, FormTemplate } from '../../forms/forms.service';
import { Patient, Couple, TreatmentCycle } from '../../../core/models/api.models';

@Component({
  selector: 'app-patient-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './patient-detail.component.html',
  styleUrls: ['./patient-detail.component.scss']
})
export class PatientDetailComponent implements OnInit {
  patient = signal<Patient | null>(null);
  couple = signal<Couple | null>(null);
  cycles = signal<TreatmentCycle[]>([]);
  formTemplates = signal<FormTemplate[]>([]);
  patientId = '';

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private patientService = inject(PatientService);
  private coupleService = inject(CoupleService);
  private cycleService = inject(CycleService);
  private formsService = inject(FormsService);

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.patientId = params['id'];
      this.loadPatient();
      this.loadFormTemplates();
    });
  }

  loadPatient(): void {
    this.patientService.getPatient(this.patientId).subscribe({
      next: (p) => {
        this.patient.set(p);
        this.loadCoupleAndCycles(p.id);
      },
      error: (err) => console.error('Error loading patient', err)
    });
  }

  loadCoupleAndCycles(patientId: string): void {
    this.coupleService.getCoupleByPatient(patientId).subscribe({
      next: (c) => {
        this.couple.set(c);
        this.loadCycles(c.id);
      },
      error: (err) => console.log('No couple found for patient', err)
    });
  }

  loadCycles(coupleId: string): void {
    this.cycleService.getCyclesByCouple(coupleId).subscribe({
      next: (cycles) => this.cycles.set(cycles),
      error: (err) => console.error('Error loading cycles', err)
    });
  }

  loadFormTemplates(): void {
    this.formsService.getTemplates(undefined, true).subscribe({
      next: (templates) => this.formTemplates.set(templates),
      error: () => console.log('No form templates found')
    });
  }

  getInitials(name?: string): string {
    if (!name) return '?';
    return name.split(' ').map(w => w[0]).join('').substring(0, 2).toUpperCase();
  }

  getTypeName(type?: string): string {
    const names: Record<string, string> = {
      'Infertility': 'Hiếm muộn', 'EggDonor': 'Cho trứng', 'SpermDonor': 'Cho tinh trùng'
    };
    return names[type || ''] || type || '';
  }

  getMethodName(method?: string): string { return method || ''; }

  getPhaseName(phase?: string): string {
    const names: Record<string, string> = {
      'Consultation': 'Tư vấn', 'OvarianStimulation': 'Kích thích', 'EggRetrieval': 'Chọc hút',
      'EmbryoCulture': 'Nuôi phôi', 'EmbryoTransfer': 'Chuyển phôi', 'Completed': 'Hoàn thành'
    };
    return names[phase || ''] || phase || '';
  }

  getOutcomeName(outcome?: string): string {
    const names: Record<string, string> = {
      'Ongoing': 'Đang điều trị', 'Pregnant': 'Có thai', 'NotPregnant': 'Không thai', 'Cancelled': 'Huỷ'
    };
    return names[outcome || ''] || outcome || '';
  }

  formatDate(date?: string): string {
    if (!date) return '';
    return new Date(date).toLocaleDateString('vi-VN');
  }

  createNewCycle(): void {
    const couple = this.couple();
    if (couple) {
      this.router.navigate(['/couples', couple.id, 'cycles', 'new']);
    } else {
      alert('Bệnh nhân chưa có hồ sơ cặp đôi. Vui lòng tạo hồ sơ cặp đôi trước.');
      // Optional: Navigate to create couple page
    }
  }
}
