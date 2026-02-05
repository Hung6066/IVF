import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { CoupleService } from '../../../core/services/couple.service';
import { Patient } from '../../../core/models/api.models';
import { PatientSearchComponent } from '../../../shared/components/patient-search/patient-search.component';

@Component({
  selector: 'app-couple-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PatientSearchComponent],
  templateUrl: './couple-form.component.html',
  styleUrls: ['./couple-form.component.scss']
})
export class CoupleFormComponent {
  saving = signal(false);
  selectedWife = signal<Patient | null>(null);
  selectedHusband = signal<Patient | null>(null);
  wifeId = '';
  husbandId = '';

  formData = {
    marriageDate: '',
    infertilityYears: null as number | null
  };

  constructor(private coupleService: CoupleService, private router: Router) { }

  canSubmit(): boolean {
    return !!this.selectedWife() && !!this.selectedHusband();
  }

  submit(): void {
    if (!this.canSubmit()) return;

    this.saving.set(true);
    this.coupleService.createCouple({
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
