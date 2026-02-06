import { Component, inject, OnInit, OnDestroy, signal, ElementRef, ViewChild, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import {
    PatientBiometricsService,
    PatientFingerprintDto,
    FingerprintType,
    FingerprintSdkType,
    RegisterFingerprintRequest
} from '../../../core/services/patient-biometrics.service';
import { GlobalNotificationService } from '../../../core/services/global-notification.service';
import { DigitalPersonaService } from '../../../core/services/fingerprint/digital-persona.service';
import { SecuGenService } from '../../../core/services/fingerprint/secugen.service';
import { ImageResizeService } from '../../../core/services/image-resize.service';
import { HandFingerSelectorComponent } from '../../../shared/components/hand-finger-selector/hand-finger-selector.component';
import { FingerprintHubService, CaptureResult, CaptureStatus, EnrollmentProgress, VerificationResult } from '../../../core/services/fingerprint/fingerprint-hub.service';
import { AuthService } from '../../../core/services/auth.service';
import { Subscription } from 'rxjs';

@Component({
    selector: 'app-patient-biometrics',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule, HandFingerSelectorComponent],
    templateUrl: './patient-biometrics.component.html',
    styleUrls: ['./patient-biometrics.component.scss']
})
export class PatientBiometricsComponent implements OnInit, OnDestroy {
    @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;
    @ViewChild('videoElement') videoElement!: ElementRef<HTMLVideoElement>;
    @ViewChild('canvasElement') canvasElement!: ElementRef<HTMLCanvasElement>;

    private route = inject(ActivatedRoute);
    private biometricsService = inject(PatientBiometricsService);
    private notificationService = inject(GlobalNotificationService);
    private dpService = inject(DigitalPersonaService);
    private sgService = inject(SecuGenService);
    private imageResizeService = inject(ImageResizeService);
    private fingerprintHub = inject(FingerprintHubService);
    private authService = inject(AuthService);

    private subscriptions = new Subscription();

    patientId = signal<string>('');
    activeTab = signal<'photo' | 'fingerprint'>('photo');

    // Photo state
    photoUrl = signal<string>('');
    hasPhoto = signal<boolean>(false);
    uploading = signal<boolean>(false);
    showWebcam = signal<boolean>(false);

    // Fingerprint state
    fingerprints = signal<PatientFingerprintDto[]>([]);
    enrolledFingerTypes = computed(() => this.fingerprints().map(f => f.fingerType));
    selectedSdk = signal<FingerprintSdkType>(FingerprintSdkType.DigitalPersona);
    selectedFinger = signal<FingerprintType>(FingerprintType.RightIndex);
    capturing = signal<boolean>(false);
    capturedData = signal<string>('');
    capturedQuality = signal<number>(0);

    // SDK readiness
    sdkReady = signal<boolean>(false);
    sdkStatus = signal<string>('Chưa kết nối');

    // SignalR mode
    useSignalR = signal<boolean>(true);  // Default to SignalR mode
    signalRConnected = signal<boolean>(false);
    captureProgress = signal<string>('');
    fingerprintPreview = signal<string>('');  // Base64 image preview

    ngOnInit(): void {
        this.route.params.subscribe(params => {
            const id = params['id'];
            if (id) {
                this.patientId.set(id);
                this.loadData();

                // Initialize SignalR after patientId is set
                if (this.useSignalR()) {
                    // Connect first (if not already), then join group
                    this.fingerprintHub.connect(this.authService.getToken() || '')
                        .then(() => {
                            if (this.fingerprintHub.isConnected()) {
                                this.fingerprintHub.joinPatientCapture(id);
                                this.signalRConnected.set(true);
                            }
                        });
                }
            }
        });

        // Subscribe to SignalR events
        this.setupSignalRSubscriptions();
    }

    ngOnDestroy(): void {
        this.subscriptions.unsubscribe();
        // Leave patient capture group and disconnect
        const pid = this.patientId();
        if (pid && this.signalRConnected()) {
            this.fingerprintHub.leavePatientCapture(pid).catch(console.error);
        }
    }

    private setupSignalRSubscriptions(): void {
        // Capture result from WinForms app
        this.subscriptions.add(
            this.fingerprintHub.captureResult$.subscribe(result => {
                this.handleCaptureResult(result);
            })
        );

        // Capture status updates
        this.subscriptions.add(
            this.fingerprintHub.captureStatus$.subscribe(status => {
                this.handleCaptureStatus(status);
            })
        );

        // Enrollment progress
        this.subscriptions.add(
            this.fingerprintHub.enrollmentProgress$.subscribe(progress => {
                this.handleEnrollmentProgress(progress);
            })
        );

        // Capture cancelled
        this.subscriptions.add(
            this.fingerprintHub.captureCancelled$.subscribe(() => {
                this.capturing.set(false);
                this.captureProgress.set('Đã hủy');
            })
        );

        // Verification results
        this.subscriptions.add(
            this.fingerprintHub.verificationResult$.subscribe(result => {
                if (result.success) {
                    this.notificationService.success('Xác thực', `✅ KHỚP mẫu vân tay: ${this.getFingerTypeName(result.fingerType || '')}`);
                } else {
                    this.notificationService.error('Xác thực', '❌ Không khớp mẫu vân tay nào');
                }
            })
        );
    }

    private handleCaptureResult(result: CaptureResult): void {
        this.capturing.set(false);

        if (result.success) {
            this.capturedData.set(result.templateData || '');
            this.capturedQuality.set(parseInt(result.quality || '0', 10) || 80);
            if (result.imageData) {
                this.fingerprintPreview.set('data:image/jpeg;base64,' + result.imageData);
            }
            this.captureProgress.set('Hoàn thành!');
            this.notificationService.success('Thành công', 'Đã chụp vân tay');
        } else {
            this.captureProgress.set('Thất bại: ' + (result.errorMessage || 'Lỗi không xác định'));
            this.notificationService.error('Lỗi', result.errorMessage || 'Không thể chụp vân tay');
        }
    }

    private handleCaptureStatus(status: CaptureStatus): void {
        const statusMessages: Record<string, string> = {
            'Starting': 'Đang khởi động...',
            'WaitingForFinger': 'Đặt ngón tay lên máy quét...',
            'Capturing': 'Đang quét...',
            'Processing': 'Đang xử lý...',
            'Completed': 'Hoàn thành!',
            'Failed': 'Thất bại'
        };
        this.captureProgress.set(statusMessages[status.status] || status.message || status.status);
    }

    private handleEnrollmentProgress(progress: EnrollmentProgress): void {
        this.captureProgress.set(`Mẫu ${progress.samplesCollected}/${progress.samplesNeeded}`);
    }

    loadData(): void {
        // Load photo using authenticated request
        this.loadPhoto();

        // Load fingerprints
        this.biometricsService.getFingerprints(this.patientId()).subscribe({
            next: fps => this.fingerprints.set(fps),
            error: () => this.fingerprints.set([])
        });
    }

    loadPhoto(): void {
        console.log('[Biometrics] Loading photo for patient:', this.patientId());
        this.biometricsService.getPhotoBlob(this.patientId()).subscribe({
            next: blob => {
                console.log('[Biometrics] Photo blob received:', blob.size, 'bytes, type:', blob.type);
                // Create object URL from blob for authenticated image display
                const objectUrl = URL.createObjectURL(blob);
                console.log('[Biometrics] Object URL created:', objectUrl);
                this.photoUrl.set(objectUrl);
                this.hasPhoto.set(true);
                console.log('[Biometrics] hasPhoto set to true, photoUrl set to:', this.photoUrl());
            },
            error: (err) => {
                console.log('[Biometrics] Photo load error:', err);
                this.hasPhoto.set(false);
                this.photoUrl.set('');
            }
        });
    }

    // ==================== Photo Methods ====================

    openFileSelector(): void {
        this.fileInput.nativeElement.click();
    }

    onFileSelected(event: Event): void {
        const input = event.target as HTMLInputElement;
        if (input.files && input.files[0]) {
            this.uploadFile(input.files[0]);
        }
    }

    async uploadFile(file: File): Promise<void> {
        if (!file.type.startsWith('image/')) {
            this.notificationService.error('Lỗi', 'Vui lòng chọn file ảnh');
            return;
        }

        this.uploading.set(true);

        try {
            // Optimize image before upload (resize to max 400x400 for profile photos)
            const needsResize = await this.imageResizeService.needsResize(file, 400, 400, 200);
            let fileToUpload = file;

            if (needsResize) {
                console.log('[Biometrics] Optimizing image:', file.name, 'size:', file.size);
                const result = await this.imageResizeService.optimizeForProfile(file);
                fileToUpload = result.file;
                console.log('[Biometrics] Optimized:', result.originalSize, '->', result.newSize,
                    'bytes, compression:', result.compressionRatio + '%');
            }

            this.biometricsService.uploadPhoto(this.patientId(), fileToUpload).subscribe({
                next: () => {
                    this.notificationService.success('Thành công', 'Đã tải ảnh lên');
                    this.uploading.set(false);
                    this.showWebcam.set(false);
                    // Reload photo using authenticated blob fetch
                    this.loadPhoto();
                },
                error: err => {
                    this.notificationService.error('Lỗi', err.error || 'Không thể tải ảnh');
                    this.uploading.set(false);
                }
            });
        } catch (error) {
            console.error('[Biometrics] Image optimization error:', error);
            this.notificationService.error('Lỗi', 'Không thể xử lý ảnh');
            this.uploading.set(false);
        }
    }

    deletePhoto(): void {
        if (confirm('Bạn có chắc muốn xóa ảnh?')) {
            this.biometricsService.deletePhoto(this.patientId()).subscribe({
                next: () => {
                    this.notificationService.success('Thành công', 'Đã xóa ảnh');
                    this.hasPhoto.set(false);
                },
                error: err => this.notificationService.error('Lỗi', err.error || 'Không thể xóa')
            });
        }
    }

    // Webcam capture
    async openWebcam(): Promise<void> {
        this.showWebcam.set(true);
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ video: true });
            this.videoElement.nativeElement.srcObject = stream;
        } catch (err) {
            this.notificationService.error('Lỗi', 'Không thể truy cập webcam');
            this.showWebcam.set(false);
        }
    }

    captureFromWebcam(): void {
        const video = this.videoElement.nativeElement;
        const canvas = this.canvasElement.nativeElement;
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        canvas.getContext('2d')!.drawImage(video, 0, 0);

        canvas.toBlob(blob => {
            if (blob) {
                const file = new File([blob], 'webcam-capture.jpg', { type: 'image/jpeg' });
                this.uploadFile(file);
            }
        }, 'image/jpeg', 0.9);
    }

    closeWebcam(): void {
        this.showWebcam.set(false);
        const video = this.videoElement?.nativeElement;
        if (video && video.srcObject) {
            (video.srcObject as MediaStream).getTracks().forEach(track => track.stop());
        }
    }

    // ==================== Fingerprint Methods ====================

    selectSdk(sdk: FingerprintSdkType): void {
        this.selectedSdk.set(sdk);
        this.initializeSdk();
    }

    async initializeSdk(): Promise<void> {
        // If using SignalR mode, connect to hub instead of SDK
        if (this.useSignalR()) {
            await this.initializeSignalR();
            return;
        }

        this.sdkStatus.set('Đang kết nối...');
        this.sdkReady.set(false);

        try {
            if (this.selectedSdk() === FingerprintSdkType.DigitalPersona) {
                await this.dpService.initialize();
                if (this.dpService.isReady()) {
                    this.sdkReady.set(true);
                    this.sdkStatus.set('Đã kết nối DigitalPersona');
                    this.notificationService.info('SDK', 'DigitalPersona đã sẵn sàng');
                } else {
                    this.sdkStatus.set(this.dpService.errorMessage() || 'Lỗi kết nối');
                }
            } else {
                await this.sgService.initialize();
                if (this.sgService.isReady()) {
                    this.sdkReady.set(true);
                    this.sdkStatus.set('Đã kết nối SecuGen');
                    this.notificationService.info('SDK', 'SecuGen đã sẵn sàng');
                } else {
                    this.sdkStatus.set(this.sgService.errorMessage() || 'Lỗi kết nối');
                }
            }
        } catch (error: any) {
            this.sdkStatus.set('Lỗi: ' + error.message);
            this.notificationService.error('SDK', error.message);
        }
    }

    private async initializeSignalR(): Promise<void> {
        this.sdkStatus.set('Đang kết nối SignalR...');
        this.sdkReady.set(false);

        try {
            const token = this.authService.getToken();
            if (!token) {
                this.sdkStatus.set('Chưa đăng nhập');
                return;
            }

            const connected = await this.fingerprintHub.connect(token);
            if (connected) {
                await this.fingerprintHub.joinPatientCapture(this.patientId());
                this.signalRConnected.set(true);
                this.sdkReady.set(true);
                this.sdkStatus.set('Đã kết nối (SignalR) - Mở ứng dụng vân tay');
                this.notificationService.info('SignalR', 'Đã kết nối - Mở ứng dụng DigitalPersona trên máy tính');
            } else {
                this.sdkStatus.set('Không thể kết nối SignalR');
                this.notificationService.error('SignalR', 'Không thể kết nối');
            }
        } catch (error: any) {
            this.sdkStatus.set('Lỗi SignalR: ' + error.message);
            this.notificationService.error('SignalR', error.message);
        }
    }

    async captureFingerprint(): Promise<void> {
        if (!this.sdkReady()) {
            this.notificationService.warning('Cảnh báo', 'Chưa kết nối');
            return;
        }

        this.capturing.set(true);
        this.capturedData.set('');
        this.fingerprintPreview.set('');
        this.captureProgress.set('');

        try {
            // SignalR mode - send capture request to WinForms app
            if (this.useSignalR()) {
                this.captureProgress.set('Đang chờ ứng dụng máy tính...');
                this.fingerprintHub.requestCapture(this.patientId(), this.selectedFinger().toString());
                // Result will come via captureResult$ subscription
                return;
            }

            // Direct SDK mode (browser-based)
            let result: { template: string; quality: number } | null = null;

            if (this.selectedSdk() === FingerprintSdkType.DigitalPersona) {
                result = await this.dpService.capture();
            } else {
                result = await this.sgService.capture();
            }

            if (result) {
                this.capturedData.set(result.template);
                this.capturedQuality.set(result.quality);
                this.notificationService.success('Thành công', `Đã chụp vân tay (Chất lượng: ${result.quality}%)`);
            } else {
                this.notificationService.error('Lỗi', 'Không thể chụp vân tay');
            }
        } catch (error: any) {
            this.notificationService.error('Lỗi', error.message);
        } finally {
            if (!this.useSignalR()) {
                this.capturing.set(false);
            }
        }
    }

    cancelCapture(): void {
        if (this.useSignalR() && this.capturing()) {
            this.fingerprintHub.cancelCapture(this.patientId()).catch(console.error);
            this.capturing.set(false);
            this.captureProgress.set('Đã hủy');
        }
    }

    async verifyFingerprint(): Promise<void> {
        if (!this.useSignalR() || !this.signalRConnected()) {
            this.notificationService.warning('Cảnh báo', 'Cần kết nối với ứng dụng máy tính qua SignalR để xác thực nhanh');
            return;
        }

        try {
            this.notificationService.info('Xác thực', 'Đang gửi yêu cầu xác thực...');
            await this.fingerprintHub.requestVerification(this.patientId());
        } catch (error: any) {
            this.notificationService.error('Lỗi', error.message);
        }
    }

    toggleSignalRMode(): void {
        this.useSignalR.update(v => !v);
        this.sdkReady.set(false);
        this.sdkStatus.set('Chưa kết nối');

        if (this.activeTab() === 'fingerprint') {
            this.initializeSdk();
        }
    }

    registerFingerprint(): void {
        if (!this.capturedData()) {
            this.notificationService.warning('Cảnh báo', 'Chưa có dữ liệu vân tay');
            return;
        }

        const request: RegisterFingerprintRequest = {
            fingerprintDataBase64: this.capturedData(),
            fingerType: this.selectedFinger(),
            sdkType: this.selectedSdk(),
            quality: this.capturedQuality()
        };

        this.biometricsService.registerFingerprint(this.patientId(), request).subscribe({
            next: fp => {
                this.notificationService.success('Thành công', 'Đã đăng ký vân tay');
                this.fingerprints.update(fps => [...fps, fp]);
                this.capturedData.set('');
            },
            error: err => this.notificationService.error('Lỗi', err.error || 'Không thể đăng ký')
        });
    }

    deleteFingerprint(fp: PatientFingerprintDto): void {
        if (confirm(`Xóa vân tay ${this.biometricsService.getFingerTypeName(fp.fingerType)}?`)) {
            this.biometricsService.deleteFingerprint(fp.id).subscribe({
                next: () => {
                    this.notificationService.success('Thành công', 'Đã xóa vân tay');
                    this.fingerprints.update(fps => fps.filter(f => f.id !== fp.id));
                },
                error: err => this.notificationService.error('Lỗi', err.error || 'Không thể xóa')
            });
        }
    }

    // ==================== Tab Navigation ====================

    setTab(tab: 'photo' | 'fingerprint'): void {
        this.activeTab.set(tab);
        if (tab === 'fingerprint') {
            this.initializeSdk();
        }
    }

    // Helpers
    getFingerTypeName(type: string): string {
        return this.biometricsService.getFingerTypeName(type);
    }

    getSdkTypeName(type: string): string {
        return this.biometricsService.getSdkTypeName(type);
    }

    fingerTypes = Object.entries(FingerprintType)
        .filter(([key]) => isNaN(Number(key)))
        .map(([key, value]) => ({ key, value: value as FingerprintType }));
}
