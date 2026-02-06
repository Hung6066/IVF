/**
 * SecuGen Fingerprint SDK Integration
 * 
 * This file provides a TypeScript interface for the SecuGen Web API.
 * 
 * Requirements:
 * 1. SecuGen WebAPI Client (SgiBioSrv) installed and running on the machine
 *    Download: https://secugen.com/webapi/
 * 2. sgfplib.js loaded in index.html
 * 
 * The SecuGen WebAPI uses a local HTTP REST service:
 * - HTTPS: https://localhost:8443
 * - HTTP:  http://localhost:8000
 * 
 * Documentation: https://secugen.com/webapi/
 */

// TypeScript declarations for SecuGen WebAPI
declare global {
    interface Window {
        SecuGen: {
            FDxFormat: typeof SGFDxFormat;
            SGFPLib: new () => SGFPLib;
            checkService: () => Promise<SGServiceStatus>;
            setApiUrl: (url: string) => void;
        };
    }
}

interface SGFPLib {
    Init(licenseKey?: string): number;
    GetDeviceInfo(): Promise<SGDeviceInfo>;
    Capture(quality?: number, timeout?: number): Promise<SGCaptureResult>;
    CaptureAndGetTemplate(quality?: number, timeout?: number): Promise<SGCaptureResult>;
    MatchTemplate(template1: string, template2: string): Promise<SGMatchResult>;
}

interface SGDeviceInfo {
    deviceId: number;
    deviceName: string;
    imageWidth: number;
    imageHeight: number;
    isOpen: boolean;
    ErrorCode?: number;
}

interface SGCaptureResult {
    success: boolean;
    imageData?: string;       // Base64 BMP image
    templateData?: string;    // Base64 fingerprint template
    quality?: number;
    errorCode?: number;
    errorMessage?: string;
}

interface SGTemplateResult {
    template: string;
    success: boolean;
}

interface SGMatchResult {
    matched: boolean;
    score: number;
}

interface SGServiceStatus {
    running: boolean;
    deviceInfo?: SGDeviceInfo;
    error?: string;
}

declare enum SGFDxFormat {
    ANSI378 = 0x001B0401,
    ISO19794 = 0x01010001
}

// Angular Service wrapper
import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class SecuGenService {
    private lib: SGFPLib | null = null;

    readonly isReady = signal(false);
    readonly deviceInfo = signal<SGDeviceInfo | null>(null);
    readonly errorMessage = signal('');

    async initialize(): Promise<void> {
        if (typeof window.SecuGen === 'undefined') {
            this.errorMessage.set('SecuGen SDK not loaded. Please ensure sgfplib.js is included in index.html');
            console.error('[SecuGen] SDK not found. Make sure sgfplib.js is loaded.');
            return;
        }

        try {
            console.log('[SecuGen] Checking service availability...');

            // First check if the SecuGen WebAPI service is running
            const serviceStatus = await window.SecuGen.checkService();

            if (!serviceStatus.running) {
                const errorMsg = `SecuGen WebAPI service is not running. ${serviceStatus.error || 'Please install and start SgiBioSrv service from https://secugen.com/webapi/'}`;
                this.errorMessage.set(errorMsg);
                console.error('[SecuGen]', errorMsg);
                return;
            }

            console.log('[SecuGen] Service is running. Initializing SDK...');

            // Initialize the SDK
            this.lib = new window.SecuGen.SGFPLib();
            const initResult = this.lib.Init();

            if (initResult !== 0) {
                this.errorMessage.set(`SDK initialization failed with code: ${initResult}`);
                return;
            }

            // Get device info
            const deviceInfo = await this.lib.GetDeviceInfo();
            if (deviceInfo.ErrorCode && deviceInfo.ErrorCode !== 0) {
                console.warn('[SecuGen] Device info warning:', deviceInfo);
            } else {
                this.deviceInfo.set(deviceInfo);
            }

            this.isReady.set(true);
            console.log('[SecuGen] SDK initialized successfully');

        } catch (error: any) {
            const errorMsg = `SDK initialization failed: ${error.message}. Ensure SecuGen WebAPI Client (SgiBioSrv) is installed and running.`;
            this.errorMessage.set(errorMsg);
            console.error('[SecuGen]', errorMsg);
        }
    }

    async capture(timeout: number = 10000): Promise<{ template: string; quality: number } | null> {
        if (!this.lib) {
            this.errorMessage.set('SDK not initialized');
            return null;
        }

        try {
            console.log('[SecuGen] Starting capture...');

            // Capture and get template in one call
            const result = await this.lib.CaptureAndGetTemplate(50, timeout);

            if (!result.success) {
                const errorMsg = result.errorMessage || `Capture failed with code: ${result.errorCode}`;
                this.errorMessage.set(errorMsg);
                console.error('[SecuGen]', errorMsg);
                return null;
            }

            console.log('[SecuGen] Capture successful, quality:', result.quality);

            return {
                template: result.templateData || '',
                quality: result.quality || 0
            };

        } catch (error: any) {
            this.errorMessage.set(error.message);
            console.error('[SecuGen] Capture error:', error);
            return null;
        }
    }

    async matchTemplates(template1: string, template2: string): Promise<{ matched: boolean; score: number } | null> {
        if (!this.lib) {
            this.errorMessage.set('SDK not initialized');
            return null;
        }

        try {
            const result = await this.lib.MatchTemplate(template1, template2);
            return result;
        } catch (error: any) {
            this.errorMessage.set(error.message);
            console.error('[SecuGen] Match error:', error);
            return null;
        }
    }

    dispose(): void {
        this.lib = null;
        this.isReady.set(false);
        this.deviceInfo.set(null);
    }
}

export type { SGDeviceInfo, SGCaptureResult, SGTemplateResult, SGMatchResult };
