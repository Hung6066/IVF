import { Injectable, inject, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../../environments/environment';
import { Subject } from 'rxjs';

export interface CaptureRequest {
    patientId: string;
    fingerType: string;
    requestedBy: string;
    requestedAt: Date;
}

export interface CaptureResult {
    patientId: string;
    success: boolean;
    templateData?: string;   // Base64 encoded fingerprint template
    imageData?: string;      // Base64 encoded fingerprint image (JPEG)
    fingerType?: string;
    quality?: string;
    errorMessage?: string;
    capturedAt: Date;
}

export interface CaptureStatus {
    patientId: string;
    status: string;   // "Starting", "WaitingForFinger", "Capturing", "Processing", "Completed", "Failed"
    message?: string;
    timestamp: Date;
}

export interface SampleQuality {
    patientId: string;
    quality: string;   // "Good", "Poor", "TooWet", "TooDry"
    imagePreview?: string;
    timestamp: Date;
}

export interface EnrollmentProgress {
    patientId: string;
    samplesCollected: number;
    samplesNeeded: number;
    timestamp: Date;
}

export interface VerificationResult {
    patientId: string;
    success: boolean;
    fingerType?: string;
    errorMessage?: string;
    verifiedAt: Date;
}

@Injectable({ providedIn: 'root' })
export class FingerprintHubService {
    private hubConnection: signalR.HubConnection | null = null;
    private readonly hubUrl = `${environment.apiUrl.replace('/api', '')}/hubs/fingerprint`;

    // Reactive state
    isConnected = signal(false);
    connectionError = signal<string | null>(null);
    currentPatientId = signal<string | null>(null);

    // Events as Subjects for components to subscribe
    captureRequested$ = new Subject<CaptureRequest>();
    captureResult$ = new Subject<CaptureResult>();
    captureStatus$ = new Subject<CaptureStatus>();
    sampleQuality$ = new Subject<SampleQuality>();
    enrollmentProgress$ = new Subject<EnrollmentProgress>();
    captureCancelled$ = new Subject<string>();
    verificationResult$ = new Subject<VerificationResult>();

    /**
     * Connect to the FingerprintHub with JWT authentication
     */
    async connect(accessToken: string): Promise<boolean> {
        if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
            console.log('[FingerprintHub] Already connected');
            return true;
        }

        try {
            this.hubConnection = new signalR.HubConnectionBuilder()
                .withUrl(this.hubUrl, {
                    accessTokenFactory: () => accessToken
                })
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .configureLogging(signalR.LogLevel.Information)
                .build();

            this.setupEventHandlers();

            await this.hubConnection.start();
            this.isConnected.set(true);
            this.connectionError.set(null);
            console.log('[FingerprintHub] Connected successfully');
            return true;
        } catch (error) {
            console.error('[FingerprintHub] Connection failed:', error);
            this.isConnected.set(false);
            this.connectionError.set(error instanceof Error ? error.message : 'Connection failed');
            return false;
        }
    }

    /**
     * Disconnect from the hub
     */
    async disconnect(): Promise<void> {
        if (this.hubConnection) {
            await this.hubConnection.stop();
            this.isConnected.set(false);
            this.currentPatientId.set(null);
            console.log('[FingerprintHub] Disconnected');
        }
    }

    /**
     * Join a patient-specific capture group
     */
    async joinPatientCapture(patientId: string): Promise<void> {
        if (!this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
            throw new Error('Hub not connected');
        }

        await this.hubConnection.invoke('JoinPatientCapture', patientId);
        this.currentPatientId.set(patientId);
        console.log('[FingerprintHub] Joined patient capture:', patientId);
    }

    /**
     * Leave a patient-specific capture group
     */
    async leavePatientCapture(patientId: string): Promise<void> {
        if (!this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
            return;
        }

        await this.hubConnection.invoke('LeavePatientCapture', patientId);
        if (this.currentPatientId() === patientId) {
            this.currentPatientId.set(null);
        }
        console.log('[FingerprintHub] Left patient capture:', patientId);
    }

    /**
     * Request fingerprint capture for a patient
     * WinForms app will receive this and open capture form
     */
    async requestCapture(patientId: string, fingerType: string): Promise<void> {
        if (!this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
            throw new Error('Hub not connected');
        }

        await this.hubConnection.invoke('RequestCapture', patientId, fingerType);
        console.log('[FingerprintHub] Capture requested for:', patientId, fingerType);
    }

    /**
     * Request fingerprint verification for a patient
     */
    async requestVerification(patientId: string): Promise<void> {
        if (!this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
            throw new Error('Hub not connected');
        }

        await this.hubConnection.invoke('RequestVerification', patientId);
        console.log('[FingerprintHub] Verification requested for:', patientId);
    }

    /**
     * Cancel an ongoing capture request
     */
    async cancelCapture(patientId: string): Promise<void> {
        if (!this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
            return;
        }

        await this.hubConnection.invoke('CancelCapture', patientId);
        console.log('[FingerprintHub] Capture cancelled for:', patientId);
    }

    /**
     * Send capture result (used by WinForms adapter if needed)
     */
    async sendCaptureResult(result: CaptureResult): Promise<void> {
        if (!this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
            throw new Error('Hub not connected');
        }

        await this.hubConnection.invoke('SendCaptureResult', result);
    }

    private setupEventHandlers(): void {
        if (!this.hubConnection) return;

        // Reconnection handling
        this.hubConnection.onreconnecting((error) => {
            console.log('[FingerprintHub] Reconnecting...', error);
            this.isConnected.set(false);
        });

        this.hubConnection.onreconnected((connectionId) => {
            console.log('[FingerprintHub] Reconnected:', connectionId);
            this.isConnected.set(true);

            // Rejoin patient group if we were in one
            const patientId = this.currentPatientId();
            if (patientId) {
                this.joinPatientCapture(patientId).catch(console.error);
            }
        });

        this.hubConnection.onclose((error) => {
            console.log('[FingerprintHub] Connection closed:', error);
            this.isConnected.set(false);
        });

        // Listen for events from hub
        this.hubConnection.on('JoinedCapture', (patientId: string) => {
            console.log('[FingerprintHub] Joined capture confirmed:', patientId);
        });

        this.hubConnection.on('CaptureRequested', (request: CaptureRequest) => {
            console.log('[FingerprintHub] Capture requested:', request);
            this.captureRequested$.next(request);
        });

        this.hubConnection.on('CaptureResult', (result: CaptureResult) => {
            console.log('[FingerprintHub] Capture result:', result);
            this.captureResult$.next(result);
        });

        this.hubConnection.on('CaptureStatus', (status: CaptureStatus) => {
            console.log('[FingerprintHub] Capture status:', status);
            this.captureStatus$.next(status);
        });

        this.hubConnection.on('SampleQuality', (quality: SampleQuality) => {
            console.log('[FingerprintHub] Sample quality:', quality);
            this.sampleQuality$.next(quality);
        });

        this.hubConnection.on('EnrollmentProgress', (progress: EnrollmentProgress) => {
            console.log('[FingerprintHub] Enrollment progress:', progress);
            this.enrollmentProgress$.next(progress);
        });

        this.hubConnection.on('CaptureCancelled', (patientId: string) => {
            console.log('[FingerprintHub] Capture cancelled:', patientId);
            this.captureCancelled$.next(patientId);
        });

        this.hubConnection.on('VerificationResult', (result: VerificationResult) => {
            console.log('[FingerprintHub] Verification result:', result);
            this.verificationResult$.next(result);
        });
    }
}
