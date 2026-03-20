import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ConsentFormService } from '../../../core/services/consent-form.service';
import { ConsentFormDto, ConsentType } from '../../../core/models/consent-form.models';

@Component({
  selector: 'app-consent-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './consent-list.component.html',
  styleUrls: ['./consent-list.component.scss']
})
export class ConsentListComponent implements OnInit {
  private service = inject(ConsentFormService);
  private router = inject(Router);

  consents = signal<ConsentFormDto[]>([]);
  filtered = signal<ConsentFormDto[]>([]);
  loading = signal(false);
  searchQuery = '';
  filterType: ConsentType | '' = '';
  filterStatus = '';

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.service.getByPatient('').subscribe({
      next: (data) => { this.consents.set(data); this.applyFilters(); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  applyFilters() {
    let result = this.consents();
    if (this.searchQuery) {
      const q = this.searchQuery.toLowerCase();
      result = result.filter(c => c.patientName.toLowerCase().includes(q) || c.title.toLowerCase().includes(q));
    }
    if (this.filterType) result = result.filter(c => c.consentType === this.filterType);
    if (this.filterStatus === 'signed') result = result.filter(c => c.isSigned && !c.isRevoked);
    if (this.filterStatus === 'unsigned') result = result.filter(c => !c.isSigned);
    if (this.filterStatus === 'revoked') result = result.filter(c => c.isRevoked);
    this.filtered.set(result);
  }

  openDetail(id: string) { this.router.navigate(['/consent', id]); }
  create() { this.router.navigate(['/consent/create']); }

  statusLabel(c: ConsentFormDto): string {
    if (c.isRevoked) return 'Đã thu hồi';
    if (c.isSigned) return 'Đã ký';
    return 'Chưa ký';
  }

  statusClass(c: ConsentFormDto): string {
    if (c.isRevoked) return 'bg-red-100 text-red-800';
    if (c.isSigned) return 'bg-green-100 text-green-800';
    return 'bg-yellow-100 text-yellow-800';
  }

  typeLabel(t: string): string {
    const map: Record<string, string> = { OPU: 'Chọc hút', IUI: 'IUI', Anesthesia: 'Tiền mê', EggDonation: 'Cho trứng', SpermDonation: 'Cho tinh trùng', FET: 'FET', General: 'Chung' };
    return map[t] || t;
  }

  formatDate(d?: string): string {
    if (!d) return '—';
    return new Date(d).toLocaleDateString('vi-VN');
  }
}
