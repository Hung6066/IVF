import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TenantService } from '../../core/services/tenant.service';
import { PricingPlan } from '../../core/models/tenant.model';

@Component({
  selector: 'app-pricing',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './pricing.component.html',
  styleUrls: ['./pricing.component.scss'],
})
export class PricingComponent implements OnInit {
  plans = signal<PricingPlan[]>([]);
  billingCycle = signal<'monthly' | 'quarterly' | 'annually'>('monthly');

  constructor(private tenantService: TenantService) {}

  ngOnInit(): void {
    this.tenantService.getPricing().subscribe({
      next: (plans) => this.plans.set(plans),
      error: () => {},
    });
  }

  getAdjustedPrice(price: number): number {
    if (price === 0) return 0;
    switch (this.billingCycle()) {
      case 'quarterly':
        return Math.round(price * 0.95);
      case 'annually':
        return Math.round(price * 0.85);
      default:
        return price;
    }
  }

  formatCurrency(amount: number): string {
    return new Intl.NumberFormat('vi-VN').format(amount);
  }

  isPrimary(plan: string): boolean {
    return this.plans().find((p) => p.plan === plan)?.isFeatured ?? false;
  }
}
