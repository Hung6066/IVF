import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import {
  StimulationService,
  FollicleChartPointDto,
} from '../../../core/services/stimulation.service';

@Component({
  selector: 'app-follicle-chart',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './follicle-chart.component.html',
  styleUrls: ['./follicle-chart.component.scss'],
})
export class FollicleChartComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(StimulationService);

  cycleId = this.route.snapshot.paramMap.get('cycleId') ?? '';
  loading = signal(true);
  error = signal('');
  points = signal<FollicleChartPointDto[]>([]);

  get maxE2(): number {
    return Math.max(...this.points().map((p) => p.e2 ?? 0), 1);
  }

  get maxEndometrium(): number {
    return Math.max(...this.points().map((p) => p.endometrium ?? 0), 1);
  }

  barWidth(value: number, max: number): string {
    return `${Math.round((value / max) * 100)}%`;
  }

  ngOnInit() {
    if (!this.cycleId) {
      this.error.set('Không tìm thấy chu kỳ');
      this.loading.set(false);
      return;
    }
    this.service.getFollicleChart(this.cycleId).subscribe({
      next: (data) => {
        this.points.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Lỗi tải dữ liệu biểu đồ');
        this.loading.set(false);
      },
    });
  }
}
