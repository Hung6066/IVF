import { Component, EventEmitter, Output, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
    selector: 'app-cryo-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './cryo-dialog.component.html',
    styleUrls: ['./cryo-dialog.component.scss']
})
export class CryoDialogComponent {
    @Input() location: any = {
        tank: '',
        canister: 0,
        cane: 0,
        goblet: 0,
        available: 50,
        used: 0,
        specimenType: 0
    };
    @Input() isEdit: boolean = false;

    @Output() save = new EventEmitter<any>();
    @Output() cancel = new EventEmitter<void>();

    onSubmit() {
        this.save.emit(this.location);
    }

    close() {
        this.cancel.emit();
    }
}
