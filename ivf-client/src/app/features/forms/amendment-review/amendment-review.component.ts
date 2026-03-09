import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AmendmentService } from '../../../core/services/amendment.service';
import { AmendmentDto, FieldChangeDto } from '../../../core/models/amendment.models';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-amendment-review',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './amendment-review.component.html',
  styleUrls: ['./amendment-review.component.scss'],
})
export class AmendmentReviewComponent implements OnInit {
  private readonly authService = inject(AuthService);

  pendingAmendments = signal<AmendmentDto[]>([]);
  totalCount = signal(0);
  page = signal(1);
  pageSize = 20;
  loading = signal(false);

  // Selected amendment detail
  selectedAmendment = signal<AmendmentDto | null>(null);
  loadingDetail = signal(false);

  // Approve/Reject
  showApproveModal = false;
  showRejectModal = false;
  reviewNotes = '';
  processing = signal(false);
  actionResult = signal<{ success: boolean; message: string } | null>(null);

  get canApproveAmendment(): boolean {
    return this.authService.hasPermission('ApproveAmendment');
  }

  constructor(private amendmentService: AmendmentService) {}

  ngOnInit() {
    this.loadPending();
  }

  loadPending() {
    this.loading.set(true);
    this.amendmentService.getPendingAmendments(this.page(), this.pageSize).subscribe({
      next: (data) => {
        this.pendingAmendments.set(data.items);
        this.totalCount.set(data.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  selectAmendment(amendment: AmendmentDto) {
    this.loadingDetail.set(true);
    this.actionResult.set(null);
    this.amendmentService.getAmendmentDetail(amendment.id).subscribe({
      next: (detail) => {
        this.selectedAmendment.set(detail);
        this.loadingDetail.set(false);
      },
      error: () => {
        this.selectedAmendment.set(amendment);
        this.loadingDetail.set(false);
      },
    });
  }

  openApproveModal() {
    this.reviewNotes = '';
    this.showApproveModal = true;
  }

  openRejectModal() {
    this.reviewNotes = '';
    this.showRejectModal = true;
  }

  approve() {
    const selected = this.selectedAmendment();
    if (!selected) return;

    this.processing.set(true);
    this.amendmentService
      .approveAmendment(selected.id, { notes: this.reviewNotes || undefined })
      .subscribe({
        next: (result) => {
          this.processing.set(false);
          this.showApproveModal = false;
          this.actionResult.set({ success: true, message: result.message });
          this.selectedAmendment.set(null);
          this.loadPending();
        },
        error: (err) => {
          this.processing.set(false);
          this.actionResult.set({
            success: false,
            message: err.error?.error || 'Lỗi khi phê duyệt.',
          });
        },
      });
  }

  reject() {
    const selected = this.selectedAmendment();
    if (!selected) return;

    if (!this.reviewNotes.trim()) {
      this.actionResult.set({ success: false, message: 'Vui lòng nhập lý do từ chối.' });
      return;
    }

    this.processing.set(true);
    this.amendmentService.rejectAmendment(selected.id, { notes: this.reviewNotes }).subscribe({
      next: (result) => {
        this.processing.set(false);
        this.showRejectModal = false;
        this.actionResult.set({ success: true, message: result.message });
        this.selectedAmendment.set(null);
        this.loadPending();
      },
      error: (err) => {
        this.processing.set(false);
        this.actionResult.set({
          success: false,
          message: err.error?.error || 'Lỗi khi từ chối.',
        });
      },
    });
  }

  changePage(newPage: number) {
    this.page.set(newPage);
    this.loadPending();
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString('vi-VN');
  }

  getDisplayValue(fc: FieldChangeDto, side: 'old' | 'new'): string {
    const text = side === 'old' ? fc.oldTextValue : fc.newTextValue;
    const num = side === 'old' ? fc.oldNumericValue : fc.newNumericValue;
    const date = side === 'old' ? fc.oldDateValue : fc.newDateValue;
    const bool = side === 'old' ? fc.oldBooleanValue : fc.newBooleanValue;
    const json = side === 'old' ? fc.oldJsonValue : fc.newJsonValue;

    if (text != null) return text;
    if (num != null) return num.toString();
    if (date != null) return new Date(date).toLocaleDateString('vi-VN');
    if (bool != null) return bool ? 'Có' : 'Không';
    if (json != null) {
      try {
        const parsed = JSON.parse(json);
        if (Array.isArray(parsed)) {
          return parsed.map((item: any) => item.label || item.value || item).join(', ');
        }
        return JSON.stringify(parsed, null, 2);
      } catch {
        return json;
      }
    }
    return '';
  }

  /** Generate inline diff HTML comparing old vs new values */
  getInlineDiffHtml(fc: FieldChangeDto): string {
    const oldStr = this.getDisplayValue(fc, 'old');
    const newStr = this.getDisplayValue(fc, 'new');

    if (fc.changeType === 'Added') {
      return `<span class="diff-added">${this.escapeHtml(newStr || '(trống)')}</span>`;
    }
    if (fc.changeType === 'Removed') {
      return `<span class="diff-removed">${this.escapeHtml(oldStr || '(trống)')}</span>`;
    }

    if (!oldStr && !newStr) return '<span class="diff-empty">(trống)</span>';
    if (!oldStr) return `<span class="diff-added">${this.escapeHtml(newStr)}</span>`;
    if (!newStr) return `<span class="diff-removed">${this.escapeHtml(oldStr)}</span>`;
    if (oldStr === newStr) return this.escapeHtml(oldStr);

    return this.computeWordDiff(oldStr, newStr);
  }

  private computeWordDiff(oldText: string, newText: string): string {
    const oldWords = oldText.split(/(\s+)/);
    const newWords = newText.split(/(\s+)/);
    const m = oldWords.length;
    const n = newWords.length;

    const dp: number[][] = Array.from({ length: m + 1 }, () => Array(n + 1).fill(0));
    for (let i = 1; i <= m; i++) {
      for (let j = 1; j <= n; j++) {
        if (oldWords[i - 1] === newWords[j - 1]) {
          dp[i][j] = dp[i - 1][j - 1] + 1;
        } else {
          dp[i][j] = Math.max(dp[i - 1][j], dp[i][j - 1]);
        }
      }
    }

    const parts: { type: 'same' | 'added' | 'removed'; text: string }[] = [];
    let i = m,
      j = n;
    while (i > 0 || j > 0) {
      if (i > 0 && j > 0 && oldWords[i - 1] === newWords[j - 1]) {
        parts.unshift({ type: 'same', text: oldWords[i - 1] });
        i--;
        j--;
      } else if (j > 0 && (i === 0 || dp[i][j - 1] >= dp[i - 1][j])) {
        parts.unshift({ type: 'added', text: newWords[j - 1] });
        j--;
      } else {
        parts.unshift({ type: 'removed', text: oldWords[i - 1] });
        i--;
      }
    }

    let html = '';
    let curType = '';
    let curTexts: string[] = [];

    const flush = () => {
      if (curTexts.length === 0) return;
      const joined = curTexts.join('');
      const escaped = this.escapeHtml(joined);
      if (curType === 'added') html += `<span class="diff-added">${escaped}</span>`;
      else if (curType === 'removed') html += `<span class="diff-removed">${escaped}</span>`;
      else html += escaped;
      curTexts = [];
    };

    for (const part of parts) {
      if (part.type !== curType) {
        flush();
        curType = part.type;
      }
      curTexts.push(part.text);
    }
    flush();

    return html;
  }

  private escapeHtml(text: string): string {
    return text
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  getChangeTypeLabel(type: string): string {
    switch (type) {
      case 'Modified':
        return 'Sửa đổi';
      case 'Added':
        return 'Thêm mới';
      case 'Removed':
        return 'Xóa';
      default:
        return type;
    }
  }

  getChangeTypeClass(type: string): string {
    switch (type) {
      case 'Modified':
        return 'change-modified';
      case 'Added':
        return 'change-added';
      case 'Removed':
        return 'change-removed';
      default:
        return '';
    }
  }
}
