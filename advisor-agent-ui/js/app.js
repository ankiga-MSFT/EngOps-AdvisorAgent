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

  // Reasoning header toggle (FAI Chain of Thought)
  document.getElementById('cotToggle').addEventListener('click', () => {
    const toggle = document.getElementById('cotToggle');
    const expanded = toggle.getAttribute('aria-expanded') === 'true';
    toggle.setAttribute('aria-expanded', String(!expanded));
    document.getElementById('cotCard').classList.toggle('open');
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
      UI.showFeedbackButtons();
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
      UI.showFeedbackButtons();
      generateFollowups(output.response);
    } else {
      UI.showError(output.response || 'The agent could not complete your request.');
    }
  }

  // ── Skill-catalog-driven follow-ups ─────────────────
  // Each entry maps to a supported skill in skills.json so follow-ups
  // always invoke scenarios the backend can handle.
  const SKILL_FOLLOWUPS = {
    RetirementSkill: {
      detect: ['retir', 'migration', 'migrate', 'sunset', 'deprecat'],
      suggestions: [
        'Show retiring resources and migration timelines for my subscriptions',
        'Create a prioritized migration plan for critical retirements'
      ]
    },
    OutageRemediationSkill: {
      detect: ['outage', 'incident', 'bcdr', 'remediat', 'service health'],
      suggestions: [
        'Analyze recent outages and recommend remediation steps',
        'Identify monitoring and alerting gaps for my workload'
      ]
    },
    ResiliencySkill: {
      detect: ['resilien', 'reliab', 'availability', 'failover', 'redundan'],
      suggestions: [
        'Assess my workload resiliency posture and score',
        'Show reliability recommendations with score impact'
      ]
    },
    CostOptimizationSkill: {
      detect: ['cost', 'saving', 'spend', 'optimi', 'underutil'],
      suggestions: [
        'Identify cost-saving opportunities across my subscriptions',
        'Estimate total potential savings and create an optimization plan'
      ]
    },
    ArchitectureSkill: {
      detect: ['architecture', 'well-architect', 'moderniz', 'container', 'design'],
      suggestions: [
        'Review my architecture against the Well-Architected Framework',
        'Compare modernization options for my workload'
      ]
    }
  };

  function generateFollowups(responseText) {
    const lower = responseText.toLowerCase();

    // Detect which skills were already covered in this response
    const coveredSkills = new Set();
    for (const [skill, cfg] of Object.entries(SKILL_FOLLOWUPS)) {
      if (cfg.detect.some(kw => lower.includes(kw))) {
        coveredSkills.add(skill);
      }
    }

    // Suggest follow-ups from skills NOT already covered (explore other scenarios)
    const followups = [];
    for (const [skill, cfg] of Object.entries(SKILL_FOLLOWUPS)) {
      if (!coveredSkills.has(skill)) {
        followups.push(cfg.suggestions[0]);
      }
    }

    // If all skills were covered or few remain, offer deeper-dive follow-ups
    // from the skills that WERE covered
    if (followups.length < 2) {
      for (const [skill, cfg] of Object.entries(SKILL_FOLLOWUPS)) {
        if (coveredSkills.has(skill) && cfg.suggestions.length > 1) {
          followups.push(cfg.suggestions[1]);
        }
      }
    }

    // Fallback: always have at least one actionable follow-up
    if (followups.length === 0) {
      followups.push('Assess my workload resiliency posture and score');
      followups.push('Identify cost-saving opportunities across my subscriptions');
      followups.push('Show retiring resources and migration timelines for my subscriptions');
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
