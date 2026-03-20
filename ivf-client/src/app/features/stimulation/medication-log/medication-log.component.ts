import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MedicationScheduleItemDto } from '../../../core/services/stimulation.service';

@Component({
  selector: 'app-medication-log',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './medication-log.component.html',
  styleUrls: ['./medication-log.component.scss']
})
export class MedicationLogComponent {
  @Input() medications: MedicationScheduleItemDto[] = [];
}
