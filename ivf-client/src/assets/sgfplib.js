/**
 * SecuGen WebAPI Integration
 * 
 * SecuGen WebAPI uses a local HTTP REST service running on the client machine.
 * The SgiBioSrv service (SecuGen WebAPI Client) must be installed and running.
 * 
 * Default service URLs:
 * - HTTP:  http://localhost:8000
 * - HTTPS: https://localhost:8443
 * 
 * Download SecuGen WebAPI Client from:
 * https://secugen.com/webapi/
 * 
 * This file provides a wrapper that communicates with the local SecuGen service.
 */

(function (global) {
    'use strict';

    // SecuGen WebAPI endpoints
    var SG_API_URL = 'https://localhost:8443';
    var SG_FALLBACK_URL = 'http://localhost:8000';

    // Helper function to make API calls to SecuGen service
    function callSecuGenApi(endpoint, data) {
        return new Promise(function (resolve, reject) {
            // Try HTTPS first, then fallback to HTTP
            tryUrl(SG_API_URL + endpoint, data)
                .then(resolve)
                .catch(function () {
                    console.log('[SecuGen] Trying HTTP fallback...');
                    return tryUrl(SG_FALLBACK_URL + endpoint, data);
                })
                .then(resolve)
                .catch(reject);
        });
    }

    function tryUrl(url, data) {
        return new Promise(function (resolve, reject) {
            var xhr = new XMLHttpRequest();
            xhr.open(data ? 'POST' : 'GET', url, true);
            xhr.setRequestHeader('Content-Type', 'application/json');

            xhr.onload = function () {
                if (xhr.status >= 200 && xhr.status < 300) {
                    try {
                        resolve(JSON.parse(xhr.responseText));
                    } catch (e) {
                        resolve(xhr.responseText);
                    }
                } else {
                    reject(new Error('HTTP ' + xhr.status + ': ' + xhr.statusText));
                }
            };

            xhr.onerror = function () {
                reject(new Error('Network error - SecuGen WebAPI service may not be running'));
            };

            xhr.timeout = 10000;
            xhr.ontimeout = function () {
                reject(new Error('Request timeout'));
            };

            xhr.send(data ? JSON.stringify(data) : null);
        });
    }

    // Format constants matching SecuGen API
    var FDxFormat = {
        ANSI378: 0x001B0401,  // ANSI 378 format
        ISO19794: 0x01010001  // ISO 19794-2 format
    };

    // SecuGen SDK interface
    var SecuGen = {
        FDxFormat: FDxFormat,
        apiUrl: SG_API_URL,

        // Set custom API URL if needed
        setApiUrl: function (url) {
            SG_API_URL = url;
        },

        // SGFPLib - Main fingerprint library class
        SGFPLib: function () {
            var self = this;
            this.licenseKey = '';
            this.deviceInfo = null;

            // Initialize the SDK
            this.Init = function (licenseKey) {
                self.licenseKey = licenseKey || '';
                console.log('[SecuGen] SDK initialized');
                return 0; // CYCL_OK
            };

            // Get device information
            this.GetDeviceInfo = function () {
                return callSecuGenApi('/SGIFPGetDeviceInfo')
                    .then(function (response) {
                        if (response.ErrorCode === 0) {
                            self.deviceInfo = response;
                            return response;
                        }
                        throw new Error('Device info error: ' + response.ErrorCode);
                    });
            };

            // Capture fingerprint with quality check
            this.Capture = function (quality, timeout) {
                quality = quality || 50;
                timeout = timeout || 10000;

                console.log('[SecuGen] Starting capture...');

                return callSecuGenApi('/SGIFPCapture', {
                    Timeout: timeout,
                    Quality: quality,
                    TemplateFormat: FDxFormat.ANSI378,
                    LicenseKey: self.licenseKey
                }).then(function (response) {
                    if (response.ErrorCode === 0) {
                        return {
                            imageData: response.BMPBase64,
                            templateData: response.TemplateBase64,
                            quality: response.ImageQuality,
                            success: true
                        };
                    }
                    return {
                        success: false,
                        errorCode: response.ErrorCode,
                        errorMessage: getErrorMessage(response.ErrorCode)
                    };
                });
            };

            // Capture and create template in one call
            this.CaptureAndGetTemplate = function (quality, timeout) {
                return this.Capture(quality, timeout);
            };

            // Verify two templates match (1:1)
            this.MatchTemplate = function (template1, template2) {
                return callSecuGenApi('/SGIFPMatch', {
                    Template1: template1,
                    Template2: template2,
                    LicenseKey: self.licenseKey
                }).then(function (response) {
                    return {
                        matched: response.MatchResult === 1,
                        score: response.Score || 0
                    };
                });
            };
        },

        // Helper function to check if SecuGen service is running
        checkService: function () {
            return callSecuGenApi('/SGIFPGetDeviceInfo')
                .then(function (response) {
                    return {
                        running: true,
                        deviceInfo: response
                    };
                })
                .catch(function (error) {
                    return {
                        running: false,
                        error: error.message
                    };
                });
        }
    };

    // Error code to message mapping
    function getErrorMessage(code) {
        var messages = {
            0: 'Success',
            1: 'Device not found',
            2: 'Driver error',
            3: 'No fingerprint detected',
            4: 'Capture timeout',
            5: 'Poor quality image',
            51: 'Invalid parameter',
            52: 'License error'
        };
        return messages[code] || 'Unknown error: ' + code;
    }

    // Export to global scope
    global.SecuGen = SecuGen;

    console.log('[SecuGen] WebAPI client loaded. Ensure SgiBioSrv service is running on localhost:8443 or localhost:8000');

})(typeof window !== 'undefined' ? window : this);
