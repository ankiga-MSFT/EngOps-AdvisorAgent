// ── UI Manager ─────────────────────────────────────────
// Handles DOM manipulation for the Advisor Agent UI.

const UI = (() => {
  // DOM references
  const els = {};
  let startTime = null;
  let stepCount = 0;
  let completedStepCount = 0;

  function init() {
    els.homeView = document.getElementById('home-view');
    els.chatView = document.getElementById('chat-view');
    els.backBtn = document.getElementById('backBtn');
    els.homeTextarea = document.getElementById('homeTextarea');
    els.chatTextarea = document.getElementById('chatTextarea');
    els.homeSendBtn = document.getElementById('homeSendBtn');
    els.chatSendBtn = document.getElementById('chatSendBtn');
    els.userBubbleText = document.getElementById('userBubbleText');
    els.reasoningHeader = document.getElementById('reasoningHeader');
    els.reasoningTitle = document.getElementById('reasoningTitle');
    els.reasoningSteps = document.getElementById('reasoningSteps');
    els.answerBlock = document.getElementById('answerBlock');
    els.answerContent = document.getElementById('answerContent');
    els.followups = document.getElementById('followups');
    els.followupChips = document.getElementById('followupChips');
    els.chatScroll = document.getElementById('chatScroll');
    els.viewToggle = document.getElementById('viewToggle');
    els.suggestionsGrid = document.getElementById('suggestionsGrid');
    els.connectionStatus = document.getElementById('connectionStatus');
  }

  // ── Connection status ────────────────────────────────
  function updateConnectionStatus(state) {
    const dot = els.connectionStatus.querySelector('.status-dot');
    dot.className = 'status-dot ' + state;
    els.connectionStatus.title = 'SignalR: ' + state;
  }

  // ── View transitions ─────────────────────────────────
  async function showChatView(prompt) {
    els.homeView.classList.add('hidden');
    els.backBtn.style.display = 'flex';

    await delay(350);
    els.homeView.style.display = 'none';
    els.chatView.style.display = 'flex';

    void els.chatView.offsetHeight;
    els.chatView.classList.add('visible');

    // Reset state
    resetChatState();

    // Show user bubble
    els.userBubbleText.textContent = prompt;
  }

  function showHomeView() {
    els.chatView.classList.remove('visible');
    setTimeout(() => {
      els.chatView.style.display = 'none';
      els.homeView.style.display = '';
      els.homeView.classList.remove('hidden');
      els.homeTextarea.value = '';
      els.backBtn.style.display = 'none';
    }, 400);
  }

  function resetChatState() {
    els.reasoningSteps.innerHTML = '';
    els.reasoningHeader.classList.add('thinking');
    els.reasoningHeader.classList.add('expanded');
    els.reasoningSteps.classList.add('open');
    els.reasoningTitle.innerHTML = 'Thinking…';
    els.answerBlock.classList.remove('visible');
    els.answerContent.innerHTML = '';
    els.followups.classList.remove('visible');
    els.followupChips.innerHTML = '';
    startTime = Date.now();
    stepCount = 0;
    completedStepCount = 0;
  }

  // ── Reasoning steps ──────────────────────────────────

  // Known steps map — tracks which steps we've created DOM elements for
  const knownSteps = new Map();

  function addOrUpdateStep(stepName, state, message) {
    const elapsed = startTime ? ((Date.now() - startTime) / 1000).toFixed(0) : '0';

    // Friendly label from step name
    const label = friendlyStepName(stepName);

    if (knownSteps.has(stepName)) {
      // Update existing step
      const stepEl = knownSteps.get(stepName);
      const indicator = stepEl.querySelector('.step-indicator');
      const detail = stepEl.querySelector('.step-detail');

      if (state === 'Running') {
        indicator.classList.remove('pending', 'done');
        indicator.classList.add('running');
        indicator.innerHTML = '';
        if (message) detail.textContent = message;
        els.reasoningTitle.innerHTML = `${label}<span class="elapsed">${elapsed}s</span>`;
      } else if (state === 'Completed') {
        indicator.classList.remove('pending', 'running');
        indicator.classList.add('done');
        indicator.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>';
        if (message) detail.textContent = message;
        completedStepCount++;
        els.reasoningTitle.innerHTML = `${label}<span class="elapsed">${elapsed}s</span>`;
      }
    } else {
      // Create new step
      stepCount++;

      // Add connector if not first step
      if (knownSteps.size > 0) {
        const conn = document.createElement('div');
        conn.className = 'step-connector visible';
        els.reasoningSteps.appendChild(conn);
      }

      const el = document.createElement('div');
      el.className = 'step';
      el.innerHTML = `
        <div class="step-indicator ${state === 'Running' ? 'running' : 'pending'}"></div>
        <div class="step-content">
          <div class="step-label">${escapeHtml(label)}</div>
          <div class="step-detail">${escapeHtml(message || '')}</div>
        </div>
      `;
      els.reasoningSteps.appendChild(el);
      knownSteps.set(stepName, el);

      // Animate in
      requestAnimationFrame(() => el.classList.add('visible'));

      // Update header
      els.reasoningTitle.innerHTML = `${escapeHtml(label)}<span class="elapsed">${elapsed}s</span>`;
    }

    scrollToBottom();
  }

  function finishReasoning() {
    const elapsed = startTime ? ((Date.now() - startTime) / 1000).toFixed(0) : '0';
    els.reasoningHeader.classList.remove('thinking');
    els.reasoningTitle.innerHTML = `Worked through ${stepCount} steps<span class="elapsed">${elapsed}s</span>`;
    knownSteps.clear();
  }

  // ── Answer rendering ─────────────────────────────────

  function showAnswer(responseText) {
    // Render Markdown to HTML using marked + DOMPurify
    let html;
    try {
      if (typeof marked !== 'undefined' && marked.parse) {
        html = marked.parse(responseText);
      } else {
        html = responseText;
      }
    } catch {
      html = responseText;
    }

    // Sanitize
    if (typeof DOMPurify !== 'undefined') {
      html = DOMPurify.sanitize(html);
    }

    els.answerContent.innerHTML = html;
    els.answerBlock.classList.add('visible');
    scrollToBottom();
  }

  function showError(message) {
    els.answerContent.innerHTML = `<div style="color:#c4314b;font-weight:600">${escapeHtml(message)}</div>`;
    els.answerBlock.classList.add('visible');
    scrollToBottom();
  }

  // ── Follow-ups ──────────────────────────────────────

  function showFollowups(suggestions) {
    els.followupChips.innerHTML = '';
    suggestions.forEach(text => {
      const chip = document.createElement('button');
      chip.className = 'followup-chip';
      chip.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="5" y1="12" x2="19" y2="12"/><polyline points="12 5 19 12 12 19"/></svg>${escapeHtml(text)}`;
      chip.addEventListener('click', () => {
        if (window.onFollowupClick) window.onFollowupClick(text);
      });
      els.followupChips.appendChild(chip);
    });
    els.followups.classList.add('visible');
    scrollToBottom();
  }

  // ── Subscription Picker Card ──────────────────────────

  /**
   * Renders a rich multi-select subscription picker card in the answer area.
   * @param {Array<{subscriptionId: string, displayName: string}>} subscriptions
   * @param {function(string[]): void} onSubmit - Called with selected subscription IDs.
   */
  function showSubscriptionPicker(subscriptions, onSubmit) {
    const maxSelect = 10;
    const card = document.createElement('div');
    card.className = 'subscription-picker';

    // Header
    card.innerHTML = `
      <div class="sp-header">
        <div class="sp-icon">
          <svg viewBox="0 0 24 24" fill="none" stroke="#0078d4" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <rect x="1" y="4" width="22" height="16" rx="2" ry="2"/>
            <line x1="1" y1="10" x2="23" y2="10"/>
          </svg>
        </div>
        <div class="sp-header-text">
          <div class="sp-title">Select subscriptions to analyze</div>
          <div class="sp-subtitle">Found <strong>${subscriptions.length}</strong> subscription${subscriptions.length !== 1 ? 's' : ''}. Select up to ${maxSelect}.</div>
        </div>
      </div>
      <div class="sp-toolbar">
        <label class="sp-select-all">
          <input type="checkbox" id="spSelectAll" />
          <span>Select all${subscriptions.length > maxSelect ? ` (first ${maxSelect})` : ''}</span>
        </label>
        <div class="sp-search">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="14" height="14">
            <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
          </svg>
          <input type="text" id="spSearch" placeholder="Filter subscriptions…" />
        </div>
        <span class="sp-count" id="spCount">0 / ${maxSelect} selected</span>
      </div>
      <div class="sp-list" id="spList"></div>
      <div class="sp-footer">
        <button class="sp-submit" id="spSubmit" disabled>
          Analyze selected subscriptions
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
            <line x1="5" y1="12" x2="19" y2="12"/><polyline points="12 5 19 12 12 19"/>
          </svg>
        </button>
      </div>
    `;

    // Render subscription rows
    const listEl = card.querySelector('#spList');
    subscriptions.forEach((sub, idx) => {
      const row = document.createElement('label');
      row.className = 'sp-row';
      row.dataset.name = (sub.displayName || '').toLowerCase();
      row.dataset.id = (sub.subscriptionId || '').toLowerCase();
      row.innerHTML = `
        <input type="checkbox" class="sp-check" value="${escapeHtml(sub.subscriptionId)}" />
        <div class="sp-row-info">
          <div class="sp-row-name">${escapeHtml(sub.displayName)}</div>
          <div class="sp-row-id">${escapeHtml(sub.subscriptionId)}</div>
        </div>
      `;
      listEl.appendChild(row);
    });

    // Selection logic
    const checkboxes = () => card.querySelectorAll('.sp-check');
    const countEl = card.querySelector('#spCount');
    const submitBtn = card.querySelector('#spSubmit');
    const selectAllCheckbox = card.querySelector('#spSelectAll');
    const searchInput = card.querySelector('#spSearch');

    function getSelected() {
      return [...checkboxes()].filter(cb => cb.checked).map(cb => cb.value);
    }

    function updateState() {
      const selected = getSelected();
      const count = selected.length;
      countEl.textContent = `${count} / ${maxSelect} selected`;
      submitBtn.disabled = count === 0;
      if (count > 0) {
        submitBtn.textContent = count === 1
          ? 'Analyze 1 subscription'
          : `Analyze ${count} subscription${count > 1 ? 's' : ''}`;
      } else {
        submitBtn.textContent = 'Analyze selected subscriptions';
      }

      // Enforce max
      checkboxes().forEach(cb => {
        if (!cb.checked && count >= maxSelect) {
          cb.disabled = true;
          cb.closest('.sp-row').classList.add('sp-disabled');
        } else {
          cb.disabled = false;
          cb.closest('.sp-row').classList.remove('sp-disabled');
        }
      });

      // Update select-all state
      const visible = [...checkboxes()].filter(cb => !cb.closest('.sp-row').classList.contains('sp-hidden'));
      const allChecked = visible.length > 0 && visible.every(cb => cb.checked);
      selectAllCheckbox.checked = allChecked;
      selectAllCheckbox.indeterminate = !allChecked && visible.some(cb => cb.checked);
    }

    listEl.addEventListener('change', updateState);

    // Select all
    selectAllCheckbox.addEventListener('change', () => {
      const shouldCheck = selectAllCheckbox.checked;
      let selected = getSelected().length;
      checkboxes().forEach(cb => {
        const row = cb.closest('.sp-row');
        if (row.classList.contains('sp-hidden')) return;
        if (shouldCheck && selected < maxSelect && !cb.checked) {
          cb.checked = true;
          selected++;
        } else if (!shouldCheck) {
          cb.checked = false;
        }
      });
      updateState();
    });

    // Search filter
    searchInput.addEventListener('input', () => {
      const q = searchInput.value.toLowerCase();
      card.querySelectorAll('.sp-row').forEach(row => {
        const match = !q || row.dataset.name.includes(q) || row.dataset.id.includes(q);
        row.classList.toggle('sp-hidden', !match);
      });
    });

    // Submit
    submitBtn.addEventListener('click', () => {
      const selected = getSelected();
      if (selected.length === 0) return;
      // Disable card after submission
      card.classList.add('sp-submitted');
      submitBtn.disabled = true;
      submitBtn.textContent = `✓ ${selected.length} subscription${selected.length > 1 ? 's' : ''} selected`;
      checkboxes().forEach(cb => cb.disabled = true);
      selectAllCheckbox.disabled = true;
      searchInput.disabled = true;
      onSubmit(selected);
    });

    els.answerContent.innerHTML = '';
    els.answerContent.appendChild(card);
    els.answerBlock.classList.add('visible');
    scrollToBottom();
  }

  // ── Follow-up turn (multi-turn) ──────────────────────

  /**
   * Appends a new user message + reasoning block to the existing chat scroll
   * without destroying previous conversation. Enables multi-turn display.
   */
  function appendFollowupTurn(prompt) {
    // Hide previous follow-up chips
    els.followups.classList.remove('visible');
    els.followupChips.innerHTML = '';

    // Create a new user bubble
    const userMsg = document.createElement('div');
    userMsg.className = 'user-msg';
    userMsg.innerHTML = `<div class="bubble">${escapeHtml(prompt)}</div>`;
    els.chatScroll.appendChild(userMsg);

    // Create a new agent response block with reasoning
    const agentResp = document.createElement('div');
    agentResp.className = 'agent-response';
    agentResp.innerHTML = `
      <div class="agent-avatar">
        <svg viewBox="0 0 24 24" fill="none">
          <path d="M12 2a1 1 0 0 1 .894.553l2.382 4.823 5.324.774a1 1 0 0 1 .554 1.706l-3.853 3.756.91 5.302a1 1 0 0 1-1.452 1.054L12 17.347l-4.76 2.502a1 1 0 0 1-1.451-1.054l.91-5.302L2.846 9.737a1 1 0 0 1 .554-1.706l5.324-.774L11.106 2.434A1 1 0 0 1 12 2z" fill="#fff"/>
        </svg>
      </div>
      <div class="agent-body">
        <div class="reasoning-block">
          <div class="reasoning-header thinking expanded" id="reasoningHeader">
            <div class="sparkle-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="#0078d4" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/>
              </svg>
            </div>
            <span class="reasoning-title" id="reasoningTitle">Thinking…</span>
            <svg class="reasoning-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="6 9 12 15 18 9"/>
            </svg>
          </div>
          <div class="reasoning-steps open" id="reasoningSteps"></div>
        </div>
        <div class="answer-block" id="answerBlock">
          <div class="answer-content" id="answerContent"></div>
          <div class="followups" id="followups">
            <div class="followups-label">Continue exploring</div>
            <div id="followupChips"></div>
          </div>
        </div>
      </div>
    `;
    els.chatScroll.appendChild(agentResp);

    // Re-bind DOM references to the NEW reasoning/answer blocks
    els.reasoningHeader = agentResp.querySelector('#reasoningHeader');
    els.reasoningTitle = agentResp.querySelector('#reasoningTitle');
    els.reasoningSteps = agentResp.querySelector('#reasoningSteps');
    els.answerBlock = agentResp.querySelector('#answerBlock');
    els.answerContent = agentResp.querySelector('#answerContent');
    els.followups = agentResp.querySelector('#followups');
    els.followupChips = agentResp.querySelector('#followupChips');

    // Wire up the reasoning header toggle on new block
    els.reasoningHeader.addEventListener('click', () => {
      els.reasoningHeader.classList.toggle('expanded');
      els.reasoningSteps.classList.toggle('open');
    });

    // Reset step tracking for the new reasoning block
    startTime = Date.now();
    stepCount = 0;
    completedStepCount = 0;
    knownSteps.clear();

    scrollToBottom();
  }

  // ── View toggle ─────────────────────────────────────
  function toggleViewMore() {
    const isExpanded = els.suggestionsGrid.classList.toggle('expanded');
    els.viewToggle.classList.toggle('expanded', isExpanded);
    els.viewToggle.innerHTML = isExpanded
      ? 'View less <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 12 15 18 9"/></svg>'
      : 'View more <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 12 15 18 9"/></svg>';
  }

  // ── Input control ───────────────────────────────────
  function disableInput() {
    els.homeSendBtn.disabled = true;
    els.chatSendBtn.disabled = true;
  }

  function enableInput() {
    els.homeSendBtn.disabled = false;
    els.chatSendBtn.disabled = false;
  }

  function getHomePrompt() {
    return els.homeTextarea.value.trim();
  }

  function getChatPrompt() {
    return els.chatTextarea.value.trim();
  }

  function clearChatInput() {
    els.chatTextarea.value = '';
  }

  function setHomePrompt(text) {
    els.homeTextarea.value = text;
  }

  // ── Toast ──────────────────────────────────────────
  function showToast(message) {
    let toast = document.querySelector('.error-toast');
    if (!toast) {
      toast = document.createElement('div');
      toast.className = 'error-toast';
      document.body.appendChild(toast);
    }
    toast.textContent = message;
    toast.classList.add('visible');
    setTimeout(() => toast.classList.remove('visible'), 5000);
  }

  // ── Helpers ─────────────────────────────────────────
  function delay(ms) { return new Promise(r => setTimeout(r, ms)); }
  function scrollToBottom() {
    els.chatScroll.scrollTo({ top: els.chatScroll.scrollHeight, behavior: 'smooth' });
  }
  function escapeHtml(str) {
    const d = document.createElement('div');
    d.textContent = str;
    return d.innerHTML;
  }

  function friendlyStepName(stepName) {
    const map = {
      'LoadConversationHistory': 'Loading conversation history',
      'ResolveAzureContext': 'Resolving Azure context',
      'SubscriptionDiscovery': 'Discovering subscriptions',
      'ClassifyIntent': 'Classifying intent',
      'AnswerDirectly': 'Generating direct answer',
      'DecomposeTasks': 'Decomposing tasks',
      'SkillExecution': 'Executing skills',
      'GenerateSkillPrompt': 'Generating skill prompt',
      'ExecuteSkill': 'Executing skill'
    };
    return map[stepName] || stepName;
  }

  // ── Auto-grow textareas ──────────────────────────────
  function setupTextareas() {
    [els.homeTextarea, els.chatTextarea].forEach(ta => {
      ta.addEventListener('input', () => {
        ta.style.height = 'auto';
        ta.style.height = ta.scrollHeight + 'px';
      });
    });
  }

  return {
    init,
    updateConnectionStatus,
    showChatView,
    showHomeView,
    resetChatState,
    addOrUpdateStep,
    finishReasoning,
    showAnswer,
    showError,
    showFollowups,
    showSubscriptionPicker,
    appendFollowupTurn,
    toggleViewMore,
    disableInput,
    enableInput,
    getHomePrompt,
    getChatPrompt,
    clearChatInput,
    setHomePrompt,
    showToast,
    setupTextareas,
    get elements() { return els; }
  };
})();
