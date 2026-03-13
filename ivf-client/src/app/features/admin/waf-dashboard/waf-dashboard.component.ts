import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { WafService } from '../../../core/services/waf.service';
import { GlobalNotificationService } from '../../../core/services/global-notification.service';
import {
  AppWafRule,
  AppWafEvent,
  AppWafAnalytics,
  CreateWafRuleRequest,
  WafStatus,
  WafEvent,
  WAF_ACTIONS,
  WAF_RULE_GROUPS,
  WAF_MATCH_TYPES,
} from '../../../core/models/waf.model';

@Component({
  selector: 'app-waf-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './waf-dashboard.component.html',
  styleUrls: ['./waf-dashboard.component.scss'],
})
export class WafDashboardComponent implements OnInit {
  activeTab = signal<'analytics' | 'rules' | 'events' | 'cloudflare'>('analytics');

  // Analytics
  analytics = signal<AppWafAnalytics | null>(null);
  analyticsLoading = signal(false);

  // Rules
  rules = signal<AppWafRule[]>([]);
  rulesLoading = signal(false);
  ruleGroupFilter = signal('');

  // Events
  events = signal<AppWafEvent[]>([]);
  eventsLoading = signal(false);
  eventsTotalCount = signal(0);
  eventsPage = signal(1);
  eventsPageSize = signal(50);
  eventsFilterIp = signal('');
  eventsFilterAction = signal('');

  // Cloudflare
  cfStatus = signal<WafStatus | null>(null);
  cfEvents = signal<WafEvent[]>([]);
  cfLoading = signal(false);
  cfError = signal<string | null>(null);

  // Create/Edit Rule Form
  formVisible = signal(false);
  editingRule = signal<AppWafRule | null>(null);
  formSaving = signal(false);

  // Form fields
  formName = signal('');
  formDescription = signal('');
  formPriority = signal(500);
  formRuleGroup = signal('Custom');
  formAction = signal('Block');
  formMatchType = signal('Any');
  formNegateMatch = signal(false);
  formExpression = signal('');
  formUriPathPatterns = signal('');
  formQueryStringPatterns = signal('');
  formHeaderPatterns = signal('');
  formBodyPatterns = signal('');
  formMethods = signal('');
  formIpCidrList = signal('');
  formCountryCodes = signal('');
  formUserAgentPatterns = signal('');
  formRateLimitRequests = signal<number | null>(null);
  formRateLimitWindowSeconds = signal<number | null>(null);
  formBlockResponseMessage = signal('');

  // Constants for template
  readonly wafActions = WAF_ACTIONS;
  readonly wafRuleGroups = WAF_RULE_GROUPS;
  readonly wafMatchTypes = WAF_MATCH_TYPES;

  constructor(
    private wafService: WafService,
    private notify: GlobalNotificationService,
  ) {}

  ngOnInit(): void {
    this.loadAnalytics();
    this.loadRules();
  }

  // ─── Analytics ───

  loadAnalytics(): void {
    this.analyticsLoading.set(true);
    this.wafService.getAnalytics().subscribe({
      next: (data) => {
        this.analytics.set(data);
        this.analyticsLoading.set(false);
      },
      error: () => {
        this.analyticsLoading.set(false);
      },
    });
  }

  // ─── Rules ───

  loadRules(): void {
    this.rulesLoading.set(true);
    const group = this.ruleGroupFilter() || undefined;
    this.wafService.getRules(group).subscribe({
      next: (data) => {
        this.rules.set(data);
        this.rulesLoading.set(false);
      },
      error: () => {
        this.notify.error('Lỗi', 'Không thể tải danh sách quy tắc WAF');
        this.rulesLoading.set(false);
      },
    });
  }

  toggleRule(rule: AppWafRule): void {
    const newState = !rule.isEnabled;
    this.wafService.toggleRule(rule.id, newState).subscribe({
      next: () => {
        this.notify.success('Thành công', `${rule.name} đã ${newState ? 'bật' : 'tắt'}`);
        this.loadRules();
      },
      error: () => this.notify.error('Lỗi', 'Không thể cập nhật quy tắc'),
    });
  }

  deleteRule(rule: AppWafRule): void {
    if (!confirm(`Xóa quy tắc "${rule.name}"? Hành động không thể hoàn tác.`)) return;

    this.wafService.deleteRule(rule.id).subscribe({
      next: () => {
        this.notify.success('Thành công', 'Quy tắc đã được xóa');
        this.loadRules();
      },
      error: (err) => {
        const msg = err.error?.error || 'Không thể xóa quy tắc';
        this.notify.error('Lỗi', msg);
      },
    });
  }

  invalidateCache(): void {
    this.wafService.invalidateCache().subscribe({
      next: () => this.notify.success('Thành công', 'Cache WAF đã được làm mới'),
      error: () => this.notify.error('Lỗi', 'Không thể làm mới cache'),
    });
  }

  // ─── Events ───

  loadEvents(): void {
    this.eventsLoading.set(true);
    const filters: Record<string, string> = {};
    if (this.eventsFilterIp()) filters['ip'] = this.eventsFilterIp();
    if (this.eventsFilterAction()) filters['action'] = this.eventsFilterAction();

    this.wafService.getWafEvents(this.eventsPage(), this.eventsPageSize(), filters).subscribe({
      next: (data) => {
        this.events.set(data.items);
        this.eventsTotalCount.set(data.totalCount);
        this.eventsLoading.set(false);
      },
      error: () => {
        this.notify.error('Lỗi', 'Không thể tải sự kiện WAF');
        this.eventsLoading.set(false);
      },
    });
  }

  eventsNextPage(): void {
    this.eventsPage.update(p => p + 1);
    this.loadEvents();
  }

  eventsPrevPage(): void {
    if (this.eventsPage() > 1) {
      this.eventsPage.update(p => p - 1);
      this.loadEvents();
    }
  }

  get eventsTotalPages(): number {
    return Math.ceil(this.eventsTotalCount() / this.eventsPageSize());
  }

  // ─── Cloudflare ───

  loadCloudflare(): void {
    this.cfLoading.set(true);
    this.cfError.set(null);
    this.wafService.getStatus().subscribe({
      next: (s) => {
        this.cfStatus.set(s);
        this.cfLoading.set(false);
      },
      error: (err) => {
        this.cfError.set(err.status === 503 ? 'Cloudflare WAF chưa được cấu hình' : 'Không thể tải');
        this.cfLoading.set(false);
      },
    });
    this.wafService.getCloudflareEvents(100).subscribe({
      next: (events) => this.cfEvents.set(events),
      error: () => {},
    });
  }

  // ─── Tab switch ───

  switchTab(tab: 'analytics' | 'rules' | 'events' | 'cloudflare'): void {
    this.activeTab.set(tab);
    if (tab === 'events' && this.events().length === 0) this.loadEvents();
    if (tab === 'cloudflare' && !this.cfStatus()) this.loadCloudflare();
  }

  // ─── Create/Edit Form ───

  openCreateForm(): void {
    this.editingRule.set(null);
    this.resetForm();
    this.formVisible.set(true);
  }

  openEditForm(rule: AppWafRule): void {
    this.editingRule.set(rule);
    this.formName.set(rule.name);
    this.formDescription.set(rule.description || '');
    this.formPriority.set(rule.priority);
    this.formRuleGroup.set(rule.ruleGroup);
    this.formAction.set(rule.action);
    this.formMatchType.set(rule.matchType);
    this.formNegateMatch.set(rule.negateMatch);
    this.formExpression.set(rule.expression || '');
    this.formUriPathPatterns.set((rule.uriPathPatterns || []).join('\n'));
    this.formQueryStringPatterns.set((rule.queryStringPatterns || []).join('\n'));
    this.formHeaderPatterns.set((rule.headerPatterns || []).join('\n'));
    this.formBodyPatterns.set((rule.bodyPatterns || []).join('\n'));
    this.formMethods.set((rule.methods || []).join(', '));
    this.formIpCidrList.set((rule.ipCidrList || []).join('\n'));
    this.formCountryCodes.set((rule.countryCodes || []).join(', '));
    this.formUserAgentPatterns.set((rule.userAgentPatterns || []).join('\n'));
    this.formRateLimitRequests.set(rule.rateLimitRequests);
    this.formRateLimitWindowSeconds.set(rule.rateLimitWindowSeconds);
    this.formBlockResponseMessage.set(rule.blockResponseMessage || '');
    this.formVisible.set(true);
  }

  closeForm(): void {
    this.formVisible.set(false);
    this.editingRule.set(null);
  }

  resetForm(): void {
    this.formName.set('');
    this.formDescription.set('');
    this.formPriority.set(500);
    this.formRuleGroup.set('Custom');
    this.formAction.set('Block');
    this.formMatchType.set('Any');
    this.formNegateMatch.set(false);
    this.formExpression.set('');
    this.formUriPathPatterns.set('');
    this.formQueryStringPatterns.set('');
    this.formHeaderPatterns.set('');
    this.formBodyPatterns.set('');
    this.formMethods.set('');
    this.formIpCidrList.set('');
    this.formCountryCodes.set('');
    this.formUserAgentPatterns.set('');
    this.formRateLimitRequests.set(null);
    this.formRateLimitWindowSeconds.set(null);
    this.formBlockResponseMessage.set('');
  }

  private splitLines(val: string): string[] | undefined {
    const arr = val.split('\n').map(s => s.trim()).filter(s => s.length > 0);
    return arr.length > 0 ? arr : undefined;
  }

  private splitComma(val: string): string[] | undefined {
    const arr = val.split(',').map(s => s.trim()).filter(s => s.length > 0);
    return arr.length > 0 ? arr : undefined;
  }

  saveRule(): void {
    if (!this.formName().trim()) {
      this.notify.warning('Cảnh báo', 'Tên quy tắc không được để trống');
      return;
    }

    this.formSaving.set(true);

    const editing = this.editingRule();
    if (editing) {
      this.wafService.updateRule(editing.id, {
        id: editing.id,
        name: this.formName().trim(),
        description: this.formDescription().trim() || undefined,
        priority: this.formPriority(),
        action: this.formAction(),
        matchType: this.formMatchType(),
        negateMatch: this.formNegateMatch(),
        expression: this.formExpression().trim() || undefined,
        uriPathPatterns: this.splitLines(this.formUriPathPatterns()),
        queryStringPatterns: this.splitLines(this.formQueryStringPatterns()),
        headerPatterns: this.splitLines(this.formHeaderPatterns()),
        bodyPatterns: this.splitLines(this.formBodyPatterns()),
        methods: this.splitComma(this.formMethods()),
        ipCidrList: this.splitLines(this.formIpCidrList()),
        countryCodes: this.splitComma(this.formCountryCodes()),
        userAgentPatterns: this.splitLines(this.formUserAgentPatterns()),
        rateLimitRequests: this.formRateLimitRequests() || undefined,
        rateLimitWindowSeconds: this.formRateLimitWindowSeconds() || undefined,
        blockResponseMessage: this.formBlockResponseMessage().trim() || undefined,
      }).subscribe({
        next: () => {
          this.notify.success('Thành công', 'Quy tắc đã được cập nhật');
          this.closeForm();
          this.loadRules();
        },
        error: (err) => {
          this.notify.error('Lỗi', err.error?.error || 'Không thể cập nhật quy tắc');
        },
        complete: () => this.formSaving.set(false),
      });
    } else {
      const request: CreateWafRuleRequest = {
        name: this.formName().trim(),
        description: this.formDescription().trim() || undefined,
        priority: this.formPriority(),
        ruleGroup: this.formRuleGroup(),
        action: this.formAction(),
        matchType: this.formMatchType(),
        negateMatch: this.formNegateMatch(),
        expression: this.formExpression().trim() || undefined,
        uriPathPatterns: this.splitLines(this.formUriPathPatterns()),
        queryStringPatterns: this.splitLines(this.formQueryStringPatterns()),
        headerPatterns: this.splitLines(this.formHeaderPatterns()),
        bodyPatterns: this.splitLines(this.formBodyPatterns()),
        methods: this.splitComma(this.formMethods()),
        ipCidrList: this.splitLines(this.formIpCidrList()),
        countryCodes: this.splitComma(this.formCountryCodes()),
        userAgentPatterns: this.splitLines(this.formUserAgentPatterns()),
        rateLimitRequests: this.formRateLimitRequests() || undefined,
        rateLimitWindowSeconds: this.formRateLimitWindowSeconds() || undefined,
        blockResponseMessage: this.formBlockResponseMessage().trim() || undefined,
      };

      this.wafService.createRule(request).subscribe({
        next: () => {
          this.notify.success('Thành công', 'Quy tắc mới đã được tạo');
          this.closeForm();
          this.loadRules();
        },
        error: (err) => {
          this.notify.error('Lỗi', err.error?.error || 'Không thể tạo quy tắc');
        },
        complete: () => this.formSaving.set(false),
      });
    }
  }

  // ─── Helpers ───

  getActionBadge(action: string): { label: string; color: string } {
    const found = WAF_ACTIONS.find(a => a.value === action);
    return found ? { label: found.label, color: found.color } : { label: action, color: 'secondary' };
  }

  getGroupLabel(group: string): string {
    return WAF_RULE_GROUPS.find(g => g.value === group)?.label || group;
  }

  getGroupIcon(group: string): string {
    return WAF_RULE_GROUPS.find(g => g.value === group)?.icon || '';
  }

  getCfActionLabel(action: string): string {
    const labels: Record<string, string> = {
      block: 'Chặn', managed_challenge: 'Thách thức', challenge: 'CAPTCHA',
      execute: 'Thực thi', log: 'Ghi log',
    };
    return labels[action] || action;
  }

  getCfActionBadgeClass(action: string): string {
    const classes: Record<string, string> = {
      block: 'badge-danger', managed_challenge: 'badge-warning',
      challenge: 'badge-warning', execute: 'badge-info', log: 'badge-secondary',
    };
    return classes[action] || 'badge-secondary';
  }

  formatTime(ts: string): string {
    return new Date(ts).toLocaleString('vi-VN');
  }

  get managedRulesCount(): number {
    return this.rules().filter(r => r.isManaged).length;
  }

  get customRulesCount(): number {
    return this.rules().filter(r => !r.isManaged).length;
  }

  get enabledRulesCount(): number {
    return this.rules().filter(r => r.isEnabled).length;
  }
}
