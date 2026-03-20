import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ProcedureService } from '../../../core/services/procedure.service';
import { PatientSearchComponent } from '../../../shared/components/patient-search/patient-search.component';
import { CycleSearchComponent } from '../../../shared/components/cycle-search/cycle-search.component';

@Component({
  selector: 'app-procedure-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PatientSearchComponent, CycleSearchComponent],
  templateUrl: './procedure-create.component.html',
  styleUrls: ['./procedure-create.component.scss'],
})
export class ProcedureCreateComponent {
  private service = inject(ProcedureService);
  private router = inject(Router);

  saving = signal(false);
  error = signal('');

  form = {
    patientId: '',
    performedByDoctorId: '',
    procedureType: 'OPU',
    procedureName: '',
    scheduledAt: '',
    cycleId: '',
    assistantDoctorId: '',
    procedureCode: '',
    anesthesiaType: '',
    roomNumber: '',
    preOpNotes: '',
  };

  procedureTypes = ['OPU', 'IUI', 'ICSI', 'IVM', 'FET', 'Biopsy'];
  anesthesiaTypes = ['', 'Local', 'General', 'Sedation', 'Regional'];

  submit() {
    if (!this.form.patientId || !this.form.performedByDoctorId || !this.form.scheduledAt) {
      this.error.set('Vui lòng điền đầy đủ thông tin bắt buộc');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.service
      .create({
        patientId: this.form.patientId,
        performedByDoctorId: this.form.performedByDoctorId,
        procedureType: this.form.procedureType,
        procedureName: this.form.procedureName || this.form.procedureType,
        scheduledAt: this.form.scheduledAt,
        cycleId: this.form.cycleId || undefined,
        assistantDoctorId: this.form.assistantDoctorId || undefined,
        procedureCode: this.form.procedureCode || undefined,
        anesthesiaType: this.form.anesthesiaType || undefined,
        roomNumber: this.form.roomNumber || undefined,
        preOpNotes: this.form.preOpNotes || undefined,
      })
      .subscribe({
        next: (r) => this.router.navigate(['/procedure', r.id]),
        error: (err) => {
          this.error.set(err.error?.message || 'Lỗi tạo thủ thuật');
          this.saving.set(false);
        },
      });
  }
}
