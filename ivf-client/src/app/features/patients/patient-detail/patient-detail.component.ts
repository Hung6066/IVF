import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
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
  private patientId = '';

  constructor(private route: ActivatedRoute, private api: ApiService) { }

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.patientId = params['id'];
      this.loadPatient();
    });
  }

  loadPatient(): void {
    this.api.getPatient(this.patientId).subscribe(p => this.patient.set(p));
    // In a real app, we would load couple and cycles here too
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
    // Navigate to create cycle form or open modal
    console.log('Create cycle clicked');
  }
}
