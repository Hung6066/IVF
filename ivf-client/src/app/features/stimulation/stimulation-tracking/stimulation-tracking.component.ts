import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  StimulationService,
  StimulationTrackerDto,
  MedicationScheduleItemDto,
} from '../../../core/services/stimulation.service';

@Component({
  selector: 'app-stimulation-tracking',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './stimulation-tracking.component.html',
  styleUrls: ['./stimulation-tracking.component.scss'],
})
export class StimulationTrackingComponent implements OnInit {
  private service = inject(StimulationService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  cycleId = '';
  activeTab = 'tracker';

  tracker = signal<StimulationTrackerDto | null>(null);
  medications = signal<MedicationScheduleItemDto[]>([]);
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  // Scan form
  showScanForm = false;
  scanForm = {
    scanDate: new Date().toISOString().slice(0, 10),
    cycleDay: 1,
    size12Follicle: null as number | null,
    size14Follicle: null as number | null,
    totalFollicles: null as number | null,
    endometriumThickness: null as number | null,
    endometriumPattern: '',
    e2: null as number | null,
    lh: null as number | null,
    p4: null as number | null,
    notes: '',
  };

  // Trigger form
  showTriggerForm = false;
  triggerForm = {
    triggerDrug: '',
    triggerDate: new Date().toISOString().slice(0, 10),
    triggerTime: '22:00',
    lhLab: null as number | null,
    e2Lab: null as number | null,
    p4Lab: null as number | null,
  };

  // Decision
  showDecisionPanel = false;
  decision = 'Proceed';
  decisionReason = '';

  ngOnInit() {
    this.cycleId = this.route.snapshot.paramMap.get('cycleId') || '';
    if (this.cycleId) this.load();
  }

  load() {
    this.loading.set(true);
    this.service.getTracker(this.cycleId).subscribe({
      next: (data) => {
        this.tracker.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Không tải được dữ liệu kích thích buồng trứng');
        this.loading.set(false);
      },
    });
    this.service.getMedicationSchedule(this.cycleId).subscribe({
      next: (meds) => this.medications.set(meds),
      error: () => {},
    });
  }

  submitScan() {
    this.saving.set(true);
    const scan = {
      ...this.scanForm,
      size12Follicle: this.scanForm.size12Follicle ?? undefined,
      size14Follicle: this.scanForm.size14Follicle ?? undefined,
      totalFollicles: this.scanForm.totalFollicles ?? undefined,
      endometriumThickness: this.scanForm.endometriumThickness ?? undefined,
      e2: this.scanForm.e2 ?? undefined,
      lh: this.scanForm.lh ?? undefined,
      p4: this.scanForm.p4 ?? undefined,
    };
    this.service.recordFollicleScan(this.cycleId, scan).subscribe({
      next: (data) => {
        this.tracker.set(data);
        this.showScanForm = false;
        this.saving.set(false);
        this.successMsg.set('Đã ghi nhận SA nang noãn');
        setTimeout(() => this.successMsg.set(''), 3000);
      },
      error: (err) => {
        this.error.set('Lỗi ghi nhận SA: ' + (err.error?.message || err.message));
        this.saving.set(false);
      },
    });
  }

  submitTrigger() {
    this.saving.set(true);
    this.service
      .recordTriggerShot(this.cycleId, {
        ...this.triggerForm,
        triggerTime: this.triggerForm.triggerTime + ':00',
        lhLab: this.triggerForm.lhLab ?? undefined,
        e2Lab: this.triggerForm.e2Lab ?? undefined,
        p4Lab: this.triggerForm.p4Lab ?? undefined,
      })
      .subscribe({
        next: (data) => {
          this.tracker.set(data);
          this.showTriggerForm = false;
          this.saving.set(false);
          this.successMsg.set('Đã ghi nhận tiêm rụng trứng');
          setTimeout(() => this.successMsg.set(''), 3000);
        },
        error: (err) => {
          this.error.set('Lỗi ghi nhận trigger: ' + (err.error?.message || err.message));
          this.saving.set(false);
        },
      });
  }

  submitDecision() {
    this.saving.set(true);
    this.service.evaluateReadiness(this.cycleId, this.decision, this.decisionReason).subscribe({
      next: () => {
        this.showDecisionPanel = false;
        this.saving.set(false);
        this.successMsg.set(`Đã ghi nhận quyết định: ${this.decision}`);
        setTimeout(() => this.successMsg.set(''), 3000);
      },
      error: (err) => {
        this.error.set('Lỗi: ' + (err.error?.message || err.message));
        this.saving.set(false);
      },
    });
  }

  formatDate(d?: string): string {
    if (!d) return '—';
    return new Date(d).toLocaleDateString('vi-VN');
  }
}
