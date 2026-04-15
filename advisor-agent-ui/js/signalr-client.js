// ── SignalR Client ─────────────────────────────────────
// Manages the SignalR connection to the Azure Functions 'advisor' hub.
// Falls back to polling if SignalR is unavailable.

const SignalRClient = (() => {
  let connection = null;
  let isConnected = false;

  // Callbacks
  let onStatusCallback = null;
  let onCompletedCallback = null;
  let onConnectionChangedCallback = null;

  function updateConnectionUI(state) {
    if (onConnectionChangedCallback) {
      onConnectionChangedCallback(state);
    }
  }

  async function connect() {
    if (connection && isConnected) return true;

    try {
      updateConnectionUI('connecting');

      connection = new signalR.HubConnectionBuilder()
        .withUrl(`${CONFIG.API_BASE}${CONFIG.NEGOTIATE_URL}`)
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

      // Register event handlers
      connection.on('agentStatus', (data) => {
        console.log('[SignalR] agentStatus:', data);
        if (onStatusCallback) onStatusCallback(data);
      });

      connection.on('agentCompleted', (data) => {
        console.log('[SignalR] agentCompleted:', data);
        if (onCompletedCallback) onCompletedCallback(data);
      });

      connection.onreconnecting(() => {
        console.log('[SignalR] Reconnecting...');
        isConnected = false;
        updateConnectionUI('connecting');
      });

      connection.onreconnected(() => {
        console.log('[SignalR] Reconnected');
        isConnected = true;
        updateConnectionUI('connected');
      });

      connection.onclose(() => {
        console.log('[SignalR] Connection closed');
        isConnected = false;
        updateConnectionUI('disconnected');
      });

      await connection.start();
      isConnected = true;
      updateConnectionUI('connected');
      console.log('[SignalR] Connected successfully');
      return true;
    } catch (err) {
      console.warn('[SignalR] Connection failed, will use polling fallback:', err.message);
      isConnected = false;
      updateConnectionUI('disconnected');
      return false;
    }
  }

  function onStatus(callback) {
    onStatusCallback = callback;
  }

  function onCompleted(callback) {
    onCompletedCallback = callback;
  }

  function onConnectionChanged(callback) {
    onConnectionChangedCallback = callback;
  }

  function getIsConnected() {
    return isConnected;
  }

  return {
    connect,
    onStatus,
    onCompleted,
    onConnectionChanged,
    get isConnected() { return getIsConnected(); }
  };
})();
