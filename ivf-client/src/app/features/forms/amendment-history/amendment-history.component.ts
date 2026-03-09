import { Component, Input, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AmendmentService } from '../../../core/services/amendment.service';
import { AmendmentDto, FieldChangeDto } from '../../../core/models/amendment.models';

@Component({
  selector: 'app-amendment-history',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './amendment-history.component.html',
  styleUrls: ['./amendment-history.component.scss'],
})
export class AmendmentHistoryComponent implements OnInit {
  @Input() formResponseId!: string;

  amendments = signal<AmendmentDto[]>([]);
  loading = signal(false);
  expandedId = signal<string | null>(null);

  constructor(private amendmentService: AmendmentService) {}

  ngOnInit() {
    if (this.formResponseId) {
      this.loadHistory();
    }
  }

  loadHistory() {
    this.loading.set(true);
    this.amendmentService.getAmendmentHistory(this.formResponseId).subscribe({
      next: (data) => {
        this.amendments.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  toggleExpand(id: string) {
    this.expandedId.set(this.expandedId() === id ? null : id);
  }

  getStatusLabel(status: string): string {
    switch (status) {
      case 'Pending':
        return 'Chờ duyệt';
      case 'Approved':
        return 'Đã duyệt';
      case 'Rejected':
        return 'Đã từ chối';
      default:
        return status;
    }
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Pending':
        return 'status-pending';
      case 'Approved':
        return 'status-approved';
      case 'Rejected':
        return 'status-rejected';
      default:
        return '';
    }
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

  /** Get display value from a field change (old or new) */
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

    // Modified — compute word-level diff
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

    // Build LCS table
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

    // Backtrack to produce diff parts
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

    // Merge consecutive same-type parts and build HTML
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

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString('vi-VN');
  }
}
