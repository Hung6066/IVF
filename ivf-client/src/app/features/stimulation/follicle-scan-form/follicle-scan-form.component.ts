import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-follicle-scan-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './follicle-scan-form.component.html',
  styleUrls: ['./follicle-scan-form.component.scss']
})
export class FollicleScanFormComponent {
  @Input() saving = false;
  @Output() submitScan = new EventEmitter<any>();
  @Output() cancel = new EventEmitter<void>();

  form = {
    scanDate: new Date().toISOString().slice(0, 10),
    cycleDay: 1,
    size12Follicle: null as number | null,
    size14Follicle: null as number | null,
    totalFollicles: null as number | null,
    endometriumThickness: null as number | null,
    endometriumPattern: '',
    e2: null as number | null,
    lh: null as number | null,
    p4: null as number | null,
    notes: ''
  };

  onSubmit() { this.submitScan.emit(this.form); }
}
