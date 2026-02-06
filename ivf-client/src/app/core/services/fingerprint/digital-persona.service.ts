/**
 * DigitalPersona Fingerprint SDK Integration
 * 
 * This file provides a TypeScript interface for the DigitalPersona Web SDK.
 * 
 * Requirements:
 * 1. HID DigitalPersona Lite Client or Workstation installed on the machine
 *    Download: https://crossmatch.hid.gl/lite-client
 * 2. dpcore.js (websdk.client.min.js) loaded in index.html
 * 3. dp-devices.js (index.umd.min.js) loaded in index.html
 * 
 * Documentation: https://hidglobal.github.io/digitalpersona-devices/
 */

// TypeScript declarations for DigitalPersona WebSDK
declare global {
    interface Window {
        // WebSDK client namespace
        WebSdk: {
            WebChannelClient: new () => WebChannelClient;
        };
        // Devices library namespace
        dp: {
            devices: {
                FingerprintReader: new (channel: any) => DPFingerprintReader;
            };
        };
    }
}

interface WebChannelClient {
    connect(): Promise<void>;
    disconnect(): void;
    isConnected(): boolean;
}

interface DPFingerprintReader {
    startAcquisition(format: number): Promise<void>;
    stopAcquisition(): Promise<void>;
    onSamplesAcquired: ((samples: DPSamples) => void) | null;
    onQualityReported: ((quality: DPQuality) => void) | null;
    onReaderConnected: (() => void) | null;
    onReaderDisconnected: (() => void) | null;
    onErrorOccurred: ((error: Error) => void) | null;
}

interface DPSamples {
    samples: Uint8Array[];
}

interface DPQuality {
    quality: number;
}

interface DPSample {
    format: string;
    data: ArrayBuffer;
}

interface DPCaptureResult {
    sample: DPSample;
    quality: number;
}

interface DPEnrollmentData {
    template: string;
    format: string;
}

interface DPMatchResult {
    index: number;
    score: number;
}

// Angular Service wrapper
import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class DigitalPersonaService {
    private channel: WebChannelClient | null = null;
    private reader: DPFingerprintReader | null = null;
    private capturedSample: Uint8Array | null = null;
    private capturedQuality = 0;

    readonly isReady = signal(false);
    readonly lastQuality = signal(0);
    readonly errorMessage = signal('');

    async initialize(): Promise<void> {
        // Check for WebSdk
        if (typeof window.WebSdk === 'undefined') {
            this.errorMessage.set('DigitalPersona WebSDK not loaded. Please ensure dpcore.js is included in index.html');
            console.error('[DigitalPersona] WebSdk not found. Make sure dpcore.js is loaded.');
            return;
        }

        // Check for devices library
        if (typeof window.dp === 'undefined' || !window.dp.devices) {
            this.errorMessage.set('DigitalPersona Devices library not loaded. Please ensure dp-devices.js is included in index.html');
            console.error('[DigitalPersona] dp.devices not found. Make sure dp-devices.js is loaded.');
            return;
        }

        try {
            console.log('[DigitalPersona] Initializing WebChannel...');

            // Create and connect WebChannel client
            this.channel = new window.WebSdk.WebChannelClient();
            await this.channel.connect();

            console.log('[DigitalPersona] WebChannel connected. Creating fingerprint reader...');

            // Create fingerprint reader using the channel
            this.reader = new window.dp.devices.FingerprintReader(this.channel);

            // Set up event handlers
            this.reader.onSamplesAcquired = (samples) => {
                console.log('[DigitalPersona] Sample acquired');
                if (samples.samples && samples.samples.length > 0) {
                    this.capturedSample = samples.samples[0];
                }
            };

            this.reader.onQualityReported = (quality) => {
                console.log('[DigitalPersona] Quality reported:', quality.quality);
                this.capturedQuality = quality.quality;
                this.lastQuality.set(quality.quality);
            };

            this.reader.onReaderConnected = () => {
                console.log('[DigitalPersona] Reader connected');
            };

            this.reader.onReaderDisconnected = () => {
                console.log('[DigitalPersona] Reader disconnected');
            };

            this.reader.onErrorOccurred = (error) => {
                console.error('[DigitalPersona] Error:', error);
                this.errorMessage.set(error.message);
            };

            this.isReady.set(true);
            console.log('[DigitalPersona] SDK initialized successfully');

        } catch (error: any) {
            const errorMsg = `SDK initialization failed: ${error.message}. Ensure HID DigitalPersona Lite Client is installed and running.`;
            this.errorMessage.set(errorMsg);
            console.error('[DigitalPersona]', errorMsg);
        }
    }

    async capture(): Promise<{ template: string; quality: number } | null> {
        if (!this.reader) {
            this.errorMessage.set('Reader not initialized');
            return null;
        }

        try {
            this.capturedSample = null;
            this.capturedQuality = 0;

            console.log('[DigitalPersona] Starting acquisition...');

            // Start acquisition (format: 0 = raw, 1 = intermediate, 2 = compressed)
            await this.reader.startAcquisition(1);

            // Wait for sample (with timeout)
            const sample = await this.waitForSample(10000);

            if (!sample) {
                throw new Error('Capture timeout - no sample received');
            }

            await this.reader.stopAcquisition();

            // Convert to base64
            const base64Template = this.uint8ArrayToBase64(sample);

            console.log('[DigitalPersona] Capture successful, quality:', this.capturedQuality);

            return {
                template: base64Template,
                quality: this.capturedQuality
            };

        } catch (error: any) {
            this.errorMessage.set(error.message);
            console.error('[DigitalPersona] Capture error:', error);
            return null;
        }
    }

    private waitForSample(timeout: number): Promise<Uint8Array | null> {
        return new Promise((resolve) => {
            const startTime = Date.now();
            const checkInterval = setInterval(() => {
                if (this.capturedSample) {
                    clearInterval(checkInterval);
                    resolve(this.capturedSample);
                } else if (Date.now() - startTime > timeout) {
                    clearInterval(checkInterval);
                    resolve(null);
                }
            }, 100);
        });
    }

    stopCapture(): void {
        this.reader?.stopAcquisition().catch(console.error);
    }

    disconnect(): void {
        this.channel?.disconnect();
        this.isReady.set(false);
    }

    private uint8ArrayToBase64(array: Uint8Array): string {
        let binary = '';
        for (let i = 0; i < array.length; i++) {
            binary += String.fromCharCode(array[i]);
        }
        return btoa(binary);
    }
}

export type { DPSample, DPCaptureResult, DPEnrollmentData, DPMatchResult };
