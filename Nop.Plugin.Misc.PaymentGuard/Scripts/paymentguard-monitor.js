// Enhanced paymentguard-monitor.js with SRI validation
(function (window, document) {

  var PaymentGuard = {
    config: {
      apiEndpoint: '/Plugins/PaymentGuard/Api',
      checkInterval: 5000,
      enabled: true
    },

    initialScripts: [],
    currentScripts: [],

    init: function () {
      if (!this.config.enabled) return;

      this.captureInitialScripts();
      this.startMonitoring();
      this.setupCSPViolationReporting();
    },

    // Enhanced script capturing with SRI information
    captureInitialScripts: function () {
      var scripts = document.querySelectorAll('script[src]');
      this.initialScripts = Array.from(scripts).map(function (script) {
        return {
          src: script.src,
          integrity: script.integrity || null,
          crossorigin: script.crossOrigin || null,
          async: script.async,
          defer: script.defer,
          type: script.type || 'text/javascript',
          // NEW: Capture SRI status
          hasSRI: !!script.integrity,
          sriAlgorithm: script.integrity ? script.integrity.split('-')[0] : null
        };
      });

      // Validate SRI for scripts that have integrity attributes
      this.validateInitialScriptsSRI();

      console.log('PaymentGuard: Captured', this.initialScripts.length, 'initial scripts');
    },

    // NEW: Validate SRI integrity for scripts
    validateInitialScriptsSRI: function () {
      this.initialScripts.forEach(function (scriptInfo) {
        if (scriptInfo.hasSRI) {
          PaymentGuard.validateScriptSRI(scriptInfo);
        } else if (PaymentGuard.shouldHaveSRI(scriptInfo.src)) {
          // Report missing SRI for scripts that should have it
          PaymentGuard.reportSRIViolation(scriptInfo.src, 'missing-sri');
        }
      });
    },

    // NEW: Check if script should have SRI based on our rules
    shouldHaveSRI: function (scriptUrl) {
      // Check if it's a trusted CDN that should have SRI
      var trustedCDNs = [
        'code.jquery.com',
        'cdnjs.cloudflare.com',
        'cdn.jsdelivr.net',
        'stackpath.bootstrapcdn.com',
        'maxcdn.bootstrapcdn.com'
      ];

      return trustedCDNs.some(function (cdn) {
        return scriptUrl.includes(cdn);
      });
    },

    // NEW: Validate SRI hash by checking with server
    validateScriptSRI: function (scriptInfo) {
      fetch(this.config.apiEndpoint + '/ValidateScriptWithSRI', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          scriptUrl: scriptInfo.src,
          integrity: scriptInfo.integrity,
          pageUrl: window.location.href
        })
      })
        .then(function (response) {
          return response.json();
        })
        .then(function (data) {
          if (data.success) {
            if (!data.hasValidSRI) {
              console.error('PaymentGuard: SRI validation failed for script:', scriptInfo.src);
              console.error('PaymentGuard: SRI Error:', data.sriError);

              PaymentGuard.reportSRIViolation(scriptInfo.src, 'sri-validation-failed', data.sriError);
            } else {
              console.log('PaymentGuard: SRI validation passed for script:', scriptInfo.src);
            }

            if (!data.isAuthorized) {
              console.warn('PaymentGuard: Unauthorized script detected:', scriptInfo.src);
              PaymentGuard.handleUnauthorizedScript(scriptInfo);
            }
          }
        })
        .catch(function (error) {
          console.error('PaymentGuard: Error validating SRI for script:', scriptInfo.src, error);
        });
    },

    // NEW: Report SRI violations
    reportSRIViolation: function (scriptUrl, violationType, details) {
      var violation = {
        src: scriptUrl,
        violation: violationType,
        details: details,
        timestamp: new Date().toISOString(),
        userAgent: navigator.userAgent
      };

      console.warn('PaymentGuard: SRI Violation -', violationType, 'for script:', scriptUrl);

      fetch(this.config.apiEndpoint + '/ReportViolation', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          violationType: violationType,
          scriptUrl: scriptUrl,
          pageUrl: window.location.href,
          timestamp: violation.timestamp,
          userAgent: violation.userAgent,
          details: details
        })
      })
        .catch(function (error) {
          console.error('PaymentGuard: Error reporting SRI violation:', error);
        });
    },

    // Enhanced script handling with SRI validation
    handleNewScript: function (scriptElement) {
      var scriptInfo = {
        src: scriptElement.src || 'inline-script-' + this.generateSimpleHash(scriptElement.textContent || ''),
        integrity: scriptElement.integrity || null,
        addedAt: new Date().toISOString(),
        authorized: false,
        hasSRI: !!scriptElement.integrity
      };

      console.warn('PaymentGuard: New script detected:', scriptInfo.src);

      // Validate both authorization and SRI
      if (scriptInfo.hasSRI) {
        this.validateScriptSRI(scriptInfo);
      } else if (this.shouldHaveSRI(scriptInfo.src)) {
        this.reportSRIViolation(scriptInfo.src, 'missing-sri');
      }

      // Also validate authorization
      this.validateScript(scriptInfo);
    },

    startMonitoring: function () {
      // Enhanced MutationObserver to catch SRI attributes
      if (window.MutationObserver) {
        var observer = new MutationObserver(function (mutations) {
          mutations.forEach(function (mutation) {
            if (mutation.type === 'childList') {
              mutation.addedNodes.forEach(function (node) {
                if (node.tagName === 'SCRIPT') {
                  PaymentGuard.handleNewScript(node);
                }
              });
            }
            // NEW: Watch for attribute changes (like integrity being added/removed)
            else if (mutation.type === 'attributes' &&
              mutation.target.tagName === 'SCRIPT' &&
              mutation.attributeName === 'integrity') {
              console.log('PaymentGuard: Script integrity attribute changed:', mutation.target.src);
              PaymentGuard.handleNewScript(mutation.target);
            }
          });
        });

        observer.observe(document.head, {
          childList: true,
          subtree: true,
          attributes: true,
          attributeFilter: ['integrity', 'src']
        });
        observer.observe(document.body, {
          childList: true,
          subtree: true,
          attributes: true,
          attributeFilter: ['integrity', 'src']
        });
      }

      // Periodic check
      setInterval(function () {
        PaymentGuard.performPeriodicCheck();
      }, this.config.checkInterval);
    },

    // Rest of the existing methods...
    validateScript: function (scriptInfo) {
      fetch(this.config.apiEndpoint + '/ValidateScript', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          scriptUrl: scriptInfo.src,
          pageUrl: window.location.href,
          timestamp: new Date().toISOString()
        })
      })
        .then(function (response) {
          return response.json();
        })
        .then(function (data) {
          if (!data.isAuthorized) {
            console.error('PaymentGuard: Unauthorized script detected:', scriptInfo.src);
            PaymentGuard.handleUnauthorizedScript(scriptInfo);
          }
        })
        .catch(function (error) {
          console.error('PaymentGuard: Error validating script:', error);
        });
    },

    performPeriodicCheck: function () {
      var currentScripts = document.querySelectorAll('script[src]');
      this.currentScripts = Array.from(currentScripts).map(function (script) {
        return {
          src: script.src,
          integrity: script.integrity,
          hasSRI: !!script.integrity
        };
      });

      // Check for new scripts
      var newScripts = this.currentScripts.filter(function (current) {
        return !PaymentGuard.initialScripts.some(function (initial) {
          return initial.src === current.src;
        });
      });

      if (newScripts.length > 0) {
        console.warn('PaymentGuard: New scripts detected during periodic check:', newScripts);
        newScripts.forEach(function (scriptInfo) {
          if (scriptInfo.hasSRI) {
            PaymentGuard.validateScriptSRI(scriptInfo);
          } else if (PaymentGuard.shouldHaveSRI(scriptInfo.src)) {
            PaymentGuard.reportSRIViolation(scriptInfo.src, 'missing-sri');
          }
        });
        this.reportNewScripts(newScripts);
      }
    },

    // ... rest of existing methods remain the same ...

    reportNewScripts: function (scripts) {
      fetch(this.config.apiEndpoint + '/ReportScripts', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          scripts: scripts.map(s => s.src),
          pageUrl: window.location.href,
          userAgent: navigator.userAgent,
          timestamp: new Date().toISOString(),
          initialScripts: this.initialScripts.map(function (s) { return s.src; })
        })
      })
        .catch(function (error) {
          console.error('PaymentGuard: Error reporting scripts:', error);
        });
    },

    handleUnauthorizedScript: function (scriptInfo) {
      console.error('PaymentGuard: SECURITY ALERT - Unauthorized script:', scriptInfo);
      this.reportSecurityViolation(scriptInfo);
    },

    reportSecurityViolation: function (scriptInfo) {
      fetch(this.config.apiEndpoint + '/ReportViolation', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          violationType: 'unauthorized-script',
          scriptUrl: scriptInfo.src,
          pageUrl: window.location.href,
          timestamp: new Date().toISOString(),
          userAgent: navigator.userAgent
        })
      })
        .catch(function (error) {
          console.error('PaymentGuard: Error reporting violation:', error);
        });
    },

    setupCSPViolationReporting: function () {
      document.addEventListener('securitypolicyviolation', function (event) {
        console.warn('PaymentGuard: CSP Violation detected:', event);

        PaymentGuard.reportCSPViolation({
          blockedURI: event.blockedURI,
          violatedDirective: event.violatedDirective,
          originalPolicy: event.originalPolicy,
          effectiveDirective: event.effectiveDirective,
          sourceFile: event.sourceFile,
          lineNumber: event.lineNumber,
          columnNumber: event.columnNumber
        });
      });
    },

    reportCSPViolation: function (violationData) {
      fetch(this.config.apiEndpoint + '/ReportCSPViolation', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          violation: violationData,
          pageUrl: window.location.href,
          timestamp: new Date().toISOString(),
          userAgent: navigator.userAgent
        })
      })
        .catch(function (error) {
          console.error('PaymentGuard: Error reporting CSP violation:', error);
        });
    },

    generateSimpleHash: function (str) {
      var hash = 0;
      if (str.length === 0) return hash.toString();
      for (var i = 0; i < str.length; i++) {
        var char = str.charCodeAt(i);
        hash = ((hash << 5) - hash) + char;
        hash = hash & hash;
      }
      return Math.abs(hash).toString(16);
    }
  };

  // Auto-initialize when DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
      PaymentGuard.init();
    });
  } else {
    PaymentGuard.init();
  }

  // Expose PaymentGuard globally for debugging
  window.PaymentGuard = PaymentGuard;

})(window, document);