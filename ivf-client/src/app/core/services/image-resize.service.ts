import { Injectable } from '@angular/core';

export interface ImageResizeOptions {
    maxWidth?: number;
    maxHeight?: number;
    quality?: number;  // 0.0 to 1.0 for JPEG quality
    outputFormat?: 'jpeg' | 'png' | 'webp';
}

export interface ResizedImage {
    blob: Blob;
    file: File;
    width: number;
    height: number;
    originalWidth: number;
    originalHeight: number;
    originalSize: number;
    newSize: number;
    compressionRatio: number;
}

const DEFAULT_OPTIONS: ImageResizeOptions = {
    maxWidth: 800,
    maxHeight: 800,
    quality: 0.85,
    outputFormat: 'jpeg'
};

@Injectable({ providedIn: 'root' })
export class ImageResizeService {

    /**
     * Resize and compress an image file.
     * @param file The original image file
     * @param options Resize options (maxWidth, maxHeight, quality, outputFormat)
     * @returns Promise with resized image data
     */
    async resizeImage(file: File, options: ImageResizeOptions = {}): Promise<ResizedImage> {
        const opts = { ...DEFAULT_OPTIONS, ...options };

        // Load image
        const img = await this.loadImage(file);

        // Calculate new dimensions
        const { width, height } = this.calculateDimensions(
            img.width,
            img.height,
            opts.maxWidth!,
            opts.maxHeight!
        );

        // Create canvas and draw resized image
        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;

        const ctx = canvas.getContext('2d')!;

        // Enable image smoothing for better quality
        ctx.imageSmoothingEnabled = true;
        ctx.imageSmoothingQuality = 'high';

        ctx.drawImage(img, 0, 0, width, height);

        // Convert to blob
        const mimeType = this.getMimeType(opts.outputFormat!);
        const blob = await this.canvasToBlob(canvas, mimeType, opts.quality!);

        // Create new File from blob
        const extension = opts.outputFormat === 'jpeg' ? 'jpg' : opts.outputFormat;
        const newFileName = this.getNewFileName(file.name, extension!);
        const resizedFile = new File([blob], newFileName, { type: mimeType });

        return {
            blob,
            file: resizedFile,
            width,
            height,
            originalWidth: img.width,
            originalHeight: img.height,
            originalSize: file.size,
            newSize: blob.size,
            compressionRatio: Math.round((1 - blob.size / file.size) * 100)
        };
    }

    /**
     * Check if an image needs resizing based on dimensions or file size.
     */
    needsResize(file: File, maxWidth: number = 800, maxHeight: number = 800, maxSizeKB: number = 500): Promise<boolean> {
        return new Promise((resolve) => {
            // Check file size first
            if (file.size > maxSizeKB * 1024) {
                resolve(true);
                return;
            }

            // Check dimensions
            const img = new Image();
            img.onload = () => {
                URL.revokeObjectURL(img.src);
                resolve(img.width > maxWidth || img.height > maxHeight);
            };
            img.onerror = () => resolve(false);
            img.src = URL.createObjectURL(file);
        });
    }

    /**
     * Create a thumbnail from an image file.
     */
    async createThumbnail(file: File, size: number = 150): Promise<Blob> {
        const result = await this.resizeImage(file, {
            maxWidth: size,
            maxHeight: size,
            quality: 0.8,
            outputFormat: 'jpeg'
        });
        return result.blob;
    }

    /**
     * Optimize image for web usage (resize + compress).
     */
    async optimizeForWeb(file: File): Promise<ResizedImage> {
        return this.resizeImage(file, {
            maxWidth: 1200,
            maxHeight: 1200,
            quality: 0.85,
            outputFormat: 'jpeg'
        });
    }

    /**
     * Optimize image for profile/avatar usage.
     */
    async optimizeForProfile(file: File): Promise<ResizedImage> {
        return this.resizeImage(file, {
            maxWidth: 400,
            maxHeight: 400,
            quality: 0.9,
            outputFormat: 'jpeg'
        });
    }

    private loadImage(file: File): Promise<HTMLImageElement> {
        return new Promise((resolve, reject) => {
            const img = new Image();
            img.onload = () => {
                URL.revokeObjectURL(img.src);
                resolve(img);
            };
            img.onerror = () => reject(new Error('Failed to load image'));
            img.src = URL.createObjectURL(file);
        });
    }

    private calculateDimensions(
        originalWidth: number,
        originalHeight: number,
        maxWidth: number,
        maxHeight: number
    ): { width: number; height: number } {
        let width = originalWidth;
        let height = originalHeight;

        // Only resize if larger than max dimensions
        if (width <= maxWidth && height <= maxHeight) {
            return { width, height };
        }

        // Calculate aspect ratio
        const aspectRatio = width / height;

        if (width > maxWidth) {
            width = maxWidth;
            height = Math.round(width / aspectRatio);
        }

        if (height > maxHeight) {
            height = maxHeight;
            width = Math.round(height * aspectRatio);
        }

        return { width, height };
    }

    private canvasToBlob(canvas: HTMLCanvasElement, mimeType: string, quality: number): Promise<Blob> {
        return new Promise((resolve, reject) => {
            canvas.toBlob(
                (blob) => {
                    if (blob) {
                        resolve(blob);
                    } else {
                        reject(new Error('Failed to create blob from canvas'));
                    }
                },
                mimeType,
                quality
            );
        });
    }

    private getMimeType(format: string): string {
        const mimeTypes: Record<string, string> = {
            'jpeg': 'image/jpeg',
            'png': 'image/png',
            'webp': 'image/webp'
        };
        return mimeTypes[format] || 'image/jpeg';
    }

    private getNewFileName(originalName: string, extension: string): string {
        const baseName = originalName.replace(/\.[^/.]+$/, '');
        return `${baseName}_optimized.${extension}`;
    }
}
