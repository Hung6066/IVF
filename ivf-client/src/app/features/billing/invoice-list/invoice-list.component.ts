import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { BillingService, Invoice, Payment, RevenueChartData } from '../billing.service';
import { PatientSearchComponent } from '../../../shared/components/patient-search/patient-search.component';
import { CatalogService } from '../../../core/services/catalog.service';
import { QueueService } from '../../../core/services/queue.service';

@Component({
  selector: 'app-invoice-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PatientSearchComponent],
  templateUrl: './invoice-list.component.html',
  styleUrls: ['./invoice-list.component.scss']
})
export class InvoiceListComponent implements OnInit {
  private service = inject(BillingService);
  private catalogService = inject(CatalogService);
  private queueService = inject(QueueService);

  services = signal<any[]>([]);

  activeTab = 'invoices';
  invoices = signal<Invoice[]>([]);
  payments = signal<Payment[]>([]);
  chartData = signal<RevenueChartData[]>([]);

  todayInvoices = signal(0);
  todayRevenue = signal(0);
  pendingPayments = signal(0);
  weekRevenue = signal(0);
  monthRevenue = signal(0);
  quarterRevenue = signal(0);

  filterDate = '';
  filterStatus = '';
  searchTerm = '';

  showCreateInvoice = false;
  newInvoice: any = { patientSearch: '', patientName: '', patientId: '', items: [{ serviceId: '', code: '', name: '', qty: 1, price: 0, unit: '' }] };

  ngOnInit(): void {
    this.refreshData();
    this.loadServices();
  }

  loadServices() {
    this.catalogService.getServices(undefined, undefined, 1, 200).subscribe({
      next: (res) => this.services.set(res.items.filter((s: any) => s.isActive)),
      error: () => { }
    });
  }

  refreshData() {
    this.service.getInvoices().subscribe(data => this.invoices.set(data));
    this.service.getPayments().subscribe(data => this.payments.set(data));
    this.service.getRevenueChartData().subscribe(data => this.chartData.set(data));

    const stats = this.service.getStats();
    this.todayInvoices.set(stats.todayInvoices);
    this.todayRevenue.set(stats.todayRevenue);
    this.pendingPayments.set(stats.pendingPayments);
    this.weekRevenue.set(stats.weekRevenue);
    this.monthRevenue.set(stats.monthRevenue);
    this.quarterRevenue.set(stats.quarterRevenue);
  }

  filteredInvoices(): Invoice[] {
    let result = this.invoices();
    if (this.filterStatus) result = result.filter(i => i.status === this.filterStatus);
    if (this.searchTerm) result = result.filter(i => i.code.includes(this.searchTerm));
    if (this.filterDate) {
      // Simplified date filter for demo
      const dateStr = new Date(this.filterDate).toLocaleDateString('vi-VN');
      result = result.filter(i => i.date === dateStr);
    }
    return result;
  }

  formatCurrency(v: number): string {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(v);
  }

  getStatusName(s: string): string {
    const map: any = { Paid: 'Đã TT', Pending: 'Chờ TT', Partial: 'TT 1 phần' };
    return map[s] || s;
  }

  viewInvoice(inv: any): void { alert('Chi tiết hoá đơn: ' + inv.code); }
  payInvoice(inv: any): void { alert('Thu tiền cho: ' + inv.code + ' - Còn lại: ' + this.formatCurrency(inv.remaining)); }
  printInvoice(inv: any): void { window.print(); }

  addItem(): void { this.newInvoice.items.push({ serviceId: '', code: '', name: '', qty: 1, price: 0, unit: '' }); }
  removeItem(idx: number): void { this.newInvoice.items.splice(idx, 1); }
  calcTotal(): number { return this.newInvoice.items.reduce((sum: number, i: any) => sum + (i.qty * i.price), 0); }

  onServiceSelect(item: any, serviceId: string) {
    const svc = this.services().find(s => s.id === serviceId);
    if (svc) {
      item.serviceId = svc.id;
      item.code = svc.code;
      item.name = svc.name;
      item.price = svc.unitPrice;
      item.unit = svc.unit;
    }
  }

  submitInvoice(): void {
    const total = this.calcTotal();
    const newCode = 'HD-' + String(this.invoices().length + 1).padStart(3, '0');
    const newInv: Invoice = {
      id: newCode,
      code: newCode,
      patient: this.newInvoice.patientName || this.newInvoice.patientSearch, // Use name if available
      date: new Date().toLocaleDateString('vi-VN'),
      total: total,
      paid: 0,
      remaining: total,
      status: 'Pending'
    };
    this.invoices.update(list => [...list, newInv]);

    this.showCreateInvoice = false;
    this.newInvoice = { patientSearch: '', patientName: '', patientId: '', items: [{ serviceId: '', code: '', name: '', qty: 1, price: 0, unit: '' }] };
  }

  onPatientSelect(patient: any) {
    if (patient) {
      this.newInvoice.patientName = patient.fullName;
      this.newInvoice.patientId = patient.id;

      // Check for pending service indications
      this.queueService.getPatientPendingTicket(patient.id).subscribe({
        next: (ticket: any) => {
          if (ticket && ticket.serviceIds && ticket.serviceIds.length > 0) {
            const indicatedItems: any[] = [];

            ticket.serviceIds.forEach((svcId: string) => {
              const svc = this.services().find(s => s.id === svcId);
              if (svc) {
                indicatedItems.push({
                  serviceId: svc.id,
                  code: svc.code,
                  name: svc.name,
                  qty: 1,
                  price: svc.unitPrice,
                  unit: svc.unit
                });
              }
            });

            if (indicatedItems.length > 0) {
              this.newInvoice.items = indicatedItems;
              // alert('Đã tải ' + indicatedItems.length + ' chỉ định dịch vụ từ phiếu khám ' + ticket.ticketNumber);
            }
          }
        },
        error: () => { } // No pending ticket or error, just ignore
      });
    }
  }
}
