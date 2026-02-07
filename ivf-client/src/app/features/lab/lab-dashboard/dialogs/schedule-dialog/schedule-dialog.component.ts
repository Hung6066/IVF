import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DateService } from '../../../../../core/services/date.service';
import { ScheduleTypes } from '../../../../../core/constants/lab.constants';

@Component({
    selector: 'app-schedule-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './schedule-dialog.component.html',
    styleUrls: ['./schedule-dialog.component.scss']
})
export class ScheduleDialogComponent {
    @Input() initialDate: Date = new Date();
    @Input() activeCycles: any[] = [];
    @Input() doctors: any[] = [];
    @Output() save = new EventEmitter<any>();
    @Output() cancel = new EventEmitter<void>();

    // Expose constants to template
    readonly ScheduleTypes = ScheduleTypes;

    schedule: any = {
        type: ScheduleTypes.RETRIEVAL,
        cycleId: '',
        cycleCode: '',
        date: new Date().toISOString().split('T')[0],
        time: '08:00',
        doctorName: ''
    };

    onCycleChange() {
        const selectedCycle = this.activeCycles.find(c => c.id === this.schedule.cycleId);
        if (selectedCycle) {
            this.schedule.cycleCode = selectedCycle.cycleCode;
        }
    }

    constructor(private dateService: DateService) { }

    ngOnInit() {
        if (this.initialDate) {
            this.schedule.date = this.dateService.toInputDate(this.initialDate);
        }
    }

    onSubmit() {
        this.save.emit(this.schedule);
    }

    close() {
        this.cancel.emit();
    }
}
