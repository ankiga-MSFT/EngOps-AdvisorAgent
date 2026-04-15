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
    els.cotToggle = document.getElementById('cotToggle');
    els.cotHeaderText = document.getElementById('cotHeaderText');
    els.cotCurrentStep = document.getElementById('cotCurrentStep');
    els.cotCard = document.getElementById('cotCard');
    els.cotActivities = document.getElementById('cotActivities');
    els.cotProgress = document.getElementById('cotProgress');
    els.cotDot = document.getElementById('cotDot');
    els.cotProgressBar = document.getElementById('cotProgressBar');
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
    els.cotActivities.innerHTML = '';
    els.cotToggle.setAttribute('aria-expanded', 'true');
    els.cotCard.classList.add('open');
    els.cotHeaderText.innerHTML = 'Thinking&hellip;';
    els.cotCurrentStep.textContent = '';
    // Show pulsing dot, hide checkmark
    els.cotDot.style.display = '';
    const existingCheck = els.cotProgress.querySelector('.cot-check');
    if (existingCheck) existingCheck.remove();
    // Show LatencyLoader-style progress bar
    if (els.cotProgressBar) els.cotProgressBar.classList.remove('done');
    els.answerBlock.classList.remove('visible');
    els.answerContent.innerHTML = '';
    els.followups.classList.remove('visible');
    els.followupChips.innerHTML = '';
    // Remove previous feedback row
    const oldFb = document.querySelector('.feedback-row');
    if (oldFb) oldFb.remove();
    startTime = Date.now();
    stepCount = 0;
    completedStepCount = 0;
  }

  // ── Reasoning steps ──────────────────────────────────

  // Known steps map — tracks which steps we've created DOM elements for
  const knownSteps = new Map();

  function addOrUpdateStep(stepName, state, message) {
    const elapsed = startTime ? ((Date.now() - startTime) / 1000).toFixed(0) : '0';
    const label = friendlyStepName(stepName);

    if (knownSteps.has(stepName)) {
      // Update existing item
      const item = knownSteps.get(stepName);
      const statusEl = item.querySelector('.cot-item-status');
      const contentEl = item.querySelector('.cot-item-content');

      if (state === 'Running') {
        statusEl.innerHTML = '<span class="cot-pulsing-dot"><span></span><span></span><span></span></span>';
        if (message) contentEl.textContent = message;
        els.cotHeaderText.innerHTML = `${escapeHtml(label)}<span class="elapsed">${elapsed}s</span>`;
        els.cotCurrentStep.textContent = label;
      } else if (state === 'Completed') {
        statusEl.innerHTML = '<svg class="cot-item-check" viewBox="0 0 20 20" fill="none"><circle cx="10" cy="10" r="9" stroke="currentColor" stroke-width="1.5"/><polyline points="6 10 9 13 14 7" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>';
        if (message) contentEl.textContent = message;
        completedStepCount++;
        els.cotHeaderText.innerHTML = `${escapeHtml(label)}<span class="elapsed">${elapsed}s</span>`;
        els.cotCurrentStep.textContent = label;
      }
    } else {
      // Create new item
      stepCount++;
      const item = document.createElement('div');
      item.className = 'cot-item';
      const isRunning = state === 'Running';
      item.innerHTML = `
        <div class="cot-item-header">
          <span class="cot-item-status">
            ${isRunning
          ? '<span class="cot-pulsing-dot"><span></span><span></span><span></span></span>'
          : '<svg class="cot-item-check" viewBox="0 0 20 20" fill="none"><circle cx="10" cy="10" r="9" stroke="currentColor" stroke-width="1.5"/><polyline points="6 10 9 13 14 7" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>'
        }
          </span>
          <span class="cot-item-label">${escapeHtml(label)}</span>
        </div>
        <div class="cot-item-content">${escapeHtml(message || '')}</div>
      `;
      els.cotActivities.appendChild(item);
      knownSteps.set(stepName, item);

      // Animate in
      requestAnimationFrame(() => item.classList.add('visible'));

      // Update header
      els.cotHeaderText.innerHTML = `${escapeHtml(label)}<span class="elapsed">${elapsed}s</span>`;
      els.cotCurrentStep.textContent = label;
    }
    scrollToBottom();
  }

  function finishReasoning() {
    const elapsed = startTime ? ((Date.now() - startTime) / 1000).toFixed(0) : '0';
    els.cotHeaderText.innerHTML = `Worked through ${stepCount} steps<span class="elapsed">${elapsed}s</span>`;
    els.cotCurrentStep.textContent = '';
    // Replace pulsing dot with static checkmark
    els.cotDot.style.display = 'none';
    if (!els.cotProgress.querySelector('.cot-check')) {
      const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      svg.setAttribute('class', 'cot-check');
      svg.setAttribute('viewBox', '0 0 20 20');
      svg.setAttribute('fill', 'none');
      svg.innerHTML = '<circle cx="10" cy="10" r="9" stroke="currentColor" stroke-width="1.5"/><polyline points="6 10 9 13 14 7" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>';
      els.cotProgress.appendChild(svg);
    }
    // Hide the LatencyLoader progress bar
    if (els.cotProgressBar) els.cotProgressBar.classList.add('done');
    knownSteps.clear();
  }

  // ── Typing Indicator ──────────────────────────────────

  function showTypingIndicator() {
    let indicator = document.getElementById('typingIndicator');
    if (!indicator) {
      indicator = document.createElement('div');
      indicator.id = 'typingIndicator';
      indicator.className = 'typing-indicator';
      indicator.innerHTML = '<span></span><span></span><span></span>';
      els.answerBlock.parentNode.insertBefore(indicator, els.answerBlock);
    }
    indicator.classList.add('visible');
    scrollToBottom();
  }

  function hideTypingIndicator() {
    const indicator = document.getElementById('typingIndicator');
    if (indicator) indicator.classList.remove('visible');
  }

  // ── Feedback Buttons ──────────────────────────────────

  function showFeedbackButtons() {
    let row = document.querySelector('.feedback-row');
    if (row) { row.classList.add('visible'); return; }

    row = document.createElement('div');
    row.className = 'feedback-row visible';
    row.innerHTML = `
      <button class="feedback-btn" data-value="up" title="Helpful">
        <svg width="16" height="16" viewBox="0 0 20 20" fill="none"><path d="M10.46 2.15a.75.75 0 00-1.32.13L7.26 6H4.75A2.75 2.75 0 002 8.75v5.5A2.75 2.75 0 004.75 17h9.64a2.75 2.75 0 002.71-2.28l.88-5a2.75 2.75 0 00-2.72-3.22H12.5l.43-1.72a2.25 2.25 0 00-.47-2.03l-2-2.6z" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>
      </button>
      <button class="feedback-btn" data-value="down" title="Not helpful">
        <svg width="16" height="16" viewBox="0 0 20 20" fill="none"><path d="M9.54 17.85a.75.75 0 001.32-.13L12.74 14h2.51A2.75 2.75 0 0018 11.25v-5.5A2.75 2.75 0 0015.25 3H5.61a2.75 2.75 0 00-2.71 2.28l-.88 5A2.75 2.75 0 004.74 13.5H7.5l-.43 1.72a2.25 2.25 0 00.47 2.03l2 2.6z" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>
      </button>
    `;
    row.querySelectorAll('.feedback-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        row.querySelectorAll('.feedback-btn').forEach(b => b.classList.remove('selected'));
        btn.classList.add('selected');
        if (window.onFeedbackClick) window.onFeedbackClick(btn.dataset.value);
      });
    });
    els.answerBlock.after(row);
    scrollToBottom();
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

    // Wrap tables in a scrollable container with a CSV download button
    let tableIdx = 0;
    els.answerContent.querySelectorAll('table').forEach(table => {
      if (!table.parentElement.classList.contains('table-wrapper')) {
        tableIdx++;
        const wrapper = document.createElement('div');
        wrapper.className = 'table-wrapper';

        // CSV download button
        const csvBtn = document.createElement('button');
        csvBtn.className = 'csv-download-btn';
        csvBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>Download CSV`;
        const tbl = table;
        const idx = tableIdx;
        csvBtn.addEventListener('click', () => {
          ExportUtils.downloadCSV(tbl, `table-${idx}.csv`);
        });

        table.parentNode.insertBefore(wrapper, table);
        wrapper.appendChild(csvBtn);
        wrapper.appendChild(table);
      }
    });

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

    // Export action chips
    const exportRow = document.createElement('div');
    exportRow.className = 'export-actions';

    const wordBtn = document.createElement('button');
    wordBtn.className = 'export-chip export-word';
    wordBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>Export to Word`;
    wordBtn.addEventListener('click', () => {
      ExportUtils.exportToWord(els.answerContent, 'advisor-response.doc');
    });

    const pdfBtn = document.createElement('button');
    pdfBtn.className = 'export-chip export-pdf';
    pdfBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="12" y1="18" x2="12" y2="12"/><polyline points="9 15 12 12 15 15"/></svg>Export to PDF`;
    pdfBtn.addEventListener('click', () => {
      ExportUtils.exportToPDF(els.answerContent, 'advisor-response.pdf');
    });

    exportRow.appendChild(wordBtn);
    exportRow.appendChild(pdfBtn);
    els.followupChips.appendChild(exportRow);

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
          <svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
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

    // Create a new agent response block with CoT
    const agentResp = document.createElement('div');
    agentResp.className = 'agent-response';
    agentResp.innerHTML = `
      <div class="agent-avatar">
        <svg viewBox="0 0 24 24" fill="none">
          <path d="M12 2a1 1 0 0 1 .894.553l2.382 4.823 5.324.774a1 1 0 0 1 .554 1.706l-3.853 3.756.91 5.302a1 1 0 0 1-1.452 1.054L12 17.347l-4.76 2.502a1 1 0 0 1-1.451-1.054l.91-5.302L2.846 9.737a1 1 0 0 1 .554-1.706l5.324-.774L11.106 2.434A1 1 0 0 1 12 2z" fill="#fff"/>
        </svg>
      </div>
      <div class="agent-body">
        <div class="cot" id="cotRoot">
          <button class="cot-toggle" id="cotToggle" aria-expanded="true">
            <span class="cot-progress" id="cotProgress">
              <span class="cot-pulsing-dot" id="cotDot"><span></span><span></span><span></span></span>
            </span>
            <span class="cot-header-text" id="cotHeaderText">Thinking&hellip;</span>
            <svg class="cot-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="6 9 12 15 18 9"/>
            </svg>
          </button>
          <div class="cot-current-step" id="cotCurrentStep"></div>
          <div class="cot-card open" id="cotCard">
            <div class="cot-progress-bar" id="cotProgressBar"></div>
            <div class="cot-activities" id="cotActivities"></div>
          </div>
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

    // Re-bind DOM references to the NEW CoT/answer blocks
    els.cotToggle = agentResp.querySelector('#cotToggle');
    els.cotHeaderText = agentResp.querySelector('#cotHeaderText');
    els.cotCurrentStep = agentResp.querySelector('#cotCurrentStep');
    els.cotCard = agentResp.querySelector('#cotCard');
    els.cotActivities = agentResp.querySelector('#cotActivities');
    els.cotProgress = agentResp.querySelector('#cotProgress');
    els.cotDot = agentResp.querySelector('#cotDot');
    els.cotProgressBar = agentResp.querySelector('#cotProgressBar');
    els.answerBlock = agentResp.querySelector('#answerBlock');
    els.answerContent = agentResp.querySelector('#answerContent');
    els.followups = agentResp.querySelector('#followups');
    els.followupChips = agentResp.querySelector('#followupChips');

    // Wire up toggle on new block
    els.cotToggle.addEventListener('click', () => {
      const expanded = els.cotToggle.getAttribute('aria-expanded') === 'true';
      els.cotToggle.setAttribute('aria-expanded', String(!expanded));
      els.cotCard.classList.toggle('open');
    });

    // Reset step tracking for the new block
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
    if (map[stepName]) return map[stepName];

    // Handle dynamic skill step names like "ExecuteSkill:RetirementSkill"
    if (stepName.startsWith('ExecuteSkill:')) {
      const skillName = stepName.split(':')[1] || '';
      const skillMap = {
        'RetirementSkill': 'Analyzing retiring resources',
        'OutageRemediationSkill': 'Analyzing outage remediation',
        'ResiliencySkill': 'Assessing resiliency posture',
        'CostOptimizationSkill': 'Optimizing costs',
        'ArchitectureSkill': 'Evaluating architecture'
      };
      return skillMap[skillName] || `Executing ${skillName}`;
    }

    return stepName;
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
    showTypingIndicator,
    hideTypingIndicator,
    showFeedbackButtons,
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
