import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { CycleService } from './cycle.service';
import { TreatmentCycle, Embryo, Ultrasound } from '../../../core/models/api.models';

@Component({
    selector: 'app-cycle-detail',
    standalone: true,
    imports: [CommonModule, RouterModule],
    templateUrl: './cycle-detail.component.html',
    styleUrls: ['./cycle-detail.component.scss']
})
export class CycleDetailComponent implements OnInit {
    private route = inject(ActivatedRoute);
    private service = inject(CycleService);

    cycle = signal<TreatmentCycle | null>(null);
    ultrasounds = signal<Ultrasound[]>([]);
    embryos = signal<Embryo[]>([]);

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

    ngOnInit(): void {
        this.route.params.subscribe(params => {
            const id = params['id'];
            if (id) {
                this.loadData(id);
            }
        });
    }

    loadData(id: string) {
        this.service.getCycle(id).subscribe(c => this.cycle.set(c));
        this.service.getUltrasounds(id).subscribe(u => this.ultrasounds.set(u));
        this.service.getEmbryos(id).subscribe(e => this.embryos.set(e));
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
        return this.cycle()?.phase === phase;
    }

    isCompletedPhase(phase: string, index: number): boolean {
        const currentPhase = this.cycle()?.phase;
        if (!currentPhase) return false;

        const currentIndex = this.phases.findIndex(p => p.key === currentPhase);
        return index < currentIndex;
    }

    advancePhase(): void {
        const c = this.cycle();
        if (!c) return;

        const currentIndex = this.phases.findIndex(p => p.key === c.phase);
        if (currentIndex < this.phases.length - 1) {
            const nextPhase = this.phases[currentIndex + 1].key;
            this.service.advancePhase(c.id, nextPhase).subscribe(updatedCycle => {
                this.cycle.set(updatedCycle);
            });
        }
    }

    formatDate(date: string): string {
        return new Date(date).toLocaleDateString('vi-VN');
    }
}
