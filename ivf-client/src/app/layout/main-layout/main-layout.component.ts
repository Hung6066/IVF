import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { SignalRService } from '../../core/services/signalr.service';
import { MenuService, MenuSectionDto } from '../../core/services/menu.service';
import { TenantService } from '../../core/services/tenant.service';
import { TenantFeatureService } from '../../core/services/tenant-feature.service';
import { TenantFeatures } from '../../core/models/tenant.model';
import { ConsentBannerService } from '../../core/services/consent-banner.service';
import { NotificationBellComponent } from '../../shared/components/notification-bell/notification-bell.component';
import { GlobalToastComponent } from '../../shared/components/global-toast/global-toast.component';

// ── Menu interfaces (used by template) ──────────────
export interface MenuItem {
  icon: string;
  label: string;
  route: string;
  permission?: string;
  adminOnly?: boolean;
  platformAdminOnly?: boolean;
  requiredFeatureCode?: string;
}

export interface MenuSection {
  header?: string;
  adminOnly?: boolean;
  platformAdminOnly?: boolean;
  items: MenuItem[];
}

/** Fallback menu config — used when API is unavailable */
const FALLBACK_MENU: MenuSection[] = [
  {
    items: [
      { icon: '📊', label: 'Dashboard', route: '/dashboard' },
      { icon: '🏥', label: 'Tiếp đón', route: '/reception', permission: 'ViewPatients' },
      { icon: '👥', label: 'Bệnh nhân', route: '/patients', permission: 'ViewPatients' },
      {
        icon: '📊',
        label: 'Phân tích BN',
        route: '/patients/analytics',
        permission: 'ViewPatients',
      },
      { icon: '💑', label: 'Cặp đôi', route: '/couples', permission: 'ViewCouples' },
      { icon: '🎫', label: 'Hàng đợi', route: '/queue/all', permission: 'ViewQueue' },
      { icon: '🗣️', label: 'Tư vấn', route: '/consultation', permission: 'ViewCycles' },
      { icon: '🔬', label: 'Siêu âm', route: '/ultrasound', permission: 'ViewUltrasounds' },
      { icon: '🧫', label: 'Phòng Lab', route: '/lab', permission: 'ViewLabResults' },
      { icon: '🔬', label: 'Nam khoa', route: '/andrology', permission: 'ViewAndrology' },
      { icon: '💉', label: 'Tiêm', route: '/injection', permission: 'ViewCycles' },
      { icon: '🏦', label: 'NHTT', route: '/sperm-bank', permission: 'ViewSpermBank' },
      { icon: '💊', label: 'Nhà thuốc', route: '/pharmacy', permission: 'ViewPrescriptions' },
      { icon: '💰', label: 'Hoá đơn', route: '/billing', permission: 'ViewBilling' },
      { icon: '📅', label: 'Lịch hẹn', route: '/appointments', permission: 'ViewSchedule' },
      { icon: '📈', label: 'Báo cáo', route: '/reports', permission: 'ViewReports' },
    ],
  },
  {
    header: 'Quản trị',
    adminOnly: true,
    items: [
      { icon: '👥', label: 'Người dùng', route: '/admin/users', permission: 'ManageUsers' },
      { icon: '🔐', label: 'Phân quyền', route: '/admin/permissions', adminOnly: true },
      { icon: '⚙️', label: 'Cấu hình quyền', route: '/admin/permission-config', adminOnly: true },
      { icon: '📋', label: 'Danh mục DV', route: '/admin/services', adminOnly: true },
      { icon: '📝', label: 'Biểu mẫu', route: '/forms', adminOnly: true },
      { icon: '📁', label: 'Danh mục BM', route: '/forms/categories', adminOnly: true },
      { icon: '📝', label: 'Nhật ký', route: '/admin/audit-logs', permission: 'ViewAuditLog' },
      { icon: '🔔', label: 'Thông báo', route: '/admin/notifications', adminOnly: true },
      { icon: '🔏', label: 'Ký số', route: '/admin/digital-signing', adminOnly: true },
    ],
  },
  {
    header: 'Nền tảng (Super Admin)',
    platformAdminOnly: true,
    items: [
      { icon: '🏢', label: 'Quản lý Trung tâm', route: '/admin/tenants', platformAdminOnly: true },
      { icon: '💰', label: 'Bảng giá', route: '/pricing', platformAdminOnly: true },
      { icon: '🗄️', label: 'Sao lưu', route: '/admin/backup-restore', platformAdminOnly: true },
      { icon: '🛡️', label: 'Bảo mật', route: '/admin/security', platformAdminOnly: true },
      {
        icon: '🏢',
        label: 'Bảo mật DN',
        route: '/admin/enterprise-security',
        platformAdminOnly: true,
      },
      {
        icon: '📋',
        label: 'Sự kiện bảo mật',
        route: '/admin/security-events',
        platformAdminOnly: true,
      },
      { icon: '🎨', label: 'UI Library', route: '/ui-library', platformAdminOnly: true },
      { icon: '🖥️', label: 'Hạ tầng', route: '/admin/infrastructure', platformAdminOnly: true },
    ],
  },
  {
    header: 'Tuân thủ',
    platformAdminOnly: true,
    items: [
      { icon: '📊', label: 'Tổng quan', route: '/compliance', platformAdminOnly: true },
      { icon: '📋', label: 'Yêu cầu DSR', route: '/compliance/dsr', platformAdminOnly: true },
      {
        icon: '📅',
        label: 'Lịch tuân thủ',
        route: '/compliance/schedule',
        platformAdminOnly: true,
      },
      {
        icon: '🗃️',
        label: 'Tài sản dữ liệu',
        route: '/compliance/assets',
        platformAdminOnly: true,
      },
      { icon: '🤖', label: 'Quản trị AI', route: '/compliance/ai', platformAdminOnly: true },
      { icon: '📚', label: 'Đào tạo', route: '/compliance/training', platformAdminOnly: true },
      { icon: '📦', label: 'Bằng chứng', route: '/compliance/evidence', platformAdminOnly: true },
      { icon: '🛡️', label: 'Audit Center', route: '/compliance/audit', platformAdminOnly: true },
    ],
  },
];

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, NotificationBellComponent, GlobalToastComponent],
  templateUrl: './main-layout.component.html',
  styleUrls: ['./main-layout.component.scss'],
})
export class MainLayoutComponent implements OnInit, OnDestroy {
  showToast = false;
  toastTitle = '';
  toastMessage = '';
  toastType = '';
  private currentNotification: any = null;

  /** Data-driven menu sections — loaded from API, fallback to static config */
  menuSections: MenuSection[] = FALLBACK_MENU;
  tenantFeatures: TenantFeatures | null = null;

  constructor(
    public authService: AuthService,
    private signalRService: SignalRService,
    private menuService: MenuService,
    private tenantService: TenantService,
    private tenantFeatureService: TenantFeatureService,
    private router: Router,
    public consentBanner: ConsentBannerService,
  ) {}

  async ngOnInit() {
    // Load menu from database
    this.loadMenuFromApi();

    // Load tenant features to determine menu visibility
    this.loadTenantFeatures();
    // Also load into shared service for guards
    this.tenantFeatureService.load();

    // Load consent status for menu lock indicators
    if (this.authService.isAuthenticated()) {
      this.consentBanner.loadConsentStatus();
    }

    const token = localStorage.getItem('ivf_access_token');
    if (token) {
      await this.initializeSignalR(token);
    }
  }

  /** Load menu configuration from the API */
  private loadMenuFromApi() {
    this.menuService.loadMenu().subscribe({
      next: (sections) => {
        if (sections && sections.length > 0) {
          this.menuSections = sections.map((s) => ({
            header: s.header ?? undefined,
            adminOnly: s.adminOnly,
            items: s.items.map((i) => ({
              icon: i.icon,
              label: i.label,
              route: i.route,
              permission: i.permission ?? undefined,
              adminOnly: i.adminOnly,
              requiredFeatureCode: i.requiredFeatureCode ?? undefined,
            })),
          }));
        }
        // else: keep FALLBACK_MENU
      },
    });
  }

  /** Load tenant feature flags from API */
  private loadTenantFeatures() {
    if (this.authService.isAuthenticated()) {
      this.tenantService.getMyFeatures().subscribe({
        next: (features) => (this.tenantFeatures = features),
        error: () => (this.tenantFeatures = null),
      });
    }
  }

  async ngOnDestroy() {
    await this.signalRService.stopConnections();
  }

  private async initializeSignalR(token: string) {
    try {
      await this.signalRService.startNotificationConnection(token);
      await this.signalRService.startQueueConnection(token);

      this.signalRService.notification$.subscribe((notification) => {
        if (notification) {
          this.displayToast(notification);
        }
      });
    } catch (error) {
      console.error('SignalR initialization error:', error);
    }
  }

  private displayToast(notification: any) {
    this.currentNotification = notification;
    this.toastTitle = notification.title;
    this.toastMessage = notification.message;
    this.toastType = notification.type;
    this.showToast = true;
    setTimeout(() => this.dismissToast(), 5000);
  }

  dismissToast() {
    this.showToast = false;
  }

  handleToastClick() {
    if (this.currentNotification?.entityType) {
      const routes: Record<string, string> = {
        Appointment: '/appointments',
        Invoice: '/billing',
        TreatmentCycle: '/couples',
        QueueTicket: '/queue/US',
      };
      const route = routes[this.currentNotification.entityType];
      if (route) {
        this.router.navigate([route]);
        this.dismissToast();
      }
    }
  }

  getUserInitials(): string {
    const name = this.authService.user()?.fullName || '';
    return name
      .split(' ')
      .map((w) => w[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }

  getPageTitle(): string {
    const path = window.location.pathname;

    // Derive titles from menu config — no separate map to maintain
    for (const section of this.menuSections) {
      for (const item of section.items) {
        if (path.startsWith(item.route)) {
          return item.label;
        }
      }
    }
    return 'IVF System';
  }

  logout(): void {
    this.authService.logout();
  }

  // ── Menu visibility helpers ─────────────────────────
  /** Whether a section header + its items should be shown at all */
  isSectionVisible(section: MenuSection): boolean {
    if (section.platformAdminOnly && !this.authService.isPlatformAdmin()) return false;
    if (section.adminOnly && !this.isAdmin()) return false;
    return section.items.some((item) => this.isMenuItemVisible(item));
  }

  /** Whether a single menu item should be visible */
  isMenuItemVisible(item: MenuItem): boolean {
    if (item.platformAdminOnly) return this.authService.isPlatformAdmin();
    if (item.adminOnly) return this.isAdmin();
    if (item.permission && !this.canView(item.permission)) return false;
    // Feature gate: if item requires a feature, check tenant has it enabled
    if (item.requiredFeatureCode && this.tenantFeatures) {
      if (
        !this.tenantFeatures.isPlatformAdmin &&
        !this.tenantFeatures.enabledFeatures.includes(item.requiredFeatureCode)
      ) {
        return false;
      }
    }
    return true;
  }

  canView(permission: string): boolean {
    return this.authService.hasPermission(permission);
  }

  isAdmin(): boolean {
    return this.authService.hasRole('Admin');
  }

  /** Whether a menu item requires consent the user hasn't granted */
  isMenuConsentBlocked(item: MenuItem): boolean {
    return this.consentBanner.isRouteBlocked(item.route);
  }

  /** Tooltip showing which consents are missing for a menu item */
  getConsentTooltip(item: MenuItem): string {
    const missing = this.consentBanner.getMissingForRoute(item.route);
    if (missing.length === 0) return '';
    const labels = missing.map((t) => this.consentBanner.getLabel(t));
    return `Thiếu đồng ý: ${labels.join(', ')}`;
  }
}
