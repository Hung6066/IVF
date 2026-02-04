import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AndrologyService, SemenAnalysis, SpermWashing, AndrologyQueueItem } from './andrology.service';

@Component({
    selector: 'app-andrology-dashboard',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule],
    templateUrl: './andrology-dashboard.component.html',
    styleUrls: ['./andrology-dashboard.component.scss']
})
export class AndrologyDashboardComponent implements OnInit {
    private service = inject(AndrologyService);

    activeTab = 'queue';
    queue = signal<AndrologyQueueItem[]>([]);
    queueCount = signal(0);

    analyses = signal<SemenAnalysis[]>([]);
    washings = signal<SpermWashing[]>([]);
    todayAnalysis = signal(0);
    todayWashing = signal(0);
    pendingCount = signal(0);
    avgConcentration = signal(0);

    showNewAnalysis = false;
    showNewWashing = false;
    filterDate = '';
    filterStatus = '';
    searchTerm = '';

    newAnalysis: any = {};
    newWashing: any = { cycleCode: '', patientName: '', method: 'Gradient', prewashConc: null, postwashConc: null, postwashMotility: null };

    ngOnInit(): void {
        this.refreshQueue();
        this.loadData();
    }

    refreshQueue() {
        this.service.getQueue().subscribe(data => {
            this.queue.set(data);
            this.queueCount.set(data.length);
        });
    }

    loadData(): void {
        this.service.getAnalyses().subscribe(data => {
            this.analyses.set(data);
            this.todayAnalysis.set(8); // Mock stats
            this.pendingCount.set(3);
            this.avgConcentration.set(38);
        });

        this.service.getWashings().subscribe(data => {
            this.washings.set(data);
            this.todayWashing.set(5);
        });
    }

    formatTime(date: string): string {
        return new Date(date).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    }

    formatDate(date: string): string {
        return new Date(date).toLocaleDateString('vi-VN');
    }

    callPatient(q: AndrologyQueueItem) {
        this.service.callPatient(q.id).subscribe(() => {
            alert(`Đang gọi ${q.patientName}`);
            this.refreshQueue();
        });
    }

    startExam(q: AndrologyQueueItem) {
        this.activeTab = 'analysis';
        this.showNewAnalysis = true;
        this.newAnalysis = { patientName: q.patientName, patientCode: q.patientCode };
    }

    filteredAnalyses(): SemenAnalysis[] {
        let result = this.analyses();
        if (this.filterStatus) result = result.filter(a => a.status === this.filterStatus);
        if (this.searchTerm) result = result.filter(a => a.patientCode.includes(this.searchTerm) || a.patientName.includes(this.searchTerm));
        if (this.filterDate) {
            // simplified date match
            result = result.filter(a => a.analysisDate.startsWith(this.filterDate));
        }
        return result;
    }

    getStatusName(status: string): string {
        const names: Record<string, string> = { 'Pending': 'Chờ XN', 'Processing': 'Đang XN', 'Completed': 'Hoàn thành' };
        return names[status] || status;
    }

    editAnalysis(item: SemenAnalysis): void { console.log('Edit', item); }
    printResult(item: SemenAnalysis): void { console.log('Print', item); }

    submitAnalysis(): void {
        this.service.createAnalysis(this.newAnalysis).subscribe(res => {
            this.analyses.update(list => [res, ...list]);
            this.showNewAnalysis = false;
        });
    }

    submitWashing(): void {
        this.service.createWashing(this.newWashing).subscribe(res => {
            this.washings.update(list => [res, ...list]);
            this.showNewWashing = false;
            this.newWashing = { cycleCode: '', patientName: '', method: 'Gradient', prewashConc: null, postwashConc: null, postwashMotility: null };
        });
    }
}
