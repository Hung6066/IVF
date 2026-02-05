import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PatientService } from '../../../core/services/patient.service';

@Component({
  selector: 'app-patient-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './patient-form.component.html',
  styleUrls: ['./patient-form.component.scss']
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

  constructor(private patientService: PatientService, private router: Router) { }

  submit(): void {
    if (!this.formData.fullName || !this.formData.dateOfBirth) {
      return;
    }

    this.saving.set(true);
    this.patientService.createPatient(this.formData as any).subscribe({
      next: (patient) => {
        this.saving.set(false);
        this.router.navigate(['/patients', patient.id]);
      },
      error: () => this.saving.set(false)
    });
  }
}
