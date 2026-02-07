import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScheduleItem } from '../../lab-dashboard.models';
import { ScheduleDialogComponent } from '../../dialogs/schedule-dialog/schedule-dialog.component';

@Component({
  selector: 'app-lab-schedule',
  standalone: true,
  imports: [CommonModule, ScheduleDialogComponent],
  templateUrl: './lab-schedule.component.html',
  styleUrls: ['./lab-schedule.component.scss']
})
export class LabScheduleComponent {
  @Input() schedule: ScheduleItem[] = [];
  @Input() activeCycles: any[] = [];
  @Input() doctors: any[] = [];
  @Input() currentDate: Date = new Date();
  @Output() toggleStatus = new EventEmitter<ScheduleItem>();
  @Output() addSchedule = new EventEmitter<any>();

  showDialog = false;

  getByType(type: string): ScheduleItem[] {
    return this.schedule.filter(s => s.type === type);
  }

  onToggle(item: ScheduleItem) {
    this.toggleStatus.emit(item);
  }

  onAdd() {
    this.showDialog = true;
  }

  onSave(data: any) {
    this.addSchedule.emit(data);
    this.showDialog = false;
  }

  onCancel() {
    this.showDialog = false;
  }
}
