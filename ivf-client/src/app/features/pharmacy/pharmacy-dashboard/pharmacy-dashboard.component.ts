import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { PharmacyService, Drug, Prescription, ImportSlip } from './pharmacy.service';

@Component({
  selector: 'app-pharmacy-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './pharmacy-dashboard.component.html',
  styleUrls: ['./pharmacy-dashboard.component.scss']
})
export class PharmacyDashboardComponent implements OnInit {
  private service = inject(PharmacyService);

  activeTab = 'prescriptions';
  prescriptions = signal<Prescription[]>([]);
  drugs = signal<Drug[]>([]);
  imports = signal<ImportSlip[]>([]);

  pendingRx = signal(0);
  completedRx = signal(0);
  lowStockCount = signal(0);
  totalItems = signal(0);
  drugSearch = '';

  // Modal State
  showNewImport = false;
  newImport: any = { supplier: '', date: '', items: [{ name: '', qty: 1, price: 0 }] };

  constructor(private router: Router) { }

  ngOnInit(): void {
    this.refreshData();
  }

  refreshData() {
    this.service.getDrugs().subscribe(d => {
      this.drugs.set(d);
      this.totalItems.set(d.length);
      this.lowStockCount.set(d.filter(x => x.stock < x.minStock).length);
    });

    this.service.getPrescriptions().subscribe(p => {
      this.prescriptions.set(p);
      this.pendingRx.set(p.filter(x => x.status === 'Pending').length);
      this.completedRx.set(p.filter(x => x.status === 'Completed').length);
    });

    this.service.getImports().subscribe(i => this.imports.set(i));
  }

  filteredDrugs(): Drug[] {
    if (!this.drugSearch) return this.drugs();
    return this.drugs().filter(d => d.name.toLowerCase().includes(this.drugSearch.toLowerCase()));
  }

  processRx(rx: any): void {
    rx.status = 'Processing';
    this.prescriptions.update(l => [...l]);
  }

  completeRx(rx: any): void {
    rx.status = 'Completed';
    this.prescriptions.update(l => [...l]);
  }

  formatCurrency(v: number): string {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(v);
  }

  viewImport(i: any): void { alert('Chi tiết phiếu nhập: ' + i.code + '\nNCC: ' + i.supplier + '\nSố mặt hàng: ' + i.items); }
  editImport(i: any): void { alert('Sửa phiếu nhập: ' + i.code); }
  deleteImport(i: any): void {
    if (confirm('Xóa phiếu nhập ' + i.code + '?')) {
      this.imports.update(list => list.filter(x => x.id !== i.id));
    }
  }

  addImportItem(): void { this.newImport.items.push({ name: '', qty: 1, price: 0 }); }
  removeImportItem(idx: number): void { this.newImport.items.splice(idx, 1); }
  calcImportTotal(): number { return this.newImport.items.reduce((sum: number, i: any) => sum + (i.qty * i.price), 0); }

  submitImport(): void {
    const list = this.imports();
    const newCode = 'NK-' + String(list.length + 1).padStart(3, '0');
    this.imports.update(curr => [...curr, {
      id: newCode,
      code: newCode,
      date: this.newImport.date || new Date().toLocaleDateString('vi-VN'),
      supplier: this.newImport.supplier,
      items: this.newImport.items.length,
      total: this.calcImportTotal(),
      status: 'Hoàn thành'
    }]);

    this.showNewImport = false;
    this.newImport = { supplier: '', date: '', items: [{ name: '', qty: 1, price: 0 }] };
  }
}
