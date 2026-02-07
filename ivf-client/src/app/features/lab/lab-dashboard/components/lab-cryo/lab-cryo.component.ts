import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CryoLocation, LabStats } from '../../lab-dashboard.models';
import { CryoDialogComponent } from '../../dialogs/cryo-dialog/cryo-dialog.component';

@Component({
  selector: 'app-lab-cryo',
  standalone: true,
  imports: [CommonModule, FormsModule, CryoDialogComponent],
  templateUrl: './lab-cryo.component.html',
  styleUrls: ['./lab-cryo.component.scss']
})
export class LabCryoComponent {
  @Input() locations: CryoLocation[] = [];
  @Input() stats: LabStats = { eggRetrievalCount: 0, cultureCount: 0, transferCount: 0, freezeCount: 0, totalFrozenEmbryos: 0, totalFrozenEggs: 0, totalFrozenSperm: 0 };
  @Output() addLocation = new EventEmitter<CryoLocation>();
  @Output() updateLocation = new EventEmitter<any>();
  @Output() deleteLocation = new EventEmitter<string>();

  showDialog = false;
  isEdit = false;
  selectedLocation: any = { tank: '', canister: 0, cane: 0, goblet: 0, available: 50, used: 0, specimenType: 0 };

  onAdd() {
    this.isEdit = false;
    this.selectedLocation = { tank: '', canister: 0, cane: 0, goblet: 0, available: 50, used: 0, specimenType: 0 };
    this.showDialog = true;
  }

  onEdit(loc: CryoLocation) {
    this.isEdit = true;
    this.selectedLocation = { ...loc }; // Clone to avoid direct mutation
    this.showDialog = true;
  }

  onSave(data: any) {
    if (this.isEdit) {
      this.updateLocation.emit(data);
    } else {
      this.addLocation.emit(data);
    }
    this.showDialog = false;
  }

  onCancel() {
    this.showDialog = false;
  }

  onDelete(tank: string) {
    if (confirm(`Bạn có chắc muốn xóa tủ đông '${tank}' không?`)) {
      this.deleteLocation.emit(tank);
    }
  }
}
