// ── Application Entry Point ──────────────────────────────
// Orchestrates the UI and API interactions.
// Real-time progress via Durable Functions customStatus polling.
// SignalR is wired for future enhancement when Azure SignalR Service is available.

(async function App() {
  'use strict';

  // ── Initialize UI ────────────────────────────────────
  UI.init();
  UI.setupTextareas();

  // State
  let currentScreen = 'home'; // home | chat
  let currentInstanceId = null;
  let currentSessionId = null;
  let orchestrationInProgress = false;

  // ── Attempt SignalR connection (graceful fallback) ───
  SignalRClient.onConnectionChanged((state) => {
    UI.updateConnectionStatus(state);
  });

  SignalRClient.onStatus((data) => {
    if (data && data.stepName) {
      UI.addOrUpdateStep(data.stepName, data.stepState, data.message);
    }
  });

  SignalRClient.onCompleted((data) => {
    handleCompletion(data);
  });

  // Try SignalR — will silently fall back to polling if unavailable
  const signalRConnected = await SignalRClient.connect();
  if (!signalRConnected) {
    console.log('[App] SignalR not available — using polling for real-time updates');
    UI.updateConnectionStatus('disconnected');
  }

  // ── Event handlers ───────────────────────────────────

  // Home send button
  document.getElementById('homeSendBtn').addEventListener('click', () => {
    const prompt = UI.getHomePrompt();
    if (prompt) submitPrompt(prompt);
  });

  // Chat send button
  document.getElementById('chatSendBtn').addEventListener('click', () => {
    const prompt = UI.getChatPrompt();
    if (prompt) submitFollowup(prompt);
  });

  // Enter to submit (Shift+Enter for newline)
  document.getElementById('homeTextarea').addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      const prompt = UI.getHomePrompt();
      if (prompt) submitPrompt(prompt);
    }
  });

  document.getElementById('chatTextarea').addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      const prompt = UI.getChatPrompt();
      if (prompt) submitFollowup(prompt);
    }
  });

  // Suggestion cards
  document.querySelectorAll('#suggestionsGrid .card').forEach(card => {
    card.addEventListener('click', () => {
      const prompt = card.dataset.prompt;
      if (prompt) {
        UI.setHomePrompt(prompt);
        submitPrompt(prompt);
      }
    });
  });

  // View toggle
  document.getElementById('viewToggle').addEventListener('click', () => UI.toggleViewMore());

  // Back button
  document.getElementById('backBtn').addEventListener('click', goBack);

  // Reasoning header toggle
  document.getElementById('reasoningHeader').addEventListener('click', () => {
    const header = document.getElementById('reasoningHeader');
    header.classList.toggle('expanded');
    document.getElementById('reasoningSteps').classList.toggle('open');
  });

  // Follow-up click handler
  window.onFollowupClick = (text) => submitFollowup(text);

  // ── Core flow ────────────────────────────────────────

  async function submitPrompt(prompt) {
    if (orchestrationInProgress) return;

    currentSessionId = crypto.randomUUID().replace(/-/g, '');
    currentScreen = 'chat';
    await UI.showChatView(prompt);
    await runOrchestration(prompt);
  }

  async function submitFollowup(prompt) {
    if (orchestrationInProgress) return;

    UI.clearChatInput();
    // Append new user message and reasoning block without destroying previous conversation
    UI.appendFollowupTurn(prompt);
    await runOrchestration(prompt);
  }

  async function runOrchestration(prompt) {
    orchestrationInProgress = true;
    UI.disableInput();

    try {
      // 1. Start orchestration via REST
      console.log('[App] Starting orchestration for:', prompt);
      const result = await ApiClient.startOrchestration(prompt, currentSessionId);
      currentInstanceId = result.instanceId;
      currentSessionId = result.sessionId;
      console.log('[App] Orchestration started — instanceId:', currentInstanceId);

      // 2. Poll for progress (customStatus) and completion
      const finalStatus = await ApiClient.pollUntilComplete(currentInstanceId, (status) => {
        // Render step-by-step progress from customStatus
        if (status.customStatus && status.customStatus.steps) {
          for (const step of status.customStatus.steps) {
            UI.addOrUpdateStep(step.stepName, step.state, step.message);
          }
        }
      });

      // 3. Handle final result
      if (finalStatus.runtimeStatus === 'Completed' && finalStatus.output) {
        handleCompletionFromPoll(finalStatus);
      } else if (finalStatus.runtimeStatus === 'Failed') {
        UI.finishReasoning();
        UI.showError('The orchestration failed. Please try again.');
      } else {
        UI.finishReasoning();
        UI.showError('The orchestration timed out. Please try again.');
      }
    } catch (err) {
      console.error('[App] Orchestration error:', err);
      UI.finishReasoning();
      UI.showError(`Error: ${err.message}`);
      UI.showToast('Failed to communicate with the backend. Is the Azure Function running?');
    } finally {
      orchestrationInProgress = false;
      UI.enableInput();
    }
  }

  function handleCompletion(data) {
    if (!data) return;
    console.log('[App] Handling completion from SignalR');

    UI.finishReasoning();

    if (data.isSuccess && data.uiAction === 'subscriptionPicker' && data.uiData) {
      UI.showSubscriptionPicker(data.uiData, (selectedIds) => {
        console.log('[App] User selected subscriptions:', selectedIds);
        const followup = `Analyze these subscriptions: ${selectedIds.join(', ')}`;
        submitFollowup(followup);
      });
    } else if (data.isSuccess && data.response) {
      UI.showAnswer(data.response);
      generateFollowups(data.response);
    } else {
      UI.showError(data.response || 'The agent could not complete your request.');
    }

    orchestrationInProgress = false;
    UI.enableInput();
  }

  function handleCompletionFromPoll(status) {
    if (!status.output) return;
    console.log('[App] Handling completion from polling');

    UI.finishReasoning();

    const output = status.output;
    if (output.isSuccess && output.uiAction === 'subscriptionPicker' && output.uiData) {
      // Render rich subscription picker card
      UI.showSubscriptionPicker(output.uiData, (selectedIds) => {
        console.log('[App] User selected subscriptions:', selectedIds);
        const followup = `Analyze these subscriptions: ${selectedIds.join(', ')}`;
        submitFollowup(followup);
      });
    } else if (output.isSuccess && output.response) {
      UI.showAnswer(output.response);
      generateFollowups(output.response);
    } else {
      UI.showError(output.response || 'The agent could not complete your request.');
    }
  }

  function generateFollowups(responseText) {
    const followups = [];
    const lower = responseText.toLowerCase();

    if (lower.includes('retir') || lower.includes('migration') || lower.includes('migrate')) {
      followups.push('Help me create a detailed migration plan for the most critical retiring resource');
      followups.push('Show me the timeline for all upcoming retirements');
    }
    if (lower.includes('cost') || lower.includes('saving') || lower.includes('spend')) {
      followups.push('Show me the detailed breakdown of underutilized resources');
      followups.push('Help me create a cost optimization report for leadership');
    }
    if (lower.includes('resilien') || lower.includes('reliab') || lower.includes('availability')) {
      followups.push('Help me implement the quick wins to improve resiliency');
      followups.push('Design a multi-region failover architecture');
    }
    if (lower.includes('outage') || lower.includes('incident') || lower.includes('bcdr')) {
      followups.push('Help me configure Service Health alerts for production');
      followups.push('Set up chaos engineering experiments for my workload');
    }
    if (lower.includes('architecture') || lower.includes('container') || lower.includes('moderniz')) {
      followups.push('Generate a detailed migration plan to Azure Container Apps');
      followups.push('Estimate cost differences between current and recommended architecture');
    }
    if (lower.includes('service group')) {
      followups.push('Fix the critical issues in the lowest scoring service group');
      followups.push('Generate a reliability improvement plan for all service groups');
    }

    // Default follow-ups if nothing specific matched
    if (followups.length === 0) {
      followups.push('Tell me more about the recommendations');
      followups.push('How do I implement the suggested action plan?');
      followups.push('What are the risks if I don\'t act on these recommendations?');
    }

    UI.showFollowups(followups.slice(0, 4));
  }

  // ── Navigation ───────────────────────────────────────
  function goBack() {
    if (currentScreen === 'chat') {
      UI.showHomeView();
      currentScreen = 'home';
    }
  }

})();
