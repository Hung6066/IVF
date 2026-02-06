import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { ReceptionService, CheckinRecord } from './reception.service';
import { Patient } from '../../../core/models/api.models';
import { CatalogService } from '../../../core/services/catalog.service';
import { Observable, forkJoin } from 'rxjs';
import { FingerprintHubService } from '../../../core/services/fingerprint/fingerprint-hub.service';
import { GlobalNotificationService } from '../../../core/services/global-notification.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-reception-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './reception-dashboard.component.html',
  styleUrls: ['./reception-dashboard.component.scss']
})
export class ReceptionDashboardComponent implements OnInit {
  private service = inject(ReceptionService);
  private router = inject(Router);
  private catalogService = inject(CatalogService);
  private notificationService = inject(GlobalNotificationService);
  private fingerprintService = inject(FingerprintHubService);
  private authService = inject(AuthService);

  services = signal<any[]>([]);

  searchTerm = '';
  searchResults = signal<Patient[]>([]);
  queueTuVan = signal(12);
  queueSieuAm = signal(8);
  queueTiem = signal(5);
  queueXN = signal(15);
  totalPayment = signal(185000000);
  paymentCount = signal(42);
  pendingPayment = signal(8);
  recentCheckins = signal<CheckinRecord[]>([]);

  showCheckinModal = false;
  selectedPatient: Patient | null = null;
  checkinData: any = { department: ['TV'], priority: 'Normal', doctor: '', notes: '', selectedServices: [] };

  departments = [
    { code: 'TV', name: 'Tư vấn (TV)' },
    { code: 'US', name: 'Siêu âm (US)' },
    { code: 'TM', name: 'Tiêm (TM)' },
    { code: 'XN', name: 'Xét nghiệm (XN)' },
    { code: 'NAM', name: 'Nam khoa' }
  ];

  ngOnInit(): void {
    this.refreshQueue();
    this.loadServices();

    // Subscribe to identification results
    this.fingerprintService.identificationResult$.subscribe(result => {
      if (result.success && result.patientId) {
        this.notificationService.success('Định danh thành công', `Tìm thấy bệnh nhân: ${result.patientId}`);
        this.router.navigate(['/patients', result.patientId]);
      } else {
        this.notificationService.error('Định danh thất bại', result.errorMessage || 'Không tìm thấy bệnh nhân');
      }
    });

    const token = this.authService.getToken();
    if (!this.fingerprintService.isConnectedState() && token) {
      this.fingerprintService.connect(token);
    }
  }

  async scanFingerprint() {
    if (!this.fingerprintService.isConnectedState()) {
      const token = this.authService.getToken();
      if (!token) {
        this.notificationService.error('Lỗi', 'Chưa đăng nhập hệ thống. Vui lòng đăng nhập lại.');
        return;
      }

      const success = await this.fingerprintService.connect(token);
      if (!success) {
        this.notificationService.error('Lỗi', 'Không thể kết nối đến máy chủ vân tay. Vui lòng thử lại.');
        return;
      }
    }

    this.fingerprintService.requestIdentification().catch(err => {
      this.notificationService.error('Lỗi', 'Không thể gửi yêu cầu: ' + err.message);
    });
  }

  loadServices() {
    this.catalogService.getServices(undefined, undefined, 1, 200).subscribe({
      next: (res) => this.services.set(res.items.filter((s: any) => s.isActive)),
      error: () => { }
    });
  }

  refreshQueue() {
    this.service.getRecentCheckins().subscribe(data => this.recentCheckins.set(data));
  }

  searchPatient(): void {
    this.service.searchPatients(this.searchTerm).subscribe(res => {
      this.searchResults.set(res.items || []);
    });
  }

  selectPatient(patient: Patient): void {
    this.router.navigate(['/patients', patient.id]);
  }

  checkinPatient(patient: Patient): void {
    this.selectedPatient = patient;
    this.checkinData = { department: ['TV'], priority: 'Normal', doctor: '', notes: '', selectedServices: [] };
    this.showCheckinModal = true;
  }

  toggleService(serviceId: string) {
    const idx = this.checkinData.selectedServices.indexOf(serviceId);
    if (idx >= 0) {
      this.checkinData.selectedServices.splice(idx, 1);
    } else {
      this.checkinData.selectedServices.push(serviceId);
    }
  }

  isServiceSelected(serviceId: string): boolean {
    return this.checkinData.selectedServices.includes(serviceId);
  }

  getSelectedServicesTotal(): number {
    return this.checkinData.selectedServices.reduce((sum: number, id: string) => {
      const svc = this.services().find(s => s.id === id);
      return sum + (svc?.unitPrice || 0);
    }, 0);
  }

  getDeptCode(category: any): string {
    // Categories: 0:Lab, 1:US, 2:Proc, 3:Meds, 4:Cons, 5:IVF, 6:Andro, 7:Sperm, 8:Other
    // Map string or int
    const cat = String(category).toLowerCase();

    if (cat === '0' || cat === 'labtest') return 'XN';
    if (cat === '1' || cat === 'ultrasound') return 'US';
    if (cat === '2' || cat === 'procedure') return 'TM'; // Procedure -> TM (Injection/Procedure)
    if (cat === '3' || cat === 'medication') return 'NT';
    if (cat === '4' || cat === 'consultation') return 'TV';
    if (cat === '5' || cat === 'ivf') return 'TV'; // IVF -> TV
    if (cat === '6' || cat === 'andrology') return 'NAM';
    if (cat === '7' || cat === 'spermbank') return 'NAM';

    const dept = Array.isArray(this.checkinData.department) ? this.checkinData.department[0] : this.checkinData.department;
    return dept || 'TV'; // Fallback
  }

  toggleDept(code: string) {
    if (!Array.isArray(this.checkinData.department)) {
      this.checkinData.department = [this.checkinData.department || 'TV'];
    }
    const idx = this.checkinData.department.indexOf(code);
    if (idx >= 0) {
      this.checkinData.department.splice(idx, 1);
    } else {
      this.checkinData.department.push(code);
    }
  }

  isDeptSelected(code: string): boolean {
    if (!Array.isArray(this.checkinData.department)) return this.checkinData.department === code;
    return this.checkinData.department.includes(code);
  }

  submitCheckin(): void {
    if (!this.selectedPatient) return;

    const requests: Observable<any>[] = [];
    const deptsToIssue = new Set<string>();

    // Add manually selected departments
    const manualArr = Array.isArray(this.checkinData.department) ? this.checkinData.department : [this.checkinData.department];
    manualArr.forEach((d: string) => deptsToIssue.add(d));

    const servicesByDept = new Map<string, string[]>();

    if (this.checkinData.selectedServices.length > 0) {
      this.checkinData.selectedServices.forEach((id: string) => {
        const svc = this.services().find(s => s.id === id);
        if (svc) {
          const dept = this.getDeptCode(svc.category);
          deptsToIssue.add(dept);
          if (!servicesByDept.has(dept)) servicesByDept.set(dept, []);
          servicesByDept.get(dept)!.push(id);
        }
      });
    }

    deptsToIssue.forEach(dept => {
      const ids = servicesByDept.get(dept);
      const req = this.service.issueTicket(
        this.selectedPatient!.id,
        dept,
        this.checkinData.priority,
        this.checkinData.notes,
        undefined,
        ids
      );
      requests.push(req);
    });

    if (requests.length > 0) {
      forkJoin(requests).subscribe({
        next: (results) => {
          this.notificationService.success('Thành công', `Đã phát ${results.length} phiếu khám thành công!`);
          this.showCheckinModal = false;
          this.refreshQueue();
        },
        error: (err) => {
          this.notificationService.error('Lỗi', 'Có lỗi xảy ra: ' + (err.error || err.message));
        }
      });
    }
  }

  formatDate(date: string): string {
    if (!date) return 'N/A';
    return new Date(date).toLocaleDateString('vi-VN');
  }

  formatTime(date: string): string {
    return new Date(date).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value);
  }
}
