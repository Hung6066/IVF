import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { LabService } from './lab.service';
import { QueueItem, EmbryoCard, ScheduleItem, CryoLocation, LabStats } from './lab-dashboard.models';

// Import Child Components
import { LabQueueComponent } from './components/lab-queue.component';
import { LabEmbryosComponent } from './components/lab-embryos.component';
import { LabScheduleComponent } from './components/lab-schedule.component';
import { LabCryoComponent } from './components/lab-cryo.component';

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
  stats = signal<LabStats>({
    eggRetrievalCount: 0,
    cultureCount: 0,
    transferCount: 0,
    freezeCount: 0,
    totalFrozenEmbryos: 0,
    totalFrozenEggs: 0,
    totalFrozenSperm: 0
  });

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
      this.cryoLocations.update(list => [...list, location]);
    });
  }

  exportExcel() {
    alert('Đang xuất file Excel...');
  }

  printReport() {
    window.print();
  }
}
