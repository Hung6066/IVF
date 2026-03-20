import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ConsentFormService } from '../../../core/services/consent-form.service';
import { ConsentFormDto } from '../../../core/models/consent-form.models';

@Component({
  selector: 'app-consent-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './consent-detail.component.html',
  styleUrls: ['./consent-detail.component.scss']
})
export class ConsentDetailComponent implements OnInit {
  private service = inject(ConsentFormService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  consent = signal<ConsentFormDto | null>(null);
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  // Sign form
  showSignForm = false;
  signForm = { signedByUserId: '', witnessName: '' };

  // Revoke form
  showRevokeForm = false;
  revokeReason = '';

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id') || '';
    if (id) this.load(id);
  }

  load(id: string) {
    this.loading.set(true);
    this.service.getById(id).subscribe({
      next: (data) => { this.consent.set(data); this.loading.set(false); },
      error: () => { this.error.set('Không tìm thấy consent form'); this.loading.set(false); }
    });
  }

  sign() {
    const c = this.consent();
    if (!c) return;
    this.saving.set(true);
    this.service.sign(c.id, this.signForm).subscribe({
      next: (data) => { this.consent.set(data); this.showSignForm = false; this.saving.set(false); this.successMsg.set('Đã ký consent'); setTimeout(() => this.successMsg.set(''), 3000); },
      error: (err) => { this.error.set(err.error?.message || 'Lỗi ký'); this.saving.set(false); }
    });
  }

  revoke() {
    const c = this.consent();
    if (!c) return;
    this.saving.set(true);
    this.service.revoke(c.id, this.revokeReason).subscribe({
      next: (data) => { this.consent.set(data); this.showRevokeForm = false; this.saving.set(false); this.successMsg.set('Đã thu hồi consent'); setTimeout(() => this.successMsg.set(''), 3000); },
      error: (err) => { this.error.set(err.error?.message || 'Lỗi thu hồi'); this.saving.set(false); }
    });
  }

  back() { this.router.navigate(['/consent']); }
  formatDate(d?: string): string { return d ? new Date(d).toLocaleDateString('vi-VN') : '—'; }
}
