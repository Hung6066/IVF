import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { LabService } from './lab.service';
import { QueueItem, EmbryoCard, ScheduleItem, CryoLocation, LabStats, EmbryoReport } from './lab-dashboard.models';

// Import Child Components
import { LabQueueComponent } from './components/lab-queue/lab-queue.component';
import { LabEmbryosComponent } from './components/lab-embryos/lab-embryos.component';
import { LabScheduleComponent } from './components/lab-schedule/lab-schedule.component';
import { LabCryoComponent } from './components/lab-cryo/lab-cryo.component';

@Component({
  selector: 'app-lab-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    LabQueueComponent,
    LabEmbryosComponent,
    LabScheduleComponent,
    LabCryoComponent
  ],
  templateUrl: './lab-dashboard.component.html',
  styleUrls: ['./lab-dashboard.component.scss']
})
export class LabDashboardComponent implements OnInit {
  private labService = inject(LabService);

  activeTab = 'queue';
  currentDate = new Date();

  // Signals for reactive state
  queue = signal<QueueItem[]>([]);
  embryos = signal<EmbryoCard[]>([]);
  schedule = signal<ScheduleItem[]>([]);
  cryoLocations = signal<CryoLocation[]>([]);
  activeCycles = signal<any[]>([]); // New signal for active cycles
  doctors = signal<any[]>([]); // New signal for doctors
  stats = signal<LabStats>({
    eggRetrievalCount: 0,
    cultureCount: 0,
    transferCount: 0,
    freezeCount: 0,
    totalFrozenEmbryos: 0,
    totalFrozenEggs: 0,
    totalFrozenSperm: 0
  });
  embryoReport = signal<EmbryoReport[]>([]);

  ngOnInit(): void {
    this.refreshData();
  }

  setActiveTab(tab: string) {
    this.activeTab = tab;
  }

  refreshData() {
    this.labService.getQueue().subscribe(data => this.queue.set(data));
    this.labService.getEmbryos().subscribe(data => this.embryos.set(data));
    this.labService.getSchedule(this.currentDate).subscribe(data => this.schedule.set(data));
    this.labService.getCryoLocations().subscribe(data => this.cryoLocations.set(data));
    this.labService.getStats().subscribe(data => this.stats.set(data));
    this.labService.getActiveCycles().subscribe(data => this.activeCycles.set(data));
    this.labService.getDoctors().subscribe(data => this.doctors.set(data));
    this.labService.getEmbryoReport(this.currentDate).subscribe(data => this.embryoReport.set(data));
  }

  changeDay(days: number) {
    const newDate = new Date(this.currentDate);
    newDate.setDate(newDate.getDate() + days);
    this.currentDate = newDate;
    this.labService.getSchedule(this.currentDate).subscribe(data => this.schedule.set(data));
  }

  goToday() {
    this.currentDate = new Date();
    this.labService.getSchedule(this.currentDate).subscribe(data => this.schedule.set(data));
  }

  // --- Event Handlers ---

  onSaveEmbryo(data: any) {
    const isEdit = !!data.id;
    const operation = isEdit
      ? this.labService.updateEmbryo(data.id, data)
      : this.labService.createEmbryo(data);

    operation.subscribe({
      next: () => {
        alert(isEdit ? 'Đã cập nhật phôi thành công' : 'Đã thêm phôi thành công');
        this.refreshData();
      },
      error: (err) => alert(`Lỗi khi ${isEdit ? 'cập nhật' : 'thêm'} phôi: ` + (err.error?.detail || err.message))
    });
  }

  onDeleteEmbryo(id: string) {
    this.labService.deleteEmbryo(id).subscribe({
      next: () => {
        alert('Đã xóa phôi thành công');
        this.refreshData();
      },
      error: (err) => alert('Lỗi khi xóa phôi: ' + (err.error?.detail || err.message))
    });
  }

  onAddSchedule(data: any) {
    this.labService.scheduleProcedure(data).subscribe({
      next: () => {
        alert('Đã lên lịch thủ thuật thành công');
        this.refreshData();
      },
      error: (err) => alert('Lỗi khi lên lịch: ' + (err.error?.detail || err.message))
    });
  }

  onCallPatient(q: QueueItem) {
    this.labService.callPatient(q.id).subscribe(() => {
      alert(`Đang gọi ${q.patientName}`);
      this.refreshData(); // Refresh queue
    });
  }

  onStartProcedure(q: QueueItem) {
    this.activeTab = 'schedule';
    alert('Mời bệnh nhân vào phòng thủ thuật/lấy mẫu');
  }

  onSelectEmbryo(embryo: EmbryoCard) {
    console.log('Selected embryo', embryo);
    // Navigate to details if needed
  }

  onToggleSchedule(item: ScheduleItem) {
    this.labService.toggleScheduleStatus(item).subscribe(() => {
      // Optimistic update or refresh
      item.status = item.status === 'done' ? 'pending' : 'done';
      this.schedule.update(s => [...s]);
    });
  }

  onAddCryoLocation(location: CryoLocation) {
    this.labService.addCryoLocation(location).subscribe(() => {
      this.refreshData();
    });
  }

  onUpdateCryoLocation(location: any) {
    this.labService.updateCryoLocation(location.tank, { used: location.used, specimenType: location.specimenType })
      .subscribe({
        next: () => this.refreshData(),
        error: (err) => alert('Lỗi cập nhật: ' + (err.error?.detail || err.message))
      });
  }

  onDeleteCryoLocation(tank: string) {
    this.labService.deleteCryoLocation(tank).subscribe({
      next: () => {
        this.refreshData();
      },
      error: (err) => alert('Không thể xóa tủ: ' + (err.error?.detail || err.message))
    });
  }

  exportExcel() {
    alert('Đang xuất file Excel...');
  }

  printReport() {
    window.print();
  }
}
