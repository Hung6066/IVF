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
  templateUrl: './couple-form.component.html',
  styleUrls: ['./couple-form.component.scss']
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
