// ── Configuration ─────────────────────────────────────
const CONFIG = {
  // Azure Functions base URL (local dev)
  API_BASE: 'http://localhost:7071/api',

  // ARM access token for local testing (paste from: az account get-access-token --query accessToken -o tsv)
  // Set to null or '' to skip sending the Authorization header.
  ARM_TOKEN: '',

  // Endpoints
  ORCHESTRATE_URL: '/advisor/orchestrate',
  STATUS_URL: '/advisor/status',     // + /{instanceId}
  NEGOTIATE_URL: '/negotiate',
  HEALTH_URL: '/advisor/health',

  // Polling settings (fallback when SignalR is unavailable)
  POLL_INTERVAL_MS: 500,
  POLL_MAX_ATTEMPTS: 480,  // 4 minutes max

  // SignalR hub name (must match backend)
  SIGNALR_HUB: 'advisor'
};
