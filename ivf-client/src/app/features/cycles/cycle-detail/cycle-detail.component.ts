import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { CycleService } from '../../../core/services/cycle.service';
import { UltrasoundService } from '../../../core/services/ultrasound.service';
import { EmbryoService } from '../../../core/services/embryo.service';
import { TreatmentCycle, Embryo, Ultrasound } from '../../../core/models/api.models';
import { GlobalNotificationService } from '../../../core/services/global-notification.service';
import { IndicationTabComponent } from './tabs/indication-tab.component';
import { StimulationTabComponent } from './tabs/stimulation-tab.component';
import { CultureTabComponent } from './tabs/culture-tab.component';
import { TransferTabComponent } from './tabs/transfer-tab.component';
import { LutealTabComponent } from './tabs/luteal-tab.component';
import { PregnancyTabComponent } from './tabs/pregnancy-tab.component';
import { BirthTabComponent } from './tabs/birth-tab.component';
import { AdverseEventsTabComponent } from './tabs/adverse-events-tab.component';

interface Tab {
    key: string;
    label: string;
    icon: string;
}

@Component({
    selector: 'app-cycle-detail',
    standalone: true,
    imports: [
        CommonModule, RouterModule,
        IndicationTabComponent, StimulationTabComponent, CultureTabComponent,
        TransferTabComponent, LutealTabComponent, PregnancyTabComponent,
        BirthTabComponent, AdverseEventsTabComponent
    ],
    templateUrl: './cycle-detail.component.html',
    styleUrls: ['./cycle-detail.component.scss']
})
export class CycleDetailComponent implements OnInit {
    private route = inject(ActivatedRoute);
    private cycleService = inject(CycleService);
    private ultrasoundService = inject(UltrasoundService);
    private embryoService = inject(EmbryoService);
    private notificationService = inject(GlobalNotificationService);

    cycle = signal<TreatmentCycle | null>(null);
    ultrasounds = signal<Ultrasound[]>([]);
    embryos = signal<Embryo[]>([]);
    cycleId = signal<string>('');
    activeTab = signal<string>('indication');

    phases = [
        { key: 'Consultation', name: 'Tư vấn' },
        { key: 'OvarianStimulation', name: 'Kích thích' },
        { key: 'TriggerShot', name: 'Trigger' },
        { key: 'EggRetrieval', name: 'Chọc hút' },
        { key: 'EmbryoCulture', name: 'Nuôi phôi' },
        { key: 'EmbryoTransfer', name: 'Chuyển phôi' },
        { key: 'LutealSupport', name: 'Hậu chuyển' },
        { key: 'PregnancyTest', name: 'Thử thai' },
        { key: 'Completed', name: 'Hoàn thành' }
    ];

    tabs: Tab[] = [
        { key: 'indication', label: 'Chỉ định', icon: '📋' },
        { key: 'stimulation', label: 'Kích thích', icon: '💉' },
        { key: 'culture', label: 'Nuôi phôi', icon: '🔬' },
        { key: 'transfer', label: 'Chuyển phôi', icon: '🎯' },
        { key: 'luteal', label: 'Hoàng thể', icon: '💊' },
        { key: 'pregnancy', label: 'Thai kỳ', icon: '🤰' },
        { key: 'birth', label: 'Sinh', icon: '👶' },
        { key: 'adverse', label: 'Biến chứng', icon: '⚠️' }
    ];

    ngOnInit(): void {
        this.route.params.subscribe(params => {
            const id = params['id'];
            if (id) {
                this.cycleId.set(id);
                this.loadData(id);
            }
        });
    }

    loadData(id: string) {
        this.cycleService.getCycle(id).subscribe((c: TreatmentCycle) => this.cycle.set(c));
        this.ultrasoundService.getUltrasoundsByCycle(id).subscribe((u: Ultrasound[]) => this.ultrasounds.set(u));
        this.embryoService.getEmbryosByCycle(id).subscribe((e: Embryo[]) => this.embryos.set(e));
    }

    selectTab(tabKey: string): void {
        this.activeTab.set(tabKey);
    }

    onTabSaved(): void {
        this.notificationService.success('Thành công', 'Đã lưu thành công!');
        // Reload data to reflect any phase changes (e.g. Auto-Advance)
        this.loadData(this.cycleId());
    }

    getMethodName(method?: string): string {
        const names: Record<string, string> = { 'QHTN': 'Quan hệ', 'IUI': 'IUI', 'ICSI': 'ICSI', 'IVM': 'IVM' };
        return names[method || ''] || method || '';
    }

    getPhaseName(phase?: string): string {
        return this.phases.find(p => p.key === phase)?.name || phase || '';
    }

    getOutcomeName(outcome?: string): string {
        const names: Record<string, string> = {
            'Ongoing': 'Đang điều trị', 'Pregnant': 'Có thai', 'NotPregnant': 'Không thai',
            'Cancelled': 'Huỷ', 'FrozenAll': 'Trữ phôi toàn bộ'
        };
        return names[outcome || ''] || outcome || '';
    }

    getEmbryoStatus(status: string): string {
        const names: Record<string, string> = {
            'Developing': 'Đang phát triển', 'Transferred': 'Đã chuyển', 'Frozen': 'Đông lạnh',
            'Thawed': 'Đã rã', 'Discarded': 'Loại bỏ', 'Arrested': 'Ngừng phát triển'
        };
        return names[status] || status;
    }

    isActivePhase(phase: string): boolean {
        return this.cycle()?.currentPhase === phase;
    }

    isCompletedPhase(phase: string, index: number): boolean {
        const currentPhase = this.cycle()?.currentPhase;
        if (!currentPhase) return false;

        const currentIndex = this.phases.findIndex(p => p.key === currentPhase);
        return index < currentIndex;
    }

    advancePhase(): void {
        const c = this.cycle();
        if (!c) return;

        const currentIndex = this.phases.findIndex(p => p.key === c.currentPhase);
        if (currentIndex < this.phases.length - 1) {
            const nextPhase = this.phases[currentIndex + 1].key;
            this.cycleService.advanceCyclePhase(c.id, nextPhase).subscribe((updatedCycle: TreatmentCycle) => {
                this.cycle.set(updatedCycle);
            });
        }
    }

    formatDate(date: string): string {
        return new Date(date).toLocaleDateString('vi-VN');
    }
}
