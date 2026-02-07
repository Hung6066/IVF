import { Component, Input, Output, EventEmitter, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EmbryoCard } from '../../lab-dashboard.models';
import { EmbryoDialogComponent } from '../../dialogs/embryo-dialog/embryo-dialog.component';

@Component({
  selector: 'app-lab-embryos',
  standalone: true,
  imports: [CommonModule, FormsModule, EmbryoDialogComponent],
  templateUrl: './lab-embryos.component.html',
  styleUrls: ['./lab-embryos.component.scss']
})
export class LabEmbryosComponent {
  @Input() embryos: EmbryoCard[] = [];
  @Input() activeCycles: any[] = [];
  @Input() cryoLocations: any[] = [];
  @Input() currentDate: Date = new Date();
  @Output() selectEmbryo = new EventEmitter<EmbryoCard>();
  @Output() saveEmbryo = new EventEmitter<any>();
  @Output() deleteEmbryo = new EventEmitter<string>();

  selectedEmbryo: EmbryoCard | null = null;
  showDialog = false;
  filterDay = signal('all'); // Using signal for local state

  // Computed signal for derivation
  filteredEmbryos = computed(() => {
    const filter = this.filterDay();
    if (filter === 'all') return this.embryos;
    return this.embryos.filter(e => e.day === filter);
  });

  getCardClass(status: string): string {
    return status.toLowerCase();
  }

  getStatusName(status: string): string {
    const names: Record<string, string> = {
      'Developing': 'Đang nuôi',
      'Frozen': 'Đông lạnh',
      'Transferred': 'Đã chuyển',
      'Discarded': 'Loại bỏ'
    };
    return names[status] || status;
  }

  onSelect(embryo: EmbryoCard) {
    this.selectedEmbryo = embryo;
    this.showDialog = true;
    this.selectEmbryo.emit(embryo);
  }

  onAdd() {
    this.selectedEmbryo = null;
    this.showDialog = true;
  }

  onSave(data: any) {
    this.saveEmbryo.emit(data);
    this.showDialog = false;
  }

  onDialogDelete(id: string) {
    this.deleteEmbryo.emit(id);
    this.showDialog = false;
  }

  onCancel() {
    this.showDialog = false;
  }
}
