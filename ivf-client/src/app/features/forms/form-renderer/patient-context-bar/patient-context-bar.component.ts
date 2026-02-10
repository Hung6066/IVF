import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Patient } from '../../../../core/models/patient.models';
import { LinkedDataValue } from '../../forms.service';

@Component({
  selector: 'app-patient-context-bar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './patient-context-bar.component.html',
  styleUrls: ['./patient-context-bar.component.scss'],
})
export class PatientContextBarComponent {
  @Input() selectedPatient: Patient | null = null;
  @Input() linkedData: LinkedDataValue[] = [];
  @Input() patientSearchQuery = '';
  @Input() patientSearchResults: Patient[] = [];
  @Input() showPatientDropdown = false;

  @Output() searchInput = new EventEmitter<string>();
  @Output() patientSelected = new EventEmitter<Patient>();
  @Output() patientCleared = new EventEmitter<void>();
  @Output() dropdownHidden = new EventEmitter<void>();

  onSearchInput(value: string) {
    this.searchInput.emit(value);
  }

  onSelectPatient(patient: Patient) {
    this.patientSelected.emit(patient);
  }

  onClearPatient() {
    this.patientCleared.emit();
  }

  onHideDropdown() {
    this.dropdownHidden.emit();
  }
}
