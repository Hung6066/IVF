import { Component, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AdvancedSecurityService } from '../../../core/services/advanced-security.service';
import { SecurityService } from '../../../core/services/security.service';
import { AuthService } from '../../../core/services/auth.service';
import { UserService } from '../../../core/services/user.service';
import QRCode from 'qrcode';
import {
  SecurityScore,
  LoginHistoryEntry,
  RateLimitStatus,
  RateLimitEvent,
  RateLimitCustomConfig,
  GeoSecurityData,
  GeoBlockRule,
  ThreatOverview,
  AccountLockout,
  WhitelistedIp,
  UserDevice,
  PasskeyCredential,
  MfaSettings,
  TotpSetupResponse,
  RISK_FACTOR_LABELS,
} from '../../../core/models/advanced-security.model';
import { SessionInfo, ThreatAssessment, IpIntelligence } from '../../../core/models/security.model';

type TabKey =
  | 'overview'
  | 'passkeys'
  | 'devices'
  | 'sessions'
  | 'history'
  | 'rate-limit'
  | 'geo-security'
  | 'threats'
  | 'lockouts'
  | 'ip-whitelist';

@Component({
  selector: 'app-advanced-security',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './advanced-security.component.html',
  styleUrls: ['./advanced-security.component.scss'],
})
export class AdvancedSecurityComponent implements OnInit, OnDestroy {
  // Tab
  activeTab = signal<TabKey>('overview');

  // Loading state
  loading = signal(false);
  autoRefresh = signal(true);
  private refreshInterval: ReturnType<typeof setInterval> | null = null;

  // Status message
  statusMessage = signal<{ text: string; type: 'success' | 'error' } | null>(null);

  // Security Score
  securityScore = signal<SecurityScore | null>(null);
  scoreLevel = computed(() => this.securityScore()?.level ?? 'good');

  // Login History
  loginHistory = signal<LoginHistoryEntry[]>([]);

  // Rate Limit
  rateLimitStatus = signal<RateLimitStatus | null>(null);
  rateLimitEvents = signal<RateLimitEvent[]>([]);
  showRateLimitForm = signal(false);
  editingRateLimit = signal<RateLimitCustomConfig | null>(null);
  rlForm = {
    policyName: '',
    windowType: 'fixed',
    windowSeconds: 60,
    permitLimit: 100,
    appliesTo: '',
    description: '',
  };

  // Geo Security
  geoData = signal<GeoSecurityData | null>(null);
  showGeoRuleForm = signal(false);
  geoRuleForm = { countryCode: '', countryName: '', isBlocked: true, reason: '' };

  // Threats
  threatOverview = signal<ThreatOverview | null>(null);
  threatAssessResult = signal<ThreatAssessment | null>(null);
  threatIpResult = signal<IpIntelligence | null>(null);
  threatAssessForm = { ipAddress: '', username: '', userAgent: '', country: '', requestPath: '' };
  threatIpLookup = '';
  threatToolTab = signal<'dashboard' | 'assess' | 'ip'>('dashboard');

  // Account Lockouts
  lockouts = signal<AccountLockout[]>([]);
  activeLockouts = computed(() => this.lockouts().filter((l) => l.isLocked));
  showLockForm = signal(false);
  lockForm = { userId: '', username: '', reason: '', durationMinutes: 30, failedAttempts: 0 };

  // IP Whitelist
  ipWhitelist = signal<WhitelistedIp[]>([]);
  newIp = '';
  newIpDescription = '';
  newIpCidrRange = '';
  newIpExpiresDays: number | null = null;

  // Sessions
  sessions = signal<SessionInfo[]>([]);
  sessionUserId = '';

  // Devices
  devices = signal<UserDevice[]>([]);
  deviceUserId = '';

  // Passkeys
  passkeysSupported = signal(false);
  passkeys = signal<PasskeyCredential[]>([]);
  passkeyUserId = '';
  passkeyDeviceName = '';
  passkeyRegistering = signal(false);

  // MFA Settings
  mfaSettings = signal<MfaSettings | null>(null);
  mfaUserId = '';

  // User list for selection
  users = signal<{ id: string; username: string; fullName: string; role: string }[]>([]);
  userSearch = '';

  // Public IP
  publicIp = signal<string>('');

  // TOTP
  totpSetup = signal<TotpSetupResponse | null>(null);
  totpQrDataUrl = signal<string>('');
  totpCode = '';
  totpUserId = '';

  // SMS OTP
  smsPhone = '';
  smsCode = '';
  smsUserId = '';

  // Current user
  currentUser = computed(() => this.authService.user());
  currentUserId = computed(() => this.authService.user()?.id ?? '');
  currentUsername = computed(
    () => this.authService.user()?.fullName ?? this.authService.user()?.username ?? '',
  );

  // Machine info
  machineUserAgent = navigator.userAgent;
  machineFingerprint = '';

  // Label helper
  riskFactorLabels = RISK_FACTOR_LABELS;

  constructor(
    private advancedSecurityService: AdvancedSecurityService,
    private securityService: SecurityService,
    private authService: AuthService,
    private userService: UserService,
    private router: Router,
  ) {}

  ngOnInit() {
    this.initCurrentUser();
    this.checkPasskeySupport();
    this.loadUsers();
    this.fetchPublicIp();
    this.loadOverview();
    this.startAutoRefresh();
  }

  private initCurrentUser() {
    const userId = this.currentUserId();
    const username = this.currentUsername();

    // Auto-populate all userId fields
    this.passkeyUserId = userId;
    this.deviceUserId = userId;
    this.sessionUserId = userId;
    this.mfaUserId = userId;
    this.totpUserId = userId;
    this.smsUserId = userId;

    // Machine fingerprint from localStorage if available
    this.machineFingerprint = localStorage.getItem('x-device-fingerprint') ?? '';
  }

  loadUsers(search?: string) {
    this.userService.getUsers(search, undefined, true, 1, 100).subscribe({
      next: (res: any) => {
        this.users.set(
          (res.items || []).map((u: any) => ({
            id: u.id,
            username: u.username,
            fullName: u.fullName,
            role: u.role,
          })),
        );
      },
    });
  }

  onUserSelected(userId: string) {
    this.passkeyUserId = userId;
    this.deviceUserId = userId;
    this.sessionUserId = userId;
    this.mfaUserId = userId;
    this.totpUserId = userId;
    this.smsUserId = userId;
    this.lockForm.userId = userId;
    const user = this.users().find((u) => u.id === userId);
    if (user) {
      this.lockForm.username = user.fullName || user.username;
    }
  }

  getUserDisplay(userId: string): string {
    const user = this.users().find((u) => u.id === userId);
    return user ? `${user.fullName || user.username} (${user.role})` : userId;
  }

  private fetchPublicIp() {
    this.advancedSecurityService.getMyIp().subscribe({
      next: (res) => this.publicIp.set(res.ip || ''),
      error: () => {},
    });
  }

  ngOnDestroy() {
    this.stopAutoRefresh();
  }

  // ─── Tab Management ───

  setTab(tab: TabKey) {
    this.activeTab.set(tab);
    this.loadTabData(tab);
  }

  private loadTabData(tab: TabKey) {
    switch (tab) {
      case 'overview':
        this.loadOverview();
        break;
      case 'passkeys':
        if (this.passkeyUserId.trim()) this.loadPasskeys();
        if (this.mfaUserId.trim()) this.loadMfaSettings();
        break;
      case 'devices':
        if (this.deviceUserId.trim()) this.loadDevices();
        break;
      case 'sessions':
        if (this.sessionUserId.trim()) this.loadSessions();
        break;
      case 'history':
        this.loadLoginHistory();
        break;
      case 'rate-limit':
        this.loadRateLimits();
        break;
      case 'geo-security':
        this.loadGeoSecurity();
        break;
      case 'threats':
        this.loadThreats();
        break;
      case 'lockouts':
        this.loadLockouts();
        break;
      case 'ip-whitelist':
        this.loadIpWhitelist();
        break;
    }
  }

  // ─── Auto Refresh ───

  toggleAutoRefresh() {
    this.autoRefresh.update((v) => !v);
    if (this.autoRefresh()) {
      this.startAutoRefresh();
    } else {
      this.stopAutoRefresh();
    }
  }

  private startAutoRefresh() {
    this.stopAutoRefresh();
    if (this.autoRefresh()) {
      this.refreshInterval = setInterval(() => this.loadTabData(this.activeTab()), 30000);
    }
  }

  private stopAutoRefresh() {
    if (this.refreshInterval) {
      clearInterval(this.refreshInterval);
      this.refreshInterval = null;
    }
  }

  refresh() {
    this.loadTabData(this.activeTab());
  }

  // ─── Overview / Score ───

  loadOverview() {
    this.loading.set(true);
    this.advancedSecurityService.getSecurityScore().subscribe({
      next: (data) => {
        this.securityScore.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Không thể tải điểm bảo mật', 'error');
        this.loading.set(false);
      },
    });
  }

  getScoreColor(): string {
    const score = this.securityScore()?.score ?? 100;
    if (score >= 80) return '#22c55e';
    if (score >= 50) return '#f59e0b';
    return '#ef4444';
  }

  getScoreDescription(): string {
    const level = this.scoreLevel();
    if (level === 'good') return 'Hệ thống an toàn';
    if (level === 'warning') return 'Cần chú ý';
    return 'Cảnh báo nghiêm trọng';
  }

  // ─── Passkeys ───

  checkPasskeySupport() {
    if (typeof window !== 'undefined' && window.PublicKeyCredential) {
      window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable().then((available) =>
        this.passkeysSupported.set(available),
      );
    }
  }

  loadPasskeys() {
    const userId = this.passkeyUserId.trim();
    if (!userId) return;
    this.loading.set(true);
    this.advancedSecurityService.getPasskeys(userId).subscribe({
      next: (data) => {
        this.passkeys.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Không thể tải passkeys', 'error');
        this.loading.set(false);
      },
    });
  }

  async registerPasskey() {
    const userId = this.passkeyUserId.trim();
    if (!userId) {
      this.showStatus('Vui lòng chọn người dùng', 'error');
      return;
    }
    this.passkeyRegistering.set(true);

    this.advancedSecurityService.beginPasskeyRegistration({ userId }).subscribe({
      next: async (serverOptions) => {
        try {
          // Convert server JSON to WebAuthn-compatible PublicKeyCredentialCreationOptions
          const publicKey: PublicKeyCredentialCreationOptions = {
            rp: serverOptions.rp,
            user: {
              ...serverOptions.user,
              id: this.base64urlToBuffer(serverOptions.user.id),
            },
            challenge: this.base64urlToBuffer(serverOptions.challenge),
            pubKeyCredParams: serverOptions.pubKeyCredParams,
            timeout: serverOptions.timeout,
            attestation: serverOptions.attestation,
            authenticatorSelection: serverOptions.authenticatorSelection,
            excludeCredentials: (serverOptions.excludeCredentials || []).map((c: any) => ({
              ...c,
              id: this.base64urlToBuffer(c.id),
            })),
          };

          const credential = (await navigator.credentials.create({
            publicKey,
          })) as PublicKeyCredential;
          if (!credential) {
            this.showStatus('Đăng ký passkey bị hủy', 'error');
            this.passkeyRegistering.set(false);
            return;
          }
          const response = credential.response as AuthenticatorAttestationResponse;
          const attestationResponse = {
            id: credential.id,
            rawId: this.bufferToBase64url(credential.rawId),
            type: credential.type,
            response: {
              attestationObject: this.bufferToBase64url(response.attestationObject),
              clientDataJSON: this.bufferToBase64url(response.clientDataJSON),
            },
          };

          this.advancedSecurityService
            .completePasskeyRegistration({
              userId,
              attestationResponse,
              deviceName: this.passkeyDeviceName || undefined,
            })
            .subscribe({
              next: () => {
                this.showStatus('Đăng ký passkey thành công', 'success');
                this.passkeyDeviceName = '';
                this.passkeyRegistering.set(false);
                this.loadPasskeys();
              },
              error: () => {
                this.showStatus('Lỗi hoàn tất đăng ký passkey', 'error');
                this.passkeyRegistering.set(false);
              },
            });
        } catch {
          this.showStatus('Đăng ký passkey thất bại', 'error');
          this.passkeyRegistering.set(false);
        }
      },
      error: () => {
        this.showStatus('Lỗi bắt đầu đăng ký passkey', 'error');
        this.passkeyRegistering.set(false);
      },
    });
  }

  revokePasskey(id: string) {
    if (!confirm('Xác nhận thu hồi passkey này?')) return;
    this.advancedSecurityService.revokePasskey(id).subscribe({
      next: () => {
        this.showStatus('Đã thu hồi passkey', 'success');
        this.loadPasskeys();
      },
      error: () => this.showStatus('Lỗi thu hồi passkey', 'error'),
    });
  }

  renamePasskey(id: string) {
    const name = prompt('Nhập tên mới cho passkey:');
    if (!name) return;
    this.advancedSecurityService.renamePasskey(id, { deviceName: name }).subscribe({
      next: () => {
        this.showStatus('Đã đổi tên passkey', 'success');
        this.loadPasskeys();
      },
      error: () => this.showStatus('Lỗi đổi tên passkey', 'error'),
    });
  }

  private bufferToBase64url(buffer: ArrayBuffer): string {
    const bytes = new Uint8Array(buffer);
    let str = '';
    for (const b of bytes) str += String.fromCharCode(b);
    return btoa(str).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
  }

  private base64urlToBuffer(base64url: string): ArrayBuffer {
    let base64 = base64url.replace(/-/g, '+').replace(/_/g, '/');
    while (base64.length % 4) base64 += '=';
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    return bytes.buffer;
  }

  // ─── MFA Settings ───

  loadMfaSettings() {
    const userId = this.mfaUserId.trim();
    if (!userId) return;
    this.loading.set(true);
    this.advancedSecurityService.getMfaSettings(userId).subscribe({
      next: (data) => {
        this.mfaSettings.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Không thể tải cài đặt MFA', 'error');
        this.loading.set(false);
      },
    });
  }

  disableMfa() {
    const userId = this.mfaUserId.trim();
    if (!userId || !confirm('Xác nhận tắt MFA cho người dùng này?')) return;
    this.advancedSecurityService.disableMfa(userId).subscribe({
      next: () => {
        this.showStatus('Đã tắt MFA', 'success');
        this.loadMfaSettings();
      },
      error: () => this.showStatus('Lỗi tắt MFA', 'error'),
    });
  }

  // ─── TOTP ───

  setupTotp() {
    const userId = this.totpUserId.trim();
    if (!userId) return;
    this.advancedSecurityService.setupTotp(userId).subscribe({
      next: (data) => {
        this.totpSetup.set(data);
        this.generateTotpQr(data.otpauthUri);
        this.showStatus('Đã tạo TOTP secret. Quét mã QR bằng ứng dụng xác thực.', 'success');
      },
      error: () => this.showStatus('Lỗi thiết lập TOTP', 'error'),
    });
  }

  private generateTotpQr(otpauthUri: string) {
    QRCode.toDataURL(otpauthUri, { width: 256, margin: 2 })
      .then((url: string) => this.totpQrDataUrl.set(url))
      .catch(() => this.totpQrDataUrl.set(''));
  }

  verifyTotp() {
    const userId = this.totpUserId.trim();
    const code = this.totpCode.trim();
    if (!userId || !code) return;
    this.advancedSecurityService.verifyTotp({ userId, code }).subscribe({
      next: (res) => {
        this.showStatus(res.message || 'TOTP đã xác minh thành công', 'success');
        this.totpCode = '';
        this.totpSetup.set(null);
      },
      error: () => this.showStatus('Mã TOTP không hợp lệ', 'error'),
    });
  }

  // ─── SMS OTP ───

  registerSmsOtp() {
    const userId = this.smsUserId.trim();
    const phone = this.smsPhone.trim();
    if (!userId || !phone) return;
    this.advancedSecurityService.registerSmsOtp({ userId, phoneNumber: phone }).subscribe({
      next: () => this.showStatus('Đã gửi mã xác minh SMS', 'success'),
      error: () => this.showStatus('Lỗi đăng ký SMS OTP', 'error'),
    });
  }

  verifySmsOtp() {
    const userId = this.smsUserId.trim();
    const code = this.smsCode.trim();
    if (!userId || !code) return;
    this.advancedSecurityService.verifySmsOtp({ userId, code }).subscribe({
      next: () => {
        this.showStatus('Xác minh SMS thành công', 'success');
        this.smsCode = '';
      },
      error: () => this.showStatus('Mã SMS không hợp lệ', 'error'),
    });
  }

  sendSmsOtp() {
    const userId = this.smsUserId.trim();
    if (!userId) return;
    this.advancedSecurityService.sendSmsOtp(userId).subscribe({
      next: () => this.showStatus('Đã gửi mã OTP qua SMS', 'success'),
      error: () => this.showStatus('Lỗi gửi SMS OTP', 'error'),
    });
  }

  // ─── Login History ───

  loadLoginHistory() {
    this.loading.set(true);
    this.advancedSecurityService.getLoginHistory(50).subscribe({
      next: (data) => {
        this.loginHistory.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Không thể tải lịch sử đăng nhập', 'error');
        this.loading.set(false);
      },
    });
  }

  // ─── Rate Limits ───

  loadRateLimits() {
    this.loading.set(true);
    this.advancedSecurityService.getRateLimitStatus().subscribe({
      next: (data) => {
        this.rateLimitStatus.set(data);
        this.advancedSecurityService.getRateLimitEvents(24).subscribe({
          next: (events) => {
            this.rateLimitEvents.set(events);
            this.loading.set(false);
          },
          error: () => this.loading.set(false),
        });
      },
      error: () => {
        this.showStatus('Không thể tải trạng thái rate limit', 'error');
        this.loading.set(false);
      },
    });
  }

  openRateLimitForm(config?: RateLimitCustomConfig) {
    if (config) {
      this.editingRateLimit.set(config);
      this.rlForm = {
        policyName: config.policyName,
        windowType: config.windowType,
        windowSeconds: config.windowSeconds,
        permitLimit: config.permitLimit,
        appliesTo: config.appliesTo || '',
        description: config.description || '',
      };
    } else {
      this.editingRateLimit.set(null);
      this.rlForm = {
        policyName: '',
        windowType: 'fixed',
        windowSeconds: 60,
        permitLimit: 100,
        appliesTo: '',
        description: '',
      };
    }
    this.showRateLimitForm.set(true);
  }

  saveRateLimit() {
    const editing = this.editingRateLimit();
    if (editing) {
      this.advancedSecurityService
        .updateRateLimit(editing.id, {
          windowType: this.rlForm.windowType,
          windowSeconds: this.rlForm.windowSeconds,
          permitLimit: this.rlForm.permitLimit,
          description: this.rlForm.description || undefined,
        })
        .subscribe({
          next: () => {
            this.showStatus('Đã cập nhật rate limit', 'success');
            this.showRateLimitForm.set(false);
            this.loadRateLimits();
          },
          error: () => this.showStatus('Lỗi cập nhật rate limit', 'error'),
        });
    } else {
      this.advancedSecurityService
        .createRateLimit({
          policyName: this.rlForm.policyName,
          windowType: this.rlForm.windowType,
          windowSeconds: this.rlForm.windowSeconds,
          permitLimit: this.rlForm.permitLimit,
          appliesTo: this.rlForm.appliesTo || undefined,
          description: this.rlForm.description || undefined,
        })
        .subscribe({
          next: () => {
            this.showStatus('Đã tạo rate limit', 'success');
            this.showRateLimitForm.set(false);
            this.loadRateLimits();
          },
          error: () => this.showStatus('Lỗi tạo rate limit', 'error'),
        });
    }
  }

  deleteRateLimit(id: string) {
    if (!confirm('Xác nhận xóa rate limit?')) return;
    this.advancedSecurityService.deleteRateLimit(id).subscribe({
      next: () => {
        this.showStatus('Đã xóa rate limit', 'success');
        this.loadRateLimits();
      },
      error: () => this.showStatus('Lỗi xóa rate limit', 'error'),
    });
  }

  // ─── Geo Security ───

  loadGeoSecurity() {
    this.loading.set(true);
    this.advancedSecurityService.getGeoSecurityData(48).subscribe({
      next: (data) => {
        this.geoData.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Không thể tải dữ liệu Geo Security', 'error');
        this.loading.set(false);
      },
    });
  }

  openGeoRuleForm() {
    this.geoRuleForm = { countryCode: '', countryName: '', isBlocked: true, reason: '' };
    this.showGeoRuleForm.set(true);
  }

  saveGeoBlockRule() {
    if (!this.geoRuleForm.countryCode || !this.geoRuleForm.countryName) return;
    this.advancedSecurityService
      .createGeoBlockRule({
        countryCode: this.geoRuleForm.countryCode,
        countryName: this.geoRuleForm.countryName,
        isBlocked: this.geoRuleForm.isBlocked,
        reason: this.geoRuleForm.reason || undefined,
      })
      .subscribe({
        next: () => {
          this.showStatus('Đã tạo luật chặn Geo', 'success');
          this.showGeoRuleForm.set(false);
          this.loadGeoSecurity();
        },
        error: () => this.showStatus('Lỗi tạo luật Geo', 'error'),
      });
  }

  deleteGeoBlockRule(id: string) {
    if (!confirm('Xác nhận xóa luật chặn Geo?')) return;
    this.advancedSecurityService.deleteGeoBlockRule(id).subscribe({
      next: () => {
        this.showStatus('Đã xóa luật Geo', 'success');
        this.loadGeoSecurity();
      },
      error: () => this.showStatus('Lỗi xóa luật Geo', 'error'),
    });
  }

  // ─── Threats ───

  loadThreats() {
    this.loading.set(true);
    this.advancedSecurityService.getThreatOverview(24).subscribe({
      next: (data) => {
        this.threatOverview.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Không thể tải dữ liệu mối đe dọa', 'error');
        this.loading.set(false);
      },
    });
  }

  runQuickAssessment() {
    if (!this.threatAssessForm.ipAddress.trim()) return;
    this.loading.set(true);
    this.securityService.assessThreat(this.threatAssessForm).subscribe({
      next: (result) => {
        this.threatAssessResult.set(result);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi đánh giá mối đe dọa', 'error');
        this.loading.set(false);
      },
    });
  }

  quickIpLookup() {
    if (!this.threatIpLookup.trim()) return;
    this.loading.set(true);
    this.securityService.checkIpIntelligence(this.threatIpLookup.trim()).subscribe({
      next: (result) => {
        this.threatIpResult.set(result);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Lỗi kiểm tra IP', 'error');
        this.loading.set(false);
      },
    });
  }

  getRiskLevelClass(level: string): string {
    switch (level) {
      case 'Critical':
        return 'severity-critical';
      case 'High':
        return 'severity-high';
      case 'Medium':
        return 'severity-warning';
      case 'Low':
        return 'severity-info';
      default:
        return '';
    }
  }

  getRiskBarWidth(score: number): number {
    return Math.min(score, 100);
  }

  // ─── Account Lockouts ───

  loadLockouts() {
    this.loading.set(true);
    this.advancedSecurityService.getAccountLockouts().subscribe({
      next: (data) => {
        this.lockouts.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Không thể tải danh sách khóa tài khoản', 'error');
        this.loading.set(false);
      },
    });
  }

  openLockForm() {
    this.lockForm = {
      userId: this.currentUserId(),
      username: this.currentUsername(),
      reason: '',
      durationMinutes: 30,
      failedAttempts: 0,
    };
    this.showLockForm.set(true);
  }

  saveLockAccount() {
    if (!this.lockForm.userId || !this.lockForm.username || !this.lockForm.reason) return;
    this.advancedSecurityService
      .lockAccount({
        userId: this.lockForm.userId,
        username: this.lockForm.username,
        reason: this.lockForm.reason,
        durationMinutes: this.lockForm.durationMinutes,
        failedAttempts: this.lockForm.failedAttempts,
      })
      .subscribe({
        next: () => {
          this.showStatus('Đã khóa tài khoản', 'success');
          this.showLockForm.set(false);
          this.loadLockouts();
        },
        error: () => this.showStatus('Lỗi khóa tài khoản', 'error'),
      });
  }

  unlockAccount(id: string) {
    if (!confirm('Xác nhận mở khóa tài khoản này?')) return;
    this.advancedSecurityService.unlockAccount(id).subscribe({
      next: () => {
        this.showStatus('Đã mở khóa tài khoản', 'success');
        this.loadLockouts();
      },
      error: () => this.showStatus('Không thể mở khóa tài khoản', 'error'),
    });
  }

  // ─── IP Whitelist ───

  isCurrentIpWhitelisted(): boolean {
    const currentIp = this.publicIp();
    if (!currentIp) return true; // Don't warn if we can't determine IP
    return this.ipWhitelist().some((entry) => entry.isActive && entry.ipAddress === currentIp);
  }

  loadIpWhitelist() {
    this.loading.set(true);
    this.advancedSecurityService.getIpWhitelist().subscribe({
      next: (data) => {
        this.ipWhitelist.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Không thể tải danh sách IP whitelist', 'error');
        this.loading.set(false);
      },
    });
  }

  addIpToWhitelist() {
    const ip = this.newIp.trim();
    if (!ip) return;

    const ipRegex =
      /^(?:(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(?:25[0-5]|2[0-4]\d|[01]?\d\d?)(?:\/\d{1,2})?$/;
    if (!ipRegex.test(ip)) {
      this.showStatus('Địa chỉ IP không hợp lệ', 'error');
      return;
    }

    this.advancedSecurityService
      .addIpToWhitelist({
        ipAddress: ip,
        description: this.newIpDescription || undefined,
        expiresInDays: this.newIpExpiresDays ?? undefined,
        cidrRange: this.newIpCidrRange || undefined,
      })
      .subscribe({
        next: () => {
          this.showStatus('Đã thêm IP vào whitelist', 'success');
          this.newIp = '';
          this.newIpDescription = '';
          this.newIpCidrRange = '';
          this.newIpExpiresDays = null;
          this.loadIpWhitelist();
        },
        error: () => this.showStatus('Không thể thêm IP', 'error'),
      });
  }

  removeIpFromWhitelist(id: string, ipAddress: string) {
    if (!confirm(`Xác nhận xóa IP ${ipAddress} khỏi whitelist?`)) return;
    this.advancedSecurityService.removeIpFromWhitelist(id).subscribe({
      next: () => {
        this.showStatus('Đã xóa IP khỏi whitelist', 'success');
        this.loadIpWhitelist();
      },
      error: () => this.showStatus('Không thể xóa IP', 'error'),
    });
  }

  // ─── Sessions ───

  loadSessions() {
    const userId = this.sessionUserId.trim();
    if (!userId) return;
    this.loading.set(true);
    this.securityService.getActiveSessions(userId).subscribe({
      next: (data) => {
        this.sessions.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Không thể tải phiên hoạt động', 'error');
        this.loading.set(false);
      },
    });
  }

  revokeSession(sessionId: string) {
    if (!confirm('Xác nhận thu hồi phiên?')) return;
    this.securityService.revokeSession(sessionId).subscribe({
      next: () => {
        this.sessions.update((list) => list.filter((s) => s.sessionId !== sessionId));
        this.showStatus('Đã thu hồi phiên', 'success');
      },
      error: () => this.showStatus('Không thể thu hồi phiên', 'error'),
    });
  }

  // ─── Devices ───

  loadDevices() {
    const userId = this.deviceUserId.trim();
    if (!userId) return;
    this.loading.set(true);
    this.advancedSecurityService.getUserDevices(userId).subscribe({
      next: (data) => {
        this.devices.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.showStatus('Không thể tải thiết bị', 'error');
        this.loading.set(false);
      },
    });
  }

  trustDevice(id: string) {
    this.advancedSecurityService.trustDevice(id).subscribe({
      next: () => {
        this.showStatus('Đã tin cậy thiết bị', 'success');
        this.loadDevices();
      },
      error: () => this.showStatus('Lỗi tin cậy thiết bị', 'error'),
    });
  }

  removeDevice(id: string) {
    if (!confirm('Xác nhận xóa thiết bị?')) return;
    this.advancedSecurityService.removeDevice(id).subscribe({
      next: () => {
        this.showStatus('Đã xóa thiết bị', 'success');
        this.loadDevices();
      },
      error: () => this.showStatus('Lỗi xóa thiết bị', 'error'),
    });
  }

  // ─── Helpers ───

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString('vi-VN');
  }

  formatTimeAgo(dateStr: string): string {
    const diff = Date.now() - new Date(dateStr).getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 1) return 'vừa xong';
    if (mins < 60) return `${mins} phút trước`;
    const hours = Math.floor(mins / 60);
    if (hours < 24) return `${hours} giờ trước`;
    const days = Math.floor(hours / 24);
    return `${days} ngày trước`;
  }

  getDeviceIcon(userAgent: string | null): string {
    if (!userAgent) return '🖥️';
    const ua = userAgent.toLowerCase();
    if (ua.includes('mobile') || ua.includes('android') || ua.includes('iphone')) return '📱';
    if (ua.includes('tablet') || ua.includes('ipad')) return '📱';
    return '🖥️';
  }

  getBrowserName(userAgent: string | null): string {
    if (!userAgent) return 'Unknown';
    if (userAgent.includes('Chrome')) return 'Chrome';
    if (userAgent.includes('Firefox')) return 'Firefox';
    if (userAgent.includes('Safari')) return 'Safari';
    if (userAgent.includes('Edge')) return 'Edge';
    return 'Other';
  }

  getOsName(userAgent: string | null): string {
    if (!userAgent) return 'Unknown';
    if (userAgent.includes('Windows')) return 'Windows';
    if (userAgent.includes('Mac OS')) return 'macOS';
    if (userAgent.includes('Linux')) return 'Linux';
    if (userAgent.includes('Android')) return 'Android';
    if (userAgent.includes('iOS') || userAgent.includes('iPhone')) return 'iOS';
    return 'Other';
  }

  formatEventType(type: string): string {
    const labels: Record<string, string> = {
      AUTH_LOGIN_SUCCESS: 'Đăng nhập thành công',
      AUTH_LOGIN_FAILED: 'Đăng nhập thất bại',
      AUTH_BRUTE_FORCE: 'Brute Force',
      THREAT_IMPOSSIBLE_TRAVEL: 'Di chuyển bất thường',
      THREAT_ANOMALOUS_ACCESS: 'Truy cập bất thường',
      THREAT_TOR_EXIT: 'Tor Exit Node',
      THREAT_VPN_PROXY: 'VPN/Proxy',
      THREAT_RATE_LIMIT: 'Rate Limit',
      THREAT_SQL_INJECTION: 'SQL Injection',
      THREAT_XSS_ATTEMPT: 'XSS Attempt',
      THREAT_PATH_TRAVERSAL: 'Path Traversal',
      THREAT_COMMAND_INJECTION: 'Command Injection',
      ZT_ACCESS_DENIED: 'ZT Từ chối',
      ZT_DEVICE_UNTRUSTED: 'Thiết bị không tin cậy',
      ZT_GEO_FENCE_VIOLATION: 'Vi phạm Geo-fence',
      // Security score factors
      critical_events: 'Sự kiện nghiêm trọng',
      failed_logins: 'Đăng nhập thất bại',
      blocked_requests: 'Yêu cầu bị chặn',
      suspicious_logins: 'Đăng nhập đáng ngờ',
      no_mfa_users: 'Người dùng chưa bật MFA',
      active_lockouts: 'Tài khoản đang bị khóa',
      untrusted_devices: 'Thiết bị không tin cậy',
      no_passkeys: 'Chưa đăng ký Passkey',
      low_trusted_devices: 'Ít thiết bị tin cậy',
      high_risk_sessions: 'Phiên rủi ro cao',
    };
    return labels[type] || type;
  }

  getThreatBarWidth(count: number, max: number): number {
    return max > 0 ? Math.round((count / max) * 100) : 0;
  }

  getRiskScoreClass(score: number | null): string {
    if (!score) return '';
    if (score >= 70) return 'risk-high';
    if (score >= 40) return 'risk-medium';
    return 'risk-low';
  }

  navigateTo(path: string) {
    this.router.navigate([path]);
  }

  private showStatus(text: string, type: 'success' | 'error') {
    this.statusMessage.set({ text, type });
    setTimeout(() => this.statusMessage.set(null), 4000);
  }
}
