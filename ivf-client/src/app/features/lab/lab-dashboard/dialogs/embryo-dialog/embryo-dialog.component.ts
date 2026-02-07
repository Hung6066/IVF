import { Component, EventEmitter, Input, Output, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EmbryoCard } from '../../lab-dashboard.models';
import { DateService } from '../../../../../core/services/date.service';

@Component({
    selector: 'app-embryo-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './embryo-dialog.component.html',
    styleUrls: ['./embryo-dialog.component.scss']
})
export class EmbryoDialogComponent implements OnInit {
    @Input() existingEmbryo: EmbryoCard | null = null;
    @Input() activeCycles: any[] = [];
    @Input() cryoLocations: any[] = [];
    @Output() save = new EventEmitter<any>();
    @Output() delete = new EventEmitter<string>();
    @Output() cancel = new EventEmitter<void>();

    embryo: any = {
        cycleId: '',
        cycleCode: '',
        embryoNumber: 1,
        grade: '',
        day: 'D3',
        status: 'Developing',
        location: '',
        patientName: '',
        fertilizationDate: new Date().toISOString().split('T')[0]
    };

    isEdit = false;

    constructor(private dateService: DateService) { }

    ngOnInit() {
        if (this.existingEmbryo) {
            this.isEdit = true;
            this.embryo = { ...this.existingEmbryo };
            // fertilizationDate might come as a full ISO string (if added to interface) 
            // or we need to ensure it's there.
            if (this.embryo.fertilizationDate) {
                this.embryo.fertilizationDate = this.dateService.toInputDate(this.embryo.fertilizationDate);
            }
        }
    }

    onCycleChange() {
        const selectedCycle = this.activeCycles.find(c => c.id === this.embryo.cycleId);
        if (selectedCycle) {
            this.embryo.cycleCode = selectedCycle.cycleCode;
            this.embryo.patientName = selectedCycle.wifeName;
            if (selectedCycle.startDate) {
                this.embryo.fertilizationDate = this.dateService.toInputDate(selectedCycle.startDate);
            }
        }
    }

    onSubmit() {
        this.save.emit(this.embryo);
    }

    onDelete() {
        if (confirm('Bạn có chắc chắn muốn xóa phôi này?')) {
            this.delete.emit(this.embryo.id);
        }
    }

    close() {
        this.cancel.emit();
    }
}
