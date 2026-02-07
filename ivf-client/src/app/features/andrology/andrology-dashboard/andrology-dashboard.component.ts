import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AndrologyService, SemenAnalysis, SpermWashing, AndrologyQueueItem } from './andrology.service';
import { PatientSearchComponent } from '../../../shared/components/patient-search/patient-search.component';
import { CycleSearchComponent } from '../../../shared/components/cycle-search/cycle-search.component';

@Component({
    selector: 'app-andrology-dashboard',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule, PatientSearchComponent, CycleSearchComponent],
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
    concentrationDist = signal<Record<string, number>>({});

    totalConcentrations = this.concentrationDist; // We will use a getter or simple logic in template for total

    getDistPercentage(key: string): number {
        const dist = this.concentrationDist();
        const total = Object.values(dist).reduce((a, b) => a + b, 0);
        if (total === 0) return 0;
        return Math.round(((dist[key] || 0) / total) * 100);
    }

    showNewAnalysis = false;
    showNewWashing = false;
    filterDate = '';
    filterStatus = '';
    searchTerm = '';

    newAnalysis: any = {
        analysisDate: new Date().toISOString(),
        analysisType: 'PreWash',
        volume: null,
        concentration: null,
        progressiveMotility: null,
        nonProgressiveMotility: null,
        immotile: null,
        normalMorphology: null,
        vitality: null
    };
    newWashing: any = {
        cycleCode: '',
        patientName: '',
        method: 'Gradient',
        preWashConcentration: null,
        postWashConcentration: null,
        postWashMotility: null,
        status: 'Processing',
        washDate: new Date().toISOString()
    };

    // Store selected patient ID for filtering cycles
    selectedWashingPatientId: string | null = null;

    // Store suggested patients based on cycle
    suggestedPatients: any[] = [];

    selectedAnalysisPatientId: string | null = null;

    constructor() {
        // Effects to log or handle signal changes if needed
    }

    onPatientChange(patient: any) {
        if (patient) {
            this.newAnalysis.patientId = patient.id;
            this.newAnalysis.patientName = patient.fullName;
            this.newAnalysis.patientCode = patient.patientCode;
            this.selectedAnalysisPatientId = patient.id;
        } else {
            this.selectedAnalysisPatientId = null;
        }
    }

    onCycleChange(cycle: any) {
        if (cycle) {
            this.newWashing.cycleCode = cycle.cycleCode;
            this.newWashing.cycleId = cycle.id;

            // Auto-fill patient if not already set
            if (cycle.couple) {
                const wife = cycle.couple.wife;
                const husband = cycle.couple.husband;

                // Prepare suggestions
                this.suggestedPatients = [];
                if (husband) this.suggestedPatients.push(husband);
                if (wife) this.suggestedPatients.push({ ...wife, fullName: wife.fullName + ' (Vợ)' });

                if (husband) {
                    this.newWashing.patientName = husband.fullName;
                    this.newWashing.patientId = husband.id; // Set ID directly
                    this.selectedWashingPatientId = husband.id;
                } else if (wife) {
                    this.newWashing.patientName = wife.fullName + " (Vợ)";
                    this.newWashing.patientId = wife.id; // Set ID directly
                    this.selectedWashingPatientId = wife.id;
                }
            }
        }
    }

    onWashingPatientChange(patient: any) {
        if (patient) {
            this.newWashing.patientName = patient.fullName;
            this.newWashing.patientId = patient.id; // Set ID directly
            this.selectedWashingPatientId = patient.id;
        } else {
            this.selectedWashingPatientId = null;
            this.newWashing.patientId = null;
        }
    }

    ngOnInit(): void {
        this.refreshQueue();
        this.loadData();

        // Auto-refresh queue every 10 seconds
        setInterval(() => this.refreshQueue(), 10000);
    }

    loadData(): void {
        // Load independent data streams
        this.service.getAnalyses().subscribe(data => this.analyses.set(data));
        this.service.getWashings().subscribe(data => this.washings.set(data));
        this.refreshStatistics();
    }

    refreshStatistics() {
        this.service.getStatistics().subscribe(stats => {
            if (stats) {
                this.todayAnalysis.set(stats.todayAnalyses);
                this.todayWashing.set(stats.todayWashings);
                this.pendingCount.set(stats.pendingAnalyses);
                this.avgConcentration.set(Math.round(stats.avgConcentration * 10) / 10);
                this.concentrationDist.set(stats.concentrationDistribution || {});
            }
        });
    }

    refreshQueue() {
        this.service.getQueue().subscribe(data => {
            this.queue.set(data);
            this.queueCount.set(data.length);
        });
        this.refreshStatistics();
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
        this.service.startService(q.id).subscribe({
            next: () => {
                this.activeTab = 'analysis';
                this.showNewAnalysis = true;
                this.newAnalysis = {
                    patientId: q.patientId,
                    patientName: q.patientName,
                    patientCode: q.patientCode,
                    analysisDate: new Date().toISOString(),
                    analysisType: 'PreWash',
                    volume: null,
                    concentration: null,
                    progressiveMotility: null,
                    nonProgressiveMotility: null,
                    immotile: null,
                    normalMorphology: null,
                    vitality: null
                };
                this.selectedAnalysisPatientId = q.patientId;
                this.refreshQueue();
            },
            error: () => alert('Lỗi bắt đầu xét nghiệm')
        });
    }

    skipPatient(q: AndrologyQueueItem) {
        if (confirm(`Bỏ qua bệnh nhân ${q.patientName}?`)) {
            this.service.skipTicket(q.id).subscribe({
                next: () => this.refreshQueue(),
                error: () => alert('Lỗi khi bỏ qua')
            });
        }
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

    // Edit Mode Tracking
    editingAnalysisId: string | null = null;
    editingWashingId: string | null = null;

    // Print Data
    printAnalysisData: SemenAnalysis | null = null;
    printWashingData: SpermWashing | null = null;

    editAnalysis(item: SemenAnalysis): void {
        this.editingAnalysisId = item.id;
        this.newAnalysis = { ...item };
        // Ensure manual mapping if needed or use object spread if fields match
        // Need to fill patient info to ensure search component shows it
        this.selectedAnalysisPatientId = null; // Reset to force re-bind if needed, or just keep name
        // actually patientId and name are in item.
        this.showNewAnalysis = true;
    }

    printResult(item: SemenAnalysis): void {
        this.printAnalysisData = item;
        setTimeout(() => window.print(), 100);
    }

    editWashing(item: SpermWashing): void {
        this.editingWashingId = item.id;
        this.newWashing = { ...item };
        this.showNewWashing = true;
    }

    printWashing(item: SpermWashing): void {
        this.printWashingData = item;
        setTimeout(() => window.print(), 100);
    }

    submitAnalysis(): void {
        if (this.editingAnalysisId) {
            // Update
            const macroData = {
                volume: this.newAnalysis.volume,
                appearance: this.newAnalysis.appearance,
                liquefaction: this.newAnalysis.liquefaction,
                ph: this.newAnalysis.ph
            };
            const microData = {
                concentration: this.newAnalysis.concentration,
                totalCount: this.newAnalysis.totalCount,
                progressiveMotility: this.newAnalysis.progressiveMotility,
                nonProgressiveMotility: this.newAnalysis.nonProgressiveMotility,
                immotile: this.newAnalysis.immotile,
                normalMorphology: this.newAnalysis.normalMorphology,
                vitality: this.newAnalysis.vitality
            };

            // Call both updates sequentially
            this.service.updateAnalysisMacroscopic(this.editingAnalysisId, macroData).subscribe(() => {
                this.service.updateAnalysisMicroscopic(this.editingAnalysisId!, microData).subscribe(() => {
                    this.loadData(); // Reload to refresh list
                    this.showNewAnalysis = false;
                    this.editingAnalysisId = null;
                    this.resetAnalysisForm();
                });
            });
        } else {
            // Create
            this.service.createAnalysis(this.newAnalysis).subscribe(res => {
                this.analyses.update(list => [res, ...list]);
                this.showNewAnalysis = false;
                this.resetAnalysisForm();
            });
        }
    }

    submitWashing(): void {
        if (this.editingWashingId) {
            // Backend only allows updating results/notes via updateWashing endpoint
            const updateData = {
                notes: this.newWashing.notes,
                preWashConcentration: this.newWashing.preWashConcentration,
                postWashConcentration: this.newWashing.postWashConcentration,
                postWashMotility: this.newWashing.postWashMotility
            };
            this.service.updateWashing(this.editingWashingId, updateData).subscribe(() => {
                this.loadData();
                this.showNewWashing = false;
                this.editingWashingId = null;
                this.resetWashingForm();
            });
        } else {
            this.service.createWashing(this.newWashing).subscribe(res => {
                this.washings.update(list => [res, ...list]);
                this.showNewWashing = false;
                this.resetWashingForm();
            });
        }
    }

    resetAnalysisForm() {
        this.newAnalysis = {
            analysisDate: new Date().toISOString(),
            analysisType: 'PreWash',
            volume: null,
            concentration: null,
            progressiveMotility: null,
            nonProgressiveMotility: null,
            immotile: null,
            normalMorphology: null,
            vitality: null
        };
        this.selectedAnalysisPatientId = null;
    }

    resetWashingForm() {
        this.newWashing = {
            cycleCode: '',
            patientName: '',
            method: 'Gradient',
            preWashConcentration: null,
            postWashConcentration: null,
            postWashMotility: null,
            washDate: new Date().toISOString()
        };
        this.selectedWashingPatientId = null;
    }
}
