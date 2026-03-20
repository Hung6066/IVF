import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { PharmacyService, Drug, ImportSlip } from './pharmacy.service';
import { PrescriptionService } from '../../../core/services/prescription.service';
import { PrescriptionDto } from '../../../core/models/prescription.models';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-pharmacy-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './pharmacy-dashboard.component.html',
  styleUrls: ['./pharmacy-dashboard.component.scss'],
})
export class PharmacyDashboardComponent implements OnInit {
  private service = inject(PharmacyService);
  private prescriptionService = inject(PrescriptionService);
  private authService = inject(AuthService);

  activeTab = 'prescriptions';
  prescriptions = signal<PrescriptionDto[]>([]);
  drugs = signal<Drug[]>([]);
  imports = signal<ImportSlip[]>([]);

  pendingRx = signal(0);
  completedRx = signal(0);
  lowStockCount = signal(0);
  totalItems = signal(0);
  drugSearch = '';
  prescriptionSearch = '';
  statusFilter = '';

  // Modal State
  showNewImport = false;
  newImport: any = { supplier: '', date: '', items: [{ name: '', qty: 1, price: 0 }] };

  constructor(private router: Router) {}

  ngOnInit(): void {
    this.refreshData();
  }

  refreshData() {
    this.service.getDrugs().subscribe((d) => {
      this.drugs.set(d);
      this.totalItems.set(d.length);
      this.lowStockCount.set(d.filter((x) => x.stock < x.minStock).length);
    });

    this.prescriptionService
      .search(this.prescriptionSearch || undefined, this.statusFilter || undefined)
      .subscribe((result) => {
        this.prescriptions.set(result.items);
        this.pendingRx.set(result.items.filter((x) => x.status === 'Pending').length);
        this.completedRx.set(result.items.filter((x) => x.status === 'Dispensed').length);
      });

    this.service.getImports().subscribe((i) => this.imports.set(i));
  }

  filteredDrugs(): Drug[] {
    if (!this.drugSearch) return this.drugs();
    return this.drugs().filter((d) => d.name.toLowerCase().includes(this.drugSearch.toLowerCase()));
  }

  processRx(rx: PrescriptionDto): void {
    const userId = this.authService.user()?.id;
    if (!userId) return;
    this.prescriptionService.enter(rx.id, userId).subscribe(() => this.refreshData());
  }

  completeRx(rx: PrescriptionDto): void {
    const userId = this.authService.user()?.id;
    if (!userId) return;
    this.prescriptionService.dispense(rx.id, userId).subscribe(() => this.refreshData());
  }

  cancelRx(rx: PrescriptionDto): void {
    if (!confirm('Hủy toa thuốc này?')) return;
    this.prescriptionService.cancel(rx.id).subscribe(() => this.refreshData());
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      Pending: 'Chờ xử lý',
      Entered: 'Đã tiếp nhận',
      Printed: 'Đã in',
      Dispensed: 'Đã phát',
      Cancelled: 'Đã hủy',
    };
    return labels[status] || status;
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      Pending: 'text-yellow-600 bg-yellow-100',
      Entered: 'text-blue-600 bg-blue-100',
      Printed: 'text-indigo-600 bg-indigo-100',
      Dispensed: 'text-green-600 bg-green-100',
      Cancelled: 'text-red-600 bg-red-100',
    };
    return classes[status] || 'text-gray-600 bg-gray-100';
  }

  formatCurrency(v: number): string {
    return new Intl.NumberFormat('vi-VN', {
      style: 'currency',
      currency: 'VND',
      maximumFractionDigits: 0,
    }).format(v);
  }

  viewImport(i: any): void {
    alert('Chi tiết phiếu nhập: ' + i.code + '\nNCC: ' + i.supplier + '\nSố mặt hàng: ' + i.items);
  }
  editImport(i: any): void {
    alert('Sửa phiếu nhập: ' + i.code);
  }
  deleteImport(i: any): void {
    if (confirm('Xóa phiếu nhập ' + i.code + '?')) {
      this.imports.update((list) => list.filter((x) => x.id !== i.id));
    }
  }

  addImportItem(): void {
    this.newImport.items.push({ name: '', qty: 1, price: 0 });
  }
  removeImportItem(idx: number): void {
    this.newImport.items.splice(idx, 1);
  }
  calcImportTotal(): number {
    return this.newImport.items.reduce((sum: number, i: any) => sum + i.qty * i.price, 0);
  }

  submitImport(): void {
    const list = this.imports();
    const newCode = 'NK-' + String(list.length + 1).padStart(3, '0');
    this.imports.update((curr) => [
      ...curr,
      {
        id: newCode,
        code: newCode,
        date: this.newImport.date || new Date().toLocaleDateString('vi-VN'),
        supplier: this.newImport.supplier,
        items: this.newImport.items.length,
        total: this.calcImportTotal(),
        status: 'Hoàn thành',
      },
    ]);

    this.showNewImport = false;
    this.newImport = { supplier: '', date: '', items: [{ name: '', qty: 1, price: 0 }] };
  }
}
