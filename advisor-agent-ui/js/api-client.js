// ── API Client ─────────────────────────────────────────
// Handles HTTP calls to the Azure Functions backend.

const ApiClient = (() => {

  /**
   * Start the advisor orchestration.
   * @param {string} prompt - The user prompt.
   * @param {string} [sessionId] - Optional session ID.
   * @returns {Promise<{instanceId: string, sessionId: string}>}
   */
  async function startOrchestration(prompt, sessionId) {
    const body = {
      userId: 'ui-user',
      prompt: prompt,
      sessionId: sessionId || crypto.randomUUID().replace(/-/g, ''),
      requestId: crypto.randomUUID().replace(/-/g, '')
    };

    const headers = { 'Content-Type': 'application/json' };
    if (CONFIG.ARM_TOKEN) {
      headers['Authorization'] = `Bearer ${CONFIG.ARM_TOKEN}`;
    }

    const resp = await fetch(`${CONFIG.API_BASE}${CONFIG.ORCHESTRATE_URL}`, {
      method: 'POST',
      headers,
      body: JSON.stringify(body)
    });

    if (!resp.ok) {
      const err = await resp.text();
      throw new Error(`Orchestration failed (${resp.status}): ${err}`);
    }

    return resp.json();
  }

  /**
   * Poll the orchestration status + custom status (progress steps).
   * @param {string} instanceId
   * @returns {Promise<{runtimeStatus: string, customStatus: object|null, output: object|null}>}
   */
  async function getStatus(instanceId) {
    const resp = await fetch(`${CONFIG.API_BASE}${CONFIG.STATUS_URL}/${instanceId}`);
    if (!resp.ok) {
      throw new Error(`Status check failed (${resp.status})`);
    }
    return resp.json();
  }

  /**
   * Poll until orchestration completes or times out.
   * Calls onProgress with custom status on each poll for step-by-step updates.
   * @param {string} instanceId
   * @param {function} onProgress - Called with { customStatus, runtimeStatus } each poll.
   * @returns {Promise<object>} The final status with output.
   */
  async function pollUntilComplete(instanceId, onProgress) {
    for (let i = 0; i < CONFIG.POLL_MAX_ATTEMPTS; i++) {
      await new Promise(r => setTimeout(r, CONFIG.POLL_INTERVAL_MS));
      const status = await getStatus(instanceId);

      // Notify UI of progress (customStatus has step-by-step updates)
      if (onProgress) onProgress(status);

      if (status.runtimeStatus === 'Completed' ||
        status.runtimeStatus === 'Failed' ||
        status.runtimeStatus === 'Terminated') {
        return status;
      }
    }
    throw new Error('Orchestration timed out');
  }

  /**
   * Check backend health.
   * @returns {Promise<boolean>}
   */
  async function checkHealth() {
    try {
      const resp = await fetch(`${CONFIG.API_BASE}${CONFIG.HEALTH_URL}`);
      return resp.ok;
    } catch {
      return false;
    }
  }

  return {
    startOrchestration,
    getStatus,
    pollUntilComplete,
    checkHealth
  };
})();
