import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-andrology-result-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './andrology-result-detail.component.html',
  styleUrls: ['./andrology-result-detail.component.scss'],
})
export class AndrologyResultDetailComponent implements OnInit {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private readonly baseUrl = environment.apiUrl;

  loading = signal(true);
  result = signal<any>(null);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.http.get(`${this.baseUrl}/andrology/semen-analysis/${id}`).subscribe({
        next: (data) => {
          this.result.set(data);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  back() {
    this.router.navigate(['/andrology']);
  }

  getMotilityClass(value?: number): string {
    if (value === undefined || value === null) return 'text-gray-500';
    if (value >= 30) return 'text-green-600 font-semibold';
    if (value >= 20) return 'text-yellow-600 font-semibold';
    return 'text-red-600 font-semibold';
  }

  getMorphologyClass(value?: number): string {
    if (value === undefined || value === null) return 'text-gray-500';
    if (value >= 4) return 'text-green-600 font-semibold';
    return 'text-red-600 font-semibold';
  }
}
