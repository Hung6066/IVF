import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { DashboardStats, CycleSuccessRates } from '../../core/models/api.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {
  stats = signal<DashboardStats | null>(null);
  successRates = signal<CycleSuccessRates | null>(null);

  constructor(private api: ApiService, private router: Router) { }

  ngOnInit(): void {
    this.api.getDashboardStats().subscribe(data => this.stats.set(data));
    this.api.getCycleSuccessRates().subscribe(data => this.successRates.set(data));
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
  }

  goToAddPatient(): void { this.router.navigate(['/patients'], { queryParams: { action: 'new' } }); }
  goToIssueQueue(): void { this.router.navigate(['/reception'], { queryParams: { action: 'queue' } }); }
  goToAppointments(): void { this.router.navigate(['/queue/US']); }
  goToReports(): void { this.router.navigate(['/reports']); }
}
