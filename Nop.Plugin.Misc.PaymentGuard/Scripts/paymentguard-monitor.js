(function (window, document) {

  var PaymentGuard = {
    config: {
      apiEndpoint: '/Plugins/PaymentGuard/Api',
      checkInterval: 5000, // 5 seconds
      enabled: true
    },

    // Store initial state
    initialScripts: [],
    currentScripts: [],

    init: function () {
      if (!this.config.enabled) return;

      this.captureInitialScripts();
      this.startMonitoring();
      this.setupCSPViolationReporting();
    },

    captureInitialScripts: function () {
      var scripts = document.querySelectorAll('script[src]');
      this.initialScripts = Array.from(scripts).map(function (script) {
        return {
          src: script.src,
          integrity: script.integrity || null,
          crossorigin: script.crossOrigin || null,
          async: script.async,
          defer: script.defer,
          type: script.type || 'text/javascript'
        };
      });

      // Also capture inline scripts (generate hash for identification)
      var inlineScripts = document.querySelectorAll('script:not([src])');
      Array.from(inlineScripts).forEach(function (script, index) {
        if (script.textContent.trim()) {
          var hash = PaymentGuard.generateSimpleHash(script.textContent);
          PaymentGuard.initialScripts.push({
            src: 'inline-script-' + hash,
            content: script.textContent.substring(0, 100), // First 100 chars for identification
            type: 'inline',
            index: index
          });
        }
      });

      console.log('PaymentGuard: Captured', this.initialScripts.length, 'initial scripts');
    },

    startMonitoring: function () {
      // Monitor for new scripts being added
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
          });
        });

        observer.observe(document.head, { childList: true, subtree: true });
        observer.observe(document.body, { childList: true, subtree: true });
      }

      // Periodic check
      setInterval(function () {
        PaymentGuard.performPeriodicCheck();
      }, this.config.checkInterval);
    },

    handleNewScript: function (scriptElement) {
      var scriptInfo = {
        src: scriptElement.src || 'inline-script-' + this.generateSimpleHash(scriptElement.textContent || ''),
        addedAt: new Date().toISOString(),
        authorized: false
      };

      console.warn('PaymentGuard: New script detected:', scriptInfo.src);

      // Send to server for validation
      this.validateScript(scriptInfo);
    },

    performPeriodicCheck: function () {
      var currentScripts = document.querySelectorAll('script[src]');
      this.currentScripts = Array.from(currentScripts).map(function (script) {
        return script.src;
      });

      // Check for new scripts
      var newScripts = this.currentScripts.filter(function (src) {
        return !PaymentGuard.initialScripts.some(function (initial) {
          return initial.src === src;
        });
      });

      if (newScripts.length > 0) {
        console.warn('PaymentGuard: New scripts detected during periodic check:', newScripts);
        this.reportNewScripts(newScripts);
      }
    },

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

    reportNewScripts: function (scripts) {
      fetch(this.config.apiEndpoint + '/ReportScripts', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          scripts: scripts,
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
      // Log the violation
      console.error('PaymentGuard: SECURITY ALERT - Unauthorized script:', scriptInfo);

      // Could potentially remove the script or block its execution
      // For now, just report it
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
      // Listen for CSP violations
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
        hash = hash & hash; // Convert to 32bit integer
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