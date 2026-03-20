import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PregnancyService, PregnancyDto, FollowUpItemDto } from '../../../core/services/pregnancy.service';

@Component({
  selector: 'app-pregnancy-result',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './pregnancy-result.component.html',
  styleUrls: ['./pregnancy-result.component.scss']
})
export class PregnancyResultComponent implements OnInit {
  private service = inject(PregnancyService);
  private route = inject(ActivatedRoute);

  cycleId = '';
  pregnancy = signal<PregnancyDto | null>(null);
  followUp = signal<FollowUpItemDto[]>([]);
  loading = signal(false);

  ngOnInit() {
    this.cycleId = this.route.snapshot.paramMap.get('cycleId') || '';
    if (this.cycleId) this.load();
  }

  load() {
    this.loading.set(true);
    this.service.getByCycle(this.cycleId).subscribe({
      next: (data) => {
        this.pregnancy.set(data);
        this.loading.set(false);
        if (data.isPregnant) {
          this.service.getFollowUp(this.cycleId).subscribe({
            next: (items) => this.followUp.set(items),
            error: () => {}
          });
        }
      },
      error: () => this.loading.set(false)
    });
  }

  formatDate(d?: string) { return d ? new Date(d).toLocaleDateString('vi-VN') : '\u2014'; }
}
